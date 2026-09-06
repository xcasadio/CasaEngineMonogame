using CasaEngine.Framework.Rendering.Depth;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering.ScrollingLayers;

/// <summary>
/// A single full-viewport colour overlay drawn beneath/above the scrolling layers (the companion's
/// optional tint - docs/engine/scrolling-layers.md). Submitted as one quad, always with
/// <see cref="SpriteBlendMode.AlphaBlend"/> (never carries its own blend mode: the DLL bakes any
/// alpha it wants into <see cref="Color"/>).
/// </summary>
public readonly struct ScrollingTintDefinition
{
    public ScrollingTintDefinition(Color color, RenderSortKey2D sortKey)
    {
        Color = color;
        SortKey = sortKey;
    }

    public Color Color { get; }

    public RenderSortKey2D SortKey { get; }
}
