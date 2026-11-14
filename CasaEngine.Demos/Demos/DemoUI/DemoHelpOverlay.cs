using CasaEngine.Framework.UI;
using MGUI.Core.UI;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// The help panel shown in the scene of every demo (ADR-0090): the demo's title, its controls, the default camera
/// controls when the demo keeps that camera, and what to look at. Its texts are formatted once, when the demo starts;
/// showing or hiding it only changes the window's visibility.
/// <para/>
/// Its tree lives in `Content/Screens/demo-help.xaml`.
/// </summary>
internal sealed class DemoHelpOverlay : XamlUIScreenBase
{
    private readonly string _title;
    private readonly string _controls;
    private readonly string _camera;
    private readonly string _highlights;

    public override UILayer Layer   => UILayer.HUD;
    public override bool    IsModal => false;

    /// <param name="title">The demo's title.</param>
    /// <param name="controls">The demo's own controls, one line each; empty collapses the part.</param>
    /// <param name="camera">The default camera controls, one line each; empty when the demo has its own camera.</param>
    /// <param name="highlights">What to look at, one line each; empty collapses the part.</param>
    public DemoHelpOverlay(string title, string controls, string camera, string highlights)
        : base(DemoScreenXaml.Source("demo-help.xaml"))
    {
        _title = title ?? string.Empty;
        _controls = controls ?? string.Empty;
        _camera = camera ?? string.Empty;
        _highlights = highlights ?? string.Empty;
    }

    protected override void OnWindowLoaded(MGWindow window)
    {
        FindControl<MGTextBlock>("lblHelpTitle").Text = _title;
        Fill("lblHelpControlsHeader", "lblHelpControls", _controls);
        Fill("lblHelpCameraHeader", "lblHelpCamera", _camera);
        Fill("lblHelpHighlightsHeader", "lblHelpHighlights", _highlights);

        // The window measured itself on its empty labels when it loaded: size it again on the text it now holds, then
        // place it again, because its bottom edge is the one that is pinned. ApplySizeToContent caps the height to the
        // room below Top, so the window is measured from the top of the screen.
        window.Top = 0;
        window.ApplySizeToContent(SizeToContent.Height, window.WindowWidth, 20, window.WindowWidth, null);
        window.ApplyScreenPlacement();
    }

    private void Fill(string headerName, string bodyName, string text)
    {
        Visibility visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        FindControl<MGTextBlock>(headerName).Visibility = visibility;

        MGTextBlock body = FindControl<MGTextBlock>(bodyName);
        body.Text = text;
        body.Visibility = visibility;
    }

    /// <summary>Shows or hides this panel's window without removing it from the stack.</summary>
    public void SetVisible(bool visible)
    {
        if (Window != null)
        {
            Window.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
