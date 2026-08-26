using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using Xunit;

namespace CasaEngine.Tests.Audio;

public class AudioServiceStreamingTests
{
    private static AudioService CreateService(out FakeAudioBackend backend)
    {
        backend = new FakeAudioBackend();
        return new AudioService(backend);
    }

    [Fact]
    public void PlayStream_CreatesAStreamingVoiceThatIsNotStartedYet()
    {
        var service = CreateService(out var backend);

        var voice = service.PlayStream(22050, 2, AudioBusNames.Music, AudioVoiceParameters.Default);

        Assert.True(voice.IsValid);
        Assert.True(backend.IsStreamingVoice(voice));
        Assert.Equal(22050, backend.GetSampleRate(voice));
        Assert.Equal(2, backend.GetChannelCount(voice));
        Assert.False(service.IsPlaying(voice));
        Assert.Equal(1, service.ActiveVoiceCount);
    }

    [Fact]
    public void StartVoice_StartsTheStream()
    {
        var service = CreateService(out _);
        var voice = service.PlayStream(22050, 2, AudioBusNames.Music, AudioVoiceParameters.Default);

        service.StartVoice(voice);

        Assert.True(service.IsPlaying(voice));
    }

    [Fact]
    public void SubmitStreamBuffer_QueuesTheBytes()
    {
        var service = CreateService(out var backend);
        var voice = service.PlayStream(22050, 2, AudioBusNames.Music, AudioVoiceParameters.Default);
        var buffer = new byte[256];

        service.SubmitStreamBuffer(voice, buffer, 0, buffer.Length);
        service.SubmitStreamBuffer(voice, buffer, 0, 128);

        Assert.Equal(2, service.GetPendingBufferCount(voice));
        Assert.Equal(384, backend.GetSubmittedBytes(voice));
    }

    [Fact]
    public void PendingBufferCount_DropsAsTheHardwareConsumes()
    {
        var service = CreateService(out var backend);
        var voice = service.PlayStream(22050, 2, AudioBusNames.Music, AudioVoiceParameters.Default);
        service.SubmitStreamBuffer(voice, new byte[64], 0, 64);
        service.SubmitStreamBuffer(voice, new byte[64], 0, 64);

        backend.ConsumeBuffers(voice, 2);

        Assert.Equal(0, service.GetPendingBufferCount(voice));
    }

    [Fact]
    public void AStreamingVoice_IsNotRecycledWhenItGoesSilent()
    {
        var service = CreateService(out var backend);
        var voice = service.PlayStream(22050, 2, AudioBusNames.Music, AudioVoiceParameters.Default);
        service.StartVoice(voice);

        // Underrun: the backend reports Stopped, but only the feeder knows whether the stream
        // is over. A resident voice would be recycled here; a stream must not.
        backend.CompleteVoice(voice);
        service.Update(0.016f);

        Assert.True(service.IsAlive(voice));
        Assert.Equal(1, service.ActiveVoiceCount);
    }

    [Fact]
    public void AStreamingVoice_IsReleasedWhenItIsStoppedExplicitly()
    {
        var service = CreateService(out _);
        var voice = service.PlayStream(22050, 2, AudioBusNames.Music, AudioVoiceParameters.Default);

        service.Stop(voice);

        Assert.False(service.IsAlive(voice));
        Assert.Equal(0, service.ActiveVoiceCount);
    }

    [Fact]
    public void AStreamingVoice_GoesThroughItsBus()
    {
        var service = CreateService(out var backend);
        service.Mixer.GetBus(AudioBusNames.Music).Volume = 0.5f;

        var voice = service.PlayStream(
            22050,
            2,
            AudioBusNames.Music,
            new AudioVoiceParameters(0.8f, 0f, 0f, false));

        Assert.Equal(0.4f, backend.GetParameters(voice).Volume, 4);

        service.Mixer.GetBus(AudioBusNames.Music).Volume = 1f;
        service.Update(0.016f);

        Assert.Equal(0.8f, backend.GetParameters(voice).Volume, 4);
    }

    [Fact]
    public void AStreamingVoice_CanBeFadedOut()
    {
        var service = CreateService(out var backend);
        var voice = service.PlayStream(22050, 2, AudioBusNames.Music, AudioVoiceParameters.Default);
        service.StartVoice(voice);

        service.StopWithFade(voice, 1f);
        service.Update(0.5f);
        Assert.Equal(0.5f, backend.GetParameters(voice).Volume, 4);
        Assert.True(service.IsAlive(voice));

        service.Update(0.5f);
        Assert.False(service.IsAlive(voice));
    }

    [Fact]
    public void AStreamingVoice_IsScopedToItsOwner()
    {
        var service = CreateService(out _);
        var owner = new object();
        var voice = service.PlayStream(22050, 2, AudioBusNames.Music, AudioVoiceParameters.Default, owner);

        service.StopVoicesOwnedBy(owner);

        Assert.False(service.IsAlive(voice));
    }

    [Fact]
    public void PlayStream_IsRefusedWhenTheBackendCannotStream()
    {
        var backend = new FakeAudioBackend { SupportsStreaming = false };
        var service = new AudioService(backend);

        Assert.False(service.PlayStream(22050, 2, AudioBusNames.Music, AudioVoiceParameters.Default).IsValid);
    }

    [Fact]
    public void StreamOperations_OnAStaleHandle_AreIgnored()
    {
        var service = CreateService(out _);
        var voice = service.PlayStream(22050, 2, AudioBusNames.Music, AudioVoiceParameters.Default);
        service.Stop(voice);

        service.SubmitStreamBuffer(voice, new byte[64], 0, 64);
        service.StartVoice(voice);

        Assert.Equal(0, service.GetPendingBufferCount(voice));
        Assert.False(service.IsPlaying(voice));
    }

    [Fact]
    public void SubmitStreamBuffer_IsIgnoredOnAResidentVoice()
    {
        var service = CreateService(out var backend);
        var provider = new FakeAudioClipProvider();
        var asset = new SoundAsset { AudioFileAssetId = provider.Register(new FakeAudioClip()) };
        service.ClipProvider = provider;
        var voice = service.PlaySound(asset);

        service.SubmitStreamBuffer(voice, new byte[64], 0, 64);

        Assert.Equal(0, backend.GetSubmittedBytes(voice));
    }
}
