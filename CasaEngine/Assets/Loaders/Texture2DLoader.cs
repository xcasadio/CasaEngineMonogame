using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Asset
{
    class Texture2DLoader
        : IAssetLoader
    {

        public object LoadAsset(string fileName, GraphicsDevice device)
        {
            return Texture2D.FromStream(device, new FileStream(fileName, FileMode.Open));
        }

    }
}
