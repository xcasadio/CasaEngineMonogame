using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets;

public interface IAssetLoader
{
    object LoadAsset(string fileName, GraphicsDevice device);
    bool IsFileSupported(string fileName);
}