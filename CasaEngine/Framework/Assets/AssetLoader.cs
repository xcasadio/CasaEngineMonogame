
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets;

public class AssetLoader<T> : IAssetLoader where T : ISerializable, new()
{
    public object LoadAsset(string fileName, GraphicsDevice device)
    {
        JObject jsonDocument = JObject.Parse(File.ReadAllText(fileName));
        var asset = new T();
        asset.Load(jsonDocument);
        return asset;
    }

    public bool IsFileSupported(string fileName)
    {
        return false; // no import from other project
    }
}