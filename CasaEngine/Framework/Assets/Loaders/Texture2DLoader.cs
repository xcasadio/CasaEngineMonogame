using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets.Loaders;

public class Texture2DLoader : IAssetLoader
{
    private static readonly string[] _extensionSupported = { ".png", ".gif", ".jpg", ".jpeg", ".bmp", ".tga" };

    /// <summary>
    /// When true, <see cref="LoadAsset"/> returns a texture with a full mipmap chain, each level being the 2 x 2 box
    /// average of the previous one (<see cref="MipmapChain"/>). Level 0 is the image as <c>Texture2D.FromStream</c>
    /// returns it. False by default: a single level, as before. Costs some load time and a third more video memory.
    /// </summary>
    public bool GenerateMipmaps { get; set; }

    public object LoadAsset(string fileName, AssetContentManager assetContentManager)
    {
        using var fileStream = new FileStream(fileName, FileMode.Open);
        var texture = Texture2D.FromStream(assetContentManager.GraphicsDevice, fileStream);
        return GenerateMipmaps ? CreateMipmappedTexture(texture, fileName) : texture;
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

    // Replaces a single-level texture by an equal one that has the whole chain; the source is disposed.
    private static Texture2D CreateMipmappedTexture(Texture2D source, string fileName)
    {
        try
        {
            if (source.Format != SurfaceFormat.Color)
            {
                throw new InvalidOperationException(
                    $"Cannot generate mipmaps for '{fileName}': the decoded texture format is {source.Format}, expected {SurfaceFormat.Color}.");
            }

            var level0Pixels = new Color[source.Width * source.Height];
            source.GetData(level0Pixels);

            var mipmapped = new Texture2D(source.GraphicsDevice, source.Width, source.Height, true, SurfaceFormat.Color)
            {
                Name = source.Name,
            };

            try
            {
                MipmapChain.UploadChain(mipmapped, level0Pixels);
            }
            catch
            {
                mipmapped.Dispose();
                throw;
            }

            return mipmapped;
        }
        finally
        {
            source.Dispose();
        }
    }
}
