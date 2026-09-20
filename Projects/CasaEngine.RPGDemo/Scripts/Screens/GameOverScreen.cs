using System;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;

namespace CasaEngine.RPGDemo.Scripts.Screens;

/// <summary>
/// MGUI modal game-over screen: "GAME OVER" label + "Return to Title" button.
/// <para/>
/// Its tree lives in the project asset `Screens/GameOver/GameOverScreen.uiscreen`, which names its XAML.
/// Only where the window goes, and what the button does, stays here.
/// </summary>
public sealed class GameOverScreen : XamlUIScreenBase
{
    private const int WindowWidth = 380;
    private const int WindowHeight = 180;

    private readonly Action _onReturnToTitle;

    public override UILayer Layer   => UILayer.Modal;
    public override bool    IsModal => true;

    public GameOverScreen(Action onReturnToTitle)
        : this(RpgDemoScreenAssets.Load("GameOverScreen"), onReturnToTitle)
    {
    }

    private GameOverScreen((UIScreenAsset Asset, string FilePath) screen, Action onReturnToTitle)
        : base(screen.Asset, screen.FilePath)
    {
        _onReturnToTitle = onReturnToTitle;
    }

    protected override void OnWindowLoaded(MGWindow window)
    {
        var bounds = window.Desktop.ValidScreenBounds;
        window.Left = bounds.Width / 2 - WindowWidth / 2;
        window.Top = bounds.Height / 2 - WindowHeight / 2;

        FindControl<MGButton>("btnReturnToTitle").AddCommandHandler((_, _) => _onReturnToTitle());
    }
}
