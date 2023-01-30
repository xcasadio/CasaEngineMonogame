using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Asset
{
    class Texture2DLoader
        : IAssetLoader
    {

        public object LoadAsset(string fileName_, GraphicsDevice device_)
        {
            return Texture2D.FromStream(device_, new FileStream(fileName_, FileMode.Open));
        }

    }
}
