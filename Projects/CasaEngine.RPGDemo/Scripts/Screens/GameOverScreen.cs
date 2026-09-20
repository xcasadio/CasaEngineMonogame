using System;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;

namespace CasaEngine.RPGDemo.Scripts.Screens;

/// <summary>
/// MGUI modal game-over screen: "GAME OVER" label + "Return to Title" button.
/// <para/>
/// Its tree lives in the project asset `Screens/GameOver/GameOverScreen.uiscreen`, which names its XAML.
/// Centring included: all that stays here is what the button does.
/// </summary>
public sealed class GameOverScreen : XamlUIScreenBase
{
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
        FindControl<MGButton>("btnReturnToTitle").AddCommandHandler((_, _) => _onReturnToTitle());
    }
}
