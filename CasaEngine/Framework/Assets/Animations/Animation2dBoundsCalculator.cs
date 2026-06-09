using CasaEngine.Framework.Assets.Sprites;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Assets.Animations;

public static class Animation2dBoundsCalculator
{
    public static bool TryCalculateLocalBounds(
        Animation2dCompositionRuntimeState runtimeState,
        IReadOnlyDictionary<Guid, SpriteData> spriteDataById,
        out BoundingBox bounds)
    {
        ArgumentNullException.ThrowIfNull(runtimeState);
        ArgumentNullException.ThrowIfNull(spriteDataById);

        var min = new Vector3(float.MaxValue, float.MaxValue, 0f);
        var max = new Vector3(float.MinValue, float.MinValue, 0.1f);
        var hasVisibleSprite = false;

        for (var partIndex = 0; partIndex < runtimeState.Parts.Count; partIndex++)
        {
            var part = runtimeState.Parts[partIndex];
            if (!part.Visible || !spriteDataById.TryGetValue(part.SpriteId, out var spriteData))
            {
                continue;
            }

            Vector2 topLeft = RotateCorner(new Vector2(-spriteData.Origin.X, spriteData.Origin.Y), part.Rotation) + part.Position;
            Vector2 topRight = RotateCorner(new Vector2(spriteData.PositionInTexture.Width - spriteData.Origin.X, spriteData.Origin.Y), part.Rotation) + part.Position;
            Vector2 bottomRight = RotateCorner(new Vector2(spriteData.PositionInTexture.Width - spriteData.Origin.X, spriteData.Origin.Y - spriteData.PositionInTexture.Height), part.Rotation) + part.Position;
            Vector2 bottomLeft = RotateCorner(new Vector2(-spriteData.Origin.X, spriteData.Origin.Y - spriteData.PositionInTexture.Height), part.Rotation) + part.Position;

            min.X = MathF.Min(min.X, MathF.Min(MathF.Min(topLeft.X, topRight.X), MathF.Min(bottomRight.X, bottomLeft.X)));
            min.Y = MathF.Min(min.Y, MathF.Min(MathF.Min(topLeft.Y, topRight.Y), MathF.Min(bottomRight.Y, bottomLeft.Y)));
            max.X = MathF.Max(max.X, MathF.Max(MathF.Max(topLeft.X, topRight.X), MathF.Max(bottomRight.X, bottomLeft.X)));
            max.Y = MathF.Max(max.Y, MathF.Max(MathF.Max(topLeft.Y, topRight.Y), MathF.Max(bottomRight.Y, bottomLeft.Y)));
            hasVisibleSprite = true;
        }

        bounds = hasVisibleSprite ? new BoundingBox(min, max) : default;
        return hasVisibleSprite;
    }

    private static Vector2 RotateCorner(Vector2 corner, float rotation)
    {
        if (Math.Abs(rotation) < 0.0001f)
        {
            return corner;
        }

        float cos = MathF.Cos(rotation);
        float sin = MathF.Sin(rotation);
        return new Vector2(
            (corner.X * cos) - (corner.Y * sin),
            (corner.X * sin) + (corner.Y * cos));
    }
}