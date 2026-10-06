using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering.Depth;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering.EffectQuads;

/// <summary>
/// One free PS1 textured quad of a pushed effect frame (docs/engine/effect-quads.md, ADR-0071): the sheet it samples, its raw texture
/// window, its four corners and its PSX mode and sort key. A plain value: <see cref="EffectQuadService"/> stores copies and
/// <see cref="Application.Components.EffectQuadComponent"/> hands every field to <c>SpriteRendererComponent.DrawPsxQuad</c> unchanged.
/// </summary>
/// <param name="SheetIndex">Index into the sheets given to <see cref="EffectQuadService.SetSheets"/>.</param>
/// <param name="Window">The raw texture window of the PS1 data in texels (<c>u, v, w, h</c>); a mirrored axis names its window one
/// texel back, so the origin may be -1 (read as column or row 0 by the clamped sampler).</param>
/// <param name="TopLeft">World position (units = PS1 pixels, y up) of the vertex that samples the top-left of the window.</param>
/// <param name="TopRight">World position of the top-right vertex.</param>
/// <param name="BottomLeft">World position of the bottom-left vertex.</param>
/// <param name="BottomRight">World position of the bottom-right vertex.</param>
/// <param name="Mode">The PSX semi-transparency mode: <see cref="SpritePsxSemiTransparency.None"/> for an opaque quad.</param>
/// <param name="SortKey">The sort key of the quad in the sprite pass.</param>
public readonly record struct EffectQuad(
    int SheetIndex,
    Rectangle Window,
    Vector2 TopLeft,
    Vector2 TopRight,
    Vector2 BottomLeft,
    Vector2 BottomRight,
    SpritePsxSemiTransparency Mode,
    RenderSortKey2D SortKey);
