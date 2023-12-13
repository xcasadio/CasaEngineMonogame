using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Logs;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets;

public class AssetLoader<T> : IAssetLoader where T : ISaveLoad, new()
{
    public object LoadAsset(string fileName, GraphicsDevice device)
    {
        var jsonDocument = JsonDocument.Parse(File.ReadAllText(fileName));
        var asset = new T();
        asset.Load(jsonDocument.RootElement, SaveOption.Editor);
        return asset;
    }

    public bool IsFileSupported(string fileName)
    {
        return false; // no import from other project
    }
}