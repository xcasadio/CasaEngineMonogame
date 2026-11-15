using CasaEngine.Demos.Demos;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>
/// The demos' help panel hides and shows with F1 (ADR-0090): shown again, it has the size it had when the demo started.
/// </summary>
public class DemoHelpOverlayTests
{
    private const string Camera =
        "Right mouse drag: look around\nRight mouse + W A S D, Q E: fly\nAlt + left mouse drag: orbit around the pivot\n" +
        "Middle mouse drag: pan\nMouse wheel: zoom on the pivot\nF: put the pivot back on the scene";

    private const string Highlights =
        "- The cube is one entity; its root component is the mesh, and the mesh carries the transform.\n" +
        "- The faces are shaded: the material is lit, and the world holds one directional light.\n" +
        "- The camera is an entity too: move it with the mouse, the scene does not change.";

    [Fact]
    public void HiddenThenShown_TheHelpPanelHasItsFirstSizeAndPlace()
    {
        var (desktop, runtime) = HeadlessUiTestHarness.NewDesktop(1024, 768);
        var overlay = new DemoHelpOverlay("Hello world", string.Empty, Camera, Highlights);
        var window = overlay.BuildWindow(desktop);
        desktop.Windows.Add(window);
        int time = 0;
        Settle();

        int height = window.WindowHeight;
        int top = window.Top;
        Assert.True(height > 100, $"The panel measured {height} px on its text.");

        overlay.SetVisible(false);
        Settle();
        overlay.SetVisible(true);
        Settle();

        Assert.Equal(height, window.WindowHeight);
        Assert.Equal(top, window.Top);

        void Settle()
        {
            for (int i = 0; i < 3; i++)
            {
                time += 16;
                HeadlessUiTestHarness.AdvanceFrame(runtime, desktop, time);
            }
        }
    }
}
