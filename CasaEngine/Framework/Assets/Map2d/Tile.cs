using CasaEngine.Framework.Game;
using CasaEngine.Framework.Graphics2D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets.Map2d;

public abstract class Tile
{
    private Renderer2dComponent _renderer2dComponent;

    public TileData? TileData { get; }

    public Tile(TileData? tileData)
    {
        TileData = tileData;
    }

    public virtual void Initialize(CasaEngineGame game)
    {
        _renderer2dComponent = game.GetGameComponent<Renderer2dComponent>();
    }

    public abstract void Update(float elapsedTime);
    public abstract void Draw(float x, float y, float z, Vector2 scale);
    public abstract void Draw(float x, float y, float z, Rectangle uvOffset, Vector2 scale);

    protected void Draw(Sprite sprite, float x, float y, float z, Rectangle uvOffset, Vector2 scale)
    {
        Rectangle texUV = new Rectangle(
            sprite.SpriteData.PositionInTexture.Left + uvOffset.Left,
            sprite.SpriteData.PositionInTexture.Top + uvOffset.Top,
            uvOffset.Width,
            uvOffset.Height);

        _renderer2dComponent.AddSprite(
            sprite.Texture.Resource,
            texUV,
            sprite.SpriteData.Origin,
            new Vector2(x, y),
            0.0f,
            scale,
            Color.White,
            z,
            SpriteEffects.None);
    }
}