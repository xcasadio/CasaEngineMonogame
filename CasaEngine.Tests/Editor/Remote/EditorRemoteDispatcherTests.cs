using System.Text.Json;
using CasaEngine.Editor.Remote;
using CasaEngine.Editor.Remote.Protocol;
using Xunit;

namespace CasaEngine.Tests.Editor.Remote;

/// <summary>ADR-0092, task T1.2: commands run on the pump, errors become answers, deferred calls time out.</summary>
public sealed class EditorRemoteDispatcherTests
{
    private double _now;
    private readonly List<string> _warnings = new();

    private (EditorRemoteCommandRegistry Registry, EditorRemoteDispatcher Dispatcher) Create()
    {
        var registry = new EditorRemoteCommandRegistry();
        return (registry, new EditorRemoteDispatcher(registry, () => _now, _warnings.Add));
    }

    private static EditorRemoteCall Call(string method, string paramsJson = null, long id = 1)
        => new(new RemoteRequest
        {
            Id = id,
            Method = method,
            Params = paramsJson == null ? null : JsonDocument.Parse(paramsJson).RootElement.Clone(),
        });

    private static void Register(EditorRemoteCommandRegistry registry, string name, Action<EditorRemoteCall> handler)
        => registry.Register(new EditorRemoteCommand(name, "test", null, readOnly: true, destructive: false, handler));

    [Fact]
    public void Call_RunsOnlyOnPump_AndAnswers()
    {
        var (registry, dispatcher) = Create();
        Register(registry, "echo", call => call.Succeed(new { text = call.GetString("text") }));
        EditorRemoteCall call = Call("echo", "{\"text\":\"hi\"}");

        dispatcher.Enqueue(call);
        Assert.False(call.IsCompleted);
        dispatcher.Pump();

        Assert.True(call.IsCompleted);
        Assert.Equal("hi", call.Completion.Result.Result!.Value.GetProperty("text").GetString());
    }

