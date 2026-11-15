using CasaEngine.Editor.Cli;
using CasaEngine.Editor.Remote.Protocol;
using Xunit;

namespace CasaEngine.Tests.Editor.Remote;

/// <summary>ADR-0092, task T4.1: the casa-editor command line, without an editor.</summary>
public sealed class CasaEditorCliTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "casa-editor-cli-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Arguments_Call_WithParamsAndOptions()
    {
        CliArguments arguments = CliArguments.Parse(new[] { "call", "entity.create", "{\"name\":\"Player\"}", "--pid", "42", "--timeout", "5" });

        Assert.Null(arguments.Error);
        Assert.Equal(CliVerb.Call, arguments.Verb);
        Assert.Equal("entity.create", arguments.Command);
        Assert.Equal("Player", arguments.Params!.Value.GetProperty("name").GetString());
        Assert.Equal(42, arguments.ProcessId);
        Assert.Equal(TimeSpan.FromSeconds(5), arguments.Timeout);
    }

    [Theory]
    [InlineData(new[] { "editors" }, CliVerb.Editors)]
    [InlineData(new[] { "commands" }, CliVerb.Commands)]
    [InlineData(new[] { "run", "script.json" }, CliVerb.Run)]
    [InlineData(new[] { "call", "editor.status" }, CliVerb.Call)]
    [InlineData(new string[0], CliVerb.Help)]
    [InlineData(new[] { "--help" }, CliVerb.Help)]
    public void Arguments_Verbs(string[] args, CliVerb verb)
    {
        CliArguments arguments = CliArguments.Parse(args);

        Assert.Null(arguments.Error);
        Assert.Equal(verb, arguments.Verb);
    }

    [Theory]
    [InlineData("call")]
    [InlineData("call", "x", "[1]")]
    [InlineData("call", "x", "{ broken")]
    [InlineData("call", "x", "{}", "extra")]
    [InlineData("frobnicate")]
    [InlineData("commands", "--pid")]
    [InlineData("commands", "--pid", "-3")]
    [InlineData("commands", "--timeout", "0")]
    [InlineData("commands", "--verbose")]
    public void Arguments_Errors(params string[] args)
    {
        Assert.NotNull(CliArguments.Parse(args).Error);
    }

    [Fact]
    public void Script_ParsesSteps()
    {
        Assert.True(CliScript.TryParse("[{\"command\":\"entity.create\",\"params\":{\"name\":\"A\"}},{\"command\":\"project.save\"}]", out var steps, out _));

        Assert.Equal(2, steps.Count);
        Assert.Equal("A", steps[0].Params!.Value.GetProperty("name").GetString());
        Assert.Null(steps[1].Params);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[{\"params\":{}}]")]
    [InlineData("[{\"command\":3}]")]
    [InlineData("[{\"command\":\"x\",\"params\":[1]}]")]
    [InlineData("not json")]
    public void Script_Errors(string json)
    {
        Assert.False(CliScript.TryParse(json, out _, out string error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void SelectEditor_OneByDefault_SeveralNeedAPid()
    {
        var one = new RemoteDiscoveryRecord { ProcessId = 10 };
        var two = new RemoteDiscoveryRecord { ProcessId = 20 };

        Assert.False(CliApp.TrySelectEditor(Array.Empty<RemoteDiscoveryRecord>(), null, out _, out _));
        Assert.True(CliApp.TrySelectEditor(new[] { one }, null, out var selected, out _));
        Assert.Same(one, selected);
        Assert.False(CliApp.TrySelectEditor(new[] { one, two }, null, out _, out string error));
        Assert.Contains("--pid", error);
        Assert.True(CliApp.TrySelectEditor(new[] { one, two }, 20, out selected, out _));
        Assert.Same(two, selected);
        Assert.False(CliApp.TrySelectEditor(new[] { one }, 99, out _, out _));
    }

    [Fact]
    public async Task Run_WithoutAnEditor_ExitsWithNoEditor()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        int code = await CliApp.RunAsync(new[] { "commands" }, output, error, _directory);

        Assert.Equal(CliApp.NoEditor, code);
        Assert.Contains("--remote", error.ToString());
    }

    [Fact]
    public async Task Run_UsageError_ExitsWithUsage()
    {
        var error = new StringWriter();

        Assert.Equal(CliApp.UsageError, await CliApp.RunAsync(new[] { "call" }, new StringWriter(), error, _directory));
        Assert.Contains("Usage", error.ToString());
    }

    /// <summary>The CLI talks to a real editor pipe: a call, then a script that stops at the first refused step.</summary>
    [Fact]
    public async Task CallAndRun_AgainstARealServer()
    {
        var host = new RemoteTestHost { CurrentWorld = new CasaEngine.Framework.Scene.World.World { Name = "Level" } };
        using var control = new CasaEngine.Editor.Remote.EditorRemoteControl(host, _directory);
        Assert.True(control.Start(refuseWhenElevated: false));
        // A dedicated thread stands in for the editor's game loop, out of the thread pool the other tests load.
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

        var output = new StringWriter();
        Assert.Equal(CliApp.Success, await CliApp.RunAsync(new[] { "call", "editor.status", "--timeout", "60" }, output, new StringWriter(), _directory));
        Assert.Contains("\"world_name\": \"Level\"", output.ToString());

        string script = Path.Combine(_directory, "script.json");
        File.WriteAllText(script, "[{\"command\":\"selection.get\"},{\"command\":\"entity.get\",\"params\":{\"entity_id\":\"nope\"}},{\"command\":\"editor.status\"}]");
        output = new StringWriter();
        var runError = new StringWriter();
        int runCode = await CliApp.RunAsync(new[] { "run", script, "--timeout", "60" }, output, runError, _directory);
        Assert.True(runCode == CliApp.CommandFailed, $"exit {runCode}: {runError} {output}");
        Assert.Contains("invalid_argument", output.ToString());
        Assert.DoesNotContain("world_name", output.ToString());

        stopPump.Cancel();
        pump.Join();
    }

    [Fact]
    public async Task Editors_ListsWithoutTheToken()
    {
        using var current = System.Diagnostics.Process.GetCurrentProcess();
        string token = RemoteDiscovery.CreateToken();
        RemoteDiscovery.Write(_directory, new RemoteDiscoveryRecord
        {
            ProcessId = Environment.ProcessId,
            ProcessStartUtcTicks = current.StartTime.ToUniversalTime().Ticks,
            PipeName = RemoteDiscovery.CreatePipeName(),
            Token = token,
            ProjectPath = "C:/game/game.casaproject",
        });
        var output = new StringWriter();

        Assert.Equal(CliApp.Success, await CliApp.RunAsync(new[] { "editors" }, output, new StringWriter(), _directory));

        Assert.Contains("game.casaproject", output.ToString());
        Assert.DoesNotContain(token, output.ToString());
    }
}
