using CasaEngine.Framework.UI;
using MGUI.Core.UI;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Small bottom-center overlay displayed when the <see cref="DemoInfoScreen"/> window is hidden.
/// Reminds the player how to bring the demo info panel back.
/// Toggle visibility with <see cref="SetVisible"/>.
/// <para/>
/// Its tree lives in `Content/Screens/demo-hint.xaml`, where it sits included.
/// </summary>
internal sealed class DemoHintOverlay : XamlUIScreenBase
{
    public override UILayer Layer   => UILayer.HUD;
    public override bool    IsModal => false;

    public DemoHintOverlay()
        : base(DemoScreenXaml.Source("demo-hint.xaml"))
    {
    }

    protected override void OnWindowLoaded(MGWindow window)
    {
        // Nothing to wire: the document places it and carries its one line of text.
    }

    /// <summary>Shows or hides this overlay's window without removing it from the stack.</summary>
    public void SetVisible(bool visible)
    {
        if (Window != null)
        {
            Window.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
