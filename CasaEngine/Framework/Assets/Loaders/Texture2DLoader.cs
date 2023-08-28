using Microsoft.Xna.Framework.Graphics;
using System.Net.NetworkInformation;

namespace CasaEngine.Framework.Assets.Loaders;

internal class Texture2DLoader : IAssetLoader
{
    private readonly string[] _extensionSupported = { ".png", ".gif", ".jpg" };

    public object LoadAsset(string fileName, GraphicsDevice device)
    {
        using var fileStream = new FileStream(fileName, FileMode.Open);
        return Texture2D.FromStream(device, fileStream);
    }

    public bool IsFileSupported(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return _extensionSupported.Any(x => string.Equals(extension, x, StringComparison.InvariantCultureIgnoreCase));
    }
}