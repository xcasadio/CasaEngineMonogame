using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Framework.Assets.Sprites;

/// <summary>
/// A sprite and the sheet texture it is cut from. The texture is a shared asset: the sprite holds it through an
/// asset handle and gives it back on <see cref="Dispose"/> (ADR-0037), so whoever creates a sprite disposes it.
/// </summary>
public class Sprite : IDisposable
{
    private AssetHandle<Texture> _textureHold;

    public Texture Texture { get; }
    public SpriteData SpriteData { get; }

    private Sprite(SpriteData spriteData, AssetHandle<Texture> textureHold)
    {
        SpriteData = spriteData;
        Texture = textureHold.Asset;
        _textureHold = textureHold;
    }

    public static Sprite Create(SpriteData spriteData, AssetContentManager assetContentManager)
    {
        var textureHold = assetContentManager.Acquire<Texture>(spriteData.SpriteSheetAssetId);
        try
        {
            textureHold.Asset.Load(assetContentManager);
        }
        catch
        {
            textureHold.Dispose();
            throw;
        }

        return new Sprite(spriteData, textureHold);
    }

    /// <summary>Gives back the hold on the sheet texture. Idempotent.</summary>
    public void Dispose()
    {
        _textureHold?.Dispose();
        _textureHold = null;
    }
}
