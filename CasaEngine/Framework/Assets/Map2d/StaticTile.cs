using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Assets.Map2d;

public class StaticTile : Tile
{
    public Sprite? Sprite { get; set; }

    public StaticTile(Sprite sprite, StaticTileData? tileData) : base(tileData)
    {
        Sprite = sprite;
    }

    public override void Update(float elapsedTime)
    {
        //do nothing
    }

    public override void Draw(float x, float y, float z, Vector2 scale)
    {
        if (Sprite == null)
        {
            return;
        }

        var uvOffset = Sprite.SpriteData.PositionInTexture;
        uvOffset.X = 0;
        uvOffset.Y = 0;
        base.Draw(Sprite, x, y, z, uvOffset, scale);
    }

    public override void Draw(float x, float y, float z, Rectangle uvOffset, Vector2 scale)
    {
        if (Sprite != null)
        {
            base.Draw(Sprite, x, y, z, uvOffset, scale);
        }
    }
}