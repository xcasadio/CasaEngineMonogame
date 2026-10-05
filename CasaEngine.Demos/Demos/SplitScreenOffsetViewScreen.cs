using CasaEngine.Framework.UI;
using MGUI.Core.UI;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// A solid red element pushed on the UI of the right view of <see cref="SplitScreenDemo"/>, the view that does not start at the
/// corner of the window (ADR-0054). Its tree lives in <c>Content/Screens/split-screen-offset-view.xaml</c>; nothing else stays here.
/// </summary>
internal sealed class SplitScreenOffsetViewScreen : XamlUIScreenBase
{
    public override UILayer Layer => UILayer.HUD;
    public override bool IsModal => false;

    public SplitScreenOffsetViewScreen()
        : base(DemoScreenXaml.Source("split-screen-offset-view.xaml"))
    {
    }

    protected override void OnWindowLoaded(MGWindow window)
    {
    }
}
