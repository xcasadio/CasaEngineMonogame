using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets;

public interface IAssetable
{
    void Dispose();
    void OnDeviceReset(GraphicsDevice device, AssetContentManager assetContentManager);
}