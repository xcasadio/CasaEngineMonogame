using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Spatial;
using CasaEngine.Framework.Audio.Streaming;
using Xunit;

namespace CasaEngine.Tests.Audio.Spatial;

/// <summary>
/// <see cref="IAudioBackend"/> over a <see cref="FakeAudioBackend"/> that records what the voice looked like when
/// <see cref="Start"/> arrived (volume and pitch received so far), to prove a value was sent before the start.
/// </summary>
internal class StartRecordingBackend : IAudioBackend
{
    public StartRecordingBackend()
    {
        Inner = new FakeAudioBackend(16);
    }

    public FakeAudioBackend Inner { get; }

    public int StartCount { get; private set; }

    public float VolumeAtStart { get; private set; }

    public float PitchAtStart { get; private set; }

    protected virtual void OnStart(AudioVoiceHandle voice)
    {
    }

    public bool IsAvailable => Inner.IsAvailable;

    public int VoiceCapacity => Inner.VoiceCapacity;

    public int ActiveVoiceCount => Inner.ActiveVoiceCount;

    public AudioVoiceHandle Play(IAudioClip clip, in AudioVoiceParameters parameters) => Inner.Play(clip, parameters);

    public void SetParameters(AudioVoiceHandle voice, in AudioVoiceParameters parameters) => Inner.SetParameters(voice, parameters);

    public void SetVolume(AudioVoiceHandle voice, float volume) => Inner.SetVolume(voice, volume);

    public AudioVoiceState GetState(AudioVoiceHandle voice) => Inner.GetState(voice);

    public void Pause(AudioVoiceHandle voice) => Inner.Pause(voice);

    public void Resume(AudioVoiceHandle voice) => Inner.Resume(voice);

    public void Stop(AudioVoiceHandle voice) => Inner.Stop(voice);

    public void Release(AudioVoiceHandle voice) => Inner.Release(voice);

    public void StopAll() => Inner.StopAll();

    public bool SupportsStreaming => Inner.SupportsStreaming;

    public AudioVoiceHandle CreateStreamingVoice(int sampleRate, int channelCount, in AudioVoiceParameters parameters)
        => Inner.CreateStreamingVoice(sampleRate, channelCount, parameters);

    public void SubmitBuffer(AudioVoiceHandle voice, byte[] buffer, int offset, int count) => Inner.SubmitBuffer(voice, buffer, offset, count);

    public int GetPendingBufferCount(AudioVoiceHandle voice) => Inner.GetPendingBufferCount(voice);

    public void Start(AudioVoiceHandle voice)
    {
        StartCount++;
        var received = Inner.GetParameters(voice);
        VolumeAtStart = received.Volume;
        PitchAtStart = received.Pitch;
        OnStart(voice);
        Inner.Start(voice);
    }

    public void Dispose() => Inner.Dispose();
}

/// <summary>A <see cref="StartRecordingBackend"/> with the modulation capability, recording the modulation the voice has at its start.</summary>
internal sealed class StartRecordingModulationBackend : StartRecordingBackend, IAudioVoiceModulationBackend
{
    private readonly Dictionary<int, (float Gain, float Pan, float Rate)> _current = new();

    public (float Gain, float Pan, float Rate) ModulationAtStart { get; private set; } = (1f, float.NaN, 1f);

    public (float Gain, float Pan, float Rate) GetModulation(AudioVoiceHandle voice)
    {
        return _current.TryGetValue(voice.Index, out var value) ? value : (1f, float.NaN, 1f);
    }

    public void SetNextVoiceModulation(float gain, float pan, float rate)
    {
    }

    public void SetVoiceModulation(AudioVoiceHandle voice, float gain, float pan, float rate)
    {
        _current[voice.Index] = (gain, pan, rate);
    }

    protected override void OnStart(AudioVoiceHandle voice)
    {
        ModulationAtStart = GetModulation(voice);
    }
}

