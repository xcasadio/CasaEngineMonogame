using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Asset
{
    public interface IAssetLoader
    {
        object LoadAsset(string fileName, GraphicsDevice device);
    }
}
