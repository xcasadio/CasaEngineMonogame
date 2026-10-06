using System.ComponentModel;

namespace CasaEngine.Framework.Configuration.Project;

public class ProjectSettings
{
    [Category("Project")]
    public string WindowTitle { get; set; } = "Game name undefined";

    [Category("Project")]
    public string ProjectName { get; set; } = "Project name undefined";

    [Category("Start")]
    public string FirstScreenName { get; set; } = string.Empty;

    [Category("Project")]
    public bool AllowUserResizing { get; set; }

    [Category("Project")]
    public bool IsFixedTimeStep { get; set; }

    [Category("Project")]
    public bool IsMouseVisible { get; set; }

    [Category("Game")]
    public string FirstWorldLoaded { get; set; } = string.Empty;

    [Category("Gameplay")]
    public string GameplayDllName { get; set; } = string.Empty;

    /// <summary>
    /// Optional path (relative to the project directory) of the gameplay scripts
    /// csproj. When set, the editor can rebuild the gameplay dll on Play.
    /// </summary>
    [Category("Gameplay")]
    public string GameplayCsprojName { get; set; } = string.Empty;

    /// <summary>
    /// Optional id or name of a <c>.uiscreen</c> asset whose markup replaces the engine's built-in dialogue box
    /// (<see cref="CasaEngine.Framework.Dialogue.UI.DialogueScreen"/>). Empty: the built-in markup. When the asset
    /// cannot be loaded, or does not declare the elements the dialogue screen drives, the screen logs a warning and
    /// uses the built-in markup.
    /// </summary>
    [Category("UI")]
    public string DialogueScreenAsset { get; set; } = string.Empty;

    /// <summary>
    /// Mutes the <see cref="CasaEngine.Framework.Audio.Mixing.AudioBusNames.Master"/> bus for this
    /// project (ADR-0040). Applied at startup and, in the editor, on every project load; no user
    /// interface, edited directly in the project file.
    /// </summary>
    [Category("Audio")]
    public bool IsAudioMuted { get; set; }

    /// <summary>
    /// Switches the Master limiter (<see cref="CasaEngine.Framework.Audio.AudioService.MasterLimiter"/>) on or off for this
    /// project. Null (absent from the project file) means on. Applied at startup and, in the editor, on every project load;
    /// no user interface, edited directly in the project file.
    /// </summary>
    [Category("Audio")]
    public bool? IsMasterLimiterEnabled { get; set; }

    /// <summary>
    /// Id (recommended) or name of the <c>.audioMixer</c> asset that configures this project's mixer (buses, effects, sends).
    /// Empty (absent from the project file) means the engine's default mixer. Applied after the asset loaders are
    /// registered and, in the editor, on every project load; no user interface, edited directly in the project file.
    /// </summary>
    [Category("Audio")]
    public string AudioMixerAsset { get; set; } = string.Empty;

    /// <summary>
    /// Audio backend this project starts with. Null means "not set": the engine default applies.
    /// The <c>CASAENGINE_AUDIO_BACKEND</c> environment variable overrides it. Takes effect at the
    /// next launch; no user interface, edited directly in the project file.
    /// </summary>
    [Category("Audio")]
    public CasaEngine.Framework.Audio.AudioBackendKind? AudioBackend { get; set; }

    /// <summary>
    /// Optional fixed logical resolution (ADR-0048). Null: the single runtime view fills the window, as it always
    /// did. Set: outside the editor the runtime fits the image into the window at a whole factor, centered, with black
    /// bands, and follows the window size in real time.
    /// </summary>
    [Category("Display")]
    public VirtualResolutionSettings VirtualResolution { get; set; }

#if !FINAL

    [Category("Debug")]
    public bool DebugIsFullScreen { get; set; }

    [Category("Debug")]
    public bool VSyncEnabled { get; set; } = true;

    [Category("Debug")]
    public int DebugWidth { get; set; } = 1024;

    [Category("Debug")]
    public int DebugHeight { get; set; } = 768;

#endif

    [Category("External Tool")]
    public string ExternalToolsDirectory { get; set; } = "ExternalTools";

}