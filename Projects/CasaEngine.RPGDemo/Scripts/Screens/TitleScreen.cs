using System;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;

namespace CasaEngine.RPGDemo.Scripts.Screens;

/// <summary>
/// MGUI title screen: "RPG Demo" label + "Start Game" and "Exit" buttons.
/// <para/>
/// Its tree lives in the project asset `Screens/TitleScreen/TitleScreen.uiscreen`, which names its XAML.
/// Only where the window goes, and what the two buttons do, stays here.
/// </summary>
public sealed class TitleScreen : XamlUIScreenBase
{
    private const int WindowWidth = 320;
    private const int WindowHeight = 200;

    private readonly Action _onStartGame;
    private readonly Action _onExit;

    public override UILayer Layer   => UILayer.Menu;
    public override bool    IsModal => true;

    public TitleScreen(Action onStartGame, Action onExit)
        : this(RpgDemoScreenAssets.Load("TitleScreen"), onStartGame, onExit)
    {
    }

    // The envelope and the path it came from belong together, and reading it once means threading them
    // through a single constructor argument.
    private TitleScreen((UIScreenAsset Asset, string FilePath) screen, Action onStartGame, Action onExit)
        : base(screen.Asset, screen.FilePath)
    {
        _onStartGame = onStartGame;
        _onExit      = onExit;
    }

    protected override void OnWindowLoaded(MGWindow window)
    {
        var bounds = window.Desktop.ValidScreenBounds;
        window.Left = bounds.Width / 2 - WindowWidth / 2;
        window.Top = bounds.Height / 2 - WindowHeight / 2;

        FindControl<MGButton>("ButtonStartGame").AddCommandHandler((_, _) => _onStartGame());
        FindControl<MGButton>("ButtonExit").AddCommandHandler((_, _) => _onExit());
    }
}