/// <summary>One service on one backend kind with its own (inline) music player, driven frame by frame.</summary>
internal sealed class MusicRig : IDisposable
{
    private readonly OfflineAudioOutput _output;

    public MusicRig(SpatialBackendKind kind)
    {
        Kind = kind;

        switch (kind)
        {
            case SpatialBackendKind.Software:
                _output = new OfflineAudioOutput();
                Service = new AudioService(new SoftwareAudioBackend(_output, 16));
                Service.MasterLimiter.IsEnabled = false;
                break;
            case SpatialBackendKind.Fallback:
                Recording = new StartRecordingBackend();
                Service = new AudioService(Recording);
                break;
            default:
                var capability = new StartRecordingModulationBackend();
                Recording = capability;
                CapabilityBackend = capability;
                Service = new AudioService(Recording);
                break;
        }

        Provider = new FakeAudioClipProvider();
        Service.ClipProvider = Provider;
        Player = new MusicPlayer(Service, false);
    }

    public SpatialBackendKind Kind { get; }

    public AudioService Service { get; }

    public StartRecordingBackend Recording { get; }

    public StartRecordingModulationBackend CapabilityBackend { get; }

    public FakeAudioClipProvider Provider { get; }

    public MusicPlayer Player { get; }

    public ReadOnlySpan<float> LastBlock => _output.LastBlock;

    public void PumpOnly() => _output?.Pump(SpatialRig.Block);

    public void Tick(int count = 1)
    {
        for (var i = 0; i < count; i++)
        {
            Service.Update(SpatialRig.Frame);
            Player.Update(SpatialRig.Frame);
            _output?.Pump(SpatialRig.Block);
        }
    }

    /// <summary>A looping streaming asset of a constant (0.5) or ramp stereo 48 kHz wav, carrying <paramref name="bindings"/>.</summary>
    public SoundAsset CreateMusic(bool ramp, params AudioParameterBinding[] bindings)
    {
        const int frames = 60000;
        var data = new byte[frames * 4];

        for (var i = 0; i < frames; i++)
        {
            var sample = ramp ? (short)(i / 2) : (short)16384;
            var low = (byte)(sample & 0xFF);
            var high = (byte)((sample >> 8) & 0xFF);
            data[i * 4] = low;
            data[(i * 4) + 1] = high;
            data[(i * 4) + 2] = low;
            data[(i * 4) + 3] = high;
        }

        var asset = new SoundAsset
        {
            Name = "music",
            AudioFileAssetId = Provider.RegisterStream(WavBuilder.Create(WavBuilder.PcmFormatTag, 48000, 2, 16, data)),
            BusName = AudioBusNames.Music,
            IsStreaming = true,
            IsLooped = true,
        };
        asset.SetParameterBindings(bindings);
        return asset;
    }

    public void Dispose()
    {
        Player.Dispose();
        Service.Dispose();
    }
}

