using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using Xunit;

namespace CasaEngine.Tests.Audio;

public class AudioServiceFadeTests
{
    private static AudioService CreateService(out FakeAudioBackend backend)
    {
        backend = new FakeAudioBackend();
        return new AudioService(backend);
    }

    private static AudioVoiceHandle PlayAtFullVolume(AudioService service)
    {
        return service.PlayClip(new FakeAudioClip(), AudioBusNames.Sfx, AudioVoiceParameters.Default);
    }

    [Fact]
    public void Fade_ReachesExactlyTheTargetAtTheEndOfTheDuration()
    {
        var service = CreateService(out var backend);
        var voice = PlayAtFullVolume(service);

        service.FadeVoice(voice, 0.2f, 1f);

        service.Update(0.5f);
        Assert.Equal(0.6f, backend.GetParameters(voice).Volume, 4);

        service.Update(0.5f);
        Assert.Equal(0.2f, backend.GetParameters(voice).Volume, 4);
        Assert.False(service.IsFading(voice));
    }

    [Fact]
    public void Fade_DoesNotOvershootWhenAFrameIsLong()
    {
        var service = CreateService(out var backend);
        var voice = PlayAtFullVolume(service);

        service.FadeVoice(voice, 0f, 0.5f);
        service.Update(10f);

        Assert.Equal(0f, backend.GetParameters(voice).Volume, 4);
    }

    [Fact]
    public void FadeWithZeroDuration_AppliesTheTargetImmediately()
    {
        var service = CreateService(out var backend);
        var voice = PlayAtFullVolume(service);

        service.FadeVoice(voice, 0.3f, 0f);

        Assert.Equal(0.3f, backend.GetParameters(voice).Volume, 4);
        Assert.False(service.IsFading(voice));
    }

    [Fact]
    public void StopWithFade_ReleasesTheVoiceOnceSilent()
    {
        var service = CreateService(out var backend);
        var voice = PlayAtFullVolume(service);

        service.StopWithFade(voice, 1f);

        service.Update(0.5f);
        Assert.True(service.IsAlive(voice));
        Assert.Equal(0.5f, backend.GetParameters(voice).Volume, 4);

        service.Update(0.5f);
        Assert.False(service.IsAlive(voice));
        Assert.Equal(0, service.ActiveVoiceCount);
        Assert.Equal(0, backend.ActiveVoiceCount);
    }

    [Fact]
    public void StopWithFade_WithZeroDuration_ReleasesImmediately()
    {
        var service = CreateService(out _);
        var voice = PlayAtFullVolume(service);

        service.StopWithFade(voice, 0f);

        Assert.False(service.IsAlive(voice));
    }

    [Fact]
    public void ASecondFade_RestartsFromTheCurrentVolume()
    {
        var service = CreateService(out var backend);
        var voice = PlayAtFullVolume(service);

        service.FadeVoice(voice, 0f, 1f);
        service.Update(0.5f);
        Assert.Equal(0.5f, backend.GetParameters(voice).Volume, 4);

        // Fade back up: it must start at 0.5, not jump back to 1.
        service.FadeVoice(voice, 1f, 1f);
        service.Update(0.5f);

        Assert.Equal(0.75f, backend.GetParameters(voice).Volume, 4);
    }

    [Fact]
    public void CancelFade_LeavesTheVoiceAtTheVolumeReachedSoFar()
    {
        var service = CreateService(out var backend);
        var voice = PlayAtFullVolume(service);

        service.FadeVoice(voice, 0f, 1f);
        service.Update(0.25f);
        service.CancelFade(voice);
        service.Update(1f);

        Assert.False(service.IsFading(voice));
        Assert.True(service.IsAlive(voice));
        Assert.Equal(0.75f, backend.GetParameters(voice).Volume, 4);
    }

    [Fact]
    public void Fade_CombinesWithTheBusGain()
    {
        var service = CreateService(out var backend);
        service.Mixer.GetBus(AudioBusNames.Sfx).Volume = 0.5f;
        var voice = PlayAtFullVolume(service);

        service.FadeVoice(voice, 0.5f, 1f);
        service.Update(1f);

        // 0.5 asked for by the fade, halved by the bus.
        Assert.Equal(0.25f, backend.GetParameters(voice).Volume, 4);
        Assert.Equal(0.5f, service.GetVoiceVolume(voice), 4);
    }

    [Fact]
    public void Fade_OnAStaleHandle_IsIgnored()
    {
        var service = CreateService(out _);
        var voice = PlayAtFullVolume(service);
        service.Stop(voice);

        service.FadeVoice(voice, 0f, 1f);
        service.StopWithFade(voice, 1f);
        service.CancelFade(voice);

        Assert.False(service.IsFading(voice));
    }

    [Fact]
    public void FadingVoices_DoNotAllocateDuringUpdate()
    {
        var service = CreateService(out _);
        var voice = PlayAtFullVolume(service);
        service.FadeVoice(voice, 0f, 1_000_000f);

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
