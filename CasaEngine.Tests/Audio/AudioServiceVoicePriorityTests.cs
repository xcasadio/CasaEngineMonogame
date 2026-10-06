using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using Xunit;

namespace CasaEngine.Tests.Audio;

/// <summary>
/// Voice stealing by priority (plan T8.4, decisions P28 and P29): a full reservoir still refuses a sound by default,
/// and only a <see cref="SoundAsset.Priority"/> strictly above the lowest priority of the reservoir takes a voice.
/// </summary>
public class AudioServiceVoicePriorityTests
{
    private const int Capacity = 4;

    private static AudioService CreateService(
        out FakeAudioBackend backend,
        out FakeAudioClipProvider provider,
        int voiceCapacity = Capacity)
    {
        backend = new FakeAudioBackend(voiceCapacity);
        provider = new FakeAudioClipProvider();
        return new AudioService(backend) { ClipProvider = provider };
    }

    private static SoundAsset CreateAsset(FakeAudioClipProvider provider, int priority, string name = "fx")
    {
        return new SoundAsset
        {
            Name = name,
            AudioFileAssetId = provider.Register(new FakeAudioClip(name)),
            Priority = priority,
        };
    }

    private static FakeAudioClip StereoSource() => new(sampleRate: 24000, monoSamples: new short[] { 1000 });

    private static AudioVoiceHandle[] FillWith(AudioService service, FakeAudioClipProvider provider, params int[] priorities)
    {
        var handles = new AudioVoiceHandle[priorities.Length];
        for (var i = 0; i < priorities.Length; i++)
        {
            handles[i] = service.PlaySound(CreateAsset(provider, priorities[i], "v" + i));
            Assert.True(handles[i].IsValid);
        }

        return handles;
    }

    [Fact]
    public void AReservoirWithoutPriority_RefusesASoundWithoutPriority()
    {
        var service = CreateService(out var backend, out var provider);
        FillWith(service, provider, 0, 0, 0, 0);

        var refused = service.PlaySound(CreateAsset(provider, 0));

        Assert.False(refused.IsValid);
        Assert.Equal(1, service.RefusedVoiceCount);
        Assert.Equal(0, service.StolenVoiceCount);
        Assert.Equal(Capacity, service.ActiveVoiceCount);
    }

    [Fact]
    public void AHigherPriority_StealsTheLowestPriorityVoice()
    {
        var service = CreateService(out var backend, out var provider);
        var voices = FillWith(service, provider, 1, 2, 2, 3);

        var stealer = service.PlaySound(CreateAsset(provider, 5, "boss"));

        Assert.True(stealer.IsValid);
        Assert.False(service.IsAlive(voices[0]));
        Assert.True(service.IsAlive(voices[1]));
        Assert.True(service.IsAlive(voices[2]));
        Assert.True(service.IsAlive(voices[3]));
        Assert.True(service.IsAlive(stealer));
        Assert.Equal(1, service.StolenVoiceCount);
        Assert.Equal(0, service.RefusedVoiceCount);
        Assert.Equal(Capacity, service.ActiveVoiceCount);
        Assert.Equal(Capacity, backend.ActiveVoiceCount);
    }

    [Fact]
    public void TheOldestOfTheLowestPriority_IsTheVictim()
    {
        var service = CreateService(out _, out var provider);
        var voices = FillWith(service, provider, 3, 2, 2, 4);

        var first = service.PlaySound(CreateAsset(provider, 9));
        Assert.True(first.IsValid);
        Assert.False(service.IsAlive(voices[1]));
        Assert.True(service.IsAlive(voices[2]));

        var second = service.PlaySound(CreateAsset(provider, 9));
        Assert.True(second.IsValid);
        Assert.False(service.IsAlive(voices[2]));
        Assert.True(service.IsAlive(voices[0]));
        Assert.True(service.IsAlive(voices[3]));
        Assert.Equal(2, service.StolenVoiceCount);
    }

    [Fact]
    public void AnEqualPriority_IsRefused()
    {
        var service = CreateService(out _, out var provider);
        var voices = FillWith(service, provider, 3, 3, 3, 3);

        var refused = service.PlaySound(CreateAsset(provider, 3));

        Assert.False(refused.IsValid);
        Assert.Equal(1, service.RefusedVoiceCount);
        Assert.Equal(0, service.StolenVoiceCount);
        foreach (var voice in voices)
        {
            Assert.True(service.IsAlive(voice));
        }
    }

    [Fact]
    public void AVoiceWithoutPriority_IsNeverStolen_EvenForTheMaximumPriority()
    {
        var service = CreateService(out _, out var provider);
        var voices = FillWith(service, provider, 0, 0, 0, 0);

        var refused = service.PlaySound(CreateAsset(provider, SoundAsset.MaxPriority));

        Assert.False(refused.IsValid);
        Assert.Equal(1, service.RefusedVoiceCount);
        Assert.Equal(0, service.StolenVoiceCount);
        foreach (var voice in voices)
        {
            Assert.True(service.IsAlive(voice));
        }
    }