    [Fact]
    public void UnknownCommand_IsMethodNotFound()
    {
        var (_, dispatcher) = Create();
        EditorRemoteCall call = Call("nope");

        dispatcher.Enqueue(call);
        dispatcher.Pump();

        Assert.Equal(RemoteErrorCodes.MethodNotFound, call.Completion.Result.Error!.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{\"text\":3}")]
    [InlineData("{\"text\":\"0123456789abc\"}")]
    public void BadArguments_AreInvalidParams(string paramsJson)
    {
        var (registry, dispatcher) = Create();
        Register(registry, "echo", call => call.Succeed(call.GetString("text", maxLength: 10)));
        EditorRemoteCall call = Call("echo", paramsJson);

        dispatcher.Enqueue(call);
        dispatcher.Pump();

        RemoteError error = call.Completion.Result.Error!;
        Assert.Equal(RemoteErrorCodes.InvalidParams, error.Code);
        Assert.Equal(RemoteErrorReasons.InvalidArgument, error.Data!.Reason);
    }

    [Fact]
    public void HandlerException_BecomesAnInternalError_AndIsLogged_WithoutLeavingThePump()
    {
        var (registry, dispatcher) = Create();
        Register(registry, "boom", _ => throw new InvalidOperationException("broken"));
        EditorRemoteCall call = Call("boom");

        dispatcher.Enqueue(call);
        dispatcher.Pump();

        Assert.Equal(RemoteErrorCodes.InternalError, call.Completion.Result.Error!.Code);
        Assert.Contains("broken", Assert.Single(_warnings));
    }

    [Fact]
    public void RefusedException_KeepsItsReason()
    {
        var (registry, dispatcher) = Create();
        Register(registry, "save", _ => throw new EditorRemoteRefusedException(RemoteErrorReasons.PlayModeActive, "Stop play first."));
        EditorRemoteCall call = Call("save");

        dispatcher.Enqueue(call);
        dispatcher.Pump();

        Assert.Equal(RemoteErrorReasons.PlayModeActive, call.Completion.Result.Error!.Data!.Reason);
    }

    [Fact]
    public void HandlerWithoutAnswer_IsAnInternalError()
    {
        var (registry, dispatcher) = Create();
        Register(registry, "silent", _ => { });
        EditorRemoteCall call = Call("silent");

        dispatcher.Enqueue(call);
        dispatcher.Pump();

        Assert.Equal(RemoteErrorCodes.InternalError, call.Completion.Result.Error!.Code);
    }

    [Fact]
    public void DeferredCall_AnswersWhenItsPollSucceeds()
    {
        var (registry, dispatcher) = Create();
        bool ready = false;
        Register(registry, "wait", call => call.Defer(c =>
        {
            if (!ready)
            {
                return false;
            }

            c.Succeed("done");
            return true;
        }, timeoutSeconds: 5));
        EditorRemoteCall call = Call("wait");

        dispatcher.Enqueue(call);
        dispatcher.Pump();
        dispatcher.Pump();
        Assert.False(call.IsCompleted);
        Assert.Equal(1, dispatcher.DeferredCount);

        ready = true;
        dispatcher.Pump();

        Assert.Equal("done", call.Completion.Result.Result!.Value.GetString());
        Assert.Equal(0, dispatcher.DeferredCount);
    }

    [Fact]
    public void DeferredCall_TimesOut()
    {
        var (registry, dispatcher) = Create();
        Register(registry, "wait", call => call.Defer(_ => false, timeoutSeconds: 2));
        EditorRemoteCall call = Call("wait");

        dispatcher.Enqueue(call);
        dispatcher.Pump();
        _now = 1.9;
        dispatcher.Pump();
        Assert.False(call.IsCompleted);

        _now = 2.1;
        dispatcher.Pump();

        Assert.Equal(RemoteErrorReasons.Timeout, call.Completion.Result.Error!.Data!.Reason);
    }

    [Fact]
    public void Pump_RunsAtMostTheCapPerFrame()
    {
        var (registry, dispatcher) = Create();
        int runs = 0;
        Register(registry, "count", call =>
        {
            runs++;
            call.Succeed(runs);
        });

        for (int i = 0; i < EditorRemoteDispatcher.MaxCallsPerPump + 3; i++)
        {
            dispatcher.Enqueue(Call("count", id: i));
        }

        dispatcher.Pump();
        Assert.Equal(EditorRemoteDispatcher.MaxCallsPerPump, runs);
        dispatcher.Pump();
        Assert.Equal(EditorRemoteDispatcher.MaxCallsPerPump + 3, runs);
    }

    [Fact]
    public void CancelAll_FailsWaitingCalls()
    {
        var (registry, dispatcher) = Create();
        Register(registry, "wait", call => call.Defer(_ => false, timeoutSeconds: 60));
        EditorRemoteCall deferred = Call("wait", id: 1);
        EditorRemoteCall queued = Call("wait", id: 2);
        dispatcher.Enqueue(deferred);
        dispatcher.Pump();
        dispatcher.Enqueue(queued);

        dispatcher.CancelAll("stopped");

        Assert.Equal(RemoteErrorReasons.Failed, deferred.Completion.Result.Error!.Data!.Reason);
        Assert.Equal(RemoteErrorReasons.Failed, queued.Completion.Result.Error!.Data!.Reason);
    }

    /// <summary>The pipe thread logs through the dispatcher: lines are written by the next pump, on the game thread.</summary>
    [Fact]
    public async Task PostLog_IsWrittenByThePump()
    {
        var (_, dispatcher) = Create();

        await Task.Run(() => dispatcher.PostLog(CasaEngine.Core.Logging.LogVerbosity.Warning, "from the pipe"));
        Assert.Empty(_warnings);

        dispatcher.Pump();

        Assert.Equal("from the pipe", Assert.Single(_warnings));
    }

    [Fact]
    public void Registry_RejectsDuplicatesAndNonObjectSchemas()
    {
        var registry = new EditorRemoteCommandRegistry();
        Register(registry, "a", _ => { });

        Assert.Throws<InvalidOperationException>(() => Register(registry, "a", _ => { }));
        Assert.Throws<ArgumentException>(() => new EditorRemoteCommand("b", "test", "{\"type\":\"string\"}", true, false, _ => { }));
    }
}
