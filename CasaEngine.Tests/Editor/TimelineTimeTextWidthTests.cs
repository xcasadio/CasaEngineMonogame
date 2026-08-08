using CasaEngine.Tests.ContentBrowser;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>Verifie le mecanisme utilise par l'entete de la timeline Animation2D : une largeur
/// reservee via MeasureText/PreferredWidth pour que le texte du temps ne decale pas ce qui suit.</summary>
public class TimelineTimeTextWidthTests
{
    private const string TimeTextTemplate = "000.000s/000.000s";

    [Fact]
    public void ReservedTimeTextWidth_KeepsFollowingTextInPlace()
    {
        ContentBrowserViewTestHarness harness = ContentBrowserViewTestHarness.Create(420, 120);

        MGStackPanel row = new(harness.Window, Orientation.Horizontal) { Spacing = 4 };
        MGTextBlock timeText = new(harness.Window, string.Empty)
        {
            WrapText = false,
            ScaleDimensionsWithResponsive = false,
        };
        MGTextBlock zoomText = new(harness.Window, "Zoom:100 %");

        row.TryAddChild(timeText);
        row.TryAddChild(zoomText);
        harness.Window.SetContent(row);

        Vector2 templateSize = timeText.MeasureText(TimeTextTemplate, false, false);
        Assert.True(templateSize.X > 0f);

        int reservedWidth = (int)MathF.Ceiling(templateSize.X);
        timeText.MinWidth = reservedWidth;
        timeText.PreferredWidth = reservedWidth;

        timeText.Text = "   2.417s/ 11.840s";
        harness.AdvanceFrame(0);
        harness.AdvanceFrame(16);
        int firstZoomLeft = zoomText.ActualLayoutBounds.Left;
        int firstTimeWidth = timeText.ActualLayoutBounds.Width;

        // Un temps beaucoup plus court : sans largeur reservee, le bloc retrecirait.
        timeText.Text = "0s/1s";
        harness.AdvanceFrame(32);
        harness.AdvanceFrame(48);

        Assert.Equal(firstTimeWidth, timeText.ActualLayoutBounds.Width);
        Assert.Equal(firstZoomLeft, zoomText.ActualLayoutBounds.Left);
    }
}
