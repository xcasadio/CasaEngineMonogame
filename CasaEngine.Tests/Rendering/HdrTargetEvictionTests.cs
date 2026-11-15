using CasaEngine.Framework.Rendering;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// The eviction rule of the HDR scene targets (ADR-0096): a size unused for more than <see cref="HdrTargetEviction.MaxIdleRenders"/>
/// renders is stale. Pure, no GPU.
/// </summary>
public class HdrTargetEvictionTests
{
    [Theory]
    [InlineData(10, 10, false)]
    [InlineData(9, 10, false)]
    [InlineData(10 - HdrTargetEviction.MaxIdleRenders, 10, false)]
    [InlineData(10 - HdrTargetEviction.MaxIdleRenders - 1, 10, true)]
    [InlineData(1, 100, true)]
    public void IsStale_FollowsTheIdleRenderCount(long lastUsed, long current, bool expected)
    {
        Assert.Equal(expected, HdrTargetEviction.IsStale(lastUsed, current));
    }

    [Fact]
    public void CollectStale_ReturnsOnlyTheSizesNoLongerUsed_AndLeavesTheMapUntouched()
    {
        var lastUsed = new Dictionary<(int, int), long>
        {
            [(256, 256)] = 100,
            [(1024, 768)] = 100,
            [(700, 500)] = 50,
            [(710, 508)] = 90,
        };
        var stale = new List<(int, int)>();

        HdrTargetEviction.CollectStale(lastUsed, 100, stale);

        Assert.Equal(2, stale.Count);
        Assert.Contains((700, 500), stale);
        Assert.Contains((710, 508), stale);
        Assert.Equal(4, lastUsed.Count);
    }

    [Fact]
    public void ConstantSizes_AreNeverStale()
    {
        // Render-to-texture (256x256 and 1024x768) and a split screen (one 512x768 for two views) used every render.
        var lastUsed = new Dictionary<(int, int), long>();
        var stale = new List<(int, int)>();

        for (long render = 1; render <= 500; render++)
        {
            lastUsed[(256, 256)] = render;
            lastUsed[(1024, 768)] = render;
            lastUsed[(512, 768)] = render;
            HdrTargetEviction.CollectStale(lastUsed, render, stale);
        }

        Assert.Empty(stale);
        Assert.Equal(3, lastUsed.Count);
    }

    [Fact]
    public void ContinuousResize_KeepsTheStaleSizesBounded()
    {
        var lastUsed = new Dictionary<(int, int), long>();
        var stale = new List<(int, int)>();
        int peak = 0;

        for (long render = 1; render <= 200; render++)
        {
            lastUsed[(700 + (int)render, 500)] = render;
            stale.Clear();
            HdrTargetEviction.CollectStale(lastUsed, render, stale);
            foreach (var key in stale)
            {
                lastUsed.Remove(key);
            }
            peak = Math.Max(peak, lastUsed.Count);
        }

        Assert.Equal(HdrTargetEviction.MaxIdleRenders + 1, peak);
    }
}
