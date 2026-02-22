using System;
using System.Collections.Generic;
using CasaEngine.Framework.GUI;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace CasaEngine.RPGDemo.Scripts.Screens;

/// <summary>
/// MGUI modal game-over screen: "GAME OVER" label + "Return to Title" button.
/// Replaces the legacy Neoforce GameOverScreen.screen.
/// </summary>
public sealed class GameOverScreen : UIScreenBase
{
    private readonly Action _onReturnToTitle;
    private MGWindow? _window;

    public override UILayer Layer   => UILayer.Modal;
    public override bool    IsModal => true;

    public GameOverScreen(Action onReturnToTitle)
    {
        _onReturnToTitle = onReturnToTitle;
    }

    protected override void OnInitialize(UIRoot root)
    {
        var bounds = root.Desktop.ValidScreenBounds;
        const int w = 380;
        const int h = 180;
        int cx = bounds.Width  / 2 - w / 2;
        int cy = bounds.Height / 2 - h / 2;

        _window = new MGWindow(root.Desktop, cx, cy, w, h)
        {
            TitleText         = string.Empty,
            IsTitleBarVisible = false,
            IsUserResizable   = false,
        };
        _window.Padding = new Thickness(24);
        _window.BackgroundBrush.NormalValue = new MGSolidFillBrush(new Color(5, 0, 0, 230));

        var stack = new MGStackPanel(_window, Orientation.Vertical) { Spacing = 18 };

        var label = new MGTextBlock(_window, "[b][color=red]GAME OVER[/color][/b]");
        label.HorizontalAlignment = HorizontalAlignment.Center;
        stack.TryAddChild(label);

        var returnBtn = new MGButton(_window, _ => _onReturnToTitle());
        returnBtn.SetContent("[color=lightgray]Return to Title[/color]");
        returnBtn.HorizontalAlignment = HorizontalAlignment.Center;
        stack.TryAddChild(returnBtn);

        _window.SetContent(stack);
    }

    public override IEnumerable<MGWindow> GetWindows()
    {
        if (_window != null) yield return _window;
    }
}
