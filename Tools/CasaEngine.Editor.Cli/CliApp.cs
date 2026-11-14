using System.Text.Json;
using CasaEngine.Editor.Remote.Protocol;

namespace CasaEngine.Editor.Cli;

/// <summary>Runs <c>casa-editor</c> (ADR-0092): finds the editor, calls it, prints JSON.</summary>
public static class CliApp
{
    public const int Success = 0;
    public const int CommandFailed = 1;
    public const int UsageError = 2;
    public const int NoEditor = 3;

    /// <summary>Longest wait for the pipe and the handshake; <c>--timeout</c> below it shortens it.</summary>
    public static readonly TimeSpan MaxConnectTimeout = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions OutputOptions = new(RemoteProtocol.JsonOptions) { WriteIndented = true };

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, string? discoveryDirectory = null, CancellationToken cancellationToken = default)
    {
        CliArguments arguments = CliArguments.Parse(args);
        if (arguments.Error != null)
        {
            await error.WriteLineAsync(arguments.Error);
            await error.WriteLineAsync(CliArguments.Usage);
            return UsageError;
        }

        if (arguments.Verb == CliVerb.Help)
        {
            await output.WriteLineAsync(CliArguments.Usage);
            return Success;
        }

        IReadOnlyList<RemoteDiscoveryRecord> editors = RemoteDiscovery.ReadAll(discoveryDirectory ?? RemoteDiscovery.DefaultDirectory);
        if (arguments.Verb == CliVerb.Editors)
        {
            // The token stays out of the output: it only proves the pipe belongs to the editor.
            await WriteAsync(output, editors.Select(e => new { process_id = e.ProcessId, project_path = e.ProjectPath }).ToList());
            return Success;
        }

        IReadOnlyList<CliScriptStep> steps;
        switch (arguments.Verb)
        {
            case CliVerb.Commands:
                steps = new[] { new CliScriptStep(RemoteProtocol.ListCommandsMethod, null) };
                break;
            case CliVerb.Call:
                steps = new[] { new CliScriptStep(arguments.Command!, arguments.Params) };
                break;
            default:
                string script;
                try
                {
                    script = await File.ReadAllTextAsync(arguments.ScriptPath!, cancellationToken);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    await error.WriteLineAsync($"Cannot read the script '{arguments.ScriptPath}': {ex.Message}");
                    return UsageError;
                }

                if (!CliScript.TryParse(script, out steps, out string? scriptError))
                {
                    await error.WriteLineAsync(scriptError);
                    return UsageError;
                }

                break;
        }

        if (!TrySelectEditor(editors, arguments.ProcessId, out RemoteDiscoveryRecord? editor, out string? selectionError))
        {
            await error.WriteLineAsync(selectionError);
            return NoEditor;
        }

        RemoteEditorClient client;
        try
        {
            TimeSpan connectTimeout = arguments.Timeout < MaxConnectTimeout ? arguments.Timeout : MaxConnectTimeout;
            client = await RemoteEditorClient.ConnectAsync(editor!, connectTimeout, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or UnauthorizedAccessException or RemoteEditorException)
        {
            await error.WriteLineAsync($"Cannot connect to the editor {editor!.ProcessId}: {ex.Message}");
            return NoEditor;
        }

        await using (client)
        {
            var results = new List<object>();
            foreach (CliScriptStep step in steps)
            {
                try
                {
                    JsonElement result = await client.CallAsync(step.Command, step.Params, arguments.Timeout, cancellationToken);
                    results.Add(new { command = step.Command, result });
                }
                catch (RemoteEditorException ex)
                {
                    results.Add(new { command = step.Command, error = ex.Error });
                    await WriteResultsAsync(output, arguments.Verb, results);
                    return CommandFailed;
                }
                catch (Exception ex) when (ex is IOException or OperationCanceledException)
                {
                    await WriteResultsAsync(output, arguments.Verb, results);
                    await error.WriteLineAsync($"The connection to the editor failed during '{step.Command}': {ex.Message}");
                    return NoEditor;
                }
            }

            await WriteResultsAsync(output, arguments.Verb, results);
            return Success;
        }
    }

    /// <summary>The editor to talk to: the one with <paramref name="processId"/>, or the only one serving.</summary>
    public static bool TrySelectEditor(IReadOnlyList<RemoteDiscoveryRecord> editors, int? processId, out RemoteDiscoveryRecord? editor, out string? error)
    {
        editor = null;
        if (processId.HasValue)
        {
            editor = editors.FirstOrDefault(e => e.ProcessId == processId.Value);
            error = editor == null ? $"No editor with process id {processId.Value} serves remote control." : null;
            return editor != null;
        }

        switch (editors.Count)
        {
            case 0:
                error = "No editor serves remote control: start it with --remote or use Tools > Remote Control.";
                return false;
            case 1:
                editor = editors[0];
                error = null;
                return true;
            default:
                error = "Several editors serve remote control: choose one with --pid (see 'casa-editor editors').";
                return false;
        }
    }

    private static Task WriteResultsAsync(TextWriter output, CliVerb verb, List<object> results)
        => verb == CliVerb.Run ? WriteAsync(output, results) : WriteAsync(output, results.Count == 1 ? results[0] : results);

    private static Task WriteAsync<T>(TextWriter output, T value)
        => output.WriteLineAsync(JsonSerializer.Serialize(value, OutputOptions));
}
