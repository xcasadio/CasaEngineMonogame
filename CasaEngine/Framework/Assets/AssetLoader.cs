using CasaEngine.Core.Logging;
using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets;

public class AssetLoader<T> : IAssetLoader where T : ISerializable, new()
{
    public object? LoadAsset(string fileName, AssetContentManager assetContentManager)
    {
        try
        {
            JObject jsonDocument = JObject.Parse(File.ReadAllText(fileName));
            var asset = new T();
            asset.Load(jsonDocument);
            return asset;
        }
        catch (Exception e)
        {
            Logs.WriteException(new Exception($"Can't load asset {fileName}", e));
        }

        return null;
    }

    public bool IsFileSupported(string fileName)
    {
        return false; // no import from other project
    }
}