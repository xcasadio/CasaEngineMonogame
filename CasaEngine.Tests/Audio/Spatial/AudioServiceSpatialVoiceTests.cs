using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Spatial;
using Xunit;
using Vector3 = System.Numerics.Vector3;

namespace CasaEngine.Tests.Audio.Spatial;

/// <summary>The backend a spatial scenario runs on: the software one (rendered level) or one of the two parameter-recording fakes.</summary>
public enum SpatialBackendKind
{
    /// <summary>The software backend, offline: the level is read from the rendered block.</summary>
    Software,

    /// <summary>The fake backend without the modulation capability: gain and pan are folded into the parameters sent.</summary>
    Fallback,

    /// <summary>A fake backend with the modulation capability: the modulation is recorded, the parameters stay the base ones.</summary>
    Capability,
}

/// <summary>
/// <see cref="IAudioBackend"/> and <see cref="IAudioVoiceModulationBackend"/> over a <see cref="FakeAudioBackend"/> that records
/// the modulation it receives, in order.
/// </summary>
internal sealed class ModulationFakeBackend : IAudioBackend, IAudioVoiceModulationBackend
{
    private readonly FakeAudioBackend _inner;
    private readonly Dictionary<int, (float Gain, float Pan, float Rate)> _current = new();
    private (float Gain, float Pan, float Rate) _pending = (1f, float.NaN, 1f);

    public ModulationFakeBackend(FakeAudioBackend inner)
    {
        _inner = inner;
    }

    /// <summary>Calls of <see cref="SetNextVoiceModulation"/>.</summary>
    public int NextSetCount { get; private set; }

    /// <summary>Calls of <see cref="SetVoiceModulation"/>.</summary>
    public int VoiceSetCount { get; private set; }

    /// <summary>The modulation the last <see cref="Play"/> took (neutral when none was set before it).</summary>
    public (float Gain, float Pan, float Rate) LastPlayModulation { get; private set; } = (1f, float.NaN, 1f);

    /// <summary>"Next" and "Play" in the order the calls arrived (a voice modulation is only counted, so a frame allocates nothing).</summary>
    public List<string> Events { get; } = new();

    public (float Gain, float Pan, float Rate) GetModulation(AudioVoiceHandle voice)
    {
        return _current.TryGetValue(voice.Index, out var value) ? value : (1f, float.NaN, 1f);
    }

    public void SetNextVoiceModulation(float gain, float pan, float rate)
    {
        NextSetCount++;
        Events.Add("Next");
        _pending = (gain, pan, rate);
    }

    public void SetVoiceModulation(AudioVoiceHandle voice, float gain, float pan, float rate)
    {
        VoiceSetCount++;
        _current[voice.Index] = (gain, pan, rate);
    }

    public bool IsAvailable => _inner.IsAvailable;

    public int VoiceCapacity => _inner.VoiceCapacity;

    public int ActiveVoiceCount => _inner.ActiveVoiceCount;

    public AudioVoiceHandle Play(IAudioClip clip, in AudioVoiceParameters parameters)
    {
        Events.Add("Play");
        LastPlayModulation = _pending;
        var taken = _pending;
        _pending = (1f, float.NaN, 1f);

        var handle = _inner.Play(clip, parameters);

        if (handle.IsValid)
        {
            _current[handle.Index] = taken;
        }

        return handle;
    }

    public void SetParameters(AudioVoiceHandle voice, in AudioVoiceParameters parameters) => _inner.SetParameters(voice, parameters);

    public void SetVolume(AudioVoiceHandle voice, float volume) => _inner.SetVolume(voice, volume);

    public AudioVoiceState GetState(AudioVoiceHandle voice) => _inner.GetState(voice);

    public void Pause(AudioVoiceHandle voice) => _inner.Pause(voice);

    public void Resume(AudioVoiceHandle voice) => _inner.Resume(voice);

    public void Stop(AudioVoiceHandle voice) => _inner.Stop(voice);

    public void Release(AudioVoiceHandle voice) => _inner.Release(voice);

    public void StopAll() => _inner.StopAll();

    public bool SupportsStreaming => _inner.SupportsStreaming;

    public AudioVoiceHandle CreateStreamingVoice(int sampleRate, int channelCount, in AudioVoiceParameters parameters)
        => _inner.CreateStreamingVoice(sampleRate, channelCount, parameters);

    public void SubmitBuffer(AudioVoiceHandle voice, byte[] buffer, int offset, int count) => _inner.SubmitBuffer(voice, buffer, offset, count);

    public int GetPendingBufferCount(AudioVoiceHandle voice) => _inner.GetPendingBufferCount(voice);

    public void Start(AudioVoiceHandle voice) => _inner.Start(voice);

