using System;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Modal pause menu screen (UILayer.Menu). Blocks input to layers below it while visible.
/// <para/>
/// Its tree lives in `Content/Screens/pause-menu.xaml`. What stays here is what the document cannot know:
/// where the window goes, which depends on the resolution, and what the Resume button does.
/// </summary>
internal sealed class PauseMenuScreen : XamlUIScreenBase
{
    private const int WindowWidth = 300;
    private const int WindowHeight = 200;

    private readonly Action _requestResume;

    public override UILayer Layer   => UILayer.Menu;
    public override bool    IsModal => true;

    /// <param name="requestResume">Callback invoked when the player clicks "Resume".</param>
    public PauseMenuScreen(Action requestResume)
        : base(DemoScreenXaml.Source("pause-menu.xaml"))
        => _requestResume = requestResume;

    protected override void OnWindowLoaded(MGWindow window)
    {
        // Centred on the desktop. The XAML declares the size but cannot place the window, because where it
        // goes depends on a resolution that is only known once there is a desktop.
        var bounds = window.Desktop.ValidScreenBounds;
        window.Left = bounds.Width / 2 - WindowWidth / 2;
        window.Top = bounds.Height / 2 - WindowHeight / 2;

        window.WindowClosed += (_, _) => _requestResume();
        FindControl<MGButton>("btnResume").AddCommandHandler((_, _) => _requestResume());
    }
}
