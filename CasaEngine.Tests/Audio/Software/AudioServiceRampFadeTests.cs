using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Streaming;
using Xunit;

namespace CasaEngine.Tests.Audio.Software;

/// <summary>
/// The public fade contract of <see cref="AudioService"/> on the software backend (<see cref="IAudioBusBackend"/>, plan
/// T5.2, decision P21), where the audio thread interpolates the ramps, compared with the same scenario on the fake
/// backend (the per-frame fallback). One tick is one game frame of 10 ms and one audio block of 480 frames.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class AudioServiceRampFadeTests
{
    private const int Block = 480;
    private const float Frame = 0.01f;

    // The chronology and the rendered audio are at most one block apart: a block of a 1 s fade is 0.01 of the volume.
    private const float OneBlockOfASecondFade = 0.012f;

    /// <summary>One service with its way to advance the audio: pumping the offline output, or nothing for the fake.</summary>
    private sealed class Rig : IDisposable
    {
        private readonly OfflineAudioOutput _output;

        public Rig(bool software)
        {
            if (software)
            {
                _output = new OfflineAudioOutput();
                Service = new AudioService(new SoftwareAudioBackend(_output, 16));
                Fake = null;
            }
            else
            {
                Fake = new FakeAudioBackend();
                Service = new AudioService(Fake);
            }

            Provider = new FakeAudioClipProvider();
            Service.ClipProvider = Provider;
        }

        public AudioService Service { get; }

        public FakeAudioBackend Fake { get; }

        public FakeAudioClipProvider Provider { get; }

        public bool IsSoftware => _output != null;

        public void Tick(int count = 1)
        {
            for (var i = 0; i < count; i++)
            {
                Service.Update(Frame);
                _output?.Pump(Block);
            }
        }

        /// <summary>Last frame of the last audio block: the level the voices ended the block at.</summary>
        public float RenderedLevel() => _output.LastBlock[^2];

        public AudioVoiceHandle PlayConstantHalf()
        {
            // Stereo, constant 0.5: the output of a voice at full volume and centre is 0.5.
            IAudioClip clip;

            if (IsSoftware)
            {
                var samples = new short[2 * 400000];
                Array.Fill(samples, (short)16384);
                clip = new PcmAudioClip(samples, 48000, 2);
            }
            else
            {
                clip = new FakeAudioClip();
            }

            return Service.PlayClip(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default);
        }

        public void Dispose()
        {
            Service.Dispose();
        }
    }

    private static void AssertRendered(Rig software, float expectedLevel, float tolerance = 0.002f)
    {
        Assert.Equal(expectedLevel * 0.5f, software.RenderedLevel(), tolerance);
    }

    [Fact]
    public void AVoiceFade_HasTheSameVolumeAndIsFadingAsTheFallback_AndTheAudioFollowsWithinABlock()
    {
        using var soft = new Rig(true);
        using var fake = new Rig(false);
        var softVoice = soft.PlayConstantHalf();
        var fakeVoice = fake.PlayConstantHalf();
        soft.Tick(2);
        fake.Tick(2);

        Assert.False(soft.Service.IsFading(softVoice));
        Assert.Equal(fake.Service.GetVoiceVolume(fakeVoice), soft.Service.GetVoiceVolume(softVoice));

        soft.Service.FadeVoice(softVoice, 0.2f, 1f);
        fake.Service.FadeVoice(fakeVoice, 0.2f, 1f);

        for (var tick = 1; tick <= 110; tick++)
        {
            soft.Tick();
            fake.Tick();

            Assert.Equal(fake.Service.IsFading(fakeVoice), soft.Service.IsFading(softVoice));
            var volume = soft.Service.GetVoiceVolume(softVoice);
            Assert.Equal(fake.Service.GetVoiceVolume(fakeVoice), volume, 5);
            Assert.True(soft.Service.IsAlive(softVoice));
            AssertRendered(soft, volume, OneBlockOfASecondFade);
        }

        Assert.False(soft.Service.IsFading(softVoice));
        Assert.Equal(0.2f, soft.Service.GetVoiceVolume(softVoice), 5);
        AssertRendered(soft, 0.2f, 1e-3f);
    }

    [Fact]
    public void CancelFade_KeepsTheValueReached_AndTheVoiceAlive_AndTheAudioStopsThere()
    {
        using var soft = new Rig(true);
        using var fake = new Rig(false);
        var softVoice = soft.PlayConstantHalf();
        var fakeVoice = fake.PlayConstantHalf();
        soft.Tick();
        fake.Tick();

        soft.Service.FadeVoice(softVoice, 0f, 1f);
        fake.Service.FadeVoice(fakeVoice, 0f, 1f);
        soft.Tick(25);
        fake.Tick(25);

        soft.Service.CancelFade(softVoice);
        fake.Service.CancelFade(fakeVoice);
        var reached = soft.Service.GetVoiceVolume(softVoice);

        soft.Tick(40);
        fake.Tick(40);

        Assert.Equal(0.75f, reached, 3);
        Assert.Equal(fake.Service.GetVoiceVolume(fakeVoice), soft.Service.GetVoiceVolume(softVoice), 5);
        Assert.Equal(reached, soft.Service.GetVoiceVolume(softVoice));
        Assert.False(soft.Service.IsFading(softVoice));
        Assert.True(soft.Service.IsAlive(softVoice));
        AssertRendered(soft, reached, 1e-3f);

        // Several blocks later nothing moved.
        var level = soft.RenderedLevel();
        soft.Tick(10);
        Assert.Equal(level, soft.RenderedLevel(), 1e-5f);
    }

    [Fact]
    public void ASecondFade_StartsFromTheValueReached_AsACrossfadeDoes()
    {
        using var soft = new Rig(true);
        using var fake = new Rig(false);
        var softVoice = soft.PlayConstantHalf();
        var fakeVoice = fake.PlayConstantHalf();
        soft.Tick();
        fake.Tick();

        soft.Service.FadeVoice(softVoice, 0f, 1f);
        fake.Service.FadeVoice(fakeVoice, 0f, 1f);
        soft.Tick(50);
        fake.Tick(50);

        soft.Service.FadeVoice(softVoice, 1f, 1f);
        fake.Service.FadeVoice(fakeVoice, 1f, 1f);
        soft.Tick(25);
        fake.Tick(25);

        // 0.5 at the switch, then a quarter of a second of the way up: 0.625.
        Assert.Equal(0.625f, soft.Service.GetVoiceVolume(softVoice), 3);
        Assert.Equal(fake.Service.GetVoiceVolume(fakeVoice), soft.Service.GetVoiceVolume(softVoice), 5);
        AssertRendered(soft, soft.Service.GetVoiceVolume(softVoice), OneBlockOfASecondFade);
    }

    [Fact]
    public void StopWithFade_ReleasesTheVoiceAtTheEndOfTheChronology_LikeTheFallback()
    {
        using var soft = new Rig(true);
        using var fake = new Rig(false);
        var softVoice = soft.PlayConstantHalf();
        var fakeVoice = fake.PlayConstantHalf();
        soft.Tick();
        fake.Tick();

        soft.Service.StopWithFade(softVoice, 0.5f);
        fake.Service.StopWithFade(fakeVoice, 0.5f);

        for (var tick = 1; tick <= 60; tick++)
        {
            soft.Tick();
            fake.Tick();

            Assert.Equal(fake.Service.IsAlive(fakeVoice), soft.Service.IsAlive(softVoice));
        }

        Assert.False(soft.Service.IsAlive(softVoice));
        Assert.Equal(0, soft.Service.ActiveVoiceCount);
        Assert.Equal(0, ((SoftwareAudioBackend)soft.Service.Backend).ActiveVoiceCount);
    }

    [Fact]
    public void AVolumeOrPanSetDuringARampedFade_DoesNotInterruptTheRamp()
    {
        using var soft = new Rig(true);
        var voice = soft.PlayConstantHalf();
        soft.Tick();

        soft.Service.FadeVoice(voice, 0f, 1f);
        soft.Tick(20);

        soft.Service.SetVoiceVolume(voice, 0.9f);
        soft.Service.SetVoicePan(voice, 0.3f);
        soft.Tick(10);

        // The fade is still the one in charge, from the chronology to the rendered audio.
        Assert.True(soft.Service.IsFading(voice));
        Assert.Equal(0.7f, soft.Service.GetVoiceVolume(voice), 3);

        // Pan 0.3 on a stereo clip: balance, the left channel is scaled by 0.7. The last frame is on the ramp.
        Assert.Equal(0.5f * 0.7f * 0.7f, soft.RenderedLevel(), 0.01f);
        Assert.True(soft.RenderedLevel() > 0.2f);
    }

    [Fact]
    public void AFadeThatEndsBeforeTheNextOne_LeavesTheTargetHeld()
    {
        using var soft = new Rig(true);
        var voice = soft.PlayConstantHalf();
        soft.Tick();

        soft.Service.FadeVoice(voice, 0.4f, 0.1f);
        soft.Tick(30);

        Assert.False(soft.Service.IsFading(voice));
        AssertRendered(soft, 0.4f, 1e-3f);

        // An explicit volume afterwards works again.
        soft.Service.SetVoiceVolume(voice, 0.8f);
        soft.Tick(3);
        AssertRendered(soft, 0.8f, 1e-3f);
    }

    [Fact]
    public void AMusicCrossfade_StartsFromTheValueTheFadeInReached_AndReleasesTheOldTrack_LikeTheFallback()
    {
        using var soft = new Rig(true);
        using var fake = new Rig(false);

        var softTracks = StartCrossfade(soft, out var softFirst, out var softSecond);
        var fakeTracks = StartCrossfade(fake, out var fakeFirst, out var fakeSecond);
        Assert.True(softTracks && fakeTracks);

        // The first track faded in for half a second (0.5), then faded out over one second: 0.375 a quarter in.
        soft.Tick(25);
        fake.Tick(25);

        Assert.Equal(0.375f, soft.Service.Music.GetVolume(softFirst), 3);
        Assert.Equal(fake.Service.Music.GetVolume(fakeFirst), soft.Service.Music.GetVolume(softFirst), 5);
        Assert.Equal(0.25f, soft.Service.Music.GetVolume(softSecond), 3);
        Assert.Equal(fake.Service.Music.GetVolume(fakeSecond), soft.Service.Music.GetVolume(softSecond), 5);

        soft.Tick(80);
        fake.Tick(80);

        Assert.False(soft.Service.Music.IsAlive(softFirst));
        Assert.False(fake.Service.Music.IsAlive(fakeFirst));
        Assert.True(soft.Service.Music.IsAlive(softSecond));
        Assert.Equal(1f, soft.Service.Music.GetVolume(softSecond), 4);
    }

    private static bool StartCrossfade(Rig rig, out MusicTrackHandle first, out MusicTrackHandle second)
    {
        first = rig.Service.Music.Play(StreamingAsset(rig, "first"), 1f);
        rig.Tick(50);

        // Half way into the fade in: the volume reached is 0.5.
        second = rig.Service.Music.Crossfade(first, StreamingAsset(rig, "second"), 1f);
        return first.IsValid && second.IsValid;
    }

    private static SoundAsset StreamingAsset(Rig rig, string name)
    {
        var wav = WavBuilder.CreatePcm16(sampleRate: 22050, channelCount: 2, sampleCount: 65536, formatChunkSize: 18);

        return new SoundAsset
        {
            Name = name,
            AudioFileAssetId = rig.Provider.RegisterStream(wav),
            BusName = AudioBusNames.Music,
            IsStreaming = true,
            IsLooped = true,
        };
    }

    [Fact]
    public void ABusFade_FollowsTheFallbackChronology_AndTheAudioFollowsWithinABlock_AndAnExplicitVolumeWinsAfterIt()
    {
        using var soft = new Rig(true);
        using var fake = new Rig(false);
        var softVoice = soft.PlayConstantHalf();
        var fakeVoice = fake.PlayConstantHalf();
        soft.Tick(2);
        fake.Tick(2);

        soft.Service.FadeBus(AudioBusNames.Sfx, 0.2f, 1f);
        fake.Service.FadeBus(AudioBusNames.Sfx, 0.2f, 1f);

        for (var tick = 1; tick <= 110; tick++)
        {
            soft.Tick();
            fake.Tick();

            var volume = soft.Service.Mixer.GetBus(AudioBusNames.Sfx).Volume;
            Assert.Equal(fake.Service.Mixer.GetBus(AudioBusNames.Sfx).Volume, volume, 5);
            AssertRendered(soft, volume, OneBlockOfASecondFade);

            // The fallback applies the bus gain in the volume of each voice.
            Assert.Equal(volume, fake.Fake.GetParameters(fakeVoice).Volume, 3);
        }

        Assert.Equal(0.2f, soft.Service.Mixer.GetBus(AudioBusNames.Sfx).Volume, 5);
        AssertRendered(soft, 0.2f, 1e-3f);

        soft.Service.Mixer.GetBus(AudioBusNames.Sfx).Volume = 1f;
        soft.Tick(5);
        AssertRendered(soft, 1f, 1e-3f);
    }

    [Fact]
    public void MutingABusDuringABusFade_TakesEffectAtTheNextUpdate_AndUnmutingResumesTheFade()
    {
        using var soft = new Rig(true);
        using var fake = new Rig(false);
        var softVoice = soft.PlayConstantHalf();
        var fakeVoice = fake.PlayConstantHalf();
        soft.Tick(2);
        fake.Tick(2);

        soft.Service.FadeBus(AudioBusNames.Sfx, 0.2f, 1f);
        fake.Service.FadeBus(AudioBusNames.Sfx, 0.2f, 1f);
        soft.Tick(30);
        fake.Tick(30);

        soft.Service.Mixer.GetBus(AudioBusNames.Sfx).IsMuted = true;
        fake.Service.Mixer.GetBus(AudioBusNames.Sfx).IsMuted = true;
        soft.Tick(3);
        fake.Tick(3);

        Assert.Equal(0f, fake.Fake.GetParameters(fakeVoice).Volume);
        Assert.Equal(0f, soft.RenderedLevel(), 1e-4f);

        // Still silent several ticks later, while the chronology goes on.
        soft.Tick(20);
        fake.Tick(20);
        Assert.Equal(0f, soft.RenderedLevel(), 1e-4f);

        soft.Service.Mixer.GetBus(AudioBusNames.Sfx).IsMuted = false;
        fake.Service.Mixer.GetBus(AudioBusNames.Sfx).IsMuted = false;
        soft.Tick(3);
        fake.Tick(3);

        var volume = soft.Service.Mixer.GetBus(AudioBusNames.Sfx).Volume;
        Assert.Equal(fake.Service.Mixer.GetBus(AudioBusNames.Sfx).Volume, volume, 5);
        Assert.Equal(volume, fake.Fake.GetParameters(fakeVoice).Volume, 3);
        AssertRendered(soft, volume, OneBlockOfASecondFade);

        // The fade still ends at its target, on the same tick as the fallback.
        for (var tick = 0; tick < 60; tick++)
        {
            soft.Tick();
            fake.Tick();
            Assert.Equal(fake.Service.Mixer.GetBus(AudioBusNames.Sfx).Volume, soft.Service.Mixer.GetBus(AudioBusNames.Sfx).Volume, 5);
        }

        Assert.Equal(0.2f, soft.Service.Mixer.GetBus(AudioBusNames.Sfx).Volume, 5);
        AssertRendered(soft, 0.2f, 1e-3f);
    }

    [Fact]
    public void ABusFadeReplacedByAZeroDurationOne_ReturnsToTheTargetAtOnce()
    {
        using var soft = new Rig(true);
        var voice = soft.PlayConstantHalf();
        soft.Tick();

        soft.Service.FadeBus(AudioBusNames.Sfx, 0f, 1f);
        soft.Tick(30);
        Assert.True(soft.Service.Mixer.GetBus(AudioBusNames.Sfx).Volume < 0.8f);

        soft.Service.FadeBus(AudioBusNames.Sfx, 0.6f, 0f);
        soft.Tick(5);

        Assert.Equal(0.6f, soft.Service.Mixer.GetBus(AudioBusNames.Sfx).Volume);
        AssertRendered(soft, 0.6f, 1e-3f);

        // The replaced fade does not come back.
        soft.Tick(100);
        AssertRendered(soft, 0.6f, 1e-3f);
        Assert.True(soft.Service.IsAlive(voice));
    }

    [Fact]
    public void FadeBus_IgnoresAnUnknownBus()
    {
        using var soft = new Rig(true);
        soft.Service.FadeBus("NoSuchBus", 0f, 1f);
        soft.Tick(3);

        Assert.Equal(1f, soft.Service.Mixer.GetBus(AudioBusNames.Sfx).Volume);
    }

    [Fact]
    public void FadesAndAudioBlocks_AllocateNothing()
    {
        using var soft = new Rig(true);
        var voice = soft.PlayConstantHalf();
        soft.Tick(5);

        // Warm up (JIT), with both kinds of fade running.
        for (var round = 0; round < 10; round++)
        {
            soft.Service.FadeVoice(voice, round % 2 == 0 ? 0.3f : 1f, 0.05f);
            soft.Service.FadeBus(AudioBusNames.Sfx, round % 2 == 0 ? 0.3f : 1f, 0.05f);
            soft.Tick(6);
        }

        var before = AllocationWindow.Start();

        for (var round = 0; round < 20; round++)
        {
            soft.Service.FadeVoice(voice, round % 2 == 0 ? 0.3f : 1f, 0.05f);
            soft.Service.FadeBus(AudioBusNames.Sfx, round % 2 == 0 ? 0.3f : 1f, 0.05f);
            soft.Tick(6);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
