using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets.TileMap;

public abstract class Tile
{
    private SpriteRendererComponent _spriteRendererComponent;

    public TileData TileData { get; }

    public Tile(TileData tileData)
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

    /// <summary>
    /// Returns the tile's current source rectangle in the tileset texture without drawing it — the same
    /// rectangle the next <see cref="Draw(float, float, float, Vector2)"/> call would use. An
    /// <see cref="AnimatedTile"/> reports whichever frame its own <see cref="Update"/> has advanced to;
    /// a <see cref="StaticTile"/> always reports the same fixed rectangle. Used by consumers that submit
    /// a tile's sprite through a path other than <see cref="Draw(float, float, float, Vector2)"/> (e.g.
    /// <see cref="Scene.Entities.Components.TileMapComponent"/>'s sorted overlay) but still need the tile
    /// to animate correctly.
    /// </summary>
    public abstract Rectangle GetCurrentSourceRectangle();

    public virtual void Draw(float x, float y, float z, Vector2 scale, TileCellFlags flags)
    {
        Draw(x, y, z, scale);
    }

    /// <summary>
    /// Draws the tile with local coordinates transformed by <paramref name="worldTransform"/>.
    /// Used when the owning tile map carries a rotation and cannot use the axis-aligned fast path.
    /// </summary>
    public virtual void Draw(float x, float y, float z, Vector2 scale, TileCellFlags flags, in Matrix worldTransform)
    {
        Draw(x, y, z, scale, flags);
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

    protected void Draw(Texture2D texture, Rectangle positionInTexture, float x, float y, float z, Rectangle uvOffset, Vector2 scale, SpriteEffects effects, in Matrix worldTransform)
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
            effects,
            in worldTransform);
    }
}