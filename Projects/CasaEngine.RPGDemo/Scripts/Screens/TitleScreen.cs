using System;
using System.Collections.Generic;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace CasaEngine.RPGDemo.Scripts.Screens;

/// <summary>
/// MGUI title screen: "RPG Demo" label + "Start Game" and "Exit" buttons.
/// Replaces the legacy Neoforce TitleScreen.screen.
/// </summary>
public sealed class TitleScreen : UIScreenBase
{
    private readonly Action _onStartGame;
    private readonly Action _onExit;
    private MGWindow? _window;

    public override UILayer Layer   => UILayer.Menu;
    public override bool    IsModal => true;

    public TitleScreen(Action onStartGame, Action onExit)
    {
        _onStartGame = onStartGame;
        _onExit      = onExit;
    }

    protected override void OnInitialize(UIRoot root)
    {
        var bounds = root.Desktop.ValidScreenBounds;
        int w  = 320;
        int h  = 200;
        int cx = bounds.Width  / 2 - w / 2;
        int cy = bounds.Height / 2 - h / 2;

        _window = new MGWindow(root.Desktop, cx, cy, w, h)
        {
            TitleText         = string.Empty,
            IsTitleBarVisible = false,
            IsUserResizable   = false,
        };
        _window.Padding = new Thickness(20);
        _window.BackgroundBrush.NormalValue = new MGSolidFillBrush(new Color(10, 10, 30, 220));

        var stack = new MGStackPanel(_window, Orientation.Vertical) { Spacing = 14 };

        var title = new MGTextBlock(_window, "[b][color=white][b]RPG Demo[/b][/color][/b]");
        title.HorizontalAlignment = HorizontalAlignment.Center;
        stack.TryAddChild(title);

        var startBtn = new MGButton(_window, _ => _onStartGame());
        startBtn.SetContent("[color=lightgray]Start Game[/color]");
        startBtn.Margin = new Thickness(0, 20, 0, 0);
        startBtn.HorizontalAlignment = HorizontalAlignment.Center;
        stack.TryAddChild(startBtn);

        var exitBtn = new MGButton(_window, _ => _onExit());
        exitBtn.SetContent("[color=lightgray]Exit[/color]");
        exitBtn.Margin = new Thickness(0, 4, 0, 0);
        exitBtn.HorizontalAlignment = HorizontalAlignment.Center;
        stack.TryAddChild(exitBtn);

        _window.SetContent(stack);
    }

    public override IEnumerable<MGWindow> GetWindows()
    {
        if (_window != null) yield return _window;
    }
}
