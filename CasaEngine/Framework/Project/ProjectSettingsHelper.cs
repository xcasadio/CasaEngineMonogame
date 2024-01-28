using CasaEngine.Engine;
using CasaEngine.Framework.Game;
using System.Text.Json;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;

namespace CasaEngine.Framework.Project;

public static class ProjectSettingsHelper
{
    public static void Load(string fileName)
    {
#if EDITOR
        Clear();
        GameSettings.ProjectSettings.ProjectFileOpened = fileName;
        EngineEnvironment.ProjectPath = Path.GetDirectoryName(fileName);
#endif

        var jsonDocument = JsonDocument.Parse(File.ReadAllText(fileName));

        var rootElement = jsonDocument.RootElement;

        GameSettings.ProjectSettings.WindowTitle = rootElement.GetJsonPropertyByName("WindowTitle").Value.GetString();
        GameSettings.ProjectSettings.ProjectName = rootElement.GetJsonPropertyByName("ProjectName").Value.GetString();
        GameSettings.ProjectSettings.FirstScreenName = rootElement.GetJsonPropertyByName("FirstScreenName").Value.GetString();
        GameSettings.ProjectSettings.DebugIsFullScreen = rootElement.GetJsonPropertyByName("DebugIsFullScreen").Value.GetBoolean();
        GameSettings.ProjectSettings.DebugHeight = rootElement.GetJsonPropertyByName("DebugHeight").Value.GetInt32();
        GameSettings.ProjectSettings.DebugWidth = rootElement.GetJsonPropertyByName("DebugWidth").Value.GetInt32();

        GameSettings.ProjectSettings.FirstWorldLoaded = rootElement.GetJsonPropertyByName("FirstWorldLoaded").Value.GetString();
        GameSettings.ProjectSettings.GameplayDllName = rootElement.GetJsonPropertyByName("GameplayDllName").Value.GetString();

        if (!string.IsNullOrWhiteSpace(GameSettings.ProjectSettings.GameplayDllName))
        {
            GameSettings.AssemblyManager.Load(GameSettings.ProjectSettings.GameplayDllName);
        }

#if EDITOR
        ProjectLoaded?.Invoke(GameSettings.ProjectSettings, EventArgs.Empty);
#endif
    }


#if EDITOR
    public static event EventHandler? ProjectLoaded;
    public static event EventHandler? ProjectClosed;

    public static void Clear()
    {
        GameSettings.ProjectSettings.ProjectFileOpened = null;
        ProjectClosed?.Invoke(GameSettings.ProjectSettings, EventArgs.Empty);
    }

    public static void CreateProject(string projectName, string path)
    {
#if !DEBUG
        try
        {
#endif

        Clear();

        var fullFileName = Path.Combine(path, projectName + Constants.FileNameExtensions.Project);
        GameSettings.ProjectSettings.WindowTitle = projectName;
        GameSettings.ProjectSettings.ProjectName = projectName;
        GameSettings.ProjectSettings.ProjectFileOpened = fullFileName;
        EngineEnvironment.ProjectPath = path;

        //CREATE hiera folders
        //create default settings
        //

        var world = new World.World();
        world.Name = "DefaultWorld";
        world.FileName = world.Name + Constants.FileNameExtensions.World;
        GameSettings.ProjectSettings.FirstWorldLoaded = world.FileName;
        AssetSaver.SaveAsset(world.FileName, world);

        Save();

        ProjectLoaded?.Invoke(GameSettings.ProjectSettings, EventArgs.Empty);

#if !DEBUG
        }
        catch (System.Exception e)
        {

        }
#endif
    }

    public static void Save()
    {
        string jsonString = JsonSerializer.Serialize(GameSettings.ProjectSettings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(GameSettings.ProjectSettings.ProjectFileOpened, jsonString);
    }

#endif
}