using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Core.Helpers;

namespace CasaEngine.Framework.Project;

public class ProjectSettings
{
#if EDITOR
    private string _dataSrcCtrlServer = string.Empty;
    private string _dataSrcCtrlUser = string.Empty;
    private string _dataSrcCtrlPassword = string.Empty;
    private string _dataSrcCtrlWorkspace = string.Empty;

    [Category("Project")]
    public int Version => 1;
#endif

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

#if EDITOR
    [Category("External Tool")]
    public string ExternalToolsDirectory { get; set; }
#endif

#endif

    public void Load(string fileName)
    {
        var jsonDocument = JsonDocument.Parse(File.ReadAllText(fileName));

        var rootElement = jsonDocument.RootElement;
        var version = rootElement.GetJsonPropertyByName("Version").Value.GetInt32();

        WindowTitle = rootElement.GetJsonPropertyByName("WindowTitle").Value.GetString();
        ProjectName = rootElement.GetJsonPropertyByName("ProjectName").Value.GetString();
        FirstScreenName = rootElement.GetJsonPropertyByName("FirstScreenName").Value.GetString();
        DebugIsFullScreen = rootElement.GetJsonPropertyByName("DebugIsFullScreen").Value.GetBoolean();
        DebugHeight = rootElement.GetJsonPropertyByName("DebugHeight").Value.GetInt32();
        DebugWidth = rootElement.GetJsonPropertyByName("DebugWidth").Value.GetInt32();

        FirstWorldLoaded = rootElement.GetJsonPropertyByName("FirstWorldLoaded").Value.GetString();
        GameplayDllName = rootElement.GetJsonPropertyByName("GameplayDllName").Value.GetString();
    }

#if EDITOR
    public void Save(string fileName)
    {
        string jsonString = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(fileName, jsonString);
    }
#endif
}