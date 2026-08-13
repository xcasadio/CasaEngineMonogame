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