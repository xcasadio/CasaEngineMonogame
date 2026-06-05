using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Assets.Sprites;

public static class SpriteDataBoundsCalculator
{
    public static BoundingBox CalculateLocalBounds(SpriteData spriteData)
    {
        ArgumentNullException.ThrowIfNull(spriteData);

        float left = -spriteData.Origin.X;
        float top = spriteData.Origin.Y - spriteData.PositionInTexture.Height;
        float right = spriteData.PositionInTexture.Width - spriteData.Origin.X;
        float bottom = spriteData.Origin.Y;

        return new BoundingBox(
            new Vector3(left, top, 0f),
            new Vector3(right, bottom, 0.1f));
    }

    public static Vector2 CalculateLocalVisualCenter(SpriteData spriteData)
    {
        BoundingBox bounds = CalculateLocalBounds(spriteData);
        return new Vector2(
            (bounds.Min.X + bounds.Max.X) * 0.5f,
            (bounds.Min.Y + bounds.Max.Y) * 0.5f);
    }
}