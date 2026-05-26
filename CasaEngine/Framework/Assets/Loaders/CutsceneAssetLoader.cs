using CasaEngine.Core.Logging;
using CasaEngine.Framework.Cutscenes;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Loaders;

public sealed class CutsceneAssetLoader : IAssetLoader
{
    public bool IsFileSupported(string fileName)
        => Path.GetExtension(fileName).Equals(Constants.FileNameExtensions.Cutscene, StringComparison.OrdinalIgnoreCase);

    public object LoadAsset(string fileName, AssetContentManager assetContentManager)
    {
        try
        {
            var jsonDocument = JObject.Parse(File.ReadAllText(fileName));
            var asset = new CutsceneAsset();
            asset.Load(jsonDocument);
            return asset;
        }
        catch (Exception exception)
        {
            Logs.WriteException(new Exception($"[CutsceneAssetLoader] Cannot load cutscene asset '{fileName}'", exception));
            return null;
        }
    }
}