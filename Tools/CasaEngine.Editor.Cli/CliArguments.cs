using System.Globalization;
using System.Text.Json;

namespace CasaEngine.Editor.Cli;

public enum CliVerb
{
    Help,
    Editors,
    Commands,
    Call,
    Run,
}

/// <summary>Parsed command line of <c>casa-editor</c>. Parsing never throws: an error is reported in <see cref="Error"/>.</summary>
public sealed class CliArguments
{
    public const string Usage =
        """
        casa-editor: drives a running CasaEngine editor started with --remote (or Tools > Remote Control).

        Usage:
          casa-editor editors                         List the editors serving remote control.
          casa-editor commands [options]              List the editor commands and their arguments.
          casa-editor call <command> [json] [options] Call one command; json is its argument object.
          casa-editor run <script.json> [options]     Call a list of commands in order; stops at the first error.

        Options:
          --pid <id>        Editor process id, needed when several editors serve.
          --timeout <s>     Seconds to wait for each answer (default 120).

        A script is a JSON array: [{"command": "entity.create", "params": {"name": "Player"}}, ...]
        Output is JSON on stdout. Exit code: 0 success, 1 the editor refused a command, 2 usage error, 3 no editor.
        """;

    public CliVerb Verb { get; private init; } = CliVerb.Help;

    public string? Command { get; private init; }

    public JsonElement? Params { get; private init; }

    public string? ScriptPath { get; private init; }

    public int? ProcessId { get; private init; }

    public TimeSpan Timeout { get; private init; } = TimeSpan.FromSeconds(120);

    public string? Error { get; private init; }

    public static CliArguments Parse(IReadOnlyList<string> args)
    {
        var positional = new List<string>();
        int? processId = null;
        TimeSpan timeout = TimeSpan.FromSeconds(120);

        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            if (arg is "--pid" or "--timeout")
            {
                if (i + 1 >= args.Count)
                {
                    return Fail($"{arg} needs a value.");
                }

                string value = args[++i];
                if (arg == "--pid")
                {
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int pid) || pid <= 0)
                    {
                        return Fail($"--pid expects a process id, got '{value}'.");
                    }

                    processId = pid;
                }
                else
                {
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) || seconds <= 0 || seconds > 3600)
                    {
                        return Fail($"--timeout expects seconds from 0 to 3600, got '{value}'.");
                    }

                    timeout = TimeSpan.FromSeconds(seconds);
                }
            }
            else if (arg is "-h" or "--help" or "help")
            {
                return new CliArguments();
            }
            else if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                return Fail($"Unknown option '{arg}'.");
            }
            else
            {
                positional.Add(arg);
            }
        }

        if (positional.Count == 0)
        {
            return new CliArguments();
        }

        switch (positional[0])
        {
            case "editors" when positional.Count == 1:
                return new CliArguments { Verb = CliVerb.Editors, ProcessId = processId, Timeout = timeout };
            case "commands" when positional.Count == 1:
                return new CliArguments { Verb = CliVerb.Commands, ProcessId = processId, Timeout = timeout };
            case "call" when positional.Count is 2 or 3:
                JsonElement? parameters = null;
                if (positional.Count == 3)
                {
                    if (!TryParseObject(positional[2], out JsonElement parsed, out string? error))
                    {
                        return Fail(error!);
                    }

                    parameters = parsed;
                }

                return new CliArguments { Verb = CliVerb.Call, Command = positional[1], Params = parameters, ProcessId = processId, Timeout = timeout };
            case "run" when positional.Count == 2:
                return new CliArguments { Verb = CliVerb.Run, ScriptPath = positional[1], ProcessId = processId, Timeout = timeout };
            default:
                return Fail($"Unexpected arguments: {string.Join(' ', positional)}");
        }
    }

    internal static bool TryParseObject(string json, out JsonElement value, out string? error)
    {
        value = default;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "The command arguments must be a JSON object.";
                return false;
            }

            value = document.RootElement.Clone();
            error = null;
            return true;
        }
        catch (JsonException ex)
        {
            error = "The command arguments are not valid JSON: " + ex.Message;
            return false;
        }
    }

    private static CliArguments Fail(string error) => new() { Verb = CliVerb.Help, Error = error };
}
