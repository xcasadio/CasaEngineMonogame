using CasaEngine.Framework.Rendering;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// The eviction rule of the HDR scene targets (ADR-0105): a size unused for more than <see cref="HdrTargetEviction.MaxIdleRenders"/>
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

    [Fact]
    public void CollectStale_TreatsTheDepthFormatAsPartOfTheKey_SoABloomKeyOutlivesNothingElse()
    {
        // The HDR scene target and a bloom target can have the same size and format; only the depth format tells them apart.
        var hdrKey = (512, 384, SurfaceFormat.HalfVector4, DepthFormat.Depth24Stencil8);
        var bloomKey = (512, 384, SurfaceFormat.HalfVector4, DepthFormat.None);
        var lastUsed = new Dictionary<(int, int, SurfaceFormat, DepthFormat), long>
        {
            [hdrKey] = 100,
            [bloomKey] = 50,
        };
        var stale = new List<(int, int, SurfaceFormat, DepthFormat)>();

        HdrTargetEviction.CollectStale(lastUsed, 100, stale);

        Assert.Equal(new[] { bloomKey }, stale);
        Assert.Equal(2, lastUsed.Count);
    }

    [Fact]
    public void CollectStale_ABloomChainOfAnOldSizeGoesStaleWhileTheCurrentChainStays()
    {
        // 1280x960 view: 640x480, 320x240, 160x120 (and their partners share the key). Then the view returns to 960x720.
        var lastUsed = new Dictionary<(int, int, SurfaceFormat, DepthFormat), long>();
        var stale = new List<(int, int, SurfaceFormat, DepthFormat)>();
        const SurfaceFormat format = SurfaceFormat.HalfVector4;

        for (long render = 1; render <= 20; render++)
        {
            if (render <= 10)
            {
                lastUsed[(640, 480, format, DepthFormat.None)] = render;
                lastUsed[(320, 240, format, DepthFormat.None)] = render;
                lastUsed[(160, 120, format, DepthFormat.None)] = render;
            }

            lastUsed[(480, 360, format, DepthFormat.None)] = render;
            lastUsed[(240, 180, format, DepthFormat.None)] = render;
            lastUsed[(120, 90, format, DepthFormat.None)] = render;

            stale.Clear();
            HdrTargetEviction.CollectStale(lastUsed, render, stale);
            foreach (var key in stale)
            {
                lastUsed.Remove(key);
            }
        }

        Assert.Equal(3, lastUsed.Count);
        Assert.Contains((480, 360, format, DepthFormat.None), lastUsed.Keys);
        Assert.DoesNotContain((640, 480, format, DepthFormat.None), lastUsed.Keys);
    }
}

/// <summary>
/// The sweep of the tracked target keys on a real pool (ADR-0108): a stale bloom key (no depth) is released by
/// <see cref="HdrTargetEviction.Sweep"/> with its own depth format, and the HDR key of the same size and format is untouched.
/// Skipped where no device can be created.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class HdrTargetEvictionGpuTests
{
    [GpuFact]
    public void Sweep_ReleasesAStaleBloomKeyWithItsDepthFormat_AndLeavesTheSameSizeHdrKey()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var pool = new RenderTargetPool(GpuDeviceHost.Instance.GraphicsDevice);
            const SurfaceFormat format = SurfaceFormat.HalfVector4;
            var hdrKey = (64, 48, format, DepthFormat.Depth24Stencil8);
            var bloomKey = (64, 48, format, DepthFormat.None);

            var hdr = pool.Acquire(64, 48, format, DepthFormat.Depth24Stencil8);
            var bloomA = pool.Acquire(64, 48, format, DepthFormat.None);
            var bloomB = pool.Acquire(64, 48, format, DepthFormat.None);
            Assert.Equal(3, pool.TotalCount);
            pool.Release(hdr);
            pool.Release(bloomA);
            pool.Release(bloomB);

            var lastUsed = new Dictionary<(int, int, SurfaceFormat, DepthFormat), long> { [hdrKey] = 10, [bloomKey] = 3 };
            var scratch = new List<(int, int, SurfaceFormat, DepthFormat)>();
            var released = new List<((int, int, SurfaceFormat, DepthFormat) Key, int Disposed)>();

            HdrTargetEviction.Sweep(lastUsed, 10, scratch, released, pool);

            var entry = Assert.Single(released);
            Assert.Equal(bloomKey, entry.Key);
            Assert.Equal(2, entry.Disposed);
            Assert.Equal(1, pool.TotalCount);
            Assert.True(bloomA.IsDisposed);
            Assert.True(bloomB.IsDisposed);
            Assert.False(hdr.IsDisposed);
            Assert.Single(lastUsed);
            Assert.True(lastUsed.ContainsKey(hdrKey));

            // A key whose targets are still acquired costs nothing: it is dropped from the map but nothing is disposed.
            var busy = pool.Acquire(32, 24, format, DepthFormat.None);
            lastUsed[(32, 24, format, DepthFormat.None)] = 1;
            released.Clear();
            HdrTargetEviction.Sweep(lastUsed, 10, scratch, released, pool);
            Assert.Empty(released);
            Assert.False(busy.IsDisposed);
        });
    }
}
