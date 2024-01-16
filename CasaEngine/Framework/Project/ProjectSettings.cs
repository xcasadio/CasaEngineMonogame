using System.ComponentModel;
using System.Text.Json.Serialization;

namespace CasaEngine.Framework.Project;

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

#if !FINAL

    [Category("Debug")]
    public bool DebugIsFullScreen { get; set; }

    [Category("Debug")]
    public int DebugWidth { get; set; } = 1024;

    [Category("Debug")]
    public int DebugHeight { get; set; } = 768;

#endif

#if EDITOR

    [Browsable(false), JsonIgnore]
    public string? ProjectFileOpened { get; set; }

    [Category("External Tool")]
    public string ExternalToolsDirectory { get; set; } = "ExternalTools";

#endif

}