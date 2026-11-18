using System;
using System.Globalization;

namespace CasaEngine.Editor;

public sealed class EditorAutomationOptions
{
    private const double DefaultCaptureDelaySeconds = 6.0;

    public string ProjectPath { get; private set; }
    public string OpenAssetPath { get; private set; }
    public string ActivatePanelId { get; private set; }
    public string ContentBrowserFolderPath { get; private set; }
    public string ContentBrowserScrollTarget { get; private set; }
    public string DockResizeTarget { get; private set; }
    public string DockInputDragTarget { get; private set; }
    public string CreateParticleAssetFolder { get; private set; }
    public string CreateParticlePresetName { get; private set; }
    public string DropParticleAssetPath { get; private set; }
    public string SetParticlePropertyKey { get; private set; }
    public string SetParticlePropertyValue { get; private set; }
    public bool ParticleUndoRedoSmoke { get; private set; }
    public string SetMaterialPropertyKey { get; private set; }
    public string SetMaterialPropertyValue { get; private set; }
    public string SetScreenPropertyNodeName { get; private set; }
    public string SetScreenPropertyName { get; private set; }
    public string SetScreenPropertyValue { get; private set; }
    public bool SaveProject { get; private set; }
    public string EntityName { get; private set; }
    public int EntityIndex { get; private set; }
    public string ComponentName { get; private set; }
    public string DiagnosticsOutputPath { get; private set; }
    public bool PlaySmoke { get; private set; }
    public bool CutscenePreviewPlay { get; private set; }
    public string CutsceneSelectActionPath { get; private set; }
    public string CutsceneAddActionType { get; private set; }
    public bool CutsceneReactivate { get; private set; }

    /// <summary>
    /// Play times, in seconds, the cutscene preview goes to one after the other (<c>--cutscene-preview-seek</c>). Empty when
    /// the option is absent or malformed: a list with an unreadable or negative value is ignored as a whole.
    /// </summary>
    public double[] CutscenePreviewSeekSeconds { get; private set; } = Array.Empty<double>();

    /// <summary>
    /// Frames the cutscene preview moves once its seeks are done (<c>--cutscene-preview-step</c>): forward when positive, back
    /// (one previous-frame move each) when negative; 0 when absent or not an integer, which is ignored.
    /// </summary>
    public int CutscenePreviewStepFrames { get; private set; }

    /// <summary>
    /// True when the cutscene edits (<c>--cutscene-select-action</c>, <c>--cutscene-add-action</c>) are applied after the seeks
    /// and steps of the preview instead of before (<c>--cutscene-edit-after-transport</c>).
    /// </summary>
    public bool CutsceneEditAfterTransport { get; private set; }

    /// <summary>
    /// The structure operations of the cutscene (<c>--cutscene-edit-ops "select:path;up;paste"</c>, ADR-0101 P14), applied one
    /// per frame after the select and add edits. Empty when the option is absent.
    /// </summary>
    public CutsceneEditOperation[] CutsceneEditOperations { get; private set; } = Array.Empty<CutsceneEditOperation>();

    /// <summary>
    /// True when the cutscene preview play is resumed, as the Play button does during a pause, after the seeks, the steps and
    /// the edits (<c>--cutscene-preview-resume</c>). Ignored when <see cref="CutscenePreviewStop"/> is set.
    /// </summary>
    public bool CutscenePreviewResume { get; private set; }

    /// <summary>True when the cutscene preview is stopped after the seeks and steps (<c>--cutscene-preview-stop</c>).</summary>
    public bool CutscenePreviewStop { get; private set; }
    public string ScreenshotOutputPath { get; private set; }
    public double CaptureDelaySeconds { get; private set; } = DefaultCaptureDelaySeconds;
    public bool ExitAfterCapture { get; private set; } = true;
    private bool EntityIndexSpecified { get; set; }
    private bool CaptureDelaySpecified { get; set; }

