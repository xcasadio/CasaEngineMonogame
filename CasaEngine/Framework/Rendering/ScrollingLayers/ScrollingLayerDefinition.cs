using CasaEngine.Framework.Rendering.Depth;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering.ScrollingLayers;

/// <summary>
/// Static description of one scrolling background layer (docs/engine/scrolling-layers.md): camera
/// parallax factors, auto-scroll speed/period (original engine units, ticks - see
/// <see cref="ScrollingLayerService"/>'s class doc), V-animation cadence and the render key it
/// submits at. Pushed once per world load via <see cref="ScrollingLayerService.SetLayers"/>; the
/// policy that builds these (which layers exist, what pass/blend/tint they use) lives in the
/// consuming game DLL, not here (plan E9.b, D-E9b-1/D-E9b-8).
/// </summary>
public struct ScrollingLayerDefinition
{
    /// <summary>
    /// One texture id per V-animation frame, in order - always at least one element. An empty/nil
    /// id (<see cref="System.Guid.Empty"/>) resolves to a null frame texture (see
    /// <see cref="ScrollingLayerComponent.ResolveTextures"/>'s D-E9-9 fallback).
    /// </summary>
    public System.Guid[] FrameTextureAssetIds;

    /// <summary>Camera parallax factor on X: <c>scroll * FactorXNum / FactorXDenom</c>, truncated
    /// integer division. A zero denominator disables this axis' parallax contribution.</summary>
    public int FactorXNum;

    public int FactorXDenom;

    /// <summary>Camera parallax factor on Y, same rule as <see cref="FactorXNum"/>/<see cref="FactorXDenom"/>.</summary>
    public int FactorYNum;

    public int FactorYDenom;

    /// <summary>Auto-scroll: pixels advanced per logic tick on X, plus one further pixel every
    /// <c>|ScrollXPeriod|</c> ticks (direction from the sign of <see cref="ScrollXSpeed"/> XOR the
    /// sign of <see cref="ScrollXPeriod"/>). A period of 0 disables the extra per-period pixel.</summary>
    public int ScrollXSpeed;

    public int ScrollXPeriod;

    /// <summary>Auto-scroll on Y, same rule as <see cref="ScrollXSpeed"/>/<see cref="ScrollXPeriod"/>.</summary>
    public int ScrollYSpeed;

    public int ScrollYPeriod;

    /// <summary>Ticks the V-animation counter holds a frame before advancing (0 = advances every tick).</summary>
    public int AnimTimer;

    public RenderPass2D Pass;

    public int SortingLayer;

    public int OrderInLayer;

    /// <summary>Tie-breaker carried into the render sort key's stable id slot - has no meaning to
    /// this mechanism beyond that.</summary>
    public int StableId;

    public SpriteBlendMode Blend;

    public Color Tint;
}
