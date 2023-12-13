using CasaEngine.Framework.Game;
using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Serialization;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using JsonSerializer = System.Text.Json.JsonSerializer;
using JsonIgnoreAttribute = System.Text.Json.Serialization.JsonIgnoreAttribute;

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

#if EDITOR
    [Category("External Tool")]
    public string ExternalToolsDirectory { get; set; } = "ExternalTools";
#endif

#endif

    public void Load(string fileName)
    {
#if EDITOR
        Clear();
        ProjectFileOpened = fileName;
        EngineEnvironment.ProjectPath = Path.GetDirectoryName(fileName);
#endif

        var jsonDocument = JsonDocument.Parse(File.ReadAllText(fileName));

        var rootElement = jsonDocument.RootElement;

        WindowTitle = rootElement.GetJsonPropertyByName("WindowTitle").Value.GetString();
        ProjectName = rootElement.GetJsonPropertyByName("ProjectName").Value.GetString();
        FirstScreenName = rootElement.GetJsonPropertyByName("FirstScreenName").Value.GetString();
        DebugIsFullScreen = rootElement.GetJsonPropertyByName("DebugIsFullScreen").Value.GetBoolean();
        DebugHeight = rootElement.GetJsonPropertyByName("DebugHeight").Value.GetInt32();
        DebugWidth = rootElement.GetJsonPropertyByName("DebugWidth").Value.GetInt32();

        FirstWorldLoaded = rootElement.GetJsonPropertyByName("FirstWorldLoaded").Value.GetString();
        GameplayDllName = rootElement.GetJsonPropertyByName("GameplayDllName").Value.GetString();

        if (!string.IsNullOrWhiteSpace(GameplayDllName))
        {
            GameSettings.PluginManager.Load(GameplayDllName);
        }

#if EDITOR
        GameSettings.ExternalToolManager.Initialize(ExternalToolsDirectory);

        ProjectLoaded?.Invoke(this, EventArgs.Empty);
#endif
    }


#if EDITOR
    public event EventHandler? ProjectLoaded;
    public event EventHandler? ProjectClosed;

    [Browsable(false), JsonIgnore]
    public string? ProjectFileOpened { get; private set; }

    public void Clear()
    {
        GameSettings.ExternalToolManager.Clear();
        ProjectFileOpened = null;

        ProjectClosed?.Invoke(this, EventArgs.Empty);
    }

    public void CreateProject(string projectName, string path)
    {
#if !DEBUG
        try
        {
#endif

        var fullFileName = Path.Combine(path, projectName + Constants.FileNameExtensions.Project);

        Clear();

        WindowTitle = ProjectName;
        ProjectName = projectName;
        ProjectFileOpened = fullFileName;
        throw new NotImplementedException();
        //CreateDefaultItem(fullFileName);
        //Save(fullFileName);

        ProjectLoaded?.Invoke(this, EventArgs.Empty);

#if !DEBUG
        }
        catch (System.Exception e)
        {

        }
#endif
    }
    /*
    private void CreateDefaultItem(string fullFileName)
    {
        var projectPath = Path.GetDirectoryName(fullFileName);
        var world = new World.World();
        world.AssetInfo.Name = "DefaultWorld";
        world.AssetInfo.FileName = world.AssetInfo.FileName;
        AssetSaver.SaveAsset(world.AssetInfo.FileName, world);
        FirstWorldLoaded = world.AssetInfo.FileName;
    }*/

    public bool Save(string fileName)
    {
        string jsonString = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(fileName, jsonString);
        return true;
    }

#endif
}