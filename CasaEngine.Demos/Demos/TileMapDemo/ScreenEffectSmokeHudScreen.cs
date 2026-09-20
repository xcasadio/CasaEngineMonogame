using CasaEngine.Framework.UI;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.FillBrushes;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Non-modal MGUI screen for the above-UI screen effect smoke (T2.2,
/// ai-agent/tasks/screen-effect-above-ui-tasks.md). A dedicated screen rather than a reuse of
/// <see cref="HudScreen"/>: <see cref="HudScreen"/>'s buttons open a pause menu and a dialogue
/// screen that <see cref="TileMapDemo"/> does not have, so its content would not fit this smoke's
/// purpose even though it has no 3D-scene dependency of its own.
/// <para/>
/// Its tree lives in `Content/Screens/screen-effect-smoke-hud.xaml`.
/// </summary>
/// <remarks>
/// The window background is a fully opaque solid colour on purpose: it is the pixel the smoke
/// samples to tell <see cref="CasaEngine.Framework.Rendering.ScreenEffects.ScreenEffectLayer.BelowUI"/>
/// from <see cref="CasaEngine.Framework.Rendering.ScreenEffects.ScreenEffectLayer.AboveUI"/> apart.
/// <para/>
/// It is therefore applied here rather than declared in the XAML, which is the one thing in this screen
/// that stays in code: the smoke's numeric check reads <see cref="ReferenceColor"/>, and a colour written
/// in two places could drift apart without anything noticing.
/// </remarks>
internal sealed class ScreenEffectSmokeHudScreen : XamlUIScreenBase
{
    /// <summary>The window's fully opaque reference colour, sampled by the smoke's numeric check.</summary>
    public static readonly Color ReferenceColor = Color.White;

    public override UILayer Layer   => UILayer.HUD;
    public override bool    IsModal => false;

    public ScreenEffectSmokeHudScreen()
        : base(DemoScreenXaml.Source("screen-effect-smoke-hud.xaml"))
    {
    }

    protected override void OnWindowLoaded(MGWindow window)
    {
        window.BackgroundBrush.NormalValue = new MGSolidFillBrush(ReferenceColor);
    }
}
