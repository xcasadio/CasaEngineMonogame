using System.Text.Json;
using CasaEngine.Editor.Mcp;
using CasaEngine.Editor.Remote;
using CasaEngine.Editor.Remote.Protocol;
using ModelContextProtocol.Protocol;
using Xunit;

namespace CasaEngine.Tests.Editor.Remote;

/// <summary>ADR-0092, task T4.2: editor commands as MCP tools, answers as MCP results, the link to the editor.</summary>
public sealed class EditorMcpServerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "casa-editor-mcp-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _capturesToDelete = new();

    public void Dispose()
    {
        foreach (string capture in _capturesToDelete)
        {
            File.Delete(capture);
        }

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static RemoteCommandDescriptor Command(string name, bool readOnly, bool destructive) => new()
    {
        Name = name,
        Description = "Does " + name,
        InputSchema = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{\"entity_id\":{\"type\":\"string\"}}}").RootElement.Clone(),
        ReadOnly = readOnly,
        Destructive = destructive,
    };

    [Fact]
    public void Build_OneToolPerCommand_WithHints_AndWithoutTheProtocolMethods()
    {
        var (tools, commandByTool) = EditorToolCatalog.Build(new[]
        {
            Command(RemoteProtocol.HelloMethod, true, false),
            Command(RemoteProtocol.ListCommandsMethod, true, false),
            Command("entity.get", true, false),
            Command("entity.delete", false, true),
        });

        Assert.Equal(new[] { "entity_get", "entity_delete" }, tools.Select(t => t.Name));
        Assert.Equal("entity.delete", commandByTool["entity_delete"]);
        Tool delete = tools[1];
        Assert.False(delete.Annotations!.ReadOnlyHint);
        Assert.True(delete.Annotations.DestructiveHint);
        Assert.True(tools[0].Annotations!.ReadOnlyHint);
        Assert.Contains(EditorToolCatalog.DataNotice, delete.Description);
        Assert.Equal("object", delete.InputSchema.GetProperty("type").GetString());
    }

    [Theory]
    [InlineData("editor.status", "editor_status")]
    [InlineData("history.undo", "history_undo")]
    [InlineData("a b/c", "a_b_c")]
    public void ToolNames_UseOnlySafeCharacters(string command, string tool)
    {
        Assert.Equal(tool, EditorToolCatalog.ToToolName(command));
    }

    [Fact]
    public void ToParams_PassesTheArgumentsThrough()
    {
        var arguments = new Dictionary<string, JsonElement> { ["name"] = JsonDocument.Parse("\"Player\"").RootElement.Clone() };

        Assert.Equal("Player", EditorToolCatalog.ToParams(arguments)!.Value.GetProperty("name").GetString());
        Assert.Null(EditorToolCatalog.ToParams(null));
        Assert.Null(EditorToolCatalog.ToParams(new Dictionary<string, JsonElement>()));
    }

    [Fact]
    public void Success_ACaptureInTheCaptureFolder_BecomesAnImage()
    {
        string captureDirectory = Path.Combine(Path.GetTempPath(), "CasaEngine", "editor-remote");
        Directory.CreateDirectory(captureDirectory);
        string capture = Path.Combine(captureDirectory, "capture-" + Guid.NewGuid().ToString("N") + ".png");
        File.WriteAllBytes(capture, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        _capturesToDelete.Add(capture);

        CallToolResult result = EditorToolCatalog.Success(RemoteMessageCodec.ToElement(new { file = capture, width = 1, height = 1, kind = "window" }));

        Assert.NotEqual(true, result.IsError);
        Assert.IsType<TextContentBlock>(result.Content[0]);
        var image = Assert.IsType<ImageContentBlock>(result.Content[1]);
        Assert.Equal("image/png", image.MimeType);
    }

    /// <summary>The MCP server reads only PNG files of the editor's capture folder, whatever path an answer names.</summary>
    [Fact]
    public void Success_AFileOutsideTheCaptureFolder_IsNotRead()
    {
        Directory.CreateDirectory(_directory);
        string elsewhere = Path.Combine(_directory, "secret.png");
        File.WriteAllBytes(elsewhere, new byte[] { 1, 2, 3 });

        CallToolResult result = EditorToolCatalog.Success(RemoteMessageCodec.ToElement(new { file = elsewhere, kind = "window" }));

        Assert.Single(result.Content);
    }

    [Fact]
    public void Failure_IsAnErrorResult()
    {
        CallToolResult result = EditorToolCatalog.Failure("refused");

        Assert.True(result.IsError);
        Assert.Equal("refused", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
    }

    [Fact]
    public async Task Connection_WithoutAnEditor_ExplainsHowToStartOne()
    {
        await using var connection = new EditorConnection(null, _directory);

        var (commands, call) = await connection.ListCommandsAsync(CancellationToken.None);

        Assert.Null(commands);
        Assert.Equal(EditorCallStatus.NoEditor, call.Status);
        Assert.Contains("--remote", call.Error);
    }

    /// <summary>ADR-0093 P2: a live editor that does not accept the connection in time is busy, not absent.</summary>
    [Fact]
    public async Task Connection_ToALiveEditorThatDoesNotAccept_IsBusy()
    {
        // A discovery record for this very process, whose pipe nobody serves: the process is alive, the pipe never answers.
        using var current = System.Diagnostics.Process.GetCurrentProcess();
        RemoteDiscovery.Write(_directory, new RemoteDiscoveryRecord
        {
            ProcessId = Environment.ProcessId,
            ProcessStartUtcTicks = current.StartTime.ToUniversalTime().Ticks,
            PipeName = RemoteDiscovery.CreatePipeName(),
            Token = RemoteDiscovery.CreateToken(),
        });
        await using var connection = new EditorConnection(null, _directory, connectTimeout: TimeSpan.FromMilliseconds(300));

        var (commands, call) = await connection.ListCommandsAsync(CancellationToken.None);

        Assert.Null(commands);
        Assert.Equal(EditorCallStatus.Busy, call.Status);
    }

    [Fact]
    public void CatalogState_KeepsItsToolsWhileTheEditorIsBusy_AndDropsThemWhenItIsGone()
    {
        var state = new EditorToolCatalogState();
        var commands = new[] { Command("entity.get", true, false) };

        Assert.True(state.Apply(commands, EditorCallStatus.Answered));
        Assert.Equal("entity_get", Assert.Single(state.Tools).Name);

        Assert.False(state.Apply(null, EditorCallStatus.Busy));
        Assert.Equal("entity_get", Assert.Single(state.Tools).Name);
        Assert.Equal("entity.get", state.FindCommand("entity_get"));
        Assert.Equal(EditorCallStatus.Busy, state.LastStatus);

        Assert.True(state.Apply(null, EditorCallStatus.NoEditor));
        Assert.Equal(EditorToolCatalog.StatusToolName, Assert.Single(state.Tools).Name);
        Assert.Null(state.FindCommand("entity_get"));
    }

    [Fact]
    public async Task Connection_AfterDispose_RefusesCalls()
    {
        var connection = new EditorConnection(null, _directory);
        await connection.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => connection.CallAsync("editor.status", null, CancellationToken.None));
    }

    [Fact]
    public async Task Connection_ListsAndCallsARealEditor()
    {
        var host = new RemoteTestHost { CurrentWorld = new CasaEngine.Framework.Scene.World.World { Name = "Level" } };
        using var control = new EditorRemoteControl(host, _directory);
        Assert.True(control.Start(refuseWhenElevated: false));
        using var stopPump = new CancellationTokenSource();
        var pump = new Thread(() =>
        {
            while (!stopPump.IsCancellationRequested)
            {
                control.Pump();
                Thread.Sleep(5);
            }
        })
        { IsBackground = true };
        pump.Start();

        await using (var connection = new EditorConnection(null, _directory))
        {
            var (commands, _) = await connection.ListCommandsAsync(CancellationToken.None);
            var (tools, commandByTool) = EditorToolCatalog.Build(commands!);
            Assert.Contains(tools, t => t.Name == "entity_create" && t.Annotations!.ReadOnlyHint == false);
            Assert.Contains(tools, t => t.Name == "play_start" && t.Annotations!.DestructiveHint == true);

            EditorCallResult status = await connection.CallAsync(commandByTool["editor_status"], null, CancellationToken.None);
            Assert.Equal("Level", status.Result!.Value.GetProperty("world_name").GetString());

            EditorCallResult refused = await connection.CallAsync("entity.get", EditorToolCatalog.ToParams(new Dictionary<string, JsonElement>
            {
                ["entity_id"] = JsonDocument.Parse("\"nope\"").RootElement.Clone(),
            }), CancellationToken.None);
            Assert.Equal(EditorCallStatus.Failed, refused.Status);
            Assert.Contains("invalid_argument", refused.Error);
        }

        stopPump.Cancel();
        pump.Join();
    }

    /// <summary>Verifier finding F1: the MCP server releases the editor's only pipe after each call, so the CLI gets in.</summary>
    [Fact]
    public async Task Connection_ReleasesThePipe_AfterEachCall()
    {
        var host = new RemoteTestHost();
        using var control = new EditorRemoteControl(host, _directory);
        Assert.True(control.Start(refuseWhenElevated: false));
        using var stopPump = new CancellationTokenSource();
        var pump = new Thread(() =>
        {
            while (!stopPump.IsCancellationRequested)
            {
                control.Pump();
                Thread.Sleep(5);
            }
        })
        { IsBackground = true };
        pump.Start();

        await using (var connection = new EditorConnection(null, _directory))
        {
            var (commands, _) = await connection.ListCommandsAsync(CancellationToken.None);
            Assert.NotNull(commands);

            RemoteDiscoveryRecord record = Assert.Single(RemoteDiscovery.ReadAll(_directory));
            await using RemoteEditorClient other = await RemoteEditorClient.ConnectAsync(record, TimeSpan.FromSeconds(10), CancellationToken.None);
            Assert.NotNull(other.Hello);
        }

        stopPump.Cancel();
        pump.Join();
    }
}
