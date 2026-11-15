using System.Text.Json;

namespace CasaEngine.Editor.Cli;

/// <summary>One step of a <c>casa-editor run</c> script.</summary>
public sealed record CliScriptStep(string Command, JsonElement? Params);

/// <summary>Reads a script: a JSON array of <c>{"command": "...", "params": {...}}</c>.</summary>
public static class CliScript
{
    public const int MaxSteps = 1000;

    public static bool TryParse(string json, out IReadOnlyList<CliScriptStep> steps, out string? error)
    {
        steps = Array.Empty<CliScriptStep>();
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            error = "The script is not valid JSON: " + ex.Message;
            return false;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                error = "A script is a JSON array of steps.";
                return false;
            }

            var parsed = new List<CliScriptStep>();
            int index = 0;
            foreach (JsonElement step in document.RootElement.EnumerateArray())
            {
                if (++index > MaxSteps)
                {
                    error = $"A script has at most {MaxSteps} steps.";
                    return false;
                }

                if (step.ValueKind != JsonValueKind.Object
                    || !step.TryGetProperty("command", out JsonElement command)
                    || command.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(command.GetString()))
                {
                    error = $"Step {index} needs a \"command\" string.";
                    return false;
                }

                JsonElement? parameters = null;
                if (step.TryGetProperty("params", out JsonElement paramsElement) && paramsElement.ValueKind != JsonValueKind.Null)
                {
                    if (paramsElement.ValueKind != JsonValueKind.Object)
                    {
                        error = $"Step {index}: \"params\" must be an object.";
                        return false;
                    }

                    parameters = paramsElement.Clone();
                }

                parsed.Add(new CliScriptStep(command.GetString()!, parameters));
            }

            steps = parsed;
            error = null;
            return true;
        }
    }
}
