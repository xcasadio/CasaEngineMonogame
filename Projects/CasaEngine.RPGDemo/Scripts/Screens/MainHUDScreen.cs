using System;
using System.Collections.Generic;
using CasaEngine.Framework.GUI;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;

namespace CasaEngine.RPGDemo.Scripts.Screens;

/// <summary>
/// MGUI in-game HUD: player portrait + life bar (bottom-left).
/// Replaces the legacy Neoforce MainHUD.screen.
/// </summary>
public sealed class MainHUDScreen : UIScreenBase
{
    private readonly Texture2D?  _portrait;
    private readonly Func<float> _getHPPercent;   // returns 0..100
    private MGWindow?             _window;
    private MGProgressBar?        _lifeBar;

    public override UILayer Layer   => UILayer.HUD;
    public override bool    IsModal => false;

    /// <param name="portrait">Optional portrait texture (loaded from MainHUD.png).</param>
    /// <param name="getHPPercent">Callback returning current HP as a 0–100 percentage.</param>
    public MainHUDScreen(Texture2D? portrait, Func<float> getHPPercent)
    {
        _portrait     = portrait;
        _getHPPercent = getHPPercent;
    }

    protected override void OnInitialize(UIRoot root)
    {
        var bounds = root.Desktop.ValidScreenBounds;

        // HUD panel: bottom-left corner, fixed size
        const int winW = 230;
        const int winH = 66;
        int left = 12;
        int top  = bounds.Height - winH - 12;

        _window = new MGWindow(root.Desktop, left, top, winW, winH)
        {
            TitleText         = string.Empty,
            IsTitleBarVisible = false,
            IsUserResizable   = false,
        };
        _window.Padding = new Thickness(6);
        _window.BackgroundBrush.NormalValue = new MGSolidFillBrush(new Color(0, 0, 0, 160));

        var dock = new MGDockPanel(_window, false);

        // Portrait on the left
        if (_portrait != null)
        {
            // Link portrait sub-rect: x=41, y=0, w=60, h=64 in MainHUD.png
            var portrait = new MGImage(_window, _portrait, new Rectangle(41, 0, 60, 64));
            portrait.PreferredWidth  = 52;
            portrait.PreferredHeight = 52;
            portrait.Margin = new Thickness(0, 0, 6, 0);
            dock.TryAddChild(portrait, Dock.Left);
        }

        // Life bar + label in remaining space
        var rightPanel = new MGStackPanel(_window, Orientation.Vertical) { Spacing = 2 };

        var hpLabel = new MGTextBlock(_window, "[color=lightgray]HP[/color]");
        rightPanel.TryAddChild(hpLabel);

        _lifeBar = new MGProgressBar(_window, 0f, 100f, 100f);
        _lifeBar.PreferredWidth  = 150;
        _lifeBar.PreferredHeight = 14;
        rightPanel.TryAddChild(_lifeBar);

        dock.TryAddChild(rightPanel, Dock.Left);

        _window.SetContent(dock);
    }

    public override void Update(GameTime gameTime)
    {
        if (_lifeBar != null)
            _lifeBar.Value = _getHPPercent();
    }

    public override IEnumerable<MGWindow> GetWindows()
    {
        if (_window != null) yield return _window;
    }
}
