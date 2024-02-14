using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets.Loaders;

public class EffectLoader : IAssetLoader
{
    private static readonly string[] _extensionSupported = { ".mgfxc" };

    public object LoadAsset(string fileName, AssetContentManager assetContentManager)
    {
        using var fileStream = new FileStream(fileName, FileMode.Open);
        using var binaryReader = new BinaryReader(fileStream);

        return new Effect(assetContentManager.GraphicsDevice, binaryReader.ReadBytes((int)fileStream.Length));
    }

    public bool IsFileSupported(string fileName)
    {
        return IsModelFile(fileName);
    }

    public static bool IsModelFile(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return _extensionSupported.Any(x => string.Equals(extension, x, StringComparison.InvariantCultureIgnoreCase));
    }
}