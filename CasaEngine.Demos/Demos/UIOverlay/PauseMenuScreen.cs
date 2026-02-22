using System;
using System.Collections.Generic;
using CasaEngine.Framework.GUI;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MonoGame.Extended;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Modal pause menu screen (UILayer.Menu).
/// Blocks input to layers below it while visible.
/// </summary>
internal sealed class PauseMenuScreen : UIScreenBase
{
    private readonly Action _requestResume;
    private MGWindow? _window;

    public override UILayer Layer   => UILayer.Menu;
    public override bool    IsModal => true;

    /// <param name="requestResume">Callback invoked when the player clicks "Resume".</param>
    public PauseMenuScreen(Action requestResume) => _requestResume = requestResume;

    protected override void OnInitialize(UIRoot root)
    {
        var bounds = root.Desktop.ValidScreenBounds;
        int cx = bounds.Width  / 2 - 150;
        int cy = bounds.Height / 2 - 100;

        _window = new MGWindow(root.Desktop, cx, cy, 300, 200)
        {
            TitleText       = "Pause",
            IsUserResizable = false,
        };
        _window.Padding = new Thickness(12);
        _window.BackgroundBrush.NormalValue = new MGSolidFillBrush(new Color(20, 20, 40, 230));

        var stack = new MGStackPanel(_window, Orientation.Vertical) { Spacing = 10 };

        var label = new MGTextBlock(_window, "[b][color=white]Game Paused[/color][/b]");
        label.Margin = new Thickness(0, 0, 0, 8);
        stack.TryAddChild(label);

        var desc = new MGTextBlock(_window,
            "[color=lightgray]Click [b]Resume[/b] to continue\nor close the app.[/color]");
        stack.TryAddChild(desc);

        var resumeBtn = new MGButton(_window, _ => _requestResume());
        resumeBtn.SetContent("[color=lightgreen]Resume[/color]");
        resumeBtn.Margin = new Thickness(0, 12, 0, 0);
        stack.TryAddChild(resumeBtn);

        _window.SetContent(stack);
    }

    public override IEnumerable<MGWindow> GetWindows()
    {
        if (_window != null) yield return _window;
    }
}
