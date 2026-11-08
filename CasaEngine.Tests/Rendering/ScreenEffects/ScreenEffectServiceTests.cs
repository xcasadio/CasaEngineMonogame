using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScreenEffects;
using Xunit;

namespace CasaEngine.Tests.Rendering.ScreenEffects;

/// <summary>
/// Covers <see cref="ScreenEffectService"/>'s ramp semantics, mirroring
/// <c>CasaEngine.Tests.Audio.AudioServiceFadeTests</c> exactly: exact target on arrival, no
/// overshoot on a long frame, duration zero applies immediately, and a second fade started while one
/// is in progress starts smoothly from whatever "from" the caller passes (the service holds no
/// memory of its own beyond the last colour it was given - see the class doc).
/// </summary>
public class ScreenEffectServiceTests
{
    // A frame at a fixed step of 166667 ticks, the step a Game starts with.
    private const float FixedStepSeconds = 166667 / (float)TimeSpan.TicksPerSecond;

    /// <summary>
    /// The recipe of docs/engine/screen-effects.md: a duration of (255 / step) frames over the rate gives an exact
    /// step per frame when the step divides 255, and ends on its last frame.
    /// </summary>
    [Theory]
    [InlineData(15)]
    [InlineData(5)]
    [InlineData(17)]
    [InlineData(51)]
    public void AFadeInFrames_StepsExactly_WhenTheStepDivides255(int step)
    {
        int frames = 255 / step;
        var service = new ScreenEffectService();

        service.StartFade(0, 0, 0, 255, 255, 255, frames / 60f, SpriteBlendMode.Subtractive);

        for (int frame = 1; frame <= frames; frame++)
        {
            Assert.True(service.IsFading);
            service.Update(FixedStepSeconds);
            Assert.Equal(step * frame, service.R);
        }

        Assert.False(service.IsFading);
    }

    [Fact]
    public void AFadeInFrames_MissesItsMultiples_WhenTheStepDoesNotDivide255()
    {
        // 255 / 16 is not whole: 16 frames at 255 / 16 per frame.
        var service = new ScreenEffectService();
        var values = new List<int>();

        service.StartFade(0, 0, 0, 255, 255, 255, 16 / 60f, SpriteBlendMode.Subtractive);

        for (int frame = 1; frame <= 16; frame++)
        {
            service.Update(FixedStepSeconds);
            values.Add(service.R);
        }

        Assert.Equal(255, values[^1]);
        Assert.Contains(values, value => value % 16 != 0);
    }

    [Fact]
    public void Fade_ReachesExactlyTheTargetAtTheEndOfTheDuration()
    {
        var service = new ScreenEffectService();

        service.StartFade(200, 200, 200, 0, 0, 0, 1f, SpriteBlendMode.Subtractive);

        service.Update(0.5f);
        Assert.Equal(100, service.R);
        Assert.Equal(100, service.G);
        Assert.Equal(100, service.B);
        Assert.True(service.IsFading);

        service.Update(0.5f);
        Assert.Equal(0, service.R);
        Assert.Equal(0, service.G);
        Assert.Equal(0, service.B);
        Assert.False(service.IsFading);
    }

    [Fact]
    public void Fade_DoesNotOvershootWhenAFrameIsLong()
    {
        var service = new ScreenEffectService();

        // A mid-range target (not 0/255) so an uncapped progress would overshoot to a value the
        // byte clamp inside the ramp would still accept as "in range" (255) but which is NOT the
        // target (150) - unlike a 0/255 target, where the byte clamp would coincidentally mask an
        // uncapped-progress bug. This is what makes the mandatory mutation (progress not capped at 1)
        // actually bite.
        service.StartFade(100, 100, 100, 150, 150, 150, 0.5f, SpriteBlendMode.Additive);
        service.Update(10f);

        Assert.Equal(150, service.R);
        Assert.Equal(150, service.G);
        Assert.Equal(150, service.B);
        Assert.False(service.IsFading);
    }

    [Fact]
    public void FadeWithZeroDuration_AppliesTheTargetImmediately()
    {
        var service = new ScreenEffectService();

        service.StartFade(10, 20, 30, 40, 50, 60, 0f, SpriteBlendMode.Additive);

        Assert.Equal(40, service.R);
        Assert.Equal(50, service.G);
        Assert.Equal(60, service.B);
        Assert.False(service.IsFading);
        Assert.True(service.Active);
    }

    [Fact]
    public void ASecondFade_RestartsFromTheCurrentValueThePassedInFrom()
    {
        var service = new ScreenEffectService();

        service.StartFade(200, 200, 200, 0, 0, 0, 1f, SpriteBlendMode.Subtractive);
        service.Update(0.5f);
        Assert.Equal(100, service.R);

        // A fresh fade started mid-ramp, with "from" equal to the value just reached: no jump.
        service.StartFade(service.R, service.G, service.B, 255, 255, 255, 1f, SpriteBlendMode.Additive);
        Assert.Equal(100, service.R);

        service.Update(0.5f);
        Assert.Equal(178, service.R); // 100 + (255-100)*0.5 = 177.5 -> MidpointRounding.ToEven -> 178
    }

