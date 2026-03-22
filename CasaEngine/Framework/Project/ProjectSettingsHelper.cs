using CasaEngine.Engine;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Project;

public static class ProjectSettingsHelper
{
    public static void Load(string fileName, EngineRuntimeContext? runtimeContext = null)
    {
        var context = runtimeContext ?? GameSettings.CreateRuntimeContext();
        var projectSettings = context.ProjectSettings;
        context.ProjectPath = Path.GetDirectoryName(fileName) ?? EngineEnvironment.ResolveProjectPath(null);
        EngineEnvironment.ProjectPath = context.ProjectPath;

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
    }
}