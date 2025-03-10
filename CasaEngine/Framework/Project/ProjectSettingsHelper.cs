﻿using CasaEngine.Engine;
using CasaEngine.Framework.Game;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

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

        var rootElement = JObject.Parse(File.ReadAllText(fileName));

        GameSettings.ProjectSettings.WindowTitle = rootElement["WindowTitle"].GetString();
        GameSettings.ProjectSettings.ProjectName = rootElement["ProjectName"].GetString();
        GameSettings.ProjectSettings.FirstScreenName = rootElement["FirstScreenName"].GetString();
        GameSettings.ProjectSettings.DebugIsFullScreen = rootElement["DebugIsFullScreen"].GetBoolean();
        GameSettings.ProjectSettings.DebugHeight = rootElement["DebugHeight"].GetInt32();
        GameSettings.ProjectSettings.DebugWidth = rootElement["DebugWidth"].GetInt32();

        GameSettings.ProjectSettings.FirstWorldLoaded = rootElement["FirstWorldLoaded"].GetString();
        GameSettings.ProjectSettings.GameplayDllName = rootElement["GameplayDllName"].GetString();

        if (!string.IsNullOrWhiteSpace(GameSettings.ProjectSettings.GameplayDllName))
        {
            GameSettings.AssemblyManager.Load(GameSettings.ProjectSettings.GameplayDllName);
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
        ProjectLoaded?.Invoke(GameSettings.ProjectSettings, EventArgs.Empty);
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

    public static void CreateProject(string projectName, string path)
    {
#if !DEBUG
        try
        {
#endif

        Clear();

        EngineEnvironment.ProjectPath = path;
        var fullFileName = Path.Combine(path, projectName + Constants.FileNameExtensions.Project);
        GameSettings.ProjectSettings.WindowTitle = projectName;
        GameSettings.ProjectSettings.ProjectName = projectName;
        GameSettings.ProjectSettings.ProjectFileOpened = fullFileName;
        var worldName = "DefaultWorld";
        var worldFileName = worldName + Constants.FileNameExtensions.World;
        GameSettings.ProjectSettings.FirstWorldLoaded = worldFileName;

        //CREATE hiera folders
        //create default settings
        var world = new World.World();
        world.Name = worldName;
        world.FileName = worldFileName;
        AssetSaver.SaveAsset(world.FileName, world);
        AssetCatalog.Add(world);

        Save();
        AssetCatalog.Save();

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
        using StreamWriter file = File.CreateText(GameSettings.ProjectSettings.ProjectFileOpened);
        using JsonTextWriter writer = new JsonTextWriter(file) { Formatting = Formatting.Indented };
        var jsonSerializer = new JsonSerializer();
        jsonSerializer.Serialize(writer, GameSettings.ProjectSettings);
    }

#endif
}