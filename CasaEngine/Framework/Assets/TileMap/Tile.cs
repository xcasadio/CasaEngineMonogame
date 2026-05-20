using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets.TileMap;

public abstract class Tile
{
    private SpriteRendererComponent _spriteRendererComponent;

    public TileData? TileData { get; }

    public Tile(TileData? tileData)
    {
        TileData = tileData;
    }

    public virtual void Initialize(CasaEngineGame game)
    {
        _spriteRendererComponent = game.GetGameComponent<SpriteRendererComponent>();
    }

    public abstract void Update(float elapsedTime);
    public abstract void Draw(float x, float y, float z, Vector2 scale);
    public abstract void Draw(float x, float y, float z, Rectangle uvOffset, Vector2 scale);

    public virtual void Draw(float x, float y, float z, Vector2 scale, TileCellFlags flags)
    {
        Draw(x, y, z, scale);
    }

    protected void Draw(Texture2D texture, Rectangle positionInTexture, float x, float y, float z, Rectangle uvOffset, Vector2 scale)
    {
        Draw(texture, positionInTexture, x, y, z, uvOffset, scale, SpriteEffects.None);
    }

    protected void Draw(Texture2D texture, Rectangle positionInTexture, float x, float y, float z, Rectangle uvOffset, Vector2 scale, SpriteEffects effects)
    {
        Rectangle texUV = new Rectangle(
            positionInTexture.Left + uvOffset.Left,
            positionInTexture.Top + uvOffset.Top,
            uvOffset.Width,
            uvOffset.Height);

        _spriteRendererComponent.DrawSprite(
            texture,
            texUV,
            Point.Zero,
            new Vector2(x, y),
            0.0f,
            scale,
            Color.White,
            z,
            effects);
    }
}