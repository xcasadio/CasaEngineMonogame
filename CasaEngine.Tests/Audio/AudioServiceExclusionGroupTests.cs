using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using Xunit;
using Vector3 = System.Numerics.Vector3;

namespace CasaEngine.Tests.Audio;

/// <summary>Exclusion groups of <see cref="SoundAsset"/> (ADR-0083), on the per-frame fade of the fake backend.</summary>
public class AudioServiceExclusionGroupTests
{
    private const string Group = "group";

    private static AudioService CreateService(
        out FakeAudioBackend backend,
        out FakeAudioClipProvider provider,
        int voiceCapacity = 8)
    {
        backend = new FakeAudioBackend(voiceCapacity);
        provider = new FakeAudioClipProvider();
        return new AudioService(backend) { ClipProvider = provider };
    }

    private static SoundAsset CreateAsset(FakeAudioClipProvider provider, string group = Group, float fadeSeconds = 0f)
    {
        return new SoundAsset
        {
            AudioFileAssetId = provider.Register(new FakeAudioClip("sound")),
            ExclusionGroup = group,
            ExclusionFadeSeconds = fadeSeconds,
        };
    }

    [Fact]
    public void ASoundStopsTheVoiceOfAnotherAssetOfItsGroup()
    {
        var service = CreateService(out _, out var provider);
        var first = service.PlaySound(CreateAsset(provider));

        var second = service.PlaySound(CreateAsset(provider));

        Assert.False(service.IsAlive(first));
        Assert.True(service.IsPlaying(second));
        Assert.Equal(1, service.ActiveVoiceCount);
        Assert.Equal(0, service.StolenVoiceCount);
    }

    [Fact]
    public void ASoundStopsItsOwnEarlierVoice()
    {
        var service = CreateService(out _, out var provider);
        var asset = CreateAsset(provider);
        var first = service.PlaySound(asset);

        var second = service.PlaySound(asset);

        Assert.False(service.IsAlive(first));
        Assert.True(service.IsPlaying(second));
    }

    [Fact]
    public void AGroupIgnoresTheCase()
    {
        var service = CreateService(out _, out var provider);
        var first = service.PlaySound(CreateAsset(provider, "Group"));

        service.PlaySound(CreateAsset(provider, "gROUP"));

        Assert.False(service.IsAlive(first));
    }

    [Fact]
    public void DifferentGroupsOverlap()
    {
        var service = CreateService(out _, out var provider);
        var first = service.PlaySound(CreateAsset(provider, "one"));

        var second = service.PlaySound(CreateAsset(provider, "two"));

        Assert.True(service.IsPlaying(first));
        Assert.True(service.IsPlaying(second));
    }

    [Fact]
    public void WithoutAGroup_SoundsOverlap()
    {
        var service = CreateService(out _, out var provider);
        var asset = CreateAsset(provider, string.Empty);
        var first = service.PlaySound(asset);

        var second = service.PlaySound(asset);

        Assert.True(service.IsPlaying(first));
        Assert.True(service.IsPlaying(second));
    }

    [Fact]
    public void ASoundWithoutAGroupStopsNothing()
    {
        var service = CreateService(out _, out var provider);
        var grouped = service.PlaySound(CreateAsset(provider));

        service.PlaySound(CreateAsset(provider, string.Empty));

        Assert.True(service.IsPlaying(grouped));
    }

    [Fact]
    public void AMissingClipStopsNothing()
    {
        var service = CreateService(out _, out var provider);
        var voice = service.PlaySound(CreateAsset(provider));
        var broken = new SoundAsset { AudioFileAssetId = Guid.NewGuid(), ExclusionGroup = Group };

        var refused = service.PlaySound(broken);

        Assert.False(refused.IsValid);
        Assert.True(service.IsPlaying(voice));
    }

    [Fact]
    public void AStreamedSoundStopsNothing()
    {
        var service = CreateService(out _, out var provider);
        var voice = service.PlaySound(CreateAsset(provider));
        var streamed = CreateAsset(provider);
        streamed.IsStreaming = true;

        service.PlaySound(streamed);

        Assert.True(service.IsPlaying(voice));
    }

    [Fact]
    public void AFade_KeepsTheOldVoiceUntilItEnds()
    {
        var service = CreateService(out _, out var provider);
        var first = service.PlaySound(CreateAsset(provider));

        var second = service.PlaySound(CreateAsset(provider, fadeSeconds: 0.5f));

        Assert.True(service.IsFading(first));
        Assert.True(service.IsPlaying(second));
        Assert.Equal(2, service.ActiveVoiceCount);

        service.Update(0.6f);

        Assert.False(service.IsAlive(first));
        Assert.True(service.IsPlaying(second));
    }

    [Fact]
    public void AVoiceFadingToAStop_IsLeftAlone()
    {
        var service = CreateService(out _, out var provider);
        var first = service.PlaySound(CreateAsset(provider));
        service.StopWithFade(first, 1f);
        service.Update(0.25f);
        var volumeBefore = service.GetVoiceVolume(first);

        service.PlaySound(CreateAsset(provider));

        Assert.True(service.IsFading(first));
        Assert.Equal(volumeBefore, service.GetVoiceVolume(first));
    }

    [Fact]
    public void AVoiceUnderAnOrdinaryFade_Stops()
    {
        var service = CreateService(out _, out var provider);
        var first = service.PlaySound(CreateAsset(provider));
        service.FadeVoice(first, 0.5f, 1f);

        service.PlaySound(CreateAsset(provider));

        Assert.False(service.IsAlive(first));
    }

