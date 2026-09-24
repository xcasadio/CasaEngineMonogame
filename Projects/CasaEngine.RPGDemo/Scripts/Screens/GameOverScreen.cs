using System;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;

namespace CasaEngine.RPGDemo.Scripts.Screens;

/// <summary>
/// MGUI modal game-over screen: "GAME OVER" label + "Return to Title" button.
/// <para/>
/// Its tree lives in the project asset `Screens/GameOver/GameOverScreen.uiscreen`, which names its XAML.
/// Centring included: all that stays here is what the button does. The envelope is acquired through
/// <see cref="AssetContentManager"/> and held for the screen's lifetime (ADR-0038); the owner must dispose
/// this screen to give it back.
/// </summary>
public sealed class GameOverScreen : XamlUIScreenBase
{
    private readonly Action _onReturnToTitle;

    public override UILayer Layer   => UILayer.Modal;
    public override bool    IsModal => true;

    public GameOverScreen(AssetContentManager assetContentManager, Action onReturnToTitle)
        : base(assetContentManager, "GameOverScreen")
    {
        _onReturnToTitle = onReturnToTitle;
    }

    protected override void OnWindowLoaded(MGWindow window)
    {
        FindControl<MGButton>("btnReturnToTitle").AddCommandHandler((_, _) => _onReturnToTitle());
    }
}