    [Fact]
    public void AStreamedVoice_AMusicTrackAndAStereoFallbackVoice_AreNeverVictims()
    {
        var service = CreateService(out var backend, out var provider);
        var parameters = AudioVoiceParameters.Default;

        var stream = service.PlayStream(22050, 2, AudioBusNames.Sfx, parameters);
        var track = service.Music.Play(CreateMusicAsset(provider));
        var stereo = service.PlayClipStereo(StereoSource(), AudioBusNames.Sfx, parameters, 1f, 1f);
        var priorityVoice = service.PlaySound(CreateAsset(provider, 1));
        Assert.True(stream.IsValid && track.IsValid && stereo.IsValid && priorityVoice.IsValid);
        Assert.Equal(Capacity, backend.ActiveVoiceCount);

        // Only the priority-1 voice is eligible.
        var stealer = service.PlaySound(CreateAsset(provider, 50));
        Assert.True(stealer.IsValid);
        Assert.False(service.IsAlive(priorityVoice));
        Assert.True(service.IsAlive(stream));
        Assert.True(service.IsAlive(stereo));
        Assert.True(service.Music.IsPlaying(track));

        // Nothing left but the protected voices and a voice of the same priority: refused.
        var refused = service.PlaySound(CreateAsset(provider, 50));
        Assert.False(refused.IsValid);
        Assert.True(service.IsAlive(stream));
        Assert.True(service.IsAlive(stereo));
        Assert.True(service.Music.IsPlaying(track));
        Assert.Equal(1, service.StolenVoiceCount);
        Assert.Equal(1, service.RefusedVoiceCount);
    }

    [Fact]
    public void AReservoirOfProtectedVoicesOnly_RefusesAPrioritySound()
    {
        var service = CreateService(out _, out var provider);
        var parameters = AudioVoiceParameters.Default;
        var stream = service.PlayStream(22050, 2, AudioBusNames.Sfx, parameters);
        var track = service.Music.Play(CreateMusicAsset(provider));
        var stereoA = service.PlayClipStereo(StereoSource(), AudioBusNames.Sfx, parameters, 1f, 1f);
        var stereoB = service.PlayClipStereo(StereoSource(), AudioBusNames.Sfx, parameters, 1f, 1f);
        Assert.True(stream.IsValid && track.IsValid && stereoA.IsValid && stereoB.IsValid);

        var refused = service.PlaySound(CreateAsset(provider, 100));

        Assert.False(refused.IsValid);
        Assert.Equal(0, service.StolenVoiceCount);
        Assert.Equal(1, service.RefusedVoiceCount);
        Assert.True(service.IsAlive(stream));
        Assert.True(service.IsAlive(stereoA));
        Assert.True(service.IsAlive(stereoB));
        Assert.True(service.Music.IsPlaying(track));
    }

    [Fact]
    public void AFinishedVoiceNotYetRecycled_IsReplacedWithoutACountedSteal()
    {
        var service = CreateService(out var backend, out var provider);
        var voices = FillWith(service, provider, 1, 2, 2, 3);
        backend.CompleteVoice(voices[2]);

        var newcomer = service.PlaySound(CreateAsset(provider, 5));

        Assert.True(newcomer.IsValid);
        Assert.Equal(0, service.StolenVoiceCount);
        Assert.Equal(0, service.RefusedVoiceCount);
        Assert.False(service.IsAlive(voices[2]));
        Assert.True(service.IsAlive(voices[0]));
        Assert.True(service.IsAlive(voices[1]));
        Assert.True(service.IsAlive(voices[3]));
        Assert.Equal(Capacity, service.ActiveVoiceCount);
    }

    [Fact]
    public void TheOverride_ZeroPriority_NeitherStealsNorCanBeStolen()
    {
        var service = CreateService(out _, out var provider);
        var asset = CreateAsset(provider, 5, "demoted");
        var demoted = service.PlaySound(asset, new SoundPlaybackOverrides { Priority = 0 });
        var voices = FillWith(service, provider, 1, 2, 2);
        Assert.True(demoted.IsValid);

        // It cannot be stolen: the stealer takes the priority-1 voice.
        var stealer = service.PlaySound(CreateAsset(provider, 100));
        Assert.True(stealer.IsValid);
        Assert.True(service.IsAlive(demoted));
        Assert.False(service.IsAlive(voices[0]));

        // It does not steal: against a reservoir of lower priorities it is refused.
        var refused = service.PlaySound(asset, new SoundPlaybackOverrides { Priority = 0 });
        Assert.False(refused.IsValid);
        Assert.Equal(1, service.StolenVoiceCount);
        Assert.Equal(1, service.RefusedVoiceCount);
    }

    [Fact]
    public void TheOverride_AddsAPriorityToAnAssetWithout()
    {
        var service = CreateService(out _, out var provider);
        var voices = FillWith(service, provider, 1, 2, 2, 3);

        var voice = service.PlaySound(CreateAsset(provider, 0), new SoundPlaybackOverrides { Priority = 9 });

        Assert.True(voice.IsValid);
        Assert.False(service.IsAlive(voices[0]));
        Assert.Equal(1, service.StolenVoiceCount);
    }