    [Fact]
    public void AVoiceWhoseStopFadeWasCancelled_Stops()
    {
        var service = CreateService(out _, out var provider);
        var first = service.PlaySound(CreateAsset(provider));
        service.StopWithFade(first, 1f);
        service.CancelFade(first);

        service.PlaySound(CreateAsset(provider));

        Assert.False(service.IsAlive(first));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    public void APausedVoice_StopsAtOnce(float fadeSeconds)
    {
        var service = CreateService(out _, out var provider);
        var first = service.PlaySound(CreateAsset(provider));
        service.Pause(first);

        service.PlaySound(CreateAsset(provider, fadeSeconds: fadeSeconds));

        Assert.False(service.IsAlive(first));
    }

    [Fact]
    public void AVoicePausedByTheSystem_DoesNotComeBack()
    {
        var service = CreateService(out _, out var provider);
        var first = service.PlaySound(CreateAsset(provider));
        service.PauseAllExceptBus(AudioBusNames.Editor);

        var second = service.PlaySound(CreateAsset(provider));
        service.ResumeAllExceptBus(AudioBusNames.Editor);

        Assert.False(service.IsAlive(first));
        Assert.True(service.IsPlaying(second));
        Assert.Equal(1, service.ActiveVoiceCount);
    }

    [Fact]
    public void AFinishedVoice_IsReleasedWithoutAFade()
    {
        var service = CreateService(out var backend, out var provider);
        var first = service.PlaySound(CreateAsset(provider));
        backend.CompleteVoice(first);

        service.PlaySound(CreateAsset(provider, fadeSeconds: 0.5f));

        Assert.False(service.IsAlive(first));
        Assert.Equal(1, service.ActiveVoiceCount);
    }

    [Fact]
    public void OnAFullBackend_TheStopIsImmediateAndTheNewVoicePlays()
    {
        var service = CreateService(out _, out var provider, voiceCapacity: 1);
        var first = service.PlaySound(CreateAsset(provider));

        var second = service.PlaySound(CreateAsset(provider, fadeSeconds: 0.5f));

        Assert.False(service.IsAlive(first));
        Assert.True(service.IsPlaying(second));
        Assert.Equal(0, service.RefusedVoiceCount);
    }

    [Fact]
    public void OnAFullBackend_AVoiceFadingToAStopStopsToo()
    {
        var service = CreateService(out _, out var provider, voiceCapacity: 1);
        var first = service.PlaySound(CreateAsset(provider));
        service.StopWithFade(first, 1f);

        var second = service.PlaySound(CreateAsset(provider));

        Assert.False(service.IsAlive(first));
        Assert.True(service.IsPlaying(second));
        Assert.Equal(0, service.RefusedVoiceCount);
    }

    [Fact]
    public void ASoundOnAMutedBus_StillStopsItsGroup()
    {
        var service = CreateService(out _, out var provider);
        var first = service.PlaySound(CreateAsset(provider));
        service.Mixer.GetBus(AudioBusNames.Ui).IsMuted = true;

        service.PlaySound(CreateAsset(provider), new SoundPlaybackOverrides(busName: AudioBusNames.Ui));

        Assert.False(service.IsAlive(first));
    }

    [Fact]
    public void APriorityDoesNotProtectAVoice()
    {
        var service = CreateService(out _, out var provider);
        var protectedAsset = CreateAsset(provider);
        protectedAsset.Priority = SoundAsset.MaxPriority;
        var first = service.PlaySound(protectedAsset);

        service.PlaySound(CreateAsset(provider));

        Assert.False(service.IsAlive(first));
    }

    [Fact]
    public void AnOwnerDoesNotProtectAVoice()
    {
        var service = CreateService(out _, out var provider);
        var first = service.PlaySound(CreateAsset(provider), new object());

        service.PlaySound(CreateAsset(provider));

        Assert.False(service.IsAlive(first));
    }

    [Fact]
    public void PlaySoundAt_FollowsTheSameRule()
    {
        var service = CreateService(out _, out var provider);
        var first = service.PlaySound(CreateAsset(provider));

        var second = service.PlaySoundAt(CreateAsset(provider), Vector3.Zero, SoundPlaybackOverrides.None);

        Assert.False(service.IsAlive(first));
        Assert.True(service.IsPlaying(second));
    }

    [Fact]
    public void AVoiceFromPlayClip_HasNoGroup()
    {
        var service = CreateService(out _, out var provider);
        var clip = service.PlayClip(new FakeAudioClip("clip"), AudioBusNames.Sfx, AudioVoiceParameters.Default);

        service.PlaySound(CreateAsset(provider));

        Assert.True(service.IsPlaying(clip));
    }

    [Fact]
    public void APreviewOnTheEditorBus_StopsNoGameVoice()
    {
        var service = CreateService(out _, out var provider);
        var game = service.PlaySound(CreateAsset(provider));

        var preview = service.PlaySound(CreateAsset(provider), new SoundPlaybackOverrides(busName: AudioBusNames.Editor));

        Assert.True(service.IsPlaying(game));
        Assert.True(service.IsPlaying(preview));
    }

    [Fact]
    public void AGameSound_StopsNoPreviewOnTheEditorBus()
    {
        var service = CreateService(out _, out var provider);
        var preview = service.PlaySound(CreateAsset(provider), new SoundPlaybackOverrides(busName: AudioBusNames.Editor));

        var game = service.PlaySound(CreateAsset(provider));

        Assert.True(service.IsPlaying(preview));
        Assert.True(service.IsPlaying(game));
    }
}
