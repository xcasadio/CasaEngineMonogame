using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
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

    protected void Draw(Texture2D texture, Rectangle positionInTexture, float x, float y, float z, Rectangle uvOffset, Vector2 scale)
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
            SpriteEffects.None);
    }
}