using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using Xunit;

namespace CasaEngine.Tests.Audio;

public class MusicPlayerTests
{
    // Big enough that the queue can hold several buffers: one 16 KB buffer is 4096 stereo
    // 16 bit samples, so a shorter file would end before the queue is full.
    private const int SampleCount = 65536;

    private static AudioService CreateService(out FakeAudioBackend backend, out FakeAudioClipProvider provider)
    {
        backend = new FakeAudioBackend();
        provider = new FakeAudioClipProvider();
        return new AudioService(backend) { ClipProvider = provider };
    }

    private static SoundAsset CreateStreamingAsset(
        FakeAudioClipProvider provider,
        string name = "theme",
        bool isLooped = false,
        int sampleCount = SampleCount)
    {
        var wav = WavBuilder.CreatePcm16(sampleRate: 22050, channelCount: 2, sampleCount: sampleCount, formatChunkSize: 18);

        return new SoundAsset
        {
            Name = name,
            AudioFileAssetId = provider.RegisterStream(wav),
            BusName = AudioBusNames.Music,
            IsStreaming = true,
            IsLooped = isLooped,
        };
    }

    [Fact]
    public void Play_StartsAStreamAndQueuesAhead()
    {
        var service = CreateService(out var backend, out var provider);
        var asset = CreateStreamingAsset(provider);

        var track = service.Music.Play(asset);

        Assert.True(track.IsValid);
        Assert.True(service.Music.IsPlaying(track));
        Assert.Equal(1, service.Music.ActiveTrackCount);
        Assert.Equal(1, backend.StreamingVoiceCount);

        // Buffers are queued before the voice starts, so it cannot starve on the first frame.
        Assert.Equal(3, service.Music.GetPendingBufferCount(track));
    }

    [Fact]
    public void Play_UsesTheFormatOfTheFile()
    {
        var service = CreateService(out var backend, out var provider);
        var asset = CreateStreamingAsset(provider);

        service.Music.Play(asset);

        var voice = new AudioVoiceHandle(0, 0);
        Assert.Equal(22050, backend.GetSampleRate(voice));
        Assert.Equal(2, backend.GetChannelCount(voice));
    }

    [Fact]
    public void Update_TopsTheQueueBackUpAsBuffersAreConsumed()
    {
        var service = CreateService(out var backend, out var provider);
        var track = service.Music.Play(CreateStreamingAsset(provider));
        var voice = new AudioVoiceHandle(0, 0);

        backend.ConsumeBuffers(voice, 2);
        Assert.Equal(1, service.Music.GetPendingBufferCount(track));

        service.Update(0.016f);

        Assert.Equal(3, service.Music.GetPendingBufferCount(track));
    }

    [Fact]
    public void ANonLoopingTrack_EndsOnceEverythingHasBeenPlayed()
    {
        var service = CreateService(out var backend, out var provider);
        var track = service.Music.Play(CreateStreamingAsset(provider, sampleCount: 2048));
        var voice = new AudioVoiceHandle(0, 0);

        for (var i = 0; i < 200 && service.Music.IsAlive(track); i++)
        {
            backend.ConsumeBuffers(voice, 3);
            service.Update(0.016f);
        }

        Assert.False(service.Music.IsAlive(track));
        Assert.Equal(0, service.Music.ActiveTrackCount);
    }

    [Fact]
    public void ALoopingTrack_RewindsInsteadOfEnding()
    {
        var service = CreateService(out var backend, out var provider);
        var track = service.Music.Play(CreateStreamingAsset(provider, isLooped: true, sampleCount: 1024));
        var voice = new AudioVoiceHandle(0, 0);

        var fileBytes = 1024 * 2 * 2;

        for (var i = 0; i < 50; i++)
        {
            backend.ConsumeBuffers(voice, 3);
            service.Update(0.016f);
        }

        Assert.True(service.Music.IsAlive(track));
        // More bytes went out than the file holds: it looped.
        Assert.True(backend.GetSubmittedBytes(voice) > fileBytes);
    }

    [Fact]
    public void Play_WithAFadeIn_StartsSilentAndRampsUp()
    {
        var service = CreateService(out var backend, out var provider);
        var track = service.Music.Play(CreateStreamingAsset(provider, isLooped: true), fadeInSeconds: 1f);
        var voice = new AudioVoiceHandle(0, 0);

        Assert.Equal(0f, backend.GetParameters(voice).Volume, 4);

        service.Update(0.5f);
        Assert.Equal(0.5f, backend.GetParameters(voice).Volume, 4);

        service.Update(0.5f);
        Assert.Equal(1f, backend.GetParameters(voice).Volume, 4);
        Assert.True(service.Music.IsAlive(track));
    }

    [Fact]
    public void Stop_WithAFadeOut_KeepsTheTrackUntilTheRampEnds()
    {
        var service = CreateService(out _, out var provider);
        var track = service.Music.Play(CreateStreamingAsset(provider, isLooped: true));

        service.Music.Stop(track, 1f);

        service.Update(0.5f);
        Assert.True(service.Music.IsAlive(track));

        service.Update(0.5f);
        Assert.False(service.Music.IsAlive(track));
        Assert.Equal(0, service.Music.ActiveTrackCount);
    }

    [Fact]
    public void Stop_WithoutFade_DropsTheTrackImmediately()
    {
        var service = CreateService(out _, out var provider);
        var track = service.Music.Play(CreateStreamingAsset(provider));

        service.Music.Stop(track);

        Assert.False(service.Music.IsAlive(track));
        Assert.Equal(0, service.Music.ActiveTrackCount);
    }

