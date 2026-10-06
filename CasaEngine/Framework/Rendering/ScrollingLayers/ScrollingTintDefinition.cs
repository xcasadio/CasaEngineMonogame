using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering.Depth;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering.ScrollingLayers;

/// <summary>
/// A single full-viewport colour overlay drawn beneath/above the scrolling layers (the companion's
/// optional tint - docs/engine/scrolling-layers.md). Submitted as one quad. Without a mode it is
/// drawn with <see cref="SpriteBlendMode.AlphaBlend"/> and the colour as given (the DLL bakes any alpha
/// it wants into <see cref="Color"/>); with a <see cref="PsxSemiTransparency"/> mode the mode picks the
/// blend state (ADR-0066).
/// </summary>
public readonly struct ScrollingTintDefinition
{
    public ScrollingTintDefinition(Color color, RenderSortKey2D sortKey)
        : this(color, sortKey, SpritePsxSemiTransparency.None)
    {
    }

    public ScrollingTintDefinition(Color color, RenderSortKey2D sortKey, SpritePsxSemiTransparency psxSemiTransparency)
    {
        Color = color;
        SortKey = sortKey;
        PsxSemiTransparency = psxSemiTransparency;
    }

    public Color Color { get; }

    public RenderSortKey2D SortKey { get; }

    public SpritePsxSemiTransparency PsxSemiTransparency { get; }
}
