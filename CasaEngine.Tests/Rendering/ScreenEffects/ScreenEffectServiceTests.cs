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
    public void Update_WithNoFadeInProgress_DoesNotAllocate()
    {
        var service = new ScreenEffectService();
        service.SetOverlay(1, 2, 3, SpriteBlendMode.Additive);

        // Warm up.
        for (var i = 0; i < 10; i++)
        {
            service.Update(0.016f);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            service.Update(0.016f);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
