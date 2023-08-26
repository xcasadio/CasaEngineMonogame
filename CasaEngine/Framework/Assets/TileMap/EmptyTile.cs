using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Assets.TileMap;

public class EmptyTile : Tile
{
    public EmptyTile() : base(null)
    { }

    public override void Update(float elapsedTime)
    {
        //do nothing
    }

    public override void Draw(float x, float y, float z, Vector2 scale)
    {
        //do nothing
    }

    public override void Draw(float x, float y, float z, Rectangle uvOffset, Vector2 scale)
    {
        //do nothing
    }
}