using Microsoft.Xna.Framework;
using CasaEngine.Framework.Rendering;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// Pins the pure integer-fit layout of a virtual resolution (ADR-0048, E19.s S-R3):
/// <c>k = max(1, floor(min(L / w, H / h)))</c>, image <c>w·k × h·k</c> centered with floored offsets, cropped by the window.
/// </summary>
public class VirtualResolutionLayoutTests
{
    [Theory]
    [InlineData(1280, 960, 4, 0, 0, 1280, 960)]
    [InlineData(1280, 944, 3, 160, 112, 960, 720)]
    [InlineData(1920, 1080, 4, 320, 60, 1280, 960)]
    [InlineData(2560, 1440, 6, 320, 0, 1920, 1440)]
    [InlineData(800, 600, 2, 80, 60, 640, 480)]
    [InlineData(640, 480, 2, 0, 0, 640, 480)]
    [InlineData(1281, 961, 4, 0, 0, 1280, 960)]
    [InlineData(300, 200, 1, 0, 0, 300, 200)]
    [InlineData(400, 200, 1, 40, 0, 320, 200)]
    public void Compute_For320x240_GivesTheTableOfTheAlundraPlan(
        int windowWidth, int windowHeight, int scale, int x, int y, int width, int height)
    {
        var fit = VirtualResolutionLayout.Compute(windowWidth, windowHeight, 320, 240);

        Assert.Equal(scale, fit.Scale);
        Assert.Equal(new Rectangle(x, y, width, height), fit.ImageRect);
    }

    [Theory]
    [InlineData(1280, 960, true)]
    [InlineData(1281, 961, false)]
    [InlineData(1920, 1080, false)]
    [InlineData(300, 200, true)]
    [InlineData(400, 200, false)]
    public void Compute_CoversWindow_IsTrueOnlyWhenTheImageIsTheWholeWindow(int windowWidth, int windowHeight, bool covers)
    {
        var fit = VirtualResolutionLayout.Compute(windowWidth, windowHeight, 320, 240);

        Assert.Equal(covers, fit.CoversWindow);
    }

    [Fact]
    public void Compute_UsesTheDeclaredVirtualSize_NotAFixed320x240()
    {
        var fit = VirtualResolutionLayout.Compute(1000, 700, 256, 224);

        Assert.Equal(3, fit.Scale);
        Assert.Equal(new Rectangle(116, 14, 768, 672), fit.ImageRect);
    }

    [Fact]
    public void Compute_TinyWindowBelowTheVirtualSize_KeepsScaleOneAndShowsTheCenter()
    {
        var fit = VirtualResolutionLayout.Compute(100, 101, 320, 240);

        Assert.Equal(1, fit.Scale);
        Assert.Equal(new Rectangle(0, 0, 100, 101), fit.ImageRect);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 480)]
    [InlineData(640, 0)]
    public void Compute_WithAnEmptyWindow_GivesAnEmptyRectangleAndScaleOne(int windowWidth, int windowHeight)
    {
        var fit = VirtualResolutionLayout.Compute(windowWidth, windowHeight, 320, 240);

        Assert.Equal(1, fit.Scale);
        Assert.True(fit.ImageRect.Width == 0 || fit.ImageRect.Height == 0);
    }

    [Theory]
    [InlineData(0, 240)]
    [InlineData(320, 0)]
    [InlineData(-1, 240)]
    public void Compute_RejectsAVirtualSizeThatIsNotPositive(int virtualWidth, int virtualHeight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => VirtualResolutionLayout.Compute(640, 480, virtualWidth, virtualHeight));
    }
}
