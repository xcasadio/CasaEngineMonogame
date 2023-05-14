using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Framework.Assets.Sprites;

public class Sprite
{
    public Texture Texture { get; }
    public SpriteData SpriteData { get; }

    public Sprite()
    { }

    private Sprite(SpriteData spriteData, Texture texture)
    {
        SpriteData = spriteData;
        Texture = texture;
    }

    public static Sprite Create(SpriteData spriteData, AssetContentManager assetContentManager)
    {
        var texture = new Texture(assetContentManager.GraphicsDevice, spriteData.SpriteSheetFileName, assetContentManager);
        return new Sprite(spriteData, texture);
    }
}