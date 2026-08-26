using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using Xunit;

namespace CasaEngine.Tests.Audio;

public class AudioServiceVoiceTests
{
    private static AudioService CreateService(out FakeAudioBackend backend, int voiceCapacity = 8)
    {
        backend = new FakeAudioBackend(voiceCapacity);
        return new AudioService(backend);
    }

    [Fact]
    public void PlayClip_TracksTheVoice()
    {
        var service = CreateService(out _);

        var voice = service.PlayClip(new FakeAudioClip(), AudioBusNames.Sfx, AudioVoiceParameters.Default);

        Assert.True(voice.IsValid);
        Assert.True(service.IsPlaying(voice));
        Assert.Equal(1, service.ActiveVoiceCount);
        Assert.Equal(AudioBusNames.Sfx, service.GetVoiceBus(voice));
    }

    [Fact]
    public void PlayClip_AppliesTheBusGainOnTopOfTheVoiceVolume()
    {
        var service = CreateService(out var backend);
        service.Mixer.GetBus(AudioBusNames.Sfx).Volume = 0.5f;

        var voice = service.PlayClip(
            new FakeAudioClip(),
            AudioBusNames.Sfx,
            new AudioVoiceParameters(0.5f, 0f, 0f, false));

        // The backend receives the product, the service keeps what the caller asked for.
        Assert.Equal(0.25f, backend.GetParameters(voice).Volume, 4);
        Assert.Equal(0.5f, service.GetVoiceVolume(voice), 4);
    }

    [Fact]
    public void ChangingABusVolume_ReappliesTheGainToItsLiveVoicesOnly()
    {
        var service = CreateService(out var backend);
        var sfxVoice = service.PlayClip(new FakeAudioClip(), AudioBusNames.Sfx, AudioVoiceParameters.Default);
        var musicVoice = service.PlayClip(new FakeAudioClip(), AudioBusNames.Music, AudioVoiceParameters.Default);

        service.Mixer.GetBus(AudioBusNames.Sfx).Volume = 0.25f;
        service.Update(0.016f);

        Assert.Equal(0.25f, backend.GetParameters(sfxVoice).Volume, 4);
        Assert.Equal(1f, backend.GetParameters(musicVoice).Volume, 4);
    }

    [Fact]
    public void MutingTheMasterBus_SilencesEveryLiveVoice()
    {
        var service = CreateService(out var backend);
        var voice = service.PlayClip(new FakeAudioClip(), AudioBusNames.Sfx, AudioVoiceParameters.Default);

        service.Mixer.GetBus(AudioBusNames.Master).IsMuted = true;
        service.Update(0.016f);

        Assert.Equal(0f, backend.GetParameters(voice).Volume, 4);
    }

    [Fact]
    public void Update_RecyclesTheVoicesThatFinished()
    {
        var service = CreateService(out var backend);
        var voice = service.PlayClip(new FakeAudioClip(), AudioBusNames.Sfx, AudioVoiceParameters.Default);

        backend.CompleteVoice(voice);
        service.Update(0.016f);

        Assert.Equal(0, service.ActiveVoiceCount);
        Assert.False(service.IsAlive(voice));
        Assert.Equal(0, backend.ActiveVoiceCount);
    }

    [Fact]
    public void Update_DoesNotAllocateOnASteadyState()
    {
        var service = CreateService(out _);
        service.PlayClip(new FakeAudioClip(), AudioBusNames.Sfx, AudioVoiceParameters.Default);
        service.Update(0.016f);

        // Warm up first: the very first calls JIT the code paths.
        for (var i = 0; i < 10; i++)
        {
            service.Update(0.016f);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            service.Update(0.016f);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Stop_ReleasesTheVoiceImmediately()
    {
        var service = CreateService(out var backend);
        var voice = service.PlayClip(new FakeAudioClip(), AudioBusNames.Sfx, AudioVoiceParameters.Default);

        service.Stop(voice);

        Assert.False(service.IsAlive(voice));
        Assert.Equal(0, service.ActiveVoiceCount);
        Assert.Equal(0, backend.ActiveVoiceCount);
    }

    [Fact]
    public void PlayClip_IsRefusedWhenTheBackendIsSaturated()
    {
        var service = CreateService(out _, voiceCapacity: 2);
        var clip = new FakeAudioClip();

        service.PlayClip(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default);
        service.PlayClip(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default);
        var refused = service.PlayClip(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default);

        Assert.False(refused.IsValid);
        Assert.Equal(1, service.RefusedVoiceCount);
        Assert.Equal(2, service.ActiveVoiceCount);
    }

    [Fact]
    public void SaturationDoesNotThrow_AndTheServiceKeepsWorkingAfterAVoiceIsFreed()
    {
        var service = CreateService(out var backend, voiceCapacity: 1);
        var clip = new FakeAudioClip();
        var first = service.PlayClip(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default);

        Assert.False(service.PlayClip(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default).IsValid);

        backend.CompleteVoice(first);
        service.Update(0.016f);

        Assert.True(service.PlayClip(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default).IsValid);
    }

    [Fact]
    public void StopAll_ClearsEveryVoice()
    {
        var service = CreateService(out var backend);
        var clip = new FakeAudioClip();
        service.PlayClip(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default);
        service.PlayClip(clip, AudioBusNames.Music, AudioVoiceParameters.Default);

        service.StopAll();

        Assert.Equal(0, service.ActiveVoiceCount);
        Assert.Equal(0, backend.ActiveVoiceCount);
    }

    [Fact]
    public void StaleHandleOperations_AreIgnored()
    {
        var service = CreateService(out _);
        var voice = service.PlayClip(new FakeAudioClip(), AudioBusNames.Sfx, AudioVoiceParameters.Default);
        service.Stop(voice);

        service.Stop(voice);
        service.Pause(voice);
        service.Resume(voice);
        service.SetVoiceVolume(voice, 0.5f);

        Assert.False(service.IsPlaying(voice));
        Assert.Equal(0f, service.GetVoiceVolume(voice));
        Assert.Null(service.GetVoiceBus(voice));
    }

    [Fact]
    public void PlayClip_OnAnUnknownBus_FallsBackToTheMasterGain()
    {
        var service = CreateService(out var backend);
        service.Mixer.GetBus(AudioBusNames.Master).Volume = 0.5f;

        var voice = service.PlayClip(new FakeAudioClip(), "NotABus", AudioVoiceParameters.Default);

        Assert.True(voice.IsValid);
        Assert.Equal(0.5f, backend.GetParameters(voice).Volume, 4);
    }
}
