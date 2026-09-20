using System;
using System.IO;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.UI.MGUI;
using Newtonsoft.Json.Linq;

namespace CasaEngine.RPGDemo.Scripts.Screens;

/// <summary>
/// Reads a screen envelope out of the project catalogue, for the screens of this demo.
/// <para/>
/// Unlike the engine demos, which ship plain XAML documents beside the executable, RPGDemo is a real
/// catalogued project: its screens are `.uiscreen` assets, so they are found by name and the XAML file they
/// name is resolved relative to the envelope.
/// </summary>
internal static class RpgDemoScreenAssets
{
    /// <summary>Loads the envelope registered under <paramref name="assetName"/>, with the path it came from.</summary>
    /// <exception cref="InvalidOperationException">No asset of that name is registered in the project.</exception>
    public static (UIScreenAsset Asset, string FilePath) Load(string assetName)
    {
        var assetInfo = AssetCatalog.Get(assetName)
            ?? throw new InvalidOperationException($"No screen asset named '{assetName}' is registered in this project.");

        var filePath = Path.Combine(EngineEnvironment.ResolveProjectPath(EngineEnvironment.ProjectPath), assetInfo.FileName);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Screen asset '{assetName}' is registered but its file is missing.", filePath);
        }

        var asset = new UIScreenAsset();
        asset.Load(JObject.Parse(File.ReadAllText(filePath)));
        return (asset, filePath);
    }
}
