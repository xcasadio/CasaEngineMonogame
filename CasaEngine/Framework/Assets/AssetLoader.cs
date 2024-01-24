using System.Text.Json;
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets;

public class AssetLoader<T> : IAssetLoader where T : ISerializable, new()
{
    public object LoadAsset(string fileName, GraphicsDevice device)
    {
        var jsonDocument = JsonDocument.Parse(File.ReadAllText(fileName));
        var asset = new T();
        asset.Load(jsonDocument.RootElement);
        return asset;
    }

    public bool IsFileSupported(string fileName)
    {
        return false; // no import from other project
    }
}