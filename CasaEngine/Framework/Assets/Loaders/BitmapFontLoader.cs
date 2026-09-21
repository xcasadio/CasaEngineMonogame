using CasaEngine.Framework.Assets.Fonts;
using FontStashSharp;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets.Loaders;

/// <summary>
/// Loads a BMFont text file (<c>.fnt</c>) as a <see cref="BitmapFont"/> (ADR-0036). Each page texture is
/// resolved through the asset catalog, relative to the <c>.fnt</c> file, and held through
/// <see cref="AssetContentManager.Acquire{T}(Guid)"/>; the font gives the holds back when it is freed.
/// </summary>
public sealed class BitmapFontLoader : IAssetLoader
{
    public object LoadAsset(string fileName, AssetContentManager assetContentManager)
    {
        var text = File.ReadAllText(fileName);
        var descriptor = BitmapFontDescriptor.Parse(text, fileName);

        var pageHandles = new List<IDisposable>(descriptor.PageFiles.Count);
        var pageTextures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        try
        {
            foreach (var pageFile in descriptor.PageFiles)
            {
                var pageInfo = assetContentManager.ResolveAssetInfoNextTo(fileName, pageFile)
                    ?? throw new InvalidOperationException(
                        $"Bitmap font '{fileName}': page '{pageFile}' is not in the asset catalog.");

                var pageHandle = assetContentManager.Acquire<Texture2D>(pageInfo.Id);
                pageHandles.Add(pageHandle);
                pageTextures[pageFile] = pageHandle.Asset;
            }

            var font = StaticSpriteFont.FromBMFont(text, pageFile => new TextureWithOffset(pageTextures[pageFile]));
            return new BitmapFont(descriptor.Face, font, pageHandles);
        }
        catch
        {
            foreach (var pageHandle in pageHandles)
            {
                pageHandle.Dispose();
            }

            throw;
        }
    }

    public bool IsFileSupported(string fileName)
    {
        return string.Equals(Path.GetExtension(fileName), ".fnt", StringComparison.OrdinalIgnoreCase);
    }
}
