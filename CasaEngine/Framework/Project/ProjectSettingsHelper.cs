using CasaEngine.Engine;
using CasaEngine.Framework.Game;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace CasaEngine.Framework.Project;

public static class ProjectSettingsHelper
{
    public static void Load(string fileName, EngineRuntimeContext? runtimeContext = null)
    {
        var context = runtimeContext ?? GameSettings.CreateRuntimeContext();
        var projectSettings = context.ProjectSettings;

#if EDITOR
        Clear();
        projectSettings.ProjectFileOpened = fileName;
        context.ProjectPath = Path.GetDirectoryName(fileName) ?? EngineEnvironment.ResolveProjectPath(null);
        EngineEnvironment.ProjectPath = context.ProjectPath;
#endif

        var rootElement = JObject.Parse(File.ReadAllText(fileName));

        projectSettings.WindowTitle = rootElement["WindowTitle"].GetString();
        projectSettings.ProjectName = rootElement["ProjectName"].GetString();
        projectSettings.FirstScreenName = rootElement["FirstScreenName"].GetString();
        projectSettings.DebugIsFullScreen = rootElement["DebugIsFullScreen"].GetBoolean();
        projectSettings.DebugHeight = rootElement["DebugHeight"].GetInt32();
        projectSettings.DebugWidth = rootElement["DebugWidth"].GetInt32();

        projectSettings.FirstWorldLoaded = rootElement["FirstWorldLoaded"].GetString();
        projectSettings.GameplayDllName = rootElement["GameplayDllName"].GetString();

        if (!string.IsNullOrWhiteSpace(projectSettings.GameplayDllName))
        {
            GameSettings.AssemblyManager.Load(projectSettings.GameplayDllName);
        }

        var assetInfoFileName = Path.Combine(Path.GetDirectoryName(fileName), "AssetInfos.json");

        //#if !EDITOR
        if (!File.Exists(assetInfoFileName))
        {
            return;
        }
        //#endif
        AssetCatalog.Load(assetInfoFileName);

#if EDITOR
        ProjectLoaded?.Invoke(projectSettings, EventArgs.Empty);
#endif
    }


#if EDITOR
    public static event EventHandler? ProjectLoaded;
    public static event EventHandler? ProjectClosed;

    public static void Clear()
    {
        GameSettings.ProjectSettings.ProjectFileOpened = null;
        AssetCatalog.Clear();
        ProjectClosed?.Invoke(GameSettings.ProjectSettings, EventArgs.Empty);
    }

    public static void CreateProject(string projectName, string path, EngineRuntimeContext? runtimeContext = null)
    {
        var context = runtimeContext ?? GameSettings.CreateRuntimeContext();
        var projectSettings = context.ProjectSettings;

#if !DEBUG
        try
        {
#endif

        Clear();

    context.ProjectPath = path;
    EngineEnvironment.ProjectPath = path;
        var fullFileName = Path.Combine(path, projectName + Constants.FileNameExtensions.Project);
    projectSettings.WindowTitle = projectName;
    projectSettings.ProjectName = projectName;
    projectSettings.ProjectFileOpened = fullFileName;
        var worldName = "DefaultWorld";
        var worldFileName = worldName + Constants.FileNameExtensions.World;
    projectSettings.FirstWorldLoaded = worldFileName;

        //CREATE hiera folders
        //create default settings
        var world = new World.World();
        world.Name = worldName;
        world.FileName = worldFileName;
        AssetSaver.SaveAsset(world.FileName, world);
        AssetCatalog.Add(world);

        Save();
        AssetCatalog.Save();

        ProjectLoaded?.Invoke(projectSettings, EventArgs.Empty);

#if !DEBUG
        }
        catch (System.Exception e)
        {

        }
#endif
    }

    public static void Save()
    {
        using StreamWriter file = File.CreateText(GameSettings.ProjectSettings.ProjectFileOpened);
        using JsonTextWriter writer = new JsonTextWriter(file) { Formatting = Formatting.Indented };
        var jsonSerializer = new JsonSerializer();
        jsonSerializer.Serialize(writer, GameSettings.ProjectSettings);
    }

#endif
}