    public void Dispose() => _inner.Dispose();
}

/// <summary>
/// One <see cref="AudioService"/> on one backend kind, with its way to advance the audio (pumping the offline output, or
/// nothing for the fakes) and the expected values recomputed from the pan laws and the distance formulas. One tick is one
/// game frame of 10 ms and one audio block of 480 frames.
/// </summary>
internal sealed class SpatialRig : IDisposable
{
    public const int Block = 480;
    public const float Frame = 0.01f;

    private readonly OfflineAudioOutput _output;
    private readonly bool _stereoClip;

    public SpatialRig(SpatialBackendKind kind, int capacity = 16, bool stereoClip = false)
    {
        Kind = kind;
        _stereoClip = stereoClip;

        switch (kind)
        {
            case SpatialBackendKind.Software:
                _output = new OfflineAudioOutput();
                Service = new AudioService(new SoftwareAudioBackend(_output, capacity));
                Service.MasterLimiter.IsEnabled = false;
                break;
            case SpatialBackendKind.Fallback:
                Fake = new FakeAudioBackend(capacity);
                Service = new AudioService(Fake);
                break;
            default:
                Fake = new FakeAudioBackend(capacity);
                Capability = new ModulationFakeBackend(Fake);
                Service = new AudioService(Capability);
                break;
        }

        Provider = new FakeAudioClipProvider();
        Service.ClipProvider = Provider;
    }

    public SpatialBackendKind Kind { get; }

    public AudioService Service { get; }

    public FakeAudioBackend Fake { get; }

    public ModulationFakeBackend Capability { get; }

    public FakeAudioClipProvider Provider { get; }

    public object ListenerSource { get; } = new();

    public void Tick(int count = 1)
    {
        for (var i = 0; i < count; i++)
        {
            Service.Update(Frame);
            _output?.Pump(Block);
        }
    }

    public void PumpOnly()
    {
        _output?.Pump(Block);
    }

    public ReadOnlySpan<float> LastBlock => _output.LastBlock;

    public IAudioClip CreateClip()
    {
        if (Kind != SpatialBackendKind.Software)
        {
            return new FakeAudioClip();
        }

        var channels = _stereoClip ? 2 : 1;
        var samples = new short[400000 * channels];
        Array.Fill(samples, (short)16384);
        return new PcmAudioClip(samples, 48000, channels);
    }

    public AudioVoiceHandle PlayPlain()
    {
        return Service.PlayClip(CreateClip(), AudioBusNames.Sfx, AudioVoiceParameters.Default);
    }

    /// <summary>A sound of the given spatial mode; reference distance 1, no maximum, rolloff 1 and the inverse clamped model unless told.</summary>
    public SoundAsset CreateAsset(
        AudioSpatialMode mode = AudioSpatialMode.Spatial3D,
        AudioDistanceModel model = AudioDistanceModel.InverseDistanceClamped,
        float referenceDistance = 1f,
        float maxDistance = float.MaxValue,
        float rolloff = 1f)
    {
        return new SoundAsset
        {
            Name = "spatial",
            AudioFileAssetId = Provider.Register(CreateClip()),
            SpatialMode = mode,
            DistanceModel = model,
            ReferenceDistance = referenceDistance,
            MaxDistance = maxDistance,
            RolloffFactor = rolloff,
        };
    }

    public AudioVoiceHandle PlayAt(SoundAsset asset, Vector3 position)
    {
        return Service.PlaySoundAt(asset, position, SoundPlaybackOverrides.None);
    }

    public void SetListener(Vector3 position)
    {
        Service.SetListener(ListenerSource, AudioListenerPose.Create(position, new Vector3(0f, 0f, -1f), Vector3.UnitY));
    }

    /// <summary>Gain of the default model (inverse clamped, reference 1, rolloff 1) at <paramref name="distance"/>.</summary>
    public static float Inverse(float distance) => distance <= 1f ? 1f : 1f / distance;

