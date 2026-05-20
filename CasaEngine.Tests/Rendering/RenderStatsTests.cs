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
        };

        stats.Reset();

        Assert.Equal(0, stats.ParticleCount);
        Assert.Equal(0, stats.TransparentItems);
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
    }
}