    [Fact]
    public void ASlotFreedFromAPriorityVoice_AndReusedByPlayClip_IsNotStealable()
    {
        var service = CreateService(out _, out var provider);
        var voices = FillWith(service, provider, 1, 5, 5, 5);
        service.Stop(voices[0]);

        var plain = service.PlayClip(new FakeAudioClip(), AudioBusNames.Sfx, AudioVoiceParameters.Default);
        Assert.True(plain.IsValid);
        Assert.Equal(voices[0].Index, plain.Index);

        var refused = service.PlaySound(CreateAsset(provider, 100));
        Assert.True(refused.IsValid);
        Assert.True(service.IsAlive(plain));
        Assert.False(service.IsAlive(voices[1]));
    }

    [Fact]
    public void ASlotReusedByAStreamAfterAPriorityVoice_IsNotStealable()
    {
        var service = CreateService(out _, out var provider);
        var voices = FillWith(service, provider, 1, 5, 5, 5);
        service.Stop(voices[0]);

        var stream = service.PlayStream(22050, 2, AudioBusNames.Sfx, AudioVoiceParameters.Default);
        Assert.Equal(voices[0].Index, stream.Index);

        var stealer = service.PlaySound(CreateAsset(provider, 100));
        Assert.True(stealer.IsValid);
        Assert.True(service.IsAlive(stream));
    }

    [Fact]
    public void AStolenHandle_IsStale_AndLeavesTheNewVoiceOfTheSameSlotUntouched()
    {
        var service = CreateService(out var backend, out var provider);
        var voices = FillWith(service, provider, 1, 2, 2, 3);
        var victim = voices[0];

        var stealer = service.PlaySound(CreateAsset(provider, 5));
        Assert.Equal(victim.Index, stealer.Index);
        Assert.NotEqual(victim, stealer);

        var volumeBefore = service.GetVoiceVolume(stealer);
        service.SetVoiceVolume(victim, 0.1f);
        service.FadeVoice(victim, 0f, 1f);
        service.Stop(victim);

        Assert.True(service.IsAlive(stealer));
        Assert.False(service.IsFading(stealer));
        Assert.Equal(volumeBefore, service.GetVoiceVolume(stealer));
        Assert.True(backend.IsVoiceAlive(stealer));
    }

    [Fact]
    public void APlayRefusedAfterASteal_CountsARefusal_NotASteal()
    {
        var service = CreateService(out var backend, out var provider);
        var voices = FillWith(service, provider, 1, 2, 2, 3);
        backend.RefusesPlay = true;

        var refused = service.PlaySound(CreateAsset(provider, 5));

        Assert.False(refused.IsValid);
        Assert.Equal(0, service.StolenVoiceCount);
        Assert.Equal(1, service.RefusedVoiceCount);
        Assert.False(service.IsAlive(voices[0]));
        Assert.Equal(Capacity - 1, service.ActiveVoiceCount);
    }

    [Fact]
    public void PlayClipAndPlayClipStereo_AreRefusedAsBeforeOnAFullReservoir()
    {
        var service = CreateService(out _, out var provider);
        var voices = FillWith(service, provider, 1, 1, 1, 1);

        var clip = service.PlayClip(new FakeAudioClip(), AudioBusNames.Sfx, AudioVoiceParameters.Default);
        var stereo = service.PlayClipStereo(StereoSource(), AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 1f);

        Assert.False(clip.IsValid);
        Assert.False(stereo.IsValid);
        Assert.Equal(2, service.RefusedVoiceCount);
        Assert.Equal(0, service.StolenVoiceCount);
        foreach (var voice in voices)
        {
            Assert.True(service.IsAlive(voice));
        }
    }

    [Fact]
    public void Stealing_AllocatesNothing()
    {
        var service = CreateService(out _, out var provider, voiceCapacity: 8);
        var low = CreateAsset(provider, 1, "low");
        var high = CreateAsset(provider, 5, "high");

        for (var i = 0; i < 8; i++)
        {
            Assert.True(service.PlaySound(low).IsValid);
        }

        // One real steal per iteration: the stealer is stopped, then a priority-1 voice refills the freed slot.
        void Cycle()
        {
            var stealer = service.PlaySound(high);
            service.Stop(stealer);
            service.PlaySound(low);
        }

        for (var i = 0; i < 20; i++)
        {
            Cycle();
        }

        var stolenBefore = service.StolenVoiceCount;
        var before = AllocationWindow.Start();
        for (var i = 0; i < 1000; i++)
        {
            Cycle();
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(stolenBefore + 1000, service.StolenVoiceCount);
        Assert.Equal(0, allocated);
    }

    private static SoundAsset CreateMusicAsset(FakeAudioClipProvider provider)
    {
        var wav = WavBuilder.CreatePcm16(sampleRate: 22050, channelCount: 2, sampleCount: 4096, formatChunkSize: 18);

        return new SoundAsset
        {
            Name = "theme",
            AudioFileAssetId = provider.RegisterStream(wav),
            BusName = AudioBusNames.Music,
            IsStreaming = true,
        };
    }
}
