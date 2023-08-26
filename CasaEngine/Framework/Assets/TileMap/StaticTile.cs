using CasaEngine.Framework.Assets.Sprites;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets.TileMap;

public class StaticTile : Tile
{
    private readonly Texture2D _texture;
    private readonly Rectangle _positionInTexture;

    public StaticTile(Texture2D texture, StaticTileData? tileData) : base(tileData)
    {
        _texture = texture;
        _positionInTexture = tileData.Location;
    }

    public override void Update(float elapsedTime)
    {
        //do nothing
    }

    public override void Draw(float x, float y, float z, Vector2 scale)
    {
        if (_texture == null)
        {
            return;
        }

        var uvOffset = _positionInTexture;
        uvOffset.X = 0;
        uvOffset.Y = 0;
        base.Draw(_texture, _positionInTexture, x, y, z, uvOffset, scale);
    }

    public override void Draw(float x, float y, float z, Rectangle uvOffset, Vector2 scale)
    {
        if (_texture != null)
        {
            base.Draw(_texture, _positionInTexture, x, y, z, uvOffset, scale);
        }
    }
}