    /// <summary>
    /// Checks one voice against the expected spatial <paramref name="gain"/> and <paramref name="pan"/> (NaN: none, the own
    /// pan <paramref name="basePan"/> applies), on the volume the caller asked for and the gain of the Sfx bus right now.
    /// Software: the rendered left and right levels, recomputed from the pan law. Fallback: the parameters received.
    /// Capability: the modulation received, with the parameters left at their base values.
    /// </summary>
    public void Verify(AudioVoiceHandle voice, float gain, float pan, float basePan = 0f, float tolerance = 0.01f)
    {
        var volume = Service.GetVoiceVolume(voice);
        var bus = Service.Mixer.GetEffectiveGain(AudioBusNames.Sfx);
        var effectivePan = float.IsNaN(pan) ? basePan : pan;

        switch (Kind)
        {
            case SpatialBackendKind.Software:
            {
                var block = LastBlock;
                var left = block[^2];
                var right = block[^1];
                var level = 0.5f * volume * bus * gain;

                float expectedLeft;
                float expectedRight;

                if (_stereoClip)
                {
                    expectedLeft = level * (effectivePan > 0f ? 1f - effectivePan : 1f);
                    expectedRight = level * (effectivePan < 0f ? 1f + effectivePan : 1f);
                }
                else
                {
                    expectedLeft = level * (float)Math.Cos((effectivePan + 1.0) * Math.PI / 4.0);
                    expectedRight = level * (float)Math.Sin((effectivePan + 1.0) * Math.PI / 4.0);
                }

                Assert.Equal(expectedLeft, left, tolerance);
                Assert.Equal(expectedRight, right, tolerance);
                break;
            }

            case SpatialBackendKind.Fallback:
            {
                var received = Fake.GetParameters(voice);

                Assert.Equal(volume * bus * gain, received.Volume, tolerance);
                Assert.Equal(effectivePan, received.Pan, tolerance);
                break;
            }

            default:
            {
                var received = Fake.GetParameters(voice);
                var modulation = Capability.GetModulation(voice);

                // The parameters are the base ones: nothing spatial is folded into them.
                Assert.Equal(volume * bus, received.Volume, 1e-3f);
                Assert.Equal(basePan, received.Pan, 1e-3f);
                Assert.Equal(gain, modulation.Gain, 0.0015f);
                Assert.Equal(float.IsNaN(pan), float.IsNaN(modulation.Pan));

                if (!float.IsNaN(pan))
                {
                    Assert.Equal(pan, modulation.Pan, 0.0025f);
                }

                Assert.Equal(1f, modulation.Rate);
                break;
            }
        }
    }

    public void Dispose()
    {
        Service.Dispose();
    }
}

