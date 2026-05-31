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

            var left = part.Position.X - spriteData.Origin.X;
            var right = left + spriteData.PositionInTexture.Width;
            var top = part.Position.Y + spriteData.Origin.Y;
            var bottom = top - spriteData.PositionInTexture.Height;

            min.X = MathF.Min(min.X, left);
            min.Y = MathF.Min(min.Y, bottom);
            max.X = MathF.Max(max.X, right);
            max.Y = MathF.Max(max.Y, top);
            hasVisibleSprite = true;
        }

        bounds = hasVisibleSprite ? new BoundingBox(min, max) : default;
        return hasVisibleSprite;
    }
}