using System.Collections.Generic;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MonoGame.Extended;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Small bottom-center overlay displayed when the <see cref="DemoInfoScreen"/> window is hidden.
/// Reminds the player how to bring the demo info panel back.
/// Toggle visibility with <see cref="SetVisible"/>.
/// </summary>
internal sealed class DemoHintOverlay : UIScreenBase
{
    private MGWindow? _window;

    public override UILayer Layer   => UILayer.HUD;
    public override bool    IsModal => false;

    protected override void OnInitialize(UIRoot root)
    {
        var bounds = root.Desktop.ValidScreenBounds;
        int winW = 300;
        int winH = 36;
        int x = bounds.Width / 2 - winW / 2;
        int y = bounds.Height - winH - 14;

        _window = new MGWindow(root.Desktop, x, y, winW, winH)
        {
            TitleText         = string.Empty,
            IsTitleBarVisible = false,
            IsUserResizable   = false,
        };
        _window.Padding = new Thickness(6, 6, 6, 6);
        _window.BackgroundBrush.NormalValue = new MGSolidFillBrush(new Color(0, 0, 0, 140));

        var hint = new MGTextBlock(_window,
            "[color=lightgray][i]Press [b]F1[/b] to show demo info[/i][/color]");
        _window.SetContent(hint);
    }

    /// <summary>Shows or hides this overlay's window without removing it from the stack.</summary>
    public void SetVisible(bool visible)
    {
        if (_window != null)
            _window.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public override IEnumerable<MGWindow> GetWindows()
    {
        if (_window != null) yield return _window;
    }
}
