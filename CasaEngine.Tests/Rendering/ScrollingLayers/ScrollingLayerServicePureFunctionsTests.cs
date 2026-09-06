using CasaEngine.Framework.Rendering.ScrollingLayers;
using Xunit;

namespace CasaEngine.Tests.Rendering.ScrollingLayers;

/// <summary>
/// Pure static functions of <see cref="ScrollingLayerService"/> - the allocation-free port of the
/// DLL's (now-retired) <c>BackdropOffsetMath</c>: truncated-integer parallax, wrap-into-range, and the
/// covering-quad origin pair that replaces its allocated <c>List&lt;int&gt;</c> (plan
/// plan-e9b-backdrops-moteur.md, D-E9b-1/D-E9b-5).
/// </summary>
public class ScrollingLayerServicePureFunctionsTests
{
    [Theory]
    [InlineData(5, 1, 3, 1)] // truncated: 5/3 = 1.667, not rounded.
    [InlineData(0, 1, 1, 0)]
    [InlineData(927, 1, 1, 927)]
    [InlineData(100, 1, 0, 0)] // zero denominator disables the axis.
    public void ComputeParallaxOffset_TruncatesIntegerDivision(int scroll, int factorNum, int factorDenom, int expected)
    {
        Assert.Equal(expected, ScrollingLayerService.ComputeParallaxOffset(scroll, factorNum, factorDenom));
    }

    [Theory]
    [InlineData(0, 640, 0)]
    [InlineData(639, 640, 639)]
    [InlineData(640, 640, 0)]
    [InlineData(927, 640, 287)]
    [InlineData(-1, 640, 639)]
    [InlineData(-640, 640, 0)]
    public void WrapOffset_WrapsIntoZeroToCanvasSize(int value, int canvasSize, int expected)
    {
        Assert.Equal(expected, ScrollingLayerService.WrapOffset(value, canvasSize));
    }

    [Theory]
    [InlineData(0, 640, 0)]
    [InlineData(287, 640, -287)]
    [InlineData(640, 640, 0)] // wraps to 0 first.
    public void CoveringOriginStart_IsNegativeOfTheWrappedOffset(int offset, int tileSize, int expected)
    {
        Assert.Equal(expected, ScrollingLayerService.CoveringOriginStart(offset, tileSize));
    }

    [Fact]
    public void CoveringOriginStartAndCount_ForA320x240ViewAgainstA640x480Canvas_ScreenFixedLayer_YieldsExactlyOneQuad()
    {
        // Factor 0/1 layer at Target (1087, -839): offset (0, 0) (plan's own "un seul quad" pin).
        var startX = ScrollingLayerService.CoveringOriginStart(0, 640);
        var countX = ScrollingLayerService.CoveringOriginCount(320, startX, 640);
        var startY = ScrollingLayerService.CoveringOriginStart(0, 480);
        var countY = ScrollingLayerService.CoveringOriginCount(240, startY, 480);

        Assert.Equal(0, startX);
        Assert.Equal(1, countX);
        Assert.Equal(0, startY);
        Assert.Equal(1, countY);
    }

    [Fact]
    public void CoveringOriginStartAndCount_ForOffsets287And239_StillYieldsOneQuad_OffsetWellInsideTheCanvasPeriod()
    {
        // Factor 1/1 layer at the same target: offset (287, 239) (plan's own pin) is still <= canvas
        // size minus view size (640 - 320 = 320) on both axes, so the visible [offset, offset+view)
        // window never crosses the canvas wrap boundary - one tile still fully covers the view.
        var startX = ScrollingLayerService.CoveringOriginStart(287, 640);
        var countX = ScrollingLayerService.CoveringOriginCount(320, startX, 640);
        var startY = ScrollingLayerService.CoveringOriginStart(239, 480);
        var countY = ScrollingLayerService.CoveringOriginCount(240, startY, 480);

        Assert.Equal(-287, startX);
        Assert.Equal(1, countX);
        Assert.Equal(-239, startY);
        Assert.Equal(1, countY);
    }

    [Fact]
    public void CoveringOriginCount_WhenTheOffsetCrossesTheWrapBoundary_YieldsTwoQuads()
    {
        // offset (600, 470): [offset, offset+view) crosses the 640/480 canvas boundary on both axes -
        // two tiles are needed to cover the view with no gap.
        var startX = ScrollingLayerService.CoveringOriginStart(600, 640);
        var countX = ScrollingLayerService.CoveringOriginCount(320, startX, 640);
        var startY = ScrollingLayerService.CoveringOriginStart(470, 480);
        var countY = ScrollingLayerService.CoveringOriginCount(240, startY, 480);

        Assert.Equal(2, countX);
        Assert.Equal(2, countY);
    }

    [Fact]
    public void CoveringOriginCount_WithNonPositiveTileSizeOrViewport_ReturnsZero()
    {
        Assert.Equal(0, ScrollingLayerService.CoveringOriginCount(320, 0, 0));
        Assert.Equal(0, ScrollingLayerService.CoveringOriginCount(0, 0, 640));
    }
}
