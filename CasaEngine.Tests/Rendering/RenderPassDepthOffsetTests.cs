using CasaEngine.Framework.Rendering.Depth;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// D1: <see cref="RenderPassDepthOffset.DeriveDepthOffset"/> folds a <see cref="RenderPass2D"/> into a
/// scalar Z contribution for tile map layers that stay chunked. It must stay monotonic in the enum's
/// declared order, sit at exactly 0 for <see cref="RenderPass2D.YSortedWorld"/> - the plane every sorted
/// sprite and overlay tile shares - and separate neighbouring passes by at least 1, well above any
/// per-layer zOffset in existing content (all below 1), so the pass always dominates it.
/// </summary>
public class RenderPassDepthOffsetTests
{
    // Declaration order of RenderPass2D, from CasaEngine/Framework/Rendering/Depth/RenderPass2D.cs.
    private static readonly RenderPass2D[] PassesInDeclaredOrder =
    {
        RenderPass2D.Background,
        RenderPass2D.Ground,
        RenderPass2D.GroundDetails,
        RenderPass2D.YSortedWorld,
        RenderPass2D.Foreground,
        RenderPass2D.Effects,
        RenderPass2D.ScreenEffects,
        RenderPass2D.UI,
    };

    [Fact]
    public void DeriveDepthOffset_YSortedWorld_IsExactlyZero()
    {
        Assert.Equal(0f, RenderPassDepthOffset.DeriveDepthOffset(RenderPass2D.YSortedWorld));
    }

    [Fact]
    public void DeriveDepthOffset_IsMonotonicOverTheWholeEnumInDeclaredOrder()
    {
        for (var index = 1; index < PassesInDeclaredOrder.Length; index++)
        {
            var previous = RenderPassDepthOffset.DeriveDepthOffset(PassesInDeclaredOrder[index - 1]);
            var current = RenderPassDepthOffset.DeriveDepthOffset(PassesInDeclaredOrder[index]);

            Assert.True(current > previous,
                $"{PassesInDeclaredOrder[index]} ({current}) must be greater than {PassesInDeclaredOrder[index - 1]} ({previous}).");
        }
    }

    [Fact]
    public void DeriveDepthOffset_StepBetweenNeighbouringPasses_IsAtLeastOne()
    {
        // The step must dominate every zOffset in existing content, which stays below 1
        // (see D1: zOffset separates layers of the same pass by increments of 0.1).
        for (var index = 1; index < PassesInDeclaredOrder.Length; index++)
        {
            var previous = RenderPassDepthOffset.DeriveDepthOffset(PassesInDeclaredOrder[index - 1]);
            var current = RenderPassDepthOffset.DeriveDepthOffset(PassesInDeclaredOrder[index]);

            Assert.True(current - previous >= 1f,
                $"step from {PassesInDeclaredOrder[index - 1]} to {PassesInDeclaredOrder[index]} was {current - previous}, expected >= 1.");
        }
    }

    [Fact]
    public void DeriveDepthOffset_PassesBeforeYSortedWorld_AreNegative()
    {
        Assert.True(RenderPassDepthOffset.DeriveDepthOffset(RenderPass2D.Background) < 0f);
        Assert.True(RenderPassDepthOffset.DeriveDepthOffset(RenderPass2D.Ground) < 0f);
        Assert.True(RenderPassDepthOffset.DeriveDepthOffset(RenderPass2D.GroundDetails) < 0f);
    }

    [Fact]
    public void DeriveDepthOffset_PassesAfterYSortedWorld_ArePositive()
    {
        Assert.True(RenderPassDepthOffset.DeriveDepthOffset(RenderPass2D.Foreground) > 0f);
        Assert.True(RenderPassDepthOffset.DeriveDepthOffset(RenderPass2D.Effects) > 0f);
        Assert.True(RenderPassDepthOffset.DeriveDepthOffset(RenderPass2D.ScreenEffects) > 0f);
        Assert.True(RenderPassDepthOffset.DeriveDepthOffset(RenderPass2D.UI) > 0f);
    }
}
