using CasaEngine.Framework.Rendering;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class RenderStatsTests
{
    [Fact]
    public void Reset_ClearsParticleCount()
    {
        var stats = new RenderStats
        {
            ParticleCount = 12,
            TransparentItems = 12,
            ParticleRenderCpuMilliseconds = 1.25,
        };

        stats.Reset();

        Assert.Equal(0, stats.ParticleCount);
        Assert.Equal(0, stats.TransparentItems);
        Assert.Equal(0.0, stats.ParticleRenderCpuMilliseconds);
    }

    [Fact]
    public void ToString_IncludesParticleCount()
    {
        var stats = new RenderStats
        {
            ParticleCount = 7,
        };

        string text = stats.ToString();

        Assert.Contains("Particles:7", text);
        Assert.Contains("ParticleRender:", text);
    }
}