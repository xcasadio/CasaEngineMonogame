using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets.Loaders;

public class Texture2DLoader : IAssetLoader
{
    private static readonly string[] _extensionSupported = { ".png", ".gif", ".jpg", ".jpeg", ".bmp", ".tga" };

    public object LoadAsset(string fileName, AssetContentManager assetContentManager)
    {
        using var fileStream = new FileStream(fileName, FileMode.Open);
        return Texture2D.FromStream(assetContentManager.GraphicsDevice, fileStream);
    }

    public bool IsFileSupported(string fileName)
    {
        return IsTextureFile(fileName);
    }

    public static bool IsTextureFile(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return _extensionSupported.Any(x => string.Equals(extension, x, StringComparison.InvariantCultureIgnoreCase));
    }
}