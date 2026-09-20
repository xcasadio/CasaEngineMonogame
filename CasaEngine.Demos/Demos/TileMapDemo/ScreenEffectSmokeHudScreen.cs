using System.Collections.Generic;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Containers;
using MonoGame.Extended;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Non-modal MGUI screen for the above-UI screen effect smoke (T2.2,
/// ai-agent/tasks/screen-effect-above-ui-tasks.md). A dedicated screen rather than a reuse of
/// <see cref="HudScreen"/>: <see cref="HudScreen"/>'s buttons open a pause menu and a dialogue
/// screen that <see cref="TileMapDemo"/> does not have, so its content would not fit this smoke's
/// purpose even though it has no 3D-scene dependency of its own.
/// </summary>
/// <remarks>
/// The window background is a fully opaque solid colour on purpose: it is the pixel the smoke
/// samples to tell <see cref="CasaEngine.Framework.Rendering.ScreenEffects.ScreenEffectLayer.BelowUI"/>
/// from <see cref="CasaEngine.Framework.Rendering.ScreenEffects.ScreenEffectLayer.AboveUI"/> apart -
/// see <see cref="TileMapDemo.ReferencePixelScreenPosition"/>.
/// </remarks>
internal sealed class ScreenEffectSmokeHudScreen : UIScreenBase
{
    /// <summary>The window's fully opaque reference colour, sampled by the smoke's numeric check.</summary>
    public static readonly Color ReferenceColor = Color.White;

    private MGWindow? _window;

    public override UILayer Layer   => UILayer.HUD;
    public override bool    IsModal => false;

    protected override void OnInitialize(UIRoot root)
    {
        _window = new MGWindow(root.Desktop, 10, 10, 320, 110)
        {
            TitleText           = string.Empty,
            IsTitleBarVisible   = false,
            IsUserResizable     = false,
        };
        _window.Padding = new Thickness(8);
        _window.BackgroundBrush.NormalValue = new MGSolidFillBrush(ReferenceColor);

        var stack = new MGStackPanel(_window, Orientation.Vertical) { Spacing = 4 };

        var title = new MGTextBlock(_window, "[b][color=black]Screen effect above-UI smoke[/color][/b]");
        stack.TryAddChild(title);

        var hint1 = new MGTextBlock(_window, "[color=black]Press 1: alpha fade to black and back, BelowUI (default)[/color]");
        stack.TryAddChild(hint1);

        var hint2 = new MGTextBlock(_window, "[color=black]Press 2: alpha fade to black and back, AboveUI[/color]");
        stack.TryAddChild(hint2);

        var hint3 = new MGTextBlock(_window, "[color=darkgray]In AboveUI this window darkens with the scene.[/color]");
        hint3.Margin = new Thickness(0, 4, 0, 0);
        stack.TryAddChild(hint3);

        _window.SetContent(stack);
    }

    public override IEnumerable<MGWindow> GetWindows()
    {
        if (_window != null) yield return _window;
    }
}
