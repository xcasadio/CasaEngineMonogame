using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Assets
{
    public interface IAssetLoader
    {
        object LoadAsset(string fileName, GraphicsDevice device);
    }
}