/// <summary>
/// Game parameters bound to the volume and the pitch of sounds and music tracks (plan T9.6, decision P39), on the software
/// backend (rendered level, playback speed), the fallback (parameters received) and the capability (modulation received).
/// The start values are checked before any <see cref="AudioService.Update"/>: the bound factor is never a ramp from full gain.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class AudioServiceGameParameterTests
{
    private const string Param = "p";

    private static AudioParameterBinding VolumeBinding(string name = Param, float outMin = 0f)
        => new(name, AudioParameterTarget.Volume, 0f, 1f, outMin, 1f);

    private static AudioParameterBinding PitchBinding(string name = Param, float outMax = 1f)
        => new(name, AudioParameterTarget.Pitch, -1f, 1f, -1f, outMax);

    private static SoundAsset BoundAsset(SpatialRig rig, params AudioParameterBinding[] bindings)
    {
        var asset = rig.CreateAsset(AudioSpatialMode.None);
        asset.SetParameterBindings(bindings);
        return asset;
    }

    /// <summary>A ramp clip on the software backend (the speed is read from the level reached), a plain one on the fakes.</summary>
    private static SoundAsset BoundRampAsset(SpatialRig rig, params AudioParameterBinding[] bindings)
    {
        var asset = BoundAsset(rig, bindings);

        if (rig.Kind == SpatialBackendKind.Software)
        {
            var samples = new short[60000];

            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] = (short)(i / 2);
            }

            asset.AudioFileAssetId = rig.Provider.Register(new PcmAudioClip(samples, 48000, 1));
        }

        return asset;
    }

    /// <summary>Last left sample of the block rendered last.</summary>
    private static float Played(SpatialRig rig) => rig.LastBlock[^2];

    // ---- neutral, written, clamped ------------------------------------------

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void AParameterNeverWritten_IsNeutral(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        var voice = rig.Service.PlaySound(BoundAsset(rig, VolumeBinding(), PitchBinding()));
        Assert.True(voice.IsValid);

        rig.Tick(3);

        Assert.True(float.IsNaN(rig.Service.GetGameParameter(rig.Service.GetGameParameterIndex(Param))));
        rig.Verify(voice, 1f, float.NaN);

        if (kind == SpatialBackendKind.Fallback)
        {
            Assert.Equal(0f, rig.Fake.GetParameters(voice).Pitch, 1e-4f);
        }
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void AParameterWritten_ChangesTheVolume_AndTheChangesFollow(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        var voice = rig.Service.PlaySound(BoundAsset(rig, VolumeBinding()));
        rig.Tick(2);

        rig.Service.SetGameParameter(Param, 0.5f);
        rig.Tick(2);
        rig.Verify(voice, 0.5f, float.NaN);

        rig.Service.SetGameParameter(rig.Service.GetGameParameterIndex("P"), 0.8f);
        rig.Tick(2);
        rig.Verify(voice, 0.8f, float.NaN);

        Assert.Equal(0.8f, rig.Service.GetGameParameter(rig.Service.GetGameParameterIndex(Param)));
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void TheInputIsClampedToItsRange(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        var voice = rig.Service.PlaySound(BoundAsset(rig, VolumeBinding(outMin: 0.2f)));

        rig.Service.SetGameParameter(Param, -3f);
        rig.Tick(2);
        rig.Verify(voice, 0.2f, float.NaN);

        rig.Service.SetGameParameter(Param, 5f);
        rig.Tick(2);
        rig.Verify(voice, 1f, float.NaN);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void VolumeBindingsMultiply(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        var voice = rig.Service.PlaySound(BoundAsset(rig, VolumeBinding("a"), VolumeBinding("b")));

        rig.Service.SetGameParameter("a", 0.5f);
        rig.Service.SetGameParameter("b", 0.4f);
        rig.Tick(2);

        rig.Verify(voice, 0.2f, float.NaN);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void APitchBinding_ChangesThePlaybackRate(SpatialBackendKind kind)
    {
        var expected = MathF.Pow(2f, 0.5f);

        if (kind == SpatialBackendKind.Software)
        {
            Assert.Equal(expected, RampProgress(0.5f, writeBeforePlay: false) / RampProgress(null, writeBeforePlay: false), 0.01f);
            return;
        }

        using var rig = new SpatialRig(kind);
        var voice = rig.Service.PlaySound(BoundAsset(rig, PitchBinding()));
        rig.Service.SetGameParameter(Param, 0.5f);
        rig.Tick(2);

        if (kind == SpatialBackendKind.Fallback)
        {
            Assert.Equal(0.5f, rig.Fake.GetParameters(voice).Pitch, 1e-3f);
        }
        else
        {
            Assert.Equal(expected, rig.Capability.GetModulation(voice).Rate, 1e-3f);
            Assert.Equal(0f, rig.Fake.GetParameters(voice).Pitch, 1e-4f);
        }
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void ThePitchSumIsClampedToOneOctave(SpatialBackendKind kind)
    {
        if (kind == SpatialBackendKind.Software)
        {
            Assert.Equal(2f, RampProgress(0.9f, writeBeforePlay: false, twoBindings: true) / RampProgress(null, writeBeforePlay: false), 0.02f);
            return;
        }

        using var rig = new SpatialRig(kind);
        var voice = rig.Service.PlaySound(BoundAsset(rig, PitchBinding("a"), PitchBinding("b")));
        rig.Service.SetGameParameter("a", 0.9f);
        rig.Service.SetGameParameter("b", 0.9f);
        rig.Tick(2);

        if (kind == SpatialBackendKind.Fallback)
        {
            Assert.Equal(1f, rig.Fake.GetParameters(voice).Pitch, 1e-3f);
        }
        else
        {
            Assert.Equal(2f, rig.Capability.GetModulation(voice).Rate, 1e-3f);
        }
    }

    /// <summary>
    /// Progress of a ramp clip over 20 frames after a warm-up, with the pitch parameter <paramref name="value"/> (not written
    /// when null), written before or after the play.
    /// </summary>
    private static float RampProgress(float? value, bool writeBeforePlay, bool twoBindings = false)
    {
        using var rig = new SpatialRig(SpatialBackendKind.Software);
        var asset = twoBindings
            ? BoundRampAsset(rig, PitchBinding("a"), PitchBinding("b"))
            : BoundRampAsset(rig, PitchBinding());

        void Write()
        {
            if (value.HasValue)
            {
                rig.Service.SetGameParameter(twoBindings ? "a" : Param, value.Value);

                if (twoBindings)
                {
                    rig.Service.SetGameParameter("b", value.Value);
                }
            }
        }

        if (writeBeforePlay)
        {
            Write();
        }

        var voice = rig.Service.PlaySound(asset);
        Assert.True(voice.IsValid);

        if (!writeBeforePlay)
        {
            Write();
        }

        rig.Tick(5);
        var start = Played(rig);
        rig.Tick(20);
        return Played(rig) - start;
    }

    // ---- start values -------------------------------------------------------

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void PlaySound_StartsAtTheBoundVolume_NeverARampFromFullGain(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        rig.Service.SetGameParameter(Param, 0.25f);

        var voice = rig.Service.PlaySound(BoundAsset(rig, VolumeBinding()));
        Assert.True(voice.IsValid);

        // No Update yet: the first block (or the first Play) already carries the factor.
        rig.PumpOnly();

        switch (kind)
        {
            case SpatialBackendKind.Software:
                rig.Verify(voice, 0.25f, float.NaN, tolerance: 0.01f);
                break;
            case SpatialBackendKind.Fallback:
                Assert.Equal(0, rig.Fake.SetParametersCount);
                Assert.Equal(0, rig.Fake.SetVolumeCount);
                rig.Verify(voice, 0.25f, float.NaN, tolerance: 0.001f);
                break;
            default:
                Assert.Equal(1, rig.Capability.NextSetCount);
                Assert.Equal(new[] { "Next", "Play" }, rig.Capability.Events.ToArray());
                Assert.Equal(0.25f, rig.Capability.LastPlayModulation.Gain, 1e-4f);
                Assert.Equal(1f, rig.Capability.LastPlayModulation.Rate);
                Assert.Equal(0, rig.Capability.VoiceSetCount);
                break;
        }

        // The next Update keeps it: nothing changes.
        rig.Tick(2);
        rig.Verify(voice, 0.25f, float.NaN);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void PlaySoundAt_StartsAtTheBoundVolume_ComposedWithTheDistance(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        rig.SetListener(System.Numerics.Vector3.Zero);
        rig.Service.SetGameParameter(Param, 0.25f);
        var asset = rig.CreateAsset();
        asset.SetParameterBindings(new[] { VolumeBinding() });

        var voice = rig.PlayAt(asset, new System.Numerics.Vector3(0f, 0f, -2f));
        rig.PumpOnly();

        // Inverse clamped at 2 x the reference distance: 0.5, times the bound 0.25.
        rig.Verify(voice, 0.125f, 0f);
        rig.Tick(2);
        rig.Verify(voice, 0.125f, 0f);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void PlaySound_StartsAtTheBoundPitch(SpatialBackendKind kind)
    {
        if (kind == SpatialBackendKind.Software)
        {
            Assert.Equal(MathF.Pow(2f, 0.5f), RampProgress(0.5f, writeBeforePlay: true) / RampProgress(null, writeBeforePlay: true), 0.01f);
            Assert.Equal(MathF.Pow(2f, 0.5f), FirstBlockEnd(0.5f) / FirstBlockEnd(null), 0.01f);
            return;
        }

        using var rig = new SpatialRig(kind);
        rig.Service.SetGameParameter(Param, 0.5f);
        var voice = rig.Service.PlaySound(BoundAsset(rig, PitchBinding()));

        if (kind == SpatialBackendKind.Fallback)
        {
            Assert.Equal(0, rig.Fake.SetParametersCount);
            Assert.Equal(0.5f, rig.Fake.GetParameters(voice).Pitch, 1e-3f);
        }
        else
        {
            Assert.Equal(MathF.Pow(2f, 0.5f), rig.Capability.LastPlayModulation.Rate, 1e-3f);
            Assert.Equal(0, rig.Capability.VoiceSetCount);
        }
    }

    /// <summary>Last sample of the very first block, before any Update.</summary>
    private static float FirstBlockEnd(float? value)
    {
        using var rig = new SpatialRig(SpatialBackendKind.Software);

        if (value.HasValue)
        {
            rig.Service.SetGameParameter(Param, value.Value);
        }

        var voice = rig.Service.PlaySound(BoundRampAsset(rig, PitchBinding()));
        Assert.True(voice.IsValid);
        rig.PumpOnly();
        return Played(rig);
    }

    // ---- music tracks -------------------------------------------------------

    private static void VerifyMusicLevel(MusicRig rig, AudioVoiceHandle voice, float expectedFactor, float fade = 1f, float tolerance = 0.005f)
    {
        var bus = rig.Service.Mixer.GetEffectiveGain(AudioBusNames.Music);
        var volume = fade;

        switch (rig.Kind)
        {
            case SpatialBackendKind.Software:
            {
                var expected = 0.5f * volume * bus * expectedFactor;
                Assert.Equal(expected, rig.LastBlock[^2], tolerance);
                Assert.Equal(expected, rig.LastBlock[^1], tolerance);
                break;
            }

            case SpatialBackendKind.Fallback:
                Assert.Equal(volume * bus * expectedFactor, rig.Recording.Inner.GetParameters(voice).Volume, tolerance);
                break;
            default:
                Assert.Equal(expectedFactor, rig.CapabilityBackend.GetModulation(voice).Gain, tolerance);
                Assert.Equal(volume * bus, rig.Recording.Inner.GetParameters(voice).Volume, tolerance);
                break;
        }
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void AMusicTrack_StartsAtTheBoundVolume_BeforeItsStart(SpatialBackendKind kind)
    {
        using var rig = new MusicRig(kind);
        rig.Service.SetGameParameter(Param, 0.25f);

        var track = rig.Player.Play(rig.CreateMusic(false, VolumeBinding()));
        Assert.True(track.IsValid);
        var voice = new AudioVoiceHandle(0, 0);

        // No Update: the values are the ones the voice started with.
        rig.PumpOnly();
        VerifyMusicLevel(rig, voice, 0.25f);

        switch (kind)
        {
            case SpatialBackendKind.Fallback:
                Assert.Equal(1, rig.Recording.StartCount);
                Assert.Equal(0.25f * rig.Service.Mixer.GetEffectiveGain(AudioBusNames.Music), rig.Recording.VolumeAtStart, 1e-4f);
                break;
            case SpatialBackendKind.Capability:
                Assert.Equal(1, rig.Recording.StartCount);
                Assert.Equal(0.25f, rig.CapabilityBackend.ModulationAtStart.Gain, 1e-4f);
                Assert.True(float.IsNaN(rig.CapabilityBackend.ModulationAtStart.Pan));
                break;
        }

        rig.Tick(3);
        VerifyMusicLevel(rig, voice, 0.25f);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void AMusicTrackWithoutBindings_IsUntouched(SpatialBackendKind kind)
    {
        using var rig = new MusicRig(kind);
        rig.Service.SetGameParameter(Param, 0.25f);

        rig.Player.Play(rig.CreateMusic(false));
        rig.PumpOnly();
        VerifyMusicLevel(rig, new AudioVoiceHandle(0, 0), 1f);

        if (kind == SpatialBackendKind.Capability)
        {
            Assert.Equal(1f, rig.CapabilityBackend.ModulationAtStart.Gain);
        }
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void AMusicTrackUnderFadeIn_KeepsTheBindingFactor(SpatialBackendKind kind)
    {
        using var rig = new MusicRig(kind);
        rig.Service.SetGameParameter(Param, 0.25f);

        var track = rig.Player.Play(rig.CreateMusic(false, VolumeBinding()), fadeInSeconds: 1f);
        Assert.True(track.IsValid);
        var voice = new AudioVoiceHandle(0, 0);

        // The fade starts from silence, the factor is already there.
        rig.PumpOnly();
        VerifyMusicLevel(rig, voice, 0.25f, fade: 0f);

        rig.Tick(50);
        VerifyMusicLevel(rig, voice, 0.25f, fade: 0.5f, tolerance: 0.02f);

        rig.Tick(60);
        VerifyMusicLevel(rig, voice, 0.25f, fade: 1f);

        // A parameter change under the fade keeps following.
        rig.Service.SetGameParameter(Param, 0.6f);
        rig.Tick(2);
        VerifyMusicLevel(rig, voice, 0.6f, fade: 1f);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void ACrossfadedMusicTrack_KeepsTheBindingFactor(SpatialBackendKind kind)
    {
        using var rig = new MusicRig(kind);
        rig.Service.SetGameParameter(Param, 0.25f);
        var first = rig.Player.Play(rig.CreateMusic(false, VolumeBinding()));
        rig.Tick(5);

        var second = rig.Player.Crossfade(first, rig.CreateMusic(false, VolumeBinding()), 1f);
        Assert.True(second.IsValid);
        var secondVoice = new AudioVoiceHandle(1, 0);

        if (kind != SpatialBackendKind.Software)
        {
            rig.Tick(50);
            VerifyMusicLevel(rig, secondVoice, 0.25f, fade: 0.5f, tolerance: 0.02f);
        }

        // Once the old track is gone, the new one is alone at its factor.
        rig.Tick(120);
        Assert.False(rig.Player.IsAlive(first));
        VerifyMusicLevel(rig, secondVoice, 0.25f);

        rig.Service.SetGameParameter(Param, 0.5f);
        rig.Tick(2);
        VerifyMusicLevel(rig, secondVoice, 0.5f);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void AMusicTrack_StartsAtTheBoundPitch(SpatialBackendKind kind)
    {
        var expected = MathF.Pow(2f, 0.5f);

        if (kind == SpatialBackendKind.Software)
        {
            Assert.Equal(expected, MusicFirstBlockEnd(0.5f) / MusicFirstBlockEnd(null), 0.01f);
            return;
        }

        using var rig = new MusicRig(kind);
        rig.Service.SetGameParameter(Param, 0.5f);
        rig.Player.Play(rig.CreateMusic(true, PitchBinding()));

        Assert.Equal(1, rig.Recording.StartCount);

        if (kind == SpatialBackendKind.Fallback)
        {
            Assert.Equal(0.5f, rig.Recording.PitchAtStart, 1e-3f);
        }
        else
        {
            Assert.Equal(expected, rig.CapabilityBackend.ModulationAtStart.Rate, 1e-3f);
            Assert.Equal(0f, rig.Recording.PitchAtStart, 1e-4f);
        }
    }

    private static float MusicFirstBlockEnd(float? value)
    {
        using var rig = new MusicRig(SpatialBackendKind.Software);

        if (value.HasValue)
        {
            rig.Service.SetGameParameter(Param, value.Value);
        }

        var track = rig.Player.Play(rig.CreateMusic(true, PitchBinding()));
        Assert.True(track.IsValid);
        rig.PumpOnly();
        return rig.LastBlock[^2];
    }

    // ---- registry and allocation -------------------------------------------

    [Fact]
    public void TheSixtyFifthParameter_IsRefused()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);

        for (var i = 0; i < 64; i++)
        {
            Assert.Equal(i, rig.Service.GetGameParameterIndex("param" + i));
        }

        Assert.Equal(-1, rig.Service.GetGameParameterIndex("one-too-many"));
        Assert.Equal(-1, rig.Service.GetGameParameterIndex(string.Empty));
        Assert.Equal(3, rig.Service.GetGameParameterIndex("PARAM3"));

        rig.Service.SetGameParameter("one-too-many", 1f);
        rig.Service.SetGameParameter(-1, 1f);
        Assert.True(float.IsNaN(rig.Service.GetGameParameter(-1)));
        Assert.True(float.IsNaN(rig.Service.GetGameParameter(3)));
    }

    [Fact]
    public void SetGameParameterByName_AllocatesNothingOnceTheNameExists()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);
        rig.Service.GetGameParameterIndex("alpha");
        rig.Service.SetGameParameter("Speed", 0.5f);

        var before = AllocationWindow.Start();

        for (var i = 0; i < 100; i++)
        {
            rig.Service.SetGameParameter("SPEED", i);
            rig.Service.SetGameParameter("speed", i + 0.5f);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void UpdateWithSixtyFourBoundVoicesWhoseParameterChangesEveryFrame_AllocatesNothing(SpatialBackendKind kind)
    {
        const int voiceCount = 64;
        using var rig = new SpatialRig(kind, voiceCount);
        var asset = BoundAsset(rig, VolumeBinding(), PitchBinding("pitch"));
        var volumeIndex = rig.Service.GetGameParameterIndex(Param);
        var pitchIndex = rig.Service.GetGameParameterIndex("pitch");

        for (var i = 0; i < voiceCount; i++)
        {
            Assert.True(rig.Service.PlaySound(asset).IsValid);
        }

        for (var frame = 0; frame < 20; frame++)
        {
            rig.Service.SetGameParameter(volumeIndex, (frame % 10) / 10f);
            rig.Service.SetGameParameter(pitchIndex, (frame % 7) / 10f);
            rig.Tick();
        }

        var before = AllocationWindow.Start();

        for (var frame = 20; frame < 120; frame++)
        {
            rig.Service.SetGameParameter(volumeIndex, (frame % 10) / 10f);
            rig.Service.SetGameParameter(pitchIndex, (frame % 7) / 10f);
            rig.Service.Update(SpatialRig.Frame);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void AStillBoundVoice_SendsNothingMore()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Capability);
        rig.Service.SetGameParameter(Param, 0.5f);
        rig.Service.PlaySound(BoundAsset(rig, VolumeBinding()));
        rig.Tick(2);
        var sent = rig.Capability.VoiceSetCount;
        var volumes = rig.Fake.SetVolumeCount;
        var parameters = rig.Fake.SetParametersCount;

        rig.Tick(100);

        Assert.Equal(sent, rig.Capability.VoiceSetCount);
        Assert.Equal(volumes, rig.Fake.SetVolumeCount);
        Assert.Equal(parameters, rig.Fake.SetParametersCount);
    }
}
