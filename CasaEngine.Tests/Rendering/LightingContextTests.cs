using CasaEngine.Framework.Rendering;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class LightingContextTests
{
    [Fact]
    public void DirectionalLightStorage_MatchesConfiguredForwardCap()
    {
        var lightingContext = new LightingContext();

        Assert.Equal(8, LightingContext.MaxDirectionalLights);
        Assert.Equal(LightingContext.MaxDirectionalLights, lightingContext.DirectionalLights.Length);
    }

    [Theory]
    [InlineData(-3, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(4, 4)]
    [InlineData(8, 8)]
    [InlineData(99, 8)]
    public void ClampActiveDirectionalLightCount_ClampsToSupportedRange(int requestedCount, int expectedCount)
    {
        int clampedCount = LightingContext.ClampActiveDirectionalLightCount(requestedCount);

        Assert.Equal(expectedCount, clampedCount);
    }
}