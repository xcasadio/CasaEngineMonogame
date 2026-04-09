using CasaEngine.Core.Packing;
using Xunit;

namespace CasaEngine.Tests.Packing;

public sealed class SimpleRectanglePackerTests : RectanglePackerTestBase
{
    [Fact]
    public void AchievesExpectedSpaceEfficiency()
    {
        float efficiency = CalculateEfficiency(new SimpleRectanglePacker(70, 70));
        Assert.True(efficiency >= 0.75f, $"Expected efficiency >= 0.75 but got {efficiency}.");
    }

    [Fact]
    public void RejectsRectanglesLargerThanPackingArea()
    {
        AssertRejectsTooLargeRectangles(new SimpleRectanglePacker(128, 128));
    }

    [Fact]
    public void ThrowsWhenPackingRectangleLargerThanArea()
    {
        AssertThrowsForTooLargeRectangle(new SimpleRectanglePacker(128, 128));
    }

    [Fact]
    public void PacksRectangleThatBarelyFits()
    {
        AssertPacksBarelyFittingRectangle(new SimpleRectanglePacker(128, 128));
    }

    [Fact]
    public void CompletesBenchmarkWithoutFailure()
    {
        float score = Benchmark(() => new SimpleRectanglePacker(1024, 1024));
        Assert.True(score > 0f, "Benchmark should pack at least one rectangle.");
    }
}