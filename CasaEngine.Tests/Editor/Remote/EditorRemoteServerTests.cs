using System.IO.Pipes;
using CasaEngine.Editor.Remote;
using CasaEngine.Editor.Remote.Protocol;
using Xunit;

namespace CasaEngine.Tests.Editor.Remote;

/// <summary>ADR-0092, task T1.2: the real pipe, the handshake and the security rules of plan P1 and P10.</summary>
public sealed class EditorRemoteServerTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "casa-remote-server-" + Guid.NewGuid().ToString("N"));
    private readonly CancellationTokenSource _pumpStop = new();

    public void Dispose()
    {
        _pumpStop.Cancel();
        _pumpStop.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static RemoteTestHost CreateHost() => new()
    {
        ProjectFilePath = "C:/projects/sample/sample.casaproject",
        CurrentWorld = new CasaEngine.Framework.Scene.World.World { Name = "DefaultWorld" },
        HasUnsavedWorldChanges = true,
    };

    /// <summary>Stands in for the editor's game loop: pumps the dispatcher every few milliseconds.</summary>
    private void PumpInBackground(EditorRemoteControl control)
    {
        CancellationToken stop = _pumpStop.Token;
        _ = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                control.Pump();
                await Task.Delay(5);
            }
        });
    }

    [Fact]
    public async Task Client_FindsTheEditor_Handshakes_AndCallsCommands()
    {
        using var control = new EditorRemoteControl(CreateHost(), _directory);
        Assert.True(control.Start(refuseWhenElevated: false));
        PumpInBackground(control);

        RemoteDiscoveryRecord record = Assert.Single(RemoteDiscovery.ReadAll(_directory));
        Assert.Equal(control.PipeName, record.PipeName);

        await using RemoteEditorClient client = await RemoteEditorClient.ConnectAsync(record, Timeout, CancellationToken.None);
        Assert.Equal("9.9.9", client.Hello!.EditorVersion);

        RemoteCommandList list = RemoteMessageCodec.FromElement<RemoteCommandList>(
            await client.CallAsync(RemoteProtocol.ListCommandsMethod, null, Timeout, CancellationToken.None))!;
        Assert.Contains(list.Commands, c => c.Name == "editor.status" && c.ReadOnly);

        var status = await client.CallAsync("editor.status", null, Timeout, CancellationToken.None);
        Assert.Equal("DefaultWorld", status.GetProperty("world_name").GetString());
        Assert.True(status.GetProperty("unsaved_changes").GetBoolean());

        var error = await Assert.ThrowsAsync<RemoteEditorException>(() => client.CallAsync("nope", null, Timeout, CancellationToken.None));
        Assert.Equal(RemoteErrorCodes.MethodNotFound, error.Error.Code);

        control.Stop();
        Assert.Empty(RemoteDiscovery.ReadAll(_directory));
    }

    [Fact]
    public async Task Client_WithAWrongToken_IsRejected()
    {
        using var control = new EditorRemoteControl(CreateHost(), _directory);
        Assert.True(control.Start(refuseWhenElevated: false));
        PumpInBackground(control);
        RemoteDiscoveryRecord record = Assert.Single(RemoteDiscovery.ReadAll(_directory));
        var forged = new RemoteDiscoveryRecord
        {
            ProcessId = record.ProcessId,
            ProcessStartUtcTicks = record.ProcessStartUtcTicks,
            PipeName = record.PipeName,
            Token = RemoteDiscovery.CreateToken(),
        };

        await Assert.ThrowsAsync<RemoteEditorException>(() => RemoteEditorClient.ConnectAsync(forged, Timeout, CancellationToken.None));
    }

    /// <summary>Security review S1: a squatted pipe name makes the editor fail instead of joining the squatter's pipe.</summary>
    [Fact]
    public void SquattedPipeName_FailsCleanly()
    {
        string name = RemoteDiscovery.CreatePipeName();
        using var squatter = new NamedPipeServerStream(name, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var dispatcher = new EditorRemoteDispatcher(new EditorRemoteCommandRegistry());
        using var server = new EditorRemoteServer(dispatcher, name, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

        Exception error = Record.Exception(server.Start);

        Assert.True(error is IOException or UnauthorizedAccessException, $"Unexpected: {error}");
        Assert.False(server.IsRunning);
    }

    /// <summary>Plan P10: an idle client is dropped, and the pump never waits for it.</summary>
    [Fact]
    public async Task IdleClient_IsDisconnected_AndDoesNotBlockThePump()
    {
        string name = RemoteDiscovery.CreatePipeName();
        var dispatcher = new EditorRemoteDispatcher(new EditorRemoteCommandRegistry());
        using var server = new EditorRemoteServer(dispatcher, name, idleTimeout: TimeSpan.FromMilliseconds(300), answerTimeout: TimeSpan.FromSeconds(5));
        server.Start();

        await using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await client.ConnectAsync(5000);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        dispatcher.Pump();
        Assert.True(watch.ElapsedMilliseconds < 100);

        var buffer = new byte[16];
        using var readTimeout = new CancellationTokenSource(Timeout);
        int read = await client.ReadAsync(buffer, readTimeout.Token);
        Assert.Equal(0, read);
    }

    /// <summary>
    /// Security review S2 (plan O3): a client that comes through the network redirector is dropped before any command.
    /// A loopback path stands in for another machine: it goes through the same redirector.
    /// </summary>
    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    public async Task NetworkRedirectorClient_IsDroppedWithoutAnAnswer(string serverName)
    {
        string name = RemoteDiscovery.CreatePipeName();
        var registry = new EditorRemoteCommandRegistry();
        bool ran = false;
        registry.Register(new EditorRemoteCommand(RemoteProtocol.HelloMethod, "test", null, true, false, call =>
        {
            ran = true;
            call.Succeed(new RemoteHelloResult());
        }));
        var dispatcher = new EditorRemoteDispatcher(registry);
        using var server = new EditorRemoteServer(dispatcher, name, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        server.Start();

        await using var client = new NamedPipeClientStream(serverName, name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(5000);
        Exception writeError = await Record.ExceptionAsync(async () =>
        {
            await RemoteMessageCodec.WriteAsync(client, new RemoteRequest { Id = 1, Method = RemoteProtocol.HelloMethod }, CancellationToken.None);
        });
        dispatcher.Pump();

        var buffer = new byte[64];
        using var readTimeout = new CancellationTokenSource(Timeout);
        int read = writeError == null ? await client.ReadAsync(buffer, readTimeout.Token) : 0;

        Assert.Equal(0, read);
        Assert.False(ran);
    }

    [Fact]
    public async Task LocalClient_IsServed()
    {
        string name = RemoteDiscovery.CreatePipeName();
        var dispatcher = new EditorRemoteDispatcher(new EditorRemoteCommandRegistry());
        using var server = new EditorRemoteServer(dispatcher, name, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        server.Start();

        await using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await client.ConnectAsync(5000);
        await RemoteMessageCodec.WriteAsync(client, new RemoteRequest { Id = 1, Method = "unknown" }, CancellationToken.None);

        using var stop = new CancellationTokenSource(Timeout);
        Task<RemoteLine> pending = new RemoteLineReader(client).ReadLineAsync(stop.Token);
        while (!pending.IsCompleted)
        {
            dispatcher.Pump();
            await Task.Delay(5);
        }

        RemoteLine line = await pending;
        Assert.True(RemoteMessageCodec.TryDecodeResponse(line.Text, out RemoteResponse response));
        Assert.Equal(RemoteErrorCodes.MethodNotFound, response.Error!.Code);
    }

    /// <summary>Verifier finding A1: a call the server answered as timed out is never run afterwards.</summary>
    [Fact]
    public async Task TimedOutCall_IsNotRunLater()
    {
        string name = RemoteDiscovery.CreatePipeName();
        var registry = new EditorRemoteCommandRegistry();
        int runs = 0;
        registry.Register(new EditorRemoteCommand("slow.count", "test", null, true, false, call =>
        {
            runs++;
            call.Succeed(runs);
        }));
        var dispatcher = new EditorRemoteDispatcher(registry);
        using var server = new EditorRemoteServer(dispatcher, name, TimeSpan.FromSeconds(30), answerTimeout: TimeSpan.FromMilliseconds(200));
        server.Start();

        await using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await client.ConnectAsync(5000);
        await RemoteMessageCodec.WriteAsync(client, new RemoteRequest { Id = 1, Method = "slow.count" }, CancellationToken.None);
        using var readTimeout = new CancellationTokenSource(Timeout);
        RemoteLine line = await new RemoteLineReader(client).ReadLineAsync(readTimeout.Token);

        Assert.True(RemoteMessageCodec.TryDecodeResponse(line.Text, out RemoteResponse response));
        Assert.Equal(RemoteErrorReasons.Timeout, response.Error!.Data!.Reason);

        dispatcher.Pump();
        Assert.Equal(0, runs);
    }

    /// <summary>Verifier finding A2: an answer over the line limit becomes a clear error instead of a line the client refuses.</summary>
    [Fact]
    public async Task OversizedAnswer_IsReplacedByAnError()
    {
        string name = RemoteDiscovery.CreatePipeName();
        var registry = new EditorRemoteCommandRegistry();
        registry.Register(new EditorRemoteCommand("huge", "test", null, true, false, call => call.Succeed(new string('x', RemoteProtocol.MaxMessageBytes + 10))));
        var dispatcher = new EditorRemoteDispatcher(registry);
        using var server = new EditorRemoteServer(dispatcher, name, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        server.Start();

        await using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await client.ConnectAsync(5000);
        await RemoteMessageCodec.WriteAsync(client, new RemoteRequest { Id = 7, Method = "huge" }, CancellationToken.None);
        using var stop = new CancellationTokenSource(Timeout);
        Task<RemoteLine> pending = new RemoteLineReader(client).ReadLineAsync(stop.Token);
        while (!pending.IsCompleted)
        {
            dispatcher.Pump();
            await Task.Delay(5);
        }

        RemoteLine line = await pending;
        Assert.Equal(RemoteLineStatus.Line, line.Status);
        Assert.True(RemoteMessageCodec.TryDecodeResponse(line.Text, out RemoteResponse response));
        Assert.Equal(7, response.Id);
        Assert.Equal(RemoteErrorReasons.MessageTooLong, response.Error!.Data!.Reason);
    }

    /// <summary>ADR-0093 P1: a client that holds an instance without talking does not keep another client out.</summary>
    [Fact]
    public async Task IdleClient_DoesNotKeepASecondClientOut()
    {
        string name = RemoteDiscovery.CreatePipeName();
        var registry = new EditorRemoteCommandRegistry();
        registry.Register(new EditorRemoteCommand("ping", "test", null, true, false, call => call.Succeed("pong")));
        var dispatcher = new EditorRemoteDispatcher(registry);
        using var server = new EditorRemoteServer(dispatcher, name, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        server.Start();

        await using var idle = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await idle.ConnectAsync(5000);

        await using var active = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await active.ConnectAsync(5000);
        await RemoteMessageCodec.WriteAsync(active, new RemoteRequest { Id = 1, Method = "ping" }, CancellationToken.None);
        using var stop = new CancellationTokenSource(Timeout);
        Task<RemoteLine> pending = new RemoteLineReader(active).ReadLineAsync(stop.Token);
        while (!pending.IsCompleted)
        {
            dispatcher.Pump();
            await Task.Delay(5);
        }

        Assert.True(RemoteMessageCodec.TryDecodeResponse((await pending).Text, out RemoteResponse response));
        Assert.Equal("pong", response.Result!.Value.GetString());
    }

    /// <summary>Security review S2-7: every instance carries the same ACL, and none is missing while the server runs.</summary>
    [Fact]
    public void EveryInstance_HasTheCurrentUserOnlyAcl()
    {
        string name = RemoteDiscovery.CreatePipeName();
        using var server = new EditorRemoteServer(new EditorRemoteDispatcher(new EditorRemoteCommandRegistry()), name, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        server.Start();

        Assert.Equal(EditorRemoteServer.MaxClients, server.Instances.Count);
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var network = new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.NetworkSid, null);
        foreach (NamedPipeServerStream instance in server.Instances)
        {
            var rules = instance.GetAccessControl()
                .GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier))
                .Cast<System.IO.Pipes.PipeAccessRule>()
                .ToList();
            Assert.Contains(rules, r => r.IdentityReference == identity.User && r.AccessControlType == System.Security.AccessControl.AccessControlType.Allow);
            Assert.Contains(rules, r => r.IdentityReference == network && r.AccessControlType == System.Security.AccessControl.AccessControlType.Deny);
            Assert.DoesNotContain(rules, r => r.AccessControlType == System.Security.AccessControl.AccessControlType.Allow && r.IdentityReference != identity.User);
        }
    }

    /// <summary>Security review S2-7: clients that come through the network redirector are dropped on every instance.</summary>
    [Fact]
    public async Task RedirectorClients_AreDroppedOnEveryInstance()
    {
        string name = RemoteDiscovery.CreatePipeName();
        var registry = new EditorRemoteCommandRegistry();
        int runs = 0;
        registry.Register(new EditorRemoteCommand(RemoteProtocol.HelloMethod, "test", null, true, false, call =>
        {
            runs++;
            call.Succeed(new RemoteHelloResult());
        }));
        var dispatcher = new EditorRemoteDispatcher(registry);
        using var server = new EditorRemoteServer(dispatcher, name, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        server.Start();

        var clients = new List<NamedPipeClientStream>();
        try
        {
            for (int i = 0; i < EditorRemoteServer.MaxClients; i++)
            {
                var client = new NamedPipeClientStream("localhost", name, PipeDirection.InOut, PipeOptions.Asynchronous);
                try
                {
                    await client.ConnectAsync(5000);
                }
                catch (IOException)
                {
                    // The server can drop a redirector client while the connection is still being set up: the connect call
                    // then reports that nothing is at the other end of the pipe. That client was dropped, which is the claim.
                    await client.DisposeAsync();
                    continue;
                }

                clients.Add(client);
                await Record.ExceptionAsync(() => RemoteMessageCodec.WriteAsync(client, new RemoteRequest { Id = 1, Method = RemoteProtocol.HelloMethod }, CancellationToken.None));
            }

            dispatcher.Pump();
            foreach (NamedPipeClientStream client in clients)
            {
                using var readTimeout = new CancellationTokenSource(Timeout);
                var buffer = new byte[16];
                int read;
                try
                {
                    read = await client.ReadAsync(buffer, readTimeout.Token);
                }
                catch (IOException)
                {
                    read = 0; // A broken pipe also means the server dropped the client.
                }

                Assert.Equal(0, read);
            }

            dispatcher.Pump();
            Assert.Equal(0, runs);
        }
        finally
        {
            foreach (NamedPipeClientStream client in clients)
            {
                await client.DisposeAsync();
            }
        }
    }

    [Fact]
    public void Start_TwiceAndStop_AreSafe()
    {
        using var control = new EditorRemoteControl(CreateHost(), _directory);
        int changes = 0;
        control.RunningChanged += () => changes++;

        Assert.True(control.Start(refuseWhenElevated: false));
        Assert.True(control.Start(refuseWhenElevated: false));
        control.Stop();
        control.Stop();

        Assert.Equal(2, changes);
        Assert.False(control.IsRunning);
    }
}
