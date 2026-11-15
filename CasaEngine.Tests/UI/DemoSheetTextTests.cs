using CasaEngine.Demos;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>
/// The text of a demo's sheet (ADR-0090), as the main screen and the in-demo help panel display it.
/// </summary>
public class DemoSheetTextTests
{
    [Fact]
    public void Controls_AreOneLineEach_WithTheGamePadButtonWhenThereIsOne()
    {
        var controls = new DemoControl[]
        {
            new("Space", "restart the cutscene", "A"),
            new("S", "stop the cutscene"),
        };

        Assert.Equal("Space / A: restart the cutscene\nS: stop the cutscene", DemoSheetText.FormatControls(controls));
    }

    [Fact]
    public void Highlights_AreOneDashedLineEach()
    {
        Assert.Equal("- first\n- second", DemoSheetText.FormatHighlights(new[] { "first", "second" }));
    }

    [Fact]
    public void AnEmptyOrMissingList_GivesAnEmptyText()
    {
        Assert.Equal(string.Empty, DemoSheetText.FormatControls(null));
        Assert.Equal(string.Empty, DemoSheetText.FormatControls(System.Array.Empty<DemoControl>()));
        Assert.Equal(string.Empty, DemoSheetText.FormatHighlights(null));
        Assert.Equal(string.Empty, DemoSheetText.FormatHighlights(System.Array.Empty<string>()));
    }
}
