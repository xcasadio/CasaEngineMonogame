using System;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;

namespace CasaEngine.RPGDemo.Scripts.Screens;

/// <summary>
/// MGUI title screen: "RPG Demo" label + "Start Game" and "Exit" buttons.
/// <para/>
/// Its tree lives in the project asset `Screens/TitleScreen/TitleScreen.uiscreen`, which names its XAML.
/// Centring included: all that stays here is what the two buttons do. The envelope is acquired through
/// <see cref="AssetContentManager"/> and held for the screen's lifetime (ADR-0038); the owner must dispose
/// this screen to give it back.
/// </summary>
public sealed class TitleScreen : XamlUIScreenBase
{
    private readonly Action _onStartGame;
    private readonly Action _onExit;

    public override UILayer Layer   => UILayer.Menu;
    public override bool    IsModal => true;

    public TitleScreen(AssetContentManager assetContentManager, Action onStartGame, Action onExit)
        : base(assetContentManager, "TitleScreen")
    {
        _onStartGame = onStartGame;
        _onExit      = onExit;
    }

    protected override void OnWindowLoaded(MGWindow window)
    {
        FindControl<MGButton>("ButtonStartGame").AddCommandHandler((_, _) => _onStartGame());
        FindControl<MGButton>("ButtonExit").AddCommandHandler((_, _) => _onExit());
    }
}