    [Fact]
    public void SetOverlay_AppliesImmediatelyAndCancelsAnyFadeInProgress()
    {
        var service = new ScreenEffectService();
        service.StartFade(0, 0, 0, 255, 255, 255, 10f, SpriteBlendMode.Additive);
        service.Update(0.1f);

        service.SetOverlay(10, 20, 30, SpriteBlendMode.Subtractive);

        Assert.Equal(10, service.R);
        Assert.Equal(20, service.G);
        Assert.Equal(30, service.B);
        Assert.Equal(SpriteBlendMode.Subtractive, service.Blend);
        Assert.True(service.Active);
        Assert.False(service.IsFading);

        // A stale Update must not resurrect the cancelled fade.
        service.Update(1f);
        Assert.Equal(10, service.R);
    }

    [Fact]
    public void Clear_DeactivatesAndCancelsAnyFadeInProgress()
    {
        var service = new ScreenEffectService();
        service.StartFade(0, 0, 0, 255, 255, 255, 1f, SpriteBlendMode.Additive);

        service.Clear();

        Assert.False(service.Active);
        Assert.False(service.IsFading);
    }

    [Fact]
    public void Layer_DefaultsToBelowUI()
    {
        var service = new ScreenEffectService();

        Assert.Equal(ScreenEffectLayer.BelowUI, service.Layer);
    }

    [Fact]
    public void Layer_CanBeSetToAboveUIAndBackToBelowUI()
    {
        var service = new ScreenEffectService();

        service.Layer = ScreenEffectLayer.AboveUI;
        Assert.Equal(ScreenEffectLayer.AboveUI, service.Layer);

        service.Layer = ScreenEffectLayer.BelowUI;
        Assert.Equal(ScreenEffectLayer.BelowUI, service.Layer);
    }

    [Fact]
    public void Clear_LeavesLayerIntactWhileResettingActiveAndFading()
    {
        var service = new ScreenEffectService();
        service.Layer = ScreenEffectLayer.AboveUI;
        service.StartFade(0, 0, 0, 255, 255, 255, 1f, SpriteBlendMode.Additive);

        service.Clear();

        Assert.Equal(ScreenEffectLayer.AboveUI, service.Layer);
        Assert.False(service.Active);
        Assert.False(service.IsFading);
    }

    [Fact]
    public void A_DefaultsTo255()
    {
        var service = new ScreenEffectService();

        Assert.Equal(255, service.A);
    }

    [Fact]
    public void SetOverlay_WithoutAlpha_LeavesAAt255()
    {
        var service = new ScreenEffectService();

        service.SetOverlay(10, 20, 30, SpriteBlendMode.Subtractive);

        Assert.Equal(255, service.A);
    }

    [Fact]
    public void SetOverlay_WithAlpha_AppliesIt()
    {
        var service = new ScreenEffectService();

        service.SetOverlay(10, 20, 30, 128, SpriteBlendMode.AlphaBlend);

        Assert.Equal(10, service.R);
        Assert.Equal(20, service.G);
        Assert.Equal(30, service.B);
        Assert.Equal(128, service.A);
        Assert.Equal(SpriteBlendMode.AlphaBlend, service.Blend);
    }

    [Fact]
    public void StartFade_WithoutAlpha_KeepsAAt255Throughout()
    {
        var service = new ScreenEffectService();

        service.StartFade(200, 200, 200, 0, 0, 0, 1f, SpriteBlendMode.Subtractive);
        Assert.Equal(255, service.A);

        service.Update(0.5f);
        Assert.Equal(255, service.A);

        service.Update(0.5f);
        Assert.Equal(255, service.A);
    }

    [Fact]
    public void StartFade_WithAlpha_InterpolatesAlphaLikeTheColourChannels()
    {
        var service = new ScreenEffectService();

        service.StartFade(0, 0, 0, 0, 0, 0, 0, 255, 1f, SpriteBlendMode.AlphaBlend);

        service.Update(0.5f);
        Assert.Equal(128, service.A);
        Assert.True(service.IsFading);

        service.Update(0.5f);
        Assert.Equal(255, service.A);
        Assert.False(service.IsFading);
    }

    [Fact]
    public void StartFadeWithAlpha_ZeroDuration_AppliesTargetAlphaImmediately()
    {
        var service = new ScreenEffectService();

        service.StartFade(10, 20, 30, 40, 50, 60, 70, 80, 0f, SpriteBlendMode.AlphaBlend);

        Assert.Equal(50, service.R);
        Assert.Equal(60, service.G);
        Assert.Equal(70, service.B);
        Assert.Equal(80, service.A);
        Assert.False(service.IsFading);
    }

    [Fact]
    public void Clear_LeavesAlphaIntactWhileResettingActiveAndFading()
    {
        var service = new ScreenEffectService();
        service.SetOverlay(1, 2, 3, 42, SpriteBlendMode.AlphaBlend);

        service.Clear();

        Assert.Equal(42, service.A);
        Assert.False(service.Active);
        Assert.False(service.IsFading);
    }

