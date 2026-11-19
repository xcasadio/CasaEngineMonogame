using CasaEngine.Editor.Remote;
using CasaEngine.Editor.Remote.Protocol;
using Xunit;

namespace CasaEngine.Tests.Editor.Remote;

/// <summary>ADR-0092, task T3.2: captures answer after the frame, play mode answers its real state.</summary>
public sealed class EditorRemoteViewCommandsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "casa-remote-view-" + Guid.NewGuid().ToString("N"));
    private readonly RemoteTestHost _host = new();
    private readonly EditorRemoteControl _control;
    private double _now;

    public EditorRemoteViewCommandsTests()
    {
        _control = new EditorRemoteControl(_host, Path.Combine(_directory, "discovery"), () => _now)
        {
            CaptureDirectory = Path.Combine(_directory, "captures"),
        };
    }

    public void Dispose()
    {
        _control.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private EditorRemoteCall Start(string method)
    {
        var call = new EditorRemoteCall(new RemoteRequest { Id = 1, Method = method });
        _control.Dispatcher.Enqueue(call);
        _control.Pump();
        return call;
    }

    [Theory]
    [InlineData("capture.window", "window")]
    [InlineData("capture.viewport", "viewport")]
    public void Capture_AnswersOnceTheFrameTookIt_WithAPathTheClientDidNotChoose(string method, string kind)
    {
        EditorRemoteCall call = Start(method);
        var (requestedKind, path) = Assert.Single(_host.CaptureRequests);

        Assert.Equal(kind, requestedKind);
        Assert.Equal(Path.Combine(_directory, "captures"), Path.GetDirectoryName(path));
        Assert.Matches("^capture-[0-9a-f]{32}\\.png$", Path.GetFileName(path));
        Assert.False(call.IsCompleted);

        _host.CaptureResults[path] = new RemoteCaptureResult { Succeeded = true, Width = 320, Height = 200 };
        _control.Pump();

        var result = call.Completion.Result.Result!.Value;
        Assert.Equal(path, result.GetProperty("file").GetString());
        Assert.Equal(320, result.GetProperty("width").GetInt32());
    }

    [Fact]
    public void Capture_Failure_IsAnError()
    {
        EditorRemoteCall call = Start("capture.viewport");
        _host.CaptureResults[_host.CaptureRequests[0].Path] = new RemoteCaptureResult { Error = "nothing drawn" };

        _control.Pump();

        Assert.Equal(RemoteErrorReasons.Failed, call.Completion.Result.Error!.Data!.Reason);
    }

    [Fact]
    public void Capture_NeverTaken_TimesOut()
    {
        EditorRemoteCall call = Start("capture.window");

        _now = EditorRemoteViewCommands.CaptureTimeoutSeconds + 1;
        _control.Pump();

        Assert.Equal(RemoteErrorReasons.Timeout, call.Completion.Result.Error!.Data!.Reason);
    }

    [Fact]
    public void Captures_AreDeletedWhenTheEditorCloses()
    {
        Start("capture.window");
        string path = _host.CaptureRequests[0].Path;
        File.WriteAllText(path, "png");

        _control.Dispose();

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void PlayStart_ReportsAFailedStart()
    {
        _host.PlayStarts = false;

        EditorRemoteCall call = Start("play.start");

        Assert.Equal(RemoteErrorReasons.Failed, call.Completion.Result.Error!.Data!.Reason);
        Assert.Equal(1, _host.StartPlayCount);
    }

    [Fact]
    public void PlayStartStatusStop()
    {
        Assert.Equal("playing", Start("play.start").Completion.Result.Result!.Value.GetProperty("state").GetString());
        Assert.Equal("playing", Start("play.status").Completion.Result.Result!.Value.GetProperty("state").GetString());
        Assert.Equal("playing", Start("play.start").Completion.Result.Result!.Value.GetProperty("state").GetString());
        Assert.Equal(1, _host.StartPlayCount);

        Assert.Equal("editing", Start("play.stop").Completion.Result.Result!.Value.GetProperty("state").GetString());
    }

    [Fact]
    public void CommandList_MarksPlayStartDestructive_AndCapturesReadOnly()
    {
        var list = RemoteMessageCodec.FromElement<RemoteCommandList>(Start(RemoteProtocol.ListCommandsMethod).Completion.Result.Result!.Value)!;

        Assert.True(list.Commands.Single(c => c.Name == "play.start").Destructive);
        Assert.True(list.Commands.Single(c => c.Name == "capture.window").ReadOnly);
        Assert.True(list.Commands.Single(c => c.Name == "entity.delete").Destructive);
        Assert.False(list.Commands.Single(c => c.Name == "project.save").ReadOnly);
    }
}
