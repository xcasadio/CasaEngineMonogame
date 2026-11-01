using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScreenEffects;
using CasaEngine.Framework.UI.MGUI;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>What a <c>fade_screen</c> action does to the service (ADR-0081). <see cref="ScreenActionContextWiringTests"/> covers the delegate.</summary>
public class ScreenFadeWiringTests
{
    private static UIScreenActionData Fade(float durationSeconds = 1f) => new()
    {
        Type = UIScreenActionType.FadeScreen,
        Layer = ScreenEffectLayer.AboveUI,
        BlendMode = SpriteBlendMode.Subtractive,
        From = new Color(10, 20, 30, 40),
        To = new Color(110, 120, 130, 140),
        DurationSeconds = durationSeconds,
    };

    [Fact]
    public void AFade_SetsTheLayer_AndStartsFromItsFirstColor()
    {
        var effects = new ScreenEffectService();

        GameManager.StartScreenFade(effects, Fade());

        Assert.Equal(ScreenEffectLayer.AboveUI, effects.Layer);
        Assert.Equal(SpriteBlendMode.Subtractive, effects.Blend);
        Assert.True(effects.Active);
        Assert.True(effects.IsFading);
        Assert.Equal(new Color(10, 20, 30, 40), new Color(effects.R, effects.G, effects.B, effects.A));
    }

    [Fact]
    public void AFade_EndsOnItsSecondColor_AfterItsDuration()
    {
        var effects = new ScreenEffectService();
        GameManager.StartScreenFade(effects, Fade(2f));

        effects.Update(1f);
        Assert.True(effects.IsFading);
        Assert.Equal(60, effects.R);

        effects.Update(1f);
        Assert.False(effects.IsFading);
        Assert.Equal(new Color(110, 120, 130, 140), new Color(effects.R, effects.G, effects.B, effects.A));
    }

    [Fact]
    public void AFadeOfNoDuration_AppliesItsEnd_AndWaitsForNothing()
    {
        var effects = new ScreenEffectService();

        GameManager.StartScreenFade(effects, Fade(0f));

        Assert.False(effects.IsFading);
        Assert.True(effects.Active);
        Assert.Equal(new Color(110, 120, 130, 140), new Color(effects.R, effects.G, effects.B, effects.A));
    }

    [Fact]
    public void ASecondFade_MovesTheLayer()
    {
        var effects = new ScreenEffectService { Layer = ScreenEffectLayer.AboveUI };
        var below = Fade();
        below.Layer = ScreenEffectLayer.BelowUI;

        GameManager.StartScreenFade(effects, below);

        Assert.Equal(ScreenEffectLayer.BelowUI, effects.Layer);
    }
}
