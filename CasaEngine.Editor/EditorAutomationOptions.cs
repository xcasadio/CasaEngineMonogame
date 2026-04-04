using System;
using System.Globalization;

namespace CasaEngine.Editor;

public sealed class EditorAutomationOptions
{
    public string? ProjectPath { get; private set; }
    public string? OpenAssetPath { get; private set; }
    public string? EntityName { get; private set; }
    public int EntityIndex { get; private set; }
    public string? ComponentName { get; private set; }
    public string? DiagnosticsOutputPath { get; private set; }
    public double CaptureDelaySeconds { get; private set; } = 6.0;
    public bool ExitAfterCapture { get; private set; } = true;

    public bool HasAutomation => !string.IsNullOrWhiteSpace(ProjectPath);

    public static EditorAutomationOptions Parse(string[] args)
    {
        var options = new EditorAutomationOptions();

        for (int index = 0; index < args.Length; index++)
        {
            string arg = args[index];
            string next = index + 1 < args.Length ? args[index + 1] : string.Empty;

            switch (arg.ToLowerInvariant())
            {
                case "--project":
                    options.ProjectPath = next;
                    index++;
                    break;

                case "--entity":
                    options.EntityName = next;
                    index++;
                    break;

                case "--open-asset":
                    options.OpenAssetPath = next;
                    index++;
                    break;

                case "--entity-index":
                    if (int.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out int entityIndex))
                    {
                        options.EntityIndex = Math.Max(0, entityIndex);
                    }

                    index++;
                    break;

                case "--component":
                    options.ComponentName = next;
                    index++;
                    break;

                case "--diagnostics-out":
                    options.DiagnosticsOutputPath = next;
                    index++;
                    break;

                case "--capture-delay":
                    if (double.TryParse(next, NumberStyles.Float, CultureInfo.InvariantCulture, out double captureDelaySeconds))
                    {
                        options.CaptureDelaySeconds = Math.Max(0.5, captureDelaySeconds);
                    }

                    index++;
                    break;

                case "--keep-open":
                    options.ExitAfterCapture = false;
                    break;
            }
        }

        return options;
    }
}