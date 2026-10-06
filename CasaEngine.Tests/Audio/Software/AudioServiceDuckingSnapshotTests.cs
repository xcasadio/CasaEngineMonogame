using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Effects;
using CasaEngine.Framework.Audio.Mixing;
using Xunit;

namespace CasaEngine.Tests.Audio.Software;

/// <summary>
/// Ducking and snapshots through <see cref="AudioService"/> (plan T5.5, decision P18): routed to the software backend, with the
/// fallback of a backend without <see cref="IAudioBusBackend"/> (snapshots apply gains only, ducking is absent with one log line).
/// One pump is one audio block (480 frames, 10 ms); the tests advance the service by the same 10 ms, so they do not depend on the
/// wall clock.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class AudioServiceDuckingSnapshotTests
{
    private const int Block = 480;

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = new();

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) => Warnings.Add(msg);
        public void WriteError(string msg) { }
    }

    private static PcmAudioClip ConstantStereoClip(short value)
    {
        var samples = new short[2 * 200000];
        Array.Fill(samples, value);
        return new PcmAudioClip(samples, 48000, 2);
    }

    // Advances the service and the audio output by one block each, count times; returns the peak of the last block.
    private static float Run(AudioService service, OfflineAudioOutput output, int blocks)
    {
        var peak = 0f;

        for (var i = 0; i < blocks; i++)
        {
            service.Update(0.01f);
            peak = output.Pump(Block);
        }

        return peak;
    }

    private static AudioService NewService(OfflineAudioOutput output)
    {
        var service = new AudioService(new SoftwareAudioBackend(output, 16));
        service.MasterLimiter.IsEnabled = false; // the tests read exact levels; the limiter has its own tests
        return service;
    }

    private static float Gain(float depthDb)
    {
        return (float)Math.Pow(10.0, -depthDb / 20.0);
    }

    [Fact]
    public void ADuckingOnTheMusicBus_AttenuatesItWhileTheVoiceBusIsActive_AndReleasesAfter()
    {
        var output = new OfflineAudioOutput();
        using var service = NewService(output);
        var music = service.Mixer.GetBus(AudioBusNames.Music);
        var voiceBus = service.Mixer.GetBus(AudioBusNames.Voice);
        music.AddEffect(new DuckingEffect(voiceBus, depthDb: 12f, thresholdDb: -30f, attackSeconds: 0.01f, releaseSeconds: 0.1f));
        service.PlayClip(ConstantStereoClip(8192), AudioBusNames.Music, AudioVoiceParameters.Default); // 0.25
        Assert.Equal(0.25f, Run(service, output, 10), 3);

        var dialogue = service.PlayClip(ConstantStereoClip(4096), AudioBusNames.Voice, AudioVoiceParameters.Default); // 0.125
        Assert.Equal((0.25f * Gain(12f)) + 0.125f, Run(service, output, 50), 3);

        service.Stop(dialogue);
        Assert.Equal(0.25f, Run(service, output, 100), 3);
    }

    [Fact]
    public void TheDuckingSetBeforeTheService_IsSentWhenItIsCreated_AndRemovingItRestoresTheMusic()
    {
        var mixer = AudioBusNames.CreateDefaultMixer();
        var ducking = new DuckingEffect(mixer.GetBus(AudioBusNames.Voice), 12f, -30f, 0.01f, 0.1f);
        mixer.GetBus(AudioBusNames.Music).AddEffect(ducking);
        var output = new OfflineAudioOutput();
        using var service = new AudioService(new SoftwareAudioBackend(output, 16), mixer);
        service.MasterLimiter.IsEnabled = false;
        service.PlayClip(ConstantStereoClip(8192), AudioBusNames.Music, AudioVoiceParameters.Default);
        service.PlayClip(ConstantStereoClip(4096), AudioBusNames.Voice, AudioVoiceParameters.Default);
        Assert.Equal((0.25f * Gain(12f)) + 0.125f, Run(service, output, 50), 3);

        Assert.True(mixer.GetBus(AudioBusNames.Music).RemoveEffect(ducking));
        Assert.Equal(0.375f, Run(service, output, 3), 3);

        // Depth is a parameter published as a last value.
        mixer.GetBus(AudioBusNames.Music).AddEffect(ducking);
        ducking.DepthDb = 6f;
        Assert.Equal((0.25f * Gain(6f)) + 0.125f, Run(service, output, 80), 3);
    }

    [Fact]
    public void ADuckingCycle_IsRefusedOnTheGameThread_LikeASendCycle()
    {
        var mixer = AudioBusNames.CreateDefaultMixer();
        var music = mixer.GetBus(AudioBusNames.Music);
        var voice = mixer.GetBus(AudioBusNames.Voice);
        var sfx = mixer.GetBus(AudioBusNames.Sfx);
        music.AddEffect(new DuckingEffect(voice));

        var itself = new DuckingEffect(music);
        Assert.Throws<InvalidOperationException>(() => music.AddEffect(itself));
        Assert.Null(itself.Bus);

        var reverse = new DuckingEffect(music);
        Assert.Throws<InvalidOperationException>(() => voice.AddEffect(reverse));
        Assert.Null(reverse.Bus);
        Assert.Single(music.Effects);
        Assert.Empty(voice.Effects);

        // A send from the ducked bus to its source would close the loop; the send in the same direction as the ducking would not.
        Assert.Throws<InvalidOperationException>(() => music.SetSend(voice, 0.5f));
        voice.SetSend(music, 0.5f);

        // A chain: Sfx ducks Voice, Voice ducks Music, so Music cannot duck Sfx.
        voice.AddEffect(new DuckingEffect(sfx));
        Assert.Throws<InvalidOperationException>(() => sfx.AddEffect(new DuckingEffect(music)));

        // A source from another mixer, and no source at all.
        var foreign = new AudioMixer().CreateBus("Foreign", null);
        Assert.Throws<ArgumentException>(() => sfx.AddEffect(new DuckingEffect(foreign)));
        Assert.Throws<ArgumentNullException>(() => new DuckingEffect(null));
    }

    [Fact]
    public void ASnapshot_CapturedModifiedAndReapplied_RestoresTheGains_AndNeverTouchesTheEditorBusOrTheMasterMute()
    {
        var output = new OfflineAudioOutput();
        using var service = NewService(output);
        var master = service.Mixer.GetBus(AudioBusNames.Master);
        var music = service.Mixer.GetBus(AudioBusNames.Music);
        var sfx = service.Mixer.GetBus(AudioBusNames.Sfx);
        var editor = service.Mixer.GetBus(AudioBusNames.Editor);
        music.Volume = 0.8f;
        sfx.Volume = 0.6f;
        master.Volume = 0.9f;
        editor.Volume = 0.3f;
        service.PlayClip(ConstantStereoClip(8192), AudioBusNames.Sfx, AudioVoiceParameters.Default); // 0.25
        Run(service, output, 5);

        var snapshot = service.CaptureSnapshot();
        Assert.False(snapshot.TryGetBusVolume(AudioBusNames.Editor, out _));
        Assert.True(snapshot.TryGetBusVolume(AudioBusNames.Music, out var capturedMusic));
        Assert.Equal(0.8f, capturedMusic);

        music.Volume = 0.1f;
        sfx.Volume = 0.2f;
        master.Volume = 0.4f;
        editor.Volume = 0.95f;
        master.IsMuted = true;

        service.ApplySnapshot(snapshot, 0.5f);
        Run(service, output, 25);

        // Half way: moving, not yet there.
        Assert.InRange(sfx.Volume, 0.3f, 0.5f);

        Run(service, output, 30);
        Assert.Equal(0.8f, music.Volume, 4);
        Assert.Equal(0.6f, sfx.Volume, 4);
        Assert.Equal(0.9f, master.Volume, 4);

        // Untouched: the Editor bus keeps the value it had when the snapshot was applied, and the Master mute stays.
        Assert.Equal(0.95f, editor.Volume);
        Assert.True(master.IsMuted);

        // The audio thread holds the restored gain too.
        master.IsMuted = false;
        Assert.Equal(0.25f * 0.6f * 0.9f, Run(service, output, 5), 3);
    }

    [Fact]
    public void ASnapshot_RampsOnTheBackendAndAppliesTheDurationExactly_AndAZeroDurationAppliesAtOnce()
    {
        var output = new OfflineAudioOutput();
        using var service = NewService(output);
        var sfx = service.Mixer.GetBus(AudioBusNames.Sfx);
        sfx.Volume = 0.8f;
        service.PlayClip(ConstantStereoClip(16384), AudioBusNames.Sfx, AudioVoiceParameters.Default); // 0.5
        Run(service, output, 5);
        var snapshot = service.CaptureSnapshot();

        sfx.Volume = 0.2f;
        Run(service, output, 3);
        service.ApplySnapshot(snapshot, 0.5f);

        var previous = Run(service, output, 3);

        // Each block is louder than the previous one (a ramp, not a step), up to 0.4 at the end of the 0.5 s.
        for (var i = 0; i < 40; i++)
        {
            var peak = Run(service, output, 1);
            Assert.True(peak >= previous - 1e-4f);
            previous = peak;
        }

        Assert.Equal(0.4f, Run(service, output, 15), 3);

        sfx.Volume = 0.1f;
        service.ApplySnapshot(snapshot, 0f);
        Assert.Equal(0.8f, sfx.Volume);
        Assert.Equal(0.4f, Run(service, output, 3), 3);
    }

    [Fact]
    public void ASnapshot_CapturesAndRestoresTheEffectParameters_AtOnce_AndSkipsAnEffectRemovedSince()
    {
        var output = new OfflineAudioOutput();
        using var service = NewService(output);
        var music = service.Mixer.GetBus(AudioBusNames.Music);
        var sfx = service.Mixer.GetBus(AudioBusNames.Sfx);
        var editor = service.Mixer.GetBus(AudioBusNames.Editor);
        var filter = new BiquadFilterEffect(BiquadFilterType.LowPass, 2000f, 1f);
        var compressor = new CompressorEffect(-20f, 4f);
        var ducking = new DuckingEffect(sfx, 12f, -40f);
        var removed = new BiquadFilterEffect(BiquadFilterType.HighPass, 100f);
        var editorFilter = new BiquadFilterEffect(BiquadFilterType.LowPass, 3000f);
        music.AddEffect(filter);
        music.AddEffect(compressor);
        music.AddEffect(ducking);
        music.AddEffect(removed);
        editor.AddEffect(editorFilter);

        var snapshot = service.CaptureSnapshot();
        Assert.Equal(4, snapshot.EffectCount);

        filter.FrequencyHz = 300f;
        compressor.Ratio = 10f;
        ducking.DepthDb = 30f;
        removed.FrequencyHz = 5000f;
        editorFilter.FrequencyHz = 500f;
        music.RemoveEffect(removed);

        // The effect parameters apply at the call, not after the ramp.
        service.ApplySnapshot(snapshot, 0.5f);
        Assert.Equal(2000f, filter.FrequencyHz);
        Assert.Equal(4f, compressor.Ratio);
        Assert.Equal(12f, ducking.DepthDb);
        Assert.Equal(5000f, removed.FrequencyHz);
        Assert.Equal(500f, editorFilter.FrequencyHz);
    }

    [Fact]
    public void ASnapshot_AppliesOnlyToTheMixerItWasCapturedFrom_AndRejectsBadArguments()
    {
        var output = new OfflineAudioOutput();
        using var service = NewService(output);
        var snapshot = AudioMixerSnapshot.Capture(AudioBusNames.CreateDefaultMixer());

        Assert.Throws<ArgumentException>(() => service.ApplySnapshot(snapshot, 0.1f));
        Assert.Throws<ArgumentNullException>(() => service.ApplySnapshot(null, 0.1f));
        Assert.Throws<ArgumentNullException>(() => AudioMixerSnapshot.Capture(null));

        // A bus created after the capture is left alone.
        var captured = service.CaptureSnapshot();
        var later = service.Mixer.CreateBus("Later", AudioBusNames.Master);
        later.Volume = 0.3f;
        service.ApplySnapshot(captured, 0f);
        Assert.Equal(0.3f, later.Volume);
        Assert.False(captured.TryGetBusVolume("Later", out _));
    }

    [Fact]
    public void WithoutTheCapability_ASnapshotAppliesTheGainsOnly_AndTheDuckingIsAbsentWithOneLine()
    {
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            var backend = new FakeAudioBackend();
            using var service = new AudioService(backend);
            var music = service.Mixer.GetBus(AudioBusNames.Music);
            var voiceBus = service.Mixer.GetBus(AudioBusNames.Voice);
            var editor = service.Mixer.GetBus(AudioBusNames.Editor);
            var master = service.Mixer.GetBus(AudioBusNames.Master);
            var filter = new BiquadFilterEffect(BiquadFilterType.LowPass, 2000f);
            music.AddEffect(new DuckingEffect(voiceBus));
            music.AddEffect(filter);
            music.Volume = 0.8f;
            var voice = service.PlayClip(new FakeAudioClip(), AudioBusNames.Music, AudioVoiceParameters.Default);

            for (var i = 0; i < 5; i++)
            {
                service.Update(0.016f);
            }

            var snapshot = service.CaptureSnapshot();
            music.Volume = 0.2f;
            editor.Volume = 0.7f;
            master.IsMuted = true;
            filter.FrequencyHz = 300f;

            // Stepped per frame, like FadeBus without the capability: the folded gain of the voice follows the bus.
            service.ApplySnapshot(snapshot, 0.5f);
            service.Update(0.25f);
            Assert.InRange(music.Volume, 0.4f, 0.6f);

            for (var i = 0; i < 10; i++)
            {
                service.Update(0.1f);
            }

            Assert.Equal(0.8f, music.Volume);
            Assert.Equal(0f, backend.GetParameters(voice).Volume); // the Master mute still folds into the voice
            master.IsMuted = false;
            service.Update(0.016f);
            Assert.Equal(0.8f, backend.GetParameters(voice).Volume, 4);
            Assert.Equal(0.7f, editor.Volume);

            // Gains only: the effect parameters are not restored where the effects are absent.
            Assert.Equal(300f, filter.FrequencyHz);
        }
        finally
        {
            Logs.Close();
        }

        Assert.Single(logger.Warnings, warning => warning.Contains("ducking"));
        Assert.Single(logger.Warnings, warning => warning.Contains("insert effects"));
    }
}