/// <summary>
/// Listener-relative gain and pan of the voices of <see cref="AudioService"/> (plan T9.4, decisions P32, P35, P36, P38, P44),
/// on the software backend (rendered level) and on the fallback (parameters received), compared with the same values
/// recomputed in the test.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class AudioServiceSpatialVoiceTests
{
    private static readonly Vector3 Ahead2 = new(0f, 0f, -2f);
    private static readonly Vector3 Ahead4 = new(0f, 0f, -4f);

    // ---- start values and the plain model ----------------------------------

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void ASourceAtTwiceTheReferenceDistance_PlaysAtHalfGainOnBothSides(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        rig.SetListener(Vector3.Zero);
        var voice = rig.PlayAt(rig.CreateAsset(), Ahead2);
        rig.Tick(2);

        rig.Verify(voice, 0.5f, 0f);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void AFarSound_IsNeverAtFullGain_EvenInTheFirstBlock(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        rig.SetListener(Vector3.Zero);
        var voice = rig.PlayAt(rig.CreateAsset(), new Vector3(0f, 0f, -10f));

        switch (kind)
        {
            case SpatialBackendKind.Software:
                // No Update yet: the first block comes with the values the voice started with.
                rig.PumpOnly();
                foreach (var sample in rig.LastBlock)
                {
                    Assert.True(MathF.Abs(sample) < 0.5f * 0.1f * 0.7072f + 0.002f, $"sample {sample}");
                }

                break;
            case SpatialBackendKind.Fallback:
                Assert.Equal(0.1f, rig.Fake.GetParameters(voice).Volume, 1e-4f);
                break;
            default:
                Assert.Equal(new[] { "Next", "Play" }, rig.Capability.Events.ToArray());
                Assert.Equal(0.1f, rig.Capability.LastPlayModulation.Gain, 1e-4f);
                Assert.Equal(0f, rig.Capability.LastPlayModulation.Pan, 1e-4f);
                break;
        }

        rig.Tick(2);
        rig.Verify(voice, 0.1f, 0f);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void TheDistanceModelsAndRolloffOfTheAssetApply(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        rig.SetListener(Vector3.Zero);

        // Linear clamped: 1 - rolloff * (d - ref) / (max - ref) = 1 - 1 * (3 - 1) / (5 - 1).
        var linear = rig.PlayAt(rig.CreateAsset(model: AudioDistanceModel.LinearDistanceClamped, maxDistance: 5f), new Vector3(0f, 0f, -3f));
        rig.Tick(2);
        rig.Verify(linear, 0.5f, 0f);

        rig.Service.Stop(linear);

        // Inverse with rolloff 2: ref / (ref + rolloff * (d - ref)) = 1 / (1 + 2 * 1) at d = 2.
        var rolled = rig.PlayAt(rig.CreateAsset(rolloff: 2f), Ahead2);
        rig.Tick(2);
        rig.Verify(rolled, 1f / 3f, 0f);
    }

    // ---- pan --------------------------------------------------------------

    [Theory]
    [InlineData(SpatialBackendKind.Software, false)]
    [InlineData(SpatialBackendKind.Software, true)]
    [InlineData(SpatialBackendKind.Fallback, false)]
    [InlineData(SpatialBackendKind.Capability, false)]
    public void ThePanFollowsTheDirection_OnAMonoAndOnAStereoClip(SpatialBackendKind kind, bool stereoClip)
    {
        using var rig = new SpatialRig(kind, stereoClip: stereoClip);
        rig.SetListener(Vector3.Zero);

        // Distance 5, direction (3, 0, -4) / 5: gain 0.2, pan = dot with the right vector (+X) = 0.6.
        var voice = rig.PlayAt(rig.CreateAsset(), new Vector3(3f, 0f, -4f));
        rig.Tick(2);
        rig.Verify(voice, 0.2f, 0.6f);

        rig.Service.SetVoicePosition(voice, new Vector3(-3f, 0f, -4f));
        rig.Tick(2);
        rig.Verify(voice, 0.2f, -0.6f);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void TheSpatialPanReplacesTheBasePan(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        var asset = rig.CreateAsset();

        // Base pan 0.8 given at the call; without a listener it is the pan of the voice.
        var voice = rig.Service.PlaySoundAt(asset, Ahead2, new SoundPlaybackOverrides(pan: 0.8f));
        rig.Tick(2);
        rig.Verify(voice, 1f, float.NaN, basePan: 0.8f);

        // With the listener the spatial pan (0, straight ahead) replaces it on both paths.
        rig.SetListener(Vector3.Zero);
        rig.Tick(2);
        rig.Verify(voice, 0.5f, 0f, basePan: 0.8f);

        // SetVoicePan changes the base pan: the spatial one still wins.
        rig.Service.SetVoicePan(voice, -0.5f);
        rig.Tick(2);
        rig.Verify(voice, 0.5f, 0f, basePan: -0.5f);

        // The listener goes: the base pan is back.
        rig.Service.RemoveListener(rig.ListenerSource);
        rig.Tick(2);
        rig.Verify(voice, 1f, float.NaN, basePan: -0.5f);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void TheTwoDimensionalModeIgnoresZ_TheThreeDimensionalOneDoesNot(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        rig.SetListener(Vector3.Zero);

        // Distance on X/Y is 2 whatever Z is, and the pan is the X component of the direction projected on X/Y: 1.
        var flat = rig.PlayAt(rig.CreateAsset(AudioSpatialMode.Spatial2D), new Vector3(2f, 0f, 50f));
        rig.Tick(2);
        rig.Verify(flat, 0.5f, 1f);

        // One voice at a time: the software backend renders them summed.
        rig.Service.Stop(flat);
        var deep = rig.PlayAt(rig.CreateAsset(AudioSpatialMode.Spatial3D), new Vector3(2f, 0f, 50f));
        rig.Tick(2);

        var distance = MathF.Sqrt((2f * 2f) + (50f * 50f));
        rig.Verify(deep, 1f / distance, 2f / distance);
    }

    // ---- P38: no position, no spatialization ------------------------------

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void ASpatialSoundPlayedWithoutAPosition_IsNotSpatial(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        rig.SetListener(new Vector3(0f, 0f, 30f));
        var voice = rig.Service.PlaySound(rig.CreateAsset());
        rig.Tick(3);

        rig.Verify(voice, 1f, float.NaN);

        if (kind == SpatialBackendKind.Capability)
        {
            Assert.Equal(0, rig.Capability.NextSetCount);
            Assert.Equal(0, rig.Capability.VoiceSetCount);
        }

        // The mode is kept off for good, not just until the first frame: moving the listener changes nothing.
        rig.SetListener(new Vector3(40f, 0f, 0f));
        rig.Tick(3);
        rig.Verify(voice, 1f, float.NaN);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void APositionGivenLaterToASpatialSound_SpatializesIt_AndANonSpatialAssetStaysPlain(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        rig.SetListener(Vector3.Zero);

        // One voice at a time: the software backend renders them summed.
        var spatial = rig.Service.PlaySound(rig.CreateAsset());
        rig.Tick(2);
        rig.Verify(spatial, 1f, float.NaN);

        rig.Service.SetVoicePosition(spatial, Ahead4);
        rig.Tick(2);
        rig.Verify(spatial, 0.25f, 0f);

        rig.Service.Stop(spatial);
        var plain = rig.PlayAt(rig.CreateAsset(AudioSpatialMode.None), Ahead4);
        rig.Tick(2);
        rig.Verify(plain, 1f, float.NaN);

        rig.Service.SetVoicePosition(plain, new Vector3(0f, 0f, -8f));
        rig.Tick(2);
        rig.Verify(plain, 1f, float.NaN);
    }

    // ---- P35: the listener is read every frame ----------------------------

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void PlaySoundAtWithNoListener_PlaysNeutral_ThenFollowsTheListenerThatComesAndGoes(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        var voice = rig.PlayAt(rig.CreateAsset(), new Vector3(3f, 0f, -4f));
        rig.Tick(3);

        rig.Verify(voice, 1f, float.NaN);

        if (kind == SpatialBackendKind.Capability)
        {
            Assert.Equal(0, rig.Capability.NextSetCount);
            Assert.Equal(0, rig.Capability.VoiceSetCount);
        }

        rig.SetListener(Vector3.Zero);
        rig.Tick(2);
        rig.Verify(voice, 0.2f, 0.6f);

        rig.Service.RemoveListener(rig.ListenerSource);
        rig.Tick(2);
        rig.Verify(voice, 1f, float.NaN);
    }

    // ---- S4 orders --------------------------------------------------------

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void AVolumeSetAndAMoveInTheSameFrame_GiveTheProduct_InEitherOrder(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        rig.SetListener(Vector3.Zero);
        var voice = rig.PlayAt(rig.CreateAsset(), Ahead2);
        rig.Tick(2);

        rig.Service.SetVoiceVolume(voice, 0.6f);
        rig.Service.SetVoicePosition(voice, Ahead4);
        rig.Tick(2);
        Assert.Equal(0.6f, rig.Service.GetVoiceVolume(voice), 1e-5f);
        rig.Verify(voice, 0.25f, 0f);

        rig.Service.SetVoicePosition(voice, Ahead2);
        rig.Service.SetVoiceVolume(voice, 0.8f);
        rig.Tick(2);
        Assert.Equal(0.8f, rig.Service.GetVoiceVolume(voice), 1e-5f);
        rig.Verify(voice, 0.5f, 0f);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void AFadeInProgressWhileTheDistanceChanges_IsTheChronologyTimesTheGain_WithinABlock(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        rig.SetListener(Vector3.Zero);
        var voice = rig.PlayAt(rig.CreateAsset(), Ahead2);
        rig.Tick(2);

        rig.Service.FadeVoice(voice, 0.2f, 1f);

        for (var tick = 1; tick <= 110; tick++)
        {
            var distance = 2f + (tick * 0.05f);
            rig.Service.SetVoicePosition(voice, new Vector3(0f, 0f, -distance));
            rig.Tick();

            rig.Verify(voice, SpatialRig.Inverse(distance), 0f, tolerance: 0.02f);
        }

        Assert.False(rig.Service.IsFading(voice));
        Assert.Equal(0.2f, rig.Service.GetVoiceVolume(voice), 1e-4f);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software, true)]
    [InlineData(SpatialBackendKind.Software, false)]
    [InlineData(SpatialBackendKind.Fallback, true)]
    [InlineData(SpatialBackendKind.Fallback, false)]
    [InlineData(SpatialBackendKind.Capability, true)]
    [InlineData(SpatialBackendKind.Capability, false)]
    public void AFadeAndAMoveInTheSameFrame_InEitherOrder_EndAtTheTargetTimesTheGain(SpatialBackendKind kind, bool fadeFirst)
    {
        using var rig = new SpatialRig(kind);
        rig.SetListener(Vector3.Zero);
        var voice = rig.PlayAt(rig.CreateAsset(), Ahead2);
        rig.Tick(2);

        if (fadeFirst)
        {
            rig.Service.FadeVoice(voice, 0.4f, 0.5f);
            rig.Service.SetVoicePosition(voice, Ahead4);
        }
        else
        {
            rig.Service.SetVoicePosition(voice, Ahead4);
            rig.Service.FadeVoice(voice, 0.4f, 0.5f);
        }

        rig.Tick(20);
        rig.Verify(voice, 0.25f, 0f, tolerance: 0.02f);

        rig.Tick(50);
        Assert.False(rig.Service.IsFading(voice));
        Assert.Equal(0.4f, rig.Service.GetVoiceVolume(voice), 1e-4f);
        rig.Verify(voice, 0.25f, 0f);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void CancelFadeAndAMoveInTheSameFrame_KeepTheValueReachedTimesTheNewGain(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        rig.SetListener(Vector3.Zero);
        var voice = rig.PlayAt(rig.CreateAsset(), Ahead2);
        rig.Tick(2);

        rig.Service.FadeVoice(voice, 0f, 1f);
        rig.Tick(30);

        rig.Service.CancelFade(voice);
        rig.Service.SetVoicePosition(voice, Ahead4);
        var reached = rig.Service.GetVoiceVolume(voice);
        rig.Tick(5);

        Assert.False(rig.Service.IsFading(voice));
        Assert.Equal(reached, rig.Service.GetVoiceVolume(voice), 1e-5f);
        Assert.InRange(reached, 0.6f, 0.8f);
        rig.Verify(voice, 0.25f, 0f);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void AVolumeSetThenCancelFadeAndAMoveInTheSameFrame_UseTheVolumeSet(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        rig.SetListener(Vector3.Zero);
        var voice = rig.PlayAt(rig.CreateAsset(), Ahead2);
        rig.Tick(2);

        rig.Service.FadeVoice(voice, 0f, 1f);
        rig.Tick(30);

        rig.Service.SetVoiceVolume(voice, 0.9f);
        rig.Service.CancelFade(voice);
        rig.Service.SetVoicePosition(voice, Ahead4);
        rig.Tick(5);

        Assert.Equal(0.9f, rig.Service.GetVoiceVolume(voice), 1e-5f);
        rig.Verify(voice, 0.25f, 0f);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void StopWithFadeWhileTheDistanceChanges_FollowsTheChronology_AndReleasesTheVoice(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        rig.SetListener(Vector3.Zero);
        var voice = rig.PlayAt(rig.CreateAsset(), Ahead2);
        rig.Tick(2);

        rig.Service.StopWithFade(voice, 0.3f);
        rig.Service.SetVoicePosition(voice, Ahead4);

        for (var tick = 1; tick <= 25; tick++)
        {
            rig.Tick();
            rig.Verify(voice, 0.25f, 0f, tolerance: 0.03f);
        }

        rig.Tick(10);

        Assert.False(rig.Service.IsAlive(voice));
        Assert.Equal(0, rig.Service.ActiveVoiceCount);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void ABusMuteAndAMoveInTheSameFrame_SilenceTheVoice_AndTheUnmuteBringsTheSpatialLevelBack(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        rig.SetListener(Vector3.Zero);
        var voice = rig.PlayAt(rig.CreateAsset(), Ahead2);
        rig.Tick(2);
        Assert.True(rig.Service.Mixer.TryGetBus(AudioBusNames.Sfx, out var bus));

        bus.IsMuted = true;
        rig.Service.SetVoicePosition(voice, Ahead4);
        rig.Tick(3);
        rig.Verify(voice, 0.25f, 0f);

        bus.IsMuted = false;
        rig.Service.SetVoicePosition(voice, Ahead2);
        rig.Tick(3);
        rig.Verify(voice, 0.5f, 0f);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void ABusFadeDuringASpatialSound_ComposesWithTheDistanceGain(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        rig.SetListener(Vector3.Zero);
        var voice = rig.PlayAt(rig.CreateAsset(), Ahead2);
        rig.Tick(2);

        rig.Service.FadeBus(AudioBusNames.Sfx, 0.2f, 1f);

        for (var tick = 1; tick <= 110; tick++)
        {
            var distance = 2f + (tick * 0.04f);
            rig.Service.SetVoicePosition(voice, new Vector3(0f, 0f, -distance));
            rig.Tick();

            rig.Verify(voice, SpatialRig.Inverse(distance), 0f, tolerance: 0.02f);
        }
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void ASnapshotAppliedDuringASpatialSound_ComposesWithTheDistanceGain(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        rig.SetListener(Vector3.Zero);
        var voice = rig.PlayAt(rig.CreateAsset(), Ahead2);
        rig.Tick(2);
        Assert.True(rig.Service.Mixer.TryGetBus(AudioBusNames.Sfx, out var bus));

        var snapshot = rig.Service.CaptureSnapshot();
        bus.Volume = 0.3f;
        rig.Tick(2);
        rig.Verify(voice, 0.5f, 0f);

        rig.Service.ApplySnapshot(snapshot, 1f);

        for (var tick = 1; tick <= 110; tick++)
        {
            var distance = 2f + (tick * 0.04f);
            rig.Service.SetVoicePosition(voice, new Vector3(0f, 0f, -distance));
            rig.Tick();

            rig.Verify(voice, SpatialRig.Inverse(distance), 0f, tolerance: 0.02f);
        }

        Assert.Equal(1f, bus.Volume, 1e-4f);
    }

    [Fact]
    public void OnTheFallback_AMixerChangeAndAGainChangeInTheSameFrame_PushTheVolumeOnce()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);
        rig.SetListener(Vector3.Zero);
        var voice = rig.PlayAt(rig.CreateAsset(), Ahead2);
        rig.Tick(3);
        Assert.True(rig.Service.Mixer.TryGetBus(AudioBusNames.Sfx, out var bus));

        var volumes = rig.Fake.SetVolumeCount;
        var parameters = rig.Fake.SetParametersCount;

        bus.Volume = 0.5f;
        rig.Service.SetVoicePosition(voice, Ahead4);
        rig.Tick();

        Assert.Equal(1, rig.Fake.SetVolumeCount - volumes);
        Assert.Equal(0, rig.Fake.SetParametersCount - parameters);
        Assert.Equal(0.5f * 0.25f, rig.Fake.GetParameters(voice).Volume, 1e-4f);
    }

    [Fact]
    public void OnTheFallback_AFadingVoiceWhoseGainChanges_IsPushedByItsFadeOnly()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);
        rig.SetListener(Vector3.Zero);
        var voice = rig.PlayAt(rig.CreateAsset(), Ahead2);
        rig.Service.FadeVoice(voice, 0.1f, 1f);
        rig.Tick(3);

        var volumes = rig.Fake.SetVolumeCount;
        var parameters = rig.Fake.SetParametersCount;

        rig.Service.SetVoicePosition(voice, Ahead4);
        rig.Tick();

        Assert.Equal(1, rig.Fake.SetVolumeCount - volumes);
        Assert.Equal(0, rig.Fake.SetParametersCount - parameters);
    }

    // ---- refusal and slot reuse -------------------------------------------

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void ARefusedSpatialPlay_LeaksNothingToTheNextPlainClip(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind, capacity: 1);
        rig.SetListener(Vector3.Zero);
        var holder = rig.PlayPlain();
        rig.Tick(2);

        var refused = rig.PlayAt(rig.CreateAsset(), new Vector3(3f, 0f, -40f));

        Assert.False(refused.IsValid);
        Assert.Equal(1, rig.Service.RefusedVoiceCount);

        rig.Service.Stop(holder);
        var plain = rig.PlayPlain();
        rig.Tick(3);

        rig.Verify(plain, 1f, float.NaN);

        if (kind == SpatialBackendKind.Capability)
        {
            Assert.Equal(1f, rig.Capability.LastPlayModulation.Gain);
            Assert.True(float.IsNaN(rig.Capability.LastPlayModulation.Pan));
            Assert.Equal(0, rig.Capability.VoiceSetCount);
        }
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void ASlotOfAStoppedSpatialVoice_TakenByAPlainClipInTheSameFrame_PlaysAtFullGainAndSendsNothing(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind, capacity: 1);
        rig.SetListener(Vector3.Zero);
        var spatial = rig.PlayAt(rig.CreateAsset(), new Vector3(3f, 0f, -40f));
        rig.Tick(3);

        rig.Service.Stop(spatial);
        var plain = rig.PlayPlain();

        Assert.Equal(spatial.Index, plain.Index);

        var nextSets = rig.Capability?.NextSetCount ?? 0;
        var voiceSets = rig.Capability?.VoiceSetCount ?? 0;
        var parameters = rig.Fake?.SetParametersCount ?? 0;
        var volumes = rig.Fake?.SetVolumeCount ?? 0;

        rig.Tick(5);

        rig.Verify(plain, 1f, float.NaN);

        if (kind == SpatialBackendKind.Capability)
        {
            Assert.Equal(nextSets, rig.Capability.NextSetCount);
            Assert.Equal(voiceSets, rig.Capability.VoiceSetCount);
        }

        if (kind != SpatialBackendKind.Software)
        {
            Assert.Equal(parameters, rig.Fake.SetParametersCount);
            Assert.Equal(volumes, rig.Fake.SetVolumeCount);
            Assert.Equal(1f, rig.Fake.GetParameters(plain).Volume, 1e-5f);
        }
    }

    // ---- P44: only what changes is sent -----------------------------------

    [Theory]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void OnlyWhatChangesIsSent_AStillSoundCostsNothing_AndAChangeBelowTheThresholdIsNotSent(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        rig.SetListener(Vector3.Zero);
        var voice = rig.PlayAt(rig.CreateAsset(), Ahead2);
        rig.Tick(3);

        int Sent() => kind == SpatialBackendKind.Fallback
            ? rig.Fake.SetParametersCount + rig.Fake.SetVolumeCount
            : rig.Capability.VoiceSetCount + rig.Capability.NextSetCount;

        var settled = Sent();

        for (var frame = 0; frame < 100; frame++)
        {
            // The same pose and position every frame, and a nudge far below the thresholds.
            rig.SetListener(Vector3.Zero);
            rig.Service.SetVoicePosition(voice, new Vector3(0f, 0f, frame % 2 == 0 ? -2f : -2.0004f));
            rig.Tick();
        }

        Assert.Equal(settled, Sent());

        // A real move sends once, then stays quiet again.
        rig.Service.SetVoicePosition(voice, Ahead4);
        rig.Tick();
        var moved = Sent();
        Assert.True(moved > settled);
        rig.Tick(20);
        Assert.Equal(moved, Sent());
        rig.Verify(voice, 0.25f, 0f);
    }

    [Fact]
    public void WithTheCapability_AMoveSendsTheModulationAndNeverAVolume()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Capability);
        rig.SetListener(Vector3.Zero);
        var voice = rig.PlayAt(rig.CreateAsset(), Ahead2);
        rig.Tick(3);

        var volumes = rig.Fake.SetVolumeCount;
        var parameters = rig.Fake.SetParametersCount;

        for (var tick = 1; tick <= 30; tick++)
        {
            rig.Service.SetVoicePosition(voice, new Vector3(tick * 0.1f, 0f, -2f - (tick * 0.1f)));
            rig.Tick();
        }

        Assert.Equal(volumes, rig.Fake.SetVolumeCount);
        Assert.Equal(parameters, rig.Fake.SetParametersCount);
        Assert.True(rig.Capability.VoiceSetCount > 0);
        Assert.Equal(1f, rig.Service.GetVoiceVolume(voice));
    }

    // ---- P35: stale and unusual handles ------------------------------------

    [Fact]
    public void SetVoicePositionOnAStaleHandle_IsIgnored()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);
        rig.SetListener(Vector3.Zero);
        var voice = rig.PlayAt(rig.CreateAsset(), Ahead2);
        rig.Service.Stop(voice);

        rig.Service.SetVoicePosition(voice, Ahead4);
        rig.Service.SetVoicePosition(AudioVoiceHandle.None, Ahead4);
        rig.Tick(2);

        Assert.False(rig.Service.IsAlive(voice));
    }

    [Fact]
    public void AStreamAndAStereoVoice_AreNeverSpatialized()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Software);
        rig.SetListener(new Vector3(0f, 0f, 100f));
        var stream = rig.Service.PlayStream(48000, 2, AudioBusNames.Music, AudioVoiceParameters.Default);
        var stereo = rig.Service.PlayClipStereo(rig.CreateClip(), AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 1f);

        Assert.True(stream.IsValid);
        Assert.True(stereo.IsValid);

        rig.Service.SetVoicePosition(stream, Ahead4);
        rig.Service.SetVoicePosition(stereo, Ahead4);
        rig.Tick(3);

        // A voice with no spatial mode keeps its left and right gains: the stereo voice renders at full level.
        Assert.Equal(0.5f, rig.LastBlock[^2], 0.01f);
        Assert.Equal(0.5f, rig.LastBlock[^1], 0.01f);
    }

    // ---- zero allocation ------------------------------------------

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void UpdateWithSixtyFourSpatialVoicesMovedEveryFrame_AllocatesNothing(SpatialBackendKind kind)
    {
        const int voiceCount = 64;
        using var rig = new SpatialRig(kind, voiceCount);
        var asset = rig.CreateAsset();
        var voices = new AudioVoiceHandle[voiceCount];
        rig.SetListener(Vector3.Zero);

        for (var i = 0; i < voiceCount; i++)
        {
            voices[i] = rig.PlayAt(asset, new Vector3(2f + (i * 0.1f), 0f, -3f));
            Assert.True(voices[i].IsValid);
        }

        for (var frame = 0; frame < 20; frame++)
        {
            MoveEverything(rig, voices, frame);
            rig.Tick();
        }

        var sentBefore = SentCount(rig);
        var before = AllocationWindow.Start();

        for (var frame = 20; frame < 120; frame++)
        {
            MoveEverything(rig, voices, frame);
            rig.Service.Update(SpatialRig.Frame);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);

        if (kind != SpatialBackendKind.Software)
        {
            Assert.True(SentCount(rig) > sentBefore + 1000, "the moves must actually be sent");
        }
    }

    private static int SentCount(SpatialRig rig)
    {
        return rig.Kind switch
        {
            SpatialBackendKind.Fallback => rig.Fake.SetParametersCount + rig.Fake.SetVolumeCount,
            SpatialBackendKind.Capability => rig.Capability.VoiceSetCount,
            _ => 0,
        };
    }

    private static void MoveEverything(SpatialRig rig, AudioVoiceHandle[] voices, int frame)
    {
        for (var i = 0; i < voices.Length; i++)
        {
            var phase = (frame * 0.3f) + i;
            rig.Service.SetVoicePosition(voices[i], new Vector3(3f * MathF.Sin(phase), 0.5f, -4f - (2f * MathF.Cos(phase))));
        }

        rig.SetListener(new Vector3(0.2f * MathF.Sin(frame * 0.1f), 0f, 0f));
    }
}
