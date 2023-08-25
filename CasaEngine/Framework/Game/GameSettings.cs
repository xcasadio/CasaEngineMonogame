using CasaEngine.Core.Design;
using CasaEngine.Editor.Tools;
using CasaEngine.Engine.Physics;
using CasaEngine.Engine.Plugin;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Project;
using CasaEngine.Framework.Scripting;

namespace CasaEngine.Framework.Game;

public static class GameSettings
{
    public static ScriptLoader ScriptLoader { get; } = new();
    public static ComponentLoader ComponentLoader { get; } = new();
    public static AssetInfoManager AssetInfoManager { get; } = new();
    public static ProjectSettings ProjectSettings { get; } = new();
    public static PluginManager PluginManager { get; } = new();

    public static GraphicsSettings GraphicsSettings { get; } = new();
    public static PhysicsEngineSettings PhysicsEngineSettings { get; } = new();

#if EDITOR
    public static ExternalToolManager ExternalToolManager { get; } = new();
#endif

    public static void Load(string projectFileName)
    {
        ProjectSettings.Load(projectFileName);

        var assetInfoFileName = Path.Combine(Path.GetDirectoryName(projectFileName), "AssetInfos.json");

        //#if !EDITOR
        if (!File.Exists(assetInfoFileName))
        {
            return;
        }
        //#endif
        AssetInfoManager.Load(assetInfoFileName, SaveOption.Editor);
    }

}