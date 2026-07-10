using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Application;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Configuration.Project;

public static class ProjectSettingsHelper
{
    public static void Load(string fileName, EngineRuntimeContext runtimeContext = null)
    {
        var context = runtimeContext ?? GameSettings.CreateRuntimeContext();
        var projectSettings = context.ProjectSettings;
        context.ProjectPath = Path.GetDirectoryName(fileName) ?? EngineEnvironment.ResolveProjectPath(null);
        EngineEnvironment.ProjectPath = context.ProjectPath;

        var rootElement = JObject.Parse(File.ReadAllText(fileName));

        projectSettings.WindowTitle = rootElement["WindowTitle"].GetString();
        projectSettings.ProjectName = rootElement["ProjectName"].GetString();
        projectSettings.FirstScreenName = rootElement["FirstScreenName"].GetString();
        projectSettings.AllowUserResizing = rootElement["AllowUserResizing"]?.GetBoolean() ?? projectSettings.AllowUserResizing;
        projectSettings.IsFixedTimeStep = rootElement["IsFixedTimeStep"]?.GetBoolean() ?? projectSettings.IsFixedTimeStep;
        projectSettings.IsMouseVisible = rootElement["IsMouseVisible"]?.GetBoolean() ?? projectSettings.IsMouseVisible;

        projectSettings.FirstWorldLoaded = rootElement["FirstWorldLoaded"].GetString();
        projectSettings.GameplayDllName = rootElement["GameplayDllName"].GetString();
        projectSettings.ExternalToolsDirectory = rootElement["ExternalToolsDirectory"]?.GetString() ?? projectSettings.ExternalToolsDirectory;

#if !FINAL
        projectSettings.DebugIsFullScreen = rootElement["DebugIsFullScreen"]?.GetBoolean() ?? projectSettings.DebugIsFullScreen;
        projectSettings.DebugHeight = rootElement["DebugHeight"]?.GetInt32() ?? projectSettings.DebugHeight;
        projectSettings.DebugWidth = rootElement["DebugWidth"]?.GetInt32() ?? projectSettings.DebugWidth;
        projectSettings.VSyncEnabled = rootElement["VSyncEnabled"]?.GetBoolean() ?? projectSettings.VSyncEnabled;
#endif

        if (!string.IsNullOrWhiteSpace(projectSettings.GameplayDllName))
        {
            GameSettings.AssemblyManager.Load(projectSettings.GameplayDllName);
        }

        var assetInfoFileName = Path.Combine(Path.GetDirectoryName(fileName), "AssetInfos.json");
        if (!File.Exists(assetInfoFileName))
        {
            return;
        }

        AssetCatalog.Load(assetInfoFileName);
    }

    public static void Save(string fileName, ProjectSettings projectSettings = null)
    {
        var settings = projectSettings ?? GameSettings.ProjectSettings;
        string directoryName = Path.GetDirectoryName(fileName);
        if (!string.IsNullOrWhiteSpace(directoryName))
        {
            Directory.CreateDirectory(directoryName);
        }

        var rootElement = new JObject
        {
            ["WindowTitle"] = settings.WindowTitle,
            ["ProjectName"] = settings.ProjectName,
            ["FirstScreenName"] = settings.FirstScreenName,
            ["AllowUserResizing"] = settings.AllowUserResizing,
            ["IsFixedTimeStep"] = settings.IsFixedTimeStep,
            ["IsMouseVisible"] = settings.IsMouseVisible,
            ["FirstWorldLoaded"] = settings.FirstWorldLoaded,
            ["GameplayDllName"] = settings.GameplayDllName,
            ["ExternalToolsDirectory"] = settings.ExternalToolsDirectory,
        };

#if !FINAL
        rootElement["DebugIsFullScreen"] = settings.DebugIsFullScreen;
        rootElement["DebugHeight"] = settings.DebugHeight;
        rootElement["DebugWidth"] = settings.DebugWidth;
        rootElement["VSyncEnabled"] = settings.VSyncEnabled;
#endif

        File.WriteAllText(fileName, rootElement.ToString());
    }
}