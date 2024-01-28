using CasaEngine.Engine.Physics;
using CasaEngine.Engine.Plugins;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Project;

namespace CasaEngine.Framework.Game;

public static class GameSettings
{
    public static AssetCatalog AssetCatalog { get; } = new();
    public static ProjectSettings ProjectSettings { get; } = new();
    public static PluginManager PluginManager { get; } = new();
    public static GraphicsSettings GraphicsSettings { get; } = new();
    public static PhysicsEngineSettings PhysicsEngineSettings { get; } = new();

    public static void Load(string projectFileName)
    {
        ProjectSettingsHelper.Load(projectFileName);

        var assetInfoFileName = Path.Combine(Path.GetDirectoryName(projectFileName), "AssetInfos.json");

        //#if !EDITOR
        if (!File.Exists(assetInfoFileName))
        {
            return;
        }
        //#endif
        AssetCatalog.Load(assetInfoFileName);
    }

}