    [Fact]
    public void Crossfade_PlaysBothTracksDuringTheTransitionThenKeepsOnlyTheNewOne()
    {
        var service = CreateService(out _, out var provider);
        var first = service.Music.Play(CreateStreamingAsset(provider, "first", isLooped: true));

        var second = service.Music.Crossfade(first, CreateStreamingAsset(provider, "second", isLooped: true), 1f);

        Assert.True(second.IsValid);
        Assert.NotEqual(first, second);
        Assert.Equal(2, service.Music.ActiveTrackCount);

        service.Update(0.5f);
        Assert.Equal(2, service.Music.ActiveTrackCount);
        Assert.True(service.Music.GetVolume(first) > 0f);
        Assert.True(service.Music.GetVolume(second) > 0f);

        service.Update(0.5f);
        Assert.False(service.Music.IsAlive(first));
        Assert.True(service.Music.IsAlive(second));
        Assert.Equal(1, service.Music.ActiveTrackCount);
        Assert.Equal(1f, service.Music.GetVolume(second), 4);
    }

    [Fact]
    public void Crossfade_KeepsTheCurrentTrackWhenTheNewOneCannotStart()
    {
        var service = CreateService(out _, out var provider);
        var first = service.Music.Play(CreateStreamingAsset(provider, "first", isLooped: true));
        var broken = new SoundAsset { Name = "broken", IsStreaming = true, AudioFileAssetId = Guid.NewGuid() };

        var second = service.Music.Crossfade(first, broken, 1f);

        Assert.False(second.IsValid);
        Assert.True(service.Music.IsAlive(first));
    }

    [Fact]
    public void ANonStreamingAsset_IsRefused()
    {
        var service = CreateService(out _, out var provider);
        var asset = CreateStreamingAsset(provider);
        asset.IsStreaming = false;

        Assert.False(service.Music.Play(asset).IsValid);
    }

    [Fact]
    public void AnUnreadableFile_IsRefusedInsteadOfThrowing()
    {
        var service = CreateService(out _, out _);
        var asset = new SoundAsset { Name = "dangling", IsStreaming = true, AudioFileAssetId = Guid.NewGuid() };

        Assert.False(service.Music.Play(asset).IsValid);
        Assert.Equal(0, service.Music.ActiveTrackCount);
    }

    [Fact]
    public void AWavThatCannotBeStreamed_IsRefusedInsteadOfThrowing()
    {
        var service = CreateService(out _, out var provider);
        var eightBitWav = WavBuilder.Create(WavBuilder.PcmFormatTag, 22050, 1, 8, new byte[256]);
        var asset = new SoundAsset
        {
            Name = "8bit",
            IsStreaming = true,
            AudioFileAssetId = provider.RegisterStream(eightBitWav),
        };

        Assert.False(service.Music.Play(asset).IsValid);
    }

    [Fact]
    public void TheMusicBusScalesTheTrack()
    {
        var service = CreateService(out var backend, out var provider);
        service.Mixer.GetBus(AudioBusNames.Music).Volume = 0.25f;

        service.Music.Play(CreateStreamingAsset(provider, isLooped: true));

        Assert.Equal(0.25f, backend.GetParameters(new AudioVoiceHandle(0, 0)).Volume, 4);
    }

    [Fact]
    public void PauseAndResume_MoveTheTrackState()
    {
        var service = CreateService(out _, out var provider);
        var track = service.Music.Play(CreateStreamingAsset(provider, isLooped: true));

        service.Music.Pause(track);
        Assert.False(service.Music.IsPlaying(track));

        service.Music.Resume(track);
        Assert.True(service.Music.IsPlaying(track));
    }

    [Fact]
    public void StopAll_DropsEveryTrack()
    {
        var service = CreateService(out _, out var provider);
        service.Music.Play(CreateStreamingAsset(provider, "a", isLooped: true));
        service.Music.Play(CreateStreamingAsset(provider, "b", isLooped: true));

        service.Music.StopAll();

        Assert.Equal(0, service.Music.ActiveTrackCount);
    }

    [Fact]
    public void GetPosition_FollowsTheDecoder()
    {
        var service = CreateService(out _, out var provider);
        var track = service.Music.Play(CreateStreamingAsset(provider, isLooped: true));

        // Three 16 KB buffers were queued at 88200 bytes per second.
        Assert.True(service.Music.GetPosition(track) > TimeSpan.Zero);
    }

    [Fact]
    public void OperationsOnAStaleTrackHandle_AreIgnored()
    {
        var service = CreateService(out _, out var provider);
        var track = service.Music.Play(CreateStreamingAsset(provider));
        service.Music.Stop(track);

        service.Music.Stop(track);
        service.Music.Pause(track);
        service.Music.Resume(track);
        service.Music.FadeVolume(track, 0.5f, 1f);

        Assert.False(service.Music.IsAlive(track));
        Assert.Equal(0f, service.Music.GetVolume(track));
        Assert.Equal(TimeSpan.Zero, service.Music.GetPosition(track));
    }

    [Fact]
    public void Update_DoesNotAllocateWhileStreaming()
    {
        var service = CreateService(out var backend, out var provider);
        service.Music.Play(CreateStreamingAsset(provider, isLooped: true, sampleCount: 65536));
        var voice = new AudioVoiceHandle(0, 0);

        for (var i = 0; i < 10; i++)
        {
            backend.ConsumeBuffers(voice, 2);
            service.Update(0.016f);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 200; i++)
        {
            backend.ConsumeBuffers(voice, 2);
            service.Update(0.016f);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
