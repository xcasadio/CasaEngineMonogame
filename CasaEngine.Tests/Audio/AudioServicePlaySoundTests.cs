using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using Xunit;

namespace CasaEngine.Tests.Audio;

public class AudioServicePlaySoundTests
{
    private static AudioService CreateService(
        out FakeAudioBackend backend,
        out FakeAudioClipProvider provider,
        int voiceCapacity = 8)
    {
        backend = new FakeAudioBackend(voiceCapacity);
        provider = new FakeAudioClipProvider();
        return new AudioService(backend) { ClipProvider = provider };
    }

    private static SoundAsset CreateAsset(FakeAudioClipProvider provider, string name = "click")
    {
        return new SoundAsset
        {
            Name = name,
            AudioFileAssetId = provider.Register(new FakeAudioClip(name)),
        };
    }

    [Fact]
    public void PlaySound_UsesTheAssetValues()
    {
        var service = CreateService(out var backend, out var provider);
        var asset = CreateAsset(provider);
        asset.Volume = 0.5f;
        asset.Pitch = 0.25f;
        asset.IsLooped = true;
        asset.BusName = AudioBusNames.Ui;

        var voice = service.PlaySound(asset);

        Assert.True(voice.IsValid);
        Assert.Equal(AudioBusNames.Ui, service.GetVoiceBus(voice));

        var applied = backend.GetParameters(voice);
        Assert.Equal(0.5f, applied.Volume, 4);
        Assert.Equal(0.25f, applied.Pitch, 4);
        Assert.True(applied.IsLooped);
    }

    [Fact]
    public void Overrides_WinOverTheAssetValues()
    {
        var service = CreateService(out var backend, out var provider);
        var asset = CreateAsset(provider);
        asset.Volume = 0.5f;
        asset.BusName = AudioBusNames.Sfx;

        var voice = service.PlaySound(
            asset,
            new SoundPlaybackOverrides(volume: 1f, pan: -1f, pitch: 0.5f, isLooped: true, busName: AudioBusNames.Voice));

        Assert.Equal(AudioBusNames.Voice, service.GetVoiceBus(voice));

        var applied = backend.GetParameters(voice);
        Assert.Equal(1f, applied.Volume, 4);
        Assert.Equal(-1f, applied.Pan, 4);
        Assert.Equal(0.5f, applied.Pitch, 4);
        Assert.True(applied.IsLooped);
    }

    [Fact]
    public void PartialOverrides_KeepTheAssetValuesForTheRest()
    {
        var service = CreateService(out var backend, out var provider);
        var asset = CreateAsset(provider);
        asset.Volume = 0.5f;
        asset.Pitch = 0.25f;

        var voice = service.PlaySound(asset, new SoundPlaybackOverrides(volume: 1f));

        var applied = backend.GetParameters(voice);
        Assert.Equal(1f, applied.Volume, 4);
        Assert.Equal(0.25f, applied.Pitch, 4);
    }

    [Fact]
    public void TheBusGainIsCombinedWithTheAssetVolume()
    {
        var service = CreateService(out var backend, out var provider);
        var asset = CreateAsset(provider);
        asset.Volume = 0.5f;
        service.Mixer.GetBus(AudioBusNames.Sfx).Volume = 0.5f;

        var voice = service.PlaySound(asset);

        Assert.Equal(0.25f, backend.GetParameters(voice).Volume, 4);
    }

    [Fact]
    public void AStreamingAsset_IsRefusedAsASoundEffect()
    {
        var service = CreateService(out _, out var provider);
        var asset = CreateAsset(provider);
        asset.IsStreaming = true;

        Assert.False(service.PlaySound(asset).IsValid);
    }

    [Fact]
    public void AnAssetWithoutAudioFile_IsSilentInsteadOfThrowing()
    {
        var service = CreateService(out _, out _);
        var asset = new SoundAsset { Name = "empty" };

        Assert.False(service.PlaySound(asset).IsValid);
        Assert.Equal(0, service.ActiveVoiceCount);
    }

    [Fact]
    public void AnUnresolvableAudioFile_IsSilentInsteadOfThrowing()
    {
        var service = CreateService(out _, out _);
        var asset = new SoundAsset { Name = "dangling", AudioFileAssetId = Guid.NewGuid() };

        Assert.False(service.PlaySound(asset).IsValid);
    }

    [Fact]
    public void WithoutAClipProvider_PlayingIsSilentInsteadOfThrowing()
    {
        var service = new AudioService(new FakeAudioBackend());
        var asset = new SoundAsset { Name = "orphan", AudioFileAssetId = Guid.NewGuid() };

        Assert.False(service.PlaySound(asset).IsValid);
    }

    [Fact]
    public void ADisposedClip_IsRefused()
    {
        var service = CreateService(out _, out var provider);
        var clip = new FakeAudioClip();
        var asset = new SoundAsset { AudioFileAssetId = provider.Register(clip) };
        clip.Dispose();

        Assert.False(service.PlaySound(asset).IsValid);
    }

    [Fact]
    public void PlaySound_ScopesTheVoiceToItsOwner()
    {
        var service = CreateService(out _, out var provider);
        var asset = CreateAsset(provider);
        var owner = new object();

        var voice = service.PlaySound(asset, owner);
        Assert.True(service.IsAlive(voice));

        service.StopVoicesOwnedBy(owner);
        Assert.False(service.IsAlive(voice));
    }

    [Fact]
    public void PlaySound_WithANullAsset_Throws()
    {
        var service = CreateService(out _, out _);

        // A null asset is a programming error, unlike a broken asset which must stay silent.
        Assert.Throws<ArgumentNullException>(() => service.PlaySound(null));
    }

    [Fact]
    public void ALoopingVoice_KeepsPlayingUntilItIsStopped()
    {
        var service = CreateService(out _, out var provider);
        var asset = CreateAsset(provider);
        asset.IsLooped = true;

        var voice = service.PlaySound(asset);
        for (var i = 0; i < 100; i++)
        {
            service.Update(0.016f);
        }

        Assert.True(service.IsPlaying(voice));

        service.Stop(voice);
        Assert.False(service.IsAlive(voice));
    }
}
