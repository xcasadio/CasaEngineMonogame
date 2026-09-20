using System;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// HUD layer screen: always visible during gameplay.
/// Shows a small info box (title, elapsed time) and a "Pause" button.
/// <para/>
/// Its tree lives in `Content/Screens/ui-overlay-hud.xaml`. What stays here is the elapsed time it rewrites
/// every frame, and what its two buttons do.
/// </summary>
internal sealed class HudScreen : XamlUIScreenBase
{
    private readonly Action _requestPause;
    private readonly Action _requestDialogue;

    private MGTextBlock _timeLabel;
    private float _elapsed;

    public override UILayer Layer   => UILayer.HUD;
    public override bool    IsModal => false;

    /// <param name="requestPause">Callback invoked when the player clicks "Pause".</param>
    public HudScreen(Action requestPause)
        : this(requestPause, static () => { })
    {
    }

    public HudScreen(Action requestPause, Action requestDialogue)
        : base(DemoScreenXaml.Source("ui-overlay-hud.xaml"))
    {
        _requestPause = requestPause;
        _requestDialogue = requestDialogue;
    }

    protected override void OnWindowLoaded(MGWindow window)
    {
        // Found once and kept: Update runs every frame, and a lookup by name there would be a per-frame
        // dictionary hit for a control that never changes.
        _timeLabel = FindControl<MGTextBlock>("lblTime");

        FindControl<MGButton>("btnPause").AddCommandHandler((_, _) => _requestPause());
        FindControl<MGButton>("btnDialogue").AddCommandHandler((_, _) => _requestDialogue());
    }

    public override void Update(GameTime gameTime)
    {
        _elapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;
        _timeLabel.Text = $"[color=lightgray]Time: {_elapsed:F1}s[/color]";
    }
}