    public bool HasProjectPath => !string.IsNullOrWhiteSpace(ProjectPath);
    public bool HasAutomation => !string.IsNullOrWhiteSpace(EntityName)
        || EntityIndexSpecified
        || !string.IsNullOrWhiteSpace(ComponentName)
        || !string.IsNullOrWhiteSpace(ActivatePanelId)
        || !string.IsNullOrWhiteSpace(ContentBrowserFolderPath)
        || !string.IsNullOrWhiteSpace(ContentBrowserScrollTarget)
        || !string.IsNullOrWhiteSpace(DockResizeTarget)
        || !string.IsNullOrWhiteSpace(DockInputDragTarget)
        || !string.IsNullOrWhiteSpace(CreateParticleAssetFolder)
        || !string.IsNullOrWhiteSpace(CreateParticlePresetName)
        || !string.IsNullOrWhiteSpace(DropParticleAssetPath)
        || !string.IsNullOrWhiteSpace(SetParticlePropertyKey)
        || ParticleUndoRedoSmoke
        || !string.IsNullOrWhiteSpace(SetMaterialPropertyKey)
        || !string.IsNullOrWhiteSpace(SetScreenPropertyNodeName)
        || SaveProject
        || !string.IsNullOrWhiteSpace(DiagnosticsOutputPath)
        || PlaySmoke
        || CutscenePreviewPlay
        || !string.IsNullOrWhiteSpace(CutsceneSelectActionPath)
        || !string.IsNullOrWhiteSpace(CutsceneAddActionType)
        || CutsceneReactivate
        || CutscenePreviewSeekSeconds.Length > 0
        || CutscenePreviewStepFrames != 0
        || CutscenePreviewStop
        || CutsceneEditAfterTransport
        || CutsceneEditOperations.Length > 0
        || CutscenePreviewResume
        || !string.IsNullOrWhiteSpace(ScreenshotOutputPath)
        || CaptureDelaySpecified;

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

                case "--set-screen-property":
                    // <NodeName>:<Property>=<Value> -- node name before the first ':', property name
                    // before the first '=' found after it, value after that '='.
                    int nodeSeparatorIndex = next.IndexOf(':');
                    if (nodeSeparatorIndex > 0)
                    {
                        int propertySeparatorIndex = next.IndexOf('=', nodeSeparatorIndex + 1);
                        if (propertySeparatorIndex > nodeSeparatorIndex + 1 && propertySeparatorIndex < next.Length - 1)
                        {
                            options.SetScreenPropertyNodeName = next[..nodeSeparatorIndex];
                            options.SetScreenPropertyName = next[(nodeSeparatorIndex + 1)..propertySeparatorIndex];
                            options.SetScreenPropertyValue = next[(propertySeparatorIndex + 1)..];
                        }
                    }

                    index++;
                    break;

                case "--save-project":
                    options.SaveProject = true;
                    break;

                case "--entity-index":
                    if (int.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out int entityIndex))
                    {
                        options.EntityIndex = Math.Max(0, entityIndex);
                        options.EntityIndexSpecified = true;
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

                case "--play-smoke":
                    options.PlaySmoke = true;
                    break;

                case "--cutscene-preview-play":
                    options.CutscenePreviewPlay = true;
                    break;

                case "--cutscene-select-action":
                    options.CutsceneSelectActionPath = next;
                    index++;
                    break;

                case "--cutscene-add-action":
                    options.CutsceneAddActionType = next;
                    index++;
                    break;

                case "--cutscene-reactivate":
                    options.CutsceneReactivate = true;
                    break;

                case "--cutscene-preview-seek":
                    options.CutscenePreviewSeekSeconds = ParseSeekSeconds(next);
                    index++;
                    break;

                case "--cutscene-preview-step":
                    if (int.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out int stepFrames) && stepFrames != 0)
                    {
                        options.CutscenePreviewStepFrames = stepFrames;
                    }

                    index++;
                    break;

                case "--cutscene-preview-stop":
                    options.CutscenePreviewStop = true;
                    break;

                case "--cutscene-edit-after-transport":
                    options.CutsceneEditAfterTransport = true;
                    break;

                case "--cutscene-edit-ops":
                    options.CutsceneEditOperations = CutsceneEditOperation.ParseList(next);
                    index++;
                    break;

                case "--cutscene-preview-resume":
                    options.CutscenePreviewResume = true;
                    break;

                case "--screenshot-out":
                    options.ScreenshotOutputPath = next;
                    index++;
                    break;

                case "--capture-delay":
                    if (double.TryParse(next, NumberStyles.Float, CultureInfo.InvariantCulture, out double captureDelaySeconds))
                    {
                        options.CaptureDelaySeconds = Math.Max(0.5, captureDelaySeconds);
                        options.CaptureDelaySpecified = true;
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

    private static double[] ParseSeekSeconds(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<double>();
        }

        string[] parts = value.Split(',');
        var seconds = new double[parts.Length];
        for (int index = 0; index < parts.Length; index++)
        {
            if (!double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                || double.IsNaN(parsed)
                || double.IsInfinity(parsed)
                || parsed < 0d)
            {
                return Array.Empty<double>();
            }

            seconds[index] = parsed;
        }

        return seconds;
    }
}
