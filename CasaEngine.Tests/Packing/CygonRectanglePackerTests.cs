using CasaEngine.Core.Packing;
using Xunit;

namespace CasaEngine.Tests.Packing;

public sealed class CygonRectanglePackerTests : RectanglePackerTestBase
{
    [Fact]
    public void AchievesExpectedSpaceEfficiency()
    {
        float efficiency = CalculateEfficiency(new CygonRectanglePacker(70, 70));
        Assert.True(efficiency >= 0.75f, $"Expected efficiency >= 0.75 but got {efficiency}.");
    }

    [Fact]
    public void RejectsRectanglesLargerThanPackingArea()
    {
        AssertRejectsTooLargeRectangles(new CygonRectanglePacker(128, 128));
    }

    [Fact]
    public void ThrowsWhenPackingRectangleLargerThanArea()
    {
        AssertThrowsForTooLargeRectangle(new CygonRectanglePacker(128, 128));
    }

    [Fact]
    public void PacksRectangleThatBarelyFits()
    {
        AssertPacksBarelyFittingRectangle(new CygonRectanglePacker(128, 128));
    }

    [Fact]
    public void CompletesBenchmarkWithoutFailure()
    {
        float score = Benchmark(() => new CygonRectanglePacker(1024, 1024));
        Assert.True(score > 0f, "Benchmark should pack at least one rectangle.");
    }
}