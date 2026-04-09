using System;
using System.Collections.Generic;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MonoGame.Extended;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// HUD layer screen: always visible during gameplay.
/// Shows a small info box (title, elapsed time) and a "Pause" button.
/// </summary>
internal sealed class HudScreen : UIScreenBase
{
    private readonly Action _requestPause;

    private MGWindow?    _window;
    private MGTextBlock? _timeLabel;
    private float        _elapsed;

    public override UILayer Layer   => UILayer.HUD;
    public override bool    IsModal => false;

    /// <param name="requestPause">Callback invoked when the player clicks "Pause".</param>
    public HudScreen(Action requestPause) => _requestPause = requestPause;

    protected override void OnInitialize(UIRoot root)
    {
        // Semi-transparent HUD window anchored to the top-left corner.
        _window = new MGWindow(root.Desktop, 10, 10, 220, 155)
        {
            TitleText           = string.Empty,
            IsTitleBarVisible   = false,
            IsUserResizable     = false,
        };
        _window.Padding = new Thickness(8);
        _window.BackgroundBrush.NormalValue = new MGSolidFillBrush(new Color(0, 0, 0, 160));

        var stack = new MGStackPanel(_window, Orientation.Vertical) { Spacing = 4 };

        // Title label
        var title = new MGTextBlock(_window, "[b][color=white]MGUI UI Demo[/color][/b]");
        stack.TryAddChild(title);

        // Elapsed time (updated every frame in Update)
        _timeLabel = new MGTextBlock(_window, "[color=lightgray]Time: 0.0s[/color]");
        stack.TryAddChild(_timeLabel);

        // Separator hint
        var hint = new MGTextBlock(_window, "[color=gray]Press F1 to toggle the demo navigator[/color]");
        hint.Margin = new Thickness(0, 4, 0, 0);
        stack.TryAddChild(hint);

        // Pause button
        var pauseBtn = new MGButton(_window, _ => _requestPause());
        pauseBtn.SetContent("[color=yellow]Open Pause Menu[/color]");
        pauseBtn.Margin = new Thickness(0, 8, 0, 0);
        stack.TryAddChild(pauseBtn);

        _window.SetContent(stack);
    }

    public override void Update(GameTime gameTime)
    {
        _elapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_timeLabel != null)
            _timeLabel.Text = $"[color=lightgray]Time: {_elapsed:F1}s[/color]";
    }

    public override IEnumerable<MGWindow> GetWindows()
    {
        if (_window != null) yield return _window;
    }
}
