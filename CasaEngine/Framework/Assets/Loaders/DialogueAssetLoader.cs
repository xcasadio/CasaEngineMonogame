using CasaEngine.Core.Logging;
using CasaEngine.Framework.Dialogue.Assets;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Loaders;

public sealed class DialogueAssetLoader : IAssetLoader
{
    public bool IsFileSupported(string fileName)
        => Path.GetExtension(fileName).Equals(Constants.FileNameExtensions.Dialogue, StringComparison.OrdinalIgnoreCase);

    public object LoadAsset(string fileName, AssetContentManager assetContentManager)
    {
        try
        {
            var jsonDocument = JObject.Parse(File.ReadAllText(fileName));
            var asset = new DialogueAsset();
            asset.Load(jsonDocument);
            return asset;
        }
        catch (Exception exception)
        {
            Logs.WriteException(new Exception($"[DialogueAssetLoader] Cannot load dialogue asset '{fileName}'", exception));
            return null;
        }
    }
}