    [Fact]
    public void Update_WithNoFadeInProgress_DoesNotAllocate()
    {
        var service = new ScreenEffectService();
        service.SetOverlay(1, 2, 3, SpriteBlendMode.Additive);

        // Warm up.
        for (var i = 0; i < 10; i++)
        {
            service.Update(0.016f);
        }

        var before = AllocationWindow.Start();
        for (var i = 0; i < 1000; i++)
        {
            service.Update(0.016f);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static byte Expected(byte from, byte to, int k, int n)
    {
        return (byte)Math.Clamp(MathF.Round(from + ((to - from) * (k / (float)n))), 0f, 255f);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(10f)]
    public void StartFadeFrames_ReachesEachValueAndTheExactTarget_WhateverTheElapsedTime(float elapsed)
    {
        const int n = 4;
        var service = new ScreenEffectService();

        service.StartFadeFrames(0, 0, 0, 255, 0, 0, 0, 0, n, SpriteBlendMode.AlphaBlend);

        Assert.True(service.Active);
        Assert.True(service.IsFading);
        Assert.Equal(255, service.A);
        for (var k = 1; k <= n; k++)
        {
            service.Update(elapsed);
            Assert.Equal(Expected(255, 0, k, n), service.A);
            Assert.Equal(k < n, service.IsFading);
        }

        Assert.Equal(0, service.A);
        Assert.Equal(SpriteBlendMode.AlphaBlend, service.Blend);
    }

    [Fact]
    public void StartFadeFrames_InterpolatesEveryChannelLikeTheSecondsRamp()
    {
        const int n = 7;
        var service = new ScreenEffectService();

        service.StartFadeFrames(10, 200, 30, 5, 250, 20, 31, 255, n, SpriteBlendMode.Additive);

        for (var k = 1; k <= n; k++)
        {
            service.Update(0.016f);
            Assert.Equal(Expected(10, 250, k, n), service.R);
            Assert.Equal(Expected(200, 20, k, n), service.G);
            Assert.Equal(Expected(30, 31, k, n), service.B);
            Assert.Equal(Expected(5, 255, k, n), service.A);
        }
    }

    [Fact]
    public void StartFadeFrames_OfOneFrame_ReachesTheTargetOnTheFirstUpdate()
    {
        var service = new ScreenEffectService();

        service.StartFadeFrames(0, 0, 0, 255, 9, 8, 7, 6, 1, SpriteBlendMode.AlphaBlend);

        Assert.True(service.IsFading);
        Assert.Equal(255, service.A);
        service.Update(0f);

        Assert.False(service.IsFading);
        Assert.Equal((9, 8, 7, 6), (service.R, service.G, service.B, service.A));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void StartFadeFrames_OfZeroOrLess_AppliesTheTargetImmediately(int frames)
    {
        var service = new ScreenEffectService();

        service.StartFadeFrames(0, 0, 0, 255, 9, 8, 7, 6, frames, SpriteBlendMode.Additive);

        Assert.True(service.Active);
        Assert.False(service.IsFading);
        Assert.Equal((9, 8, 7, 6), (service.R, service.G, service.B, service.A));
    }

    [Fact]
    public void AFadeInSeconds_CancelsAFrameRamp_AndRunsOnTime()
    {
        var service = new ScreenEffectService();
        service.StartFadeFrames(0, 0, 0, 255, 255, 255, 255, 255, 4, SpriteBlendMode.AlphaBlend);
        service.Update(0f);

        service.StartFade(0, 0, 0, 100, 100, 100, 1f, SpriteBlendMode.Additive);
        service.Update(0f);
        Assert.Equal(0, service.R);

        service.Update(0.5f);
        Assert.Equal(50, service.R);
        service.Update(0.5f);
        Assert.Equal(100, service.R);
        Assert.False(service.IsFading);
    }

    [Fact]
    public void SetOverlay_CancelsAFrameRamp()
    {
        var service = new ScreenEffectService();
        service.StartFadeFrames(0, 0, 0, 255, 255, 255, 255, 255, 4, SpriteBlendMode.AlphaBlend);
        service.Update(0f);

        service.SetOverlay(1, 2, 3, SpriteBlendMode.Additive);
        service.Update(0f);

        Assert.False(service.IsFading);
        Assert.Equal((1, 2, 3, 255), (service.R, service.G, service.B, service.A));
    }

    [Fact]
    public void Clear_CancelsAFrameRamp()
    {
        var service = new ScreenEffectService();
        service.StartFadeFrames(0, 0, 0, 255, 255, 255, 255, 255, 4, SpriteBlendMode.AlphaBlend);
        service.Update(0f);

        service.Clear();
        var r = service.R;
        service.Update(0f);

        Assert.False(service.Active);
        Assert.False(service.IsFading);
        Assert.Equal(r, service.R);
    }

    [Fact]
    public void FrameRampUpdate_DoesNotAllocate()
    {
        var service = new ScreenEffectService();
        service.StartFadeFrames(0, 0, 0, 255, 255, 255, 255, 0, 100000, SpriteBlendMode.AlphaBlend);
        service.Update(0f);

        var before = AllocationWindow.Start();
        for (var i = 0; i < 1000; i++)
        {
            service.Update(0.016f);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
