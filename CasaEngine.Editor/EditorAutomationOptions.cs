using System;
using System.Globalization;

namespace CasaEngine.Editor;

public sealed class EditorAutomationOptions
{
    public string? ProjectPath { get; private set; }
    public string? OpenAssetPath { get; private set; }
    public string? ActivatePanelId { get; private set; }
    public string? ContentBrowserFolderPath { get; private set; }
    public string? ContentBrowserScrollTarget { get; private set; }
    public string? DockResizeTarget { get; private set; }
    public string? DockInputDragTarget { get; private set; }
    public string? CreateParticleAssetFolder { get; private set; }
    public string? CreateParticlePresetName { get; private set; }
    public string? DropParticleAssetPath { get; private set; }
    public string? SetParticlePropertyKey { get; private set; }
    public string? SetParticlePropertyValue { get; private set; }
    public bool ParticleUndoRedoSmoke { get; private set; }
    public string? SetMaterialPropertyKey { get; private set; }
    public string? SetMaterialPropertyValue { get; private set; }
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

                case "--activate-panel":
                    options.ActivatePanelId = next;
                    index++;
                    break;

                case "--content-folder":
                    options.ContentBrowserFolderPath = next;
                    index++;
                    break;

                case "--content-scroll":
                    options.ContentBrowserScrollTarget = next;
                    index++;
                    break;

                case "--dock-resize":
                    options.DockResizeTarget = next;
                    index++;
                    break;

                case "--dock-input-drag":
                    options.DockInputDragTarget = next;
                    index++;
                    break;

                case "--create-particle-asset":
                    options.CreateParticleAssetFolder = next;
                    index++;
                    break;

                case "--create-particle-preset":
                    options.CreateParticlePresetName = next;
                    index++;
                    break;

                case "--drop-particle-asset":
                    options.DropParticleAssetPath = next;
                    index++;
                    break;

                case "--set-particle-property":
                    int particleSeparatorIndex = next.IndexOf('=');
                    if (particleSeparatorIndex > 0 && particleSeparatorIndex < next.Length - 1)
                    {
                        options.SetParticlePropertyKey = next[..particleSeparatorIndex];
                        options.SetParticlePropertyValue = next[(particleSeparatorIndex + 1)..];
                    }

                    index++;
                    break;

                case "--set-material-property":
                    int separatorIndex = next.IndexOf('=');
                    if (separatorIndex > 0 && separatorIndex < next.Length - 1)
                    {
                        options.SetMaterialPropertyKey = next[..separatorIndex];
                        options.SetMaterialPropertyValue = next[(separatorIndex + 1)..];
                    }

                    index++;
                    break;

                case "--particle-undo-redo-smoke":
                    options.ParticleUndoRedoSmoke = true;
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