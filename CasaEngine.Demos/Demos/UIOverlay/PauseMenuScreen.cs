using System;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Modal pause menu screen (UILayer.Menu). Blocks input to layers below it while visible.
/// <para/>
/// Its tree lives in `Content/Screens/pause-menu.xaml`, centring included. All that stays here is what the
/// Resume button does.
/// </summary>
internal sealed class PauseMenuScreen : XamlUIScreenBase
{
    private readonly Action _requestResume;

    public override UILayer Layer   => UILayer.Menu;
    public override bool    IsModal => true;

    /// <param name="requestResume">Callback invoked when the player clicks "Resume".</param>
    public PauseMenuScreen(Action requestResume)
        : base(DemoScreenXaml.Source("pause-menu.xaml"))
        => _requestResume = requestResume;

    protected override void OnWindowLoaded(MGWindow window)
    {
        window.WindowClosed += (_, _) => _requestResume();
        FindControl<MGButton>("btnResume").AddCommandHandler((_, _) => _requestResume());
    }
}
