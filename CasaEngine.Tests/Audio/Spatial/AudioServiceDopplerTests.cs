using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Spatial;
using Xunit;
using Vector3 = System.Numerics.Vector3;

namespace CasaEngine.Tests.Audio.Spatial;

/// <summary>
/// Doppler effect of the spatial voices of <see cref="AudioService"/> and the settable voice pitch (plan T9.5, decisions P33
/// and P37): software backend (playback speed measured on a ramp clip), fallback (pitch received) and capability (rate
/// received).
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class AudioServiceDopplerTests
{
    private const float SpeedOfSound = 343.3f;
    private const float Speed = 34.33f;
    private const float Approach = SpeedOfSound / (SpeedOfSound - Speed);

    private static readonly Vector3 Far = new(0f, 0f, -100f);

    private static SoundAsset CreateDopplerAsset(SpatialRig rig, float dopplerFactor, bool ramp = false)
    {
        IAudioClip clip = rig.CreateClip();

        if (ramp && rig.Kind == SpatialBackendKind.Software)
        {
            var samples = new short[400000];

            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] = (short)(i / 16);
            }

            clip = new PcmAudioClip(samples, 48000, 1);
        }

        return new SoundAsset
        {
            Name = "doppler",
            AudioFileAssetId = rig.Provider.Register(clip),
            SpatialMode = AudioSpatialMode.Spatial3D,
            DistanceModel = AudioDistanceModel.None,
            DopplerFactor = dopplerFactor,
        };
    }

    /// <summary>One frame: the source and the listener are pushed (positions at time <paramref name="frame"/>), then the audio advances.</summary>
    private static void Step(SpatialRig rig, AudioVoiceHandle voice, int frame, float sourceSpeedZ, float listenerSpeedZ, bool pushSource = true)
    {
        var time = frame * SpatialRig.Frame;

        if (pushSource)
        {
            rig.Service.SetVoicePosition(voice, Far + new Vector3(0f, 0f, sourceSpeedZ * time));
        }

        rig.SetListener(new Vector3(0f, 0f, -listenerSpeedZ * time));
        rig.Tick();
    }

    private static float Played(SpatialRig rig) => rig.LastBlock[^2];

    /// <summary>Level progression of the ramp over <paramref name="frames"/> frames after a warm-up.</summary>
    private static float RampProgress(float sourceSpeedZ, float listenerSpeedZ, float dopplerFactor)
    {
        using var rig = new SpatialRig(SpatialBackendKind.Software);
        rig.Service.SpeedOfSound = SpeedOfSound;
        var voice = rig.Service.PlaySoundAt(CreateDopplerAsset(rig, dopplerFactor, ramp: true), Far, SoundPlaybackOverrides.None);
        Assert.True(voice.IsValid);

        for (var frame = 1; frame <= 5; frame++)
        {
            Step(rig, voice, frame, sourceSpeedZ, listenerSpeedZ);
        }

        var start = Played(rig);

        for (var frame = 6; frame <= 25; frame++)
        {
            Step(rig, voice, frame, sourceSpeedZ, listenerSpeedZ);
        }

        return Played(rig) - start;
    }

    [Fact]
    public void SpeedOfSound_DefaultsToTheSpecificationValue_AndIgnoresInvalidValues()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);

        Assert.Equal(AudioDoppler.DefaultSpeedOfSound, rig.Service.SpeedOfSound);

        rig.Service.SpeedOfSound = 100f;
        Assert.Equal(100f, rig.Service.SpeedOfSound);

        rig.Service.SpeedOfSound = float.NaN;
        rig.Service.SpeedOfSound = 0f;
        rig.Service.SpeedOfSound = -5f;
        Assert.Equal(100f, rig.Service.SpeedOfSound);
    }

    [Fact]
    public void SoftwareBackend_ApproachingSource_PlaysFasterByTheDopplerRatio()
    {
        var baseline = RampProgress(Speed, 0f, 0f);
        var doppler = RampProgress(Speed, 0f, 1f);

        Assert.True(baseline > 0f);
        Assert.Equal(Approach, doppler / baseline, 0.002f);
    }

    [Fact]
    public void SoftwareBackend_DopplerOffByDefault_PlaysAtNormalSpeed()
    {
        var still = RampProgress(0f, 0f, 0f);
        var moving = RampProgress(Speed, 0f, 0f);

        Assert.Equal(1f, moving / still, 0.002f);
    }

    [Fact]
    public void SoftwareBackend_ListenerMovingTowardAStillSource_PlaysFaster()
    {
        var baseline = RampProgress(0f, 0f, 0f);
        var doppler = RampProgress(0f, Speed, 1f);

        Assert.Equal(1.1f, doppler / baseline, 0.002f);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void DopplerOffByDefault_NeutralRateWhateverTheMotion(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        rig.Service.SpeedOfSound = SpeedOfSound;
        var voice = rig.Service.PlaySoundAt(CreateDopplerAsset(rig, 0f), Far, SoundPlaybackOverrides.None);

        for (var frame = 1; frame <= 20; frame++)
        {
            Step(rig, voice, frame, Speed, 0f);
        }

        AssertRatio(rig, voice, 1f);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void ApproachingSource_SendsTheDopplerRatio(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        rig.Service.SpeedOfSound = SpeedOfSound;
        var voice = rig.Service.PlaySoundAt(CreateDopplerAsset(rig, 1f), Far, SoundPlaybackOverrides.None);

        for (var frame = 1; frame <= 10; frame++)
        {
            Step(rig, voice, frame, Speed, 0f);
        }

        AssertRatio(rig, voice, Approach);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void ListenerMovingTowardAStillSource_SendsTheDopplerRatio(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        rig.Service.SpeedOfSound = SpeedOfSound;
        var voice = rig.Service.PlaySoundAt(CreateDopplerAsset(rig, 1f), Far, SoundPlaybackOverrides.None);

        for (var frame = 1; frame <= 10; frame++)
        {
            Step(rig, voice, frame, 0f, Speed, pushSource: false);
        }

        AssertRatio(rig, voice, 1.1f);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void PositionNotPushedInAFrame_VelocityIsZeroAndTheRatioReturnsToOne(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        rig.Service.SpeedOfSound = SpeedOfSound;
        var voice = rig.Service.PlaySoundAt(CreateDopplerAsset(rig, 1f), Far, SoundPlaybackOverrides.None);

        for (var frame = 1; frame <= 10; frame++)
        {
            Step(rig, voice, frame, Speed, 0f);
        }

        AssertRatio(rig, voice, Approach);

        Step(rig, voice, 11, Speed, 0f, pushSource: false);
        AssertRatio(rig, voice, 1f);

        // Pushed again: the first push after a gap has nothing to compare with, the next one measures the motion.
        Step(rig, voice, 12, Speed, 0f);
        Step(rig, voice, 13, Speed, 0f);
        AssertRatio(rig, voice, Approach);
    }

    [Fact]
    public void ListenerPoseNotPushedInAFrame_VelocityIsZero()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Capability);
        rig.Service.SpeedOfSound = SpeedOfSound;
        var voice = rig.Service.PlaySoundAt(CreateDopplerAsset(rig, 1f), Far, SoundPlaybackOverrides.None);

        for (var frame = 1; frame <= 10; frame++)
        {
            Step(rig, voice, frame, 0f, Speed, pushSource: false);
        }

        AssertRatio(rig, voice, 1.1f);

        // The listener is not pushed this frame.
        rig.Tick();
        AssertRatio(rig, voice, 1f);
    }

    [Fact]
    public void Fallback_TotalPitchStaysWithinOneOctave_WhenTheMaximumRatioIsRequested()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);
        rig.Service.SpeedOfSound = SpeedOfSound;
        var voice = rig.Service.PlaySoundAt(CreateDopplerAsset(rig, 1f), Far, SoundPlaybackOverrides.None);
        rig.Service.SetVoicePitch(voice, 0.5f);

        for (var frame = 1; frame <= 10; frame++)
        {
            Step(rig, voice, frame, 330f, 0f);
        }

        Assert.Equal(1f, rig.Fake.GetParameters(voice).Pitch, 1e-4f);
        Assert.Equal(0.5f, rig.Service.GetVoicePitch(voice));
    }

    [Fact]
    public void Capability_MaximumRatioIsSentWhole()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Capability);
        rig.Service.SpeedOfSound = SpeedOfSound;
        var voice = rig.Service.PlaySoundAt(CreateDopplerAsset(rig, 1f), Far, SoundPlaybackOverrides.None);

        for (var frame = 1; frame <= 10; frame++)
        {
            Step(rig, voice, frame, 330f, 0f);
        }

        Assert.Equal(AudioDoppler.MaxRatio, rig.Capability.GetModulation(voice).Rate, 1e-3f);
    }

    [Fact]
    public void Fallback_BasePitchAndDopplerOffsetAdd()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);
        rig.Service.SpeedOfSound = SpeedOfSound;
        var voice = rig.Service.PlaySoundAt(CreateDopplerAsset(rig, 1f), Far, SoundPlaybackOverrides.None);

        for (var frame = 1; frame <= 10; frame++)
        {
            Step(rig, voice, frame, Speed, 0f);
        }

        rig.Service.SetVoicePitch(voice, 0.25f);

        Assert.Equal(0.25f + MathF.Log2(Approach), rig.Fake.GetParameters(voice).Pitch, 1e-3f);

        // A further move keeps the base pitch.
        Step(rig, voice, 11, Speed, 0f);
        Assert.Equal(0.25f + MathF.Log2(Approach), rig.Fake.GetParameters(voice).Pitch, 1e-3f);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void SetVoicePitch_IsClampedPushedAndKeptAfterASpatialMove(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        var voice = rig.Service.PlaySoundAt(CreateDopplerAsset(rig, 0f), Far, SoundPlaybackOverrides.None);
        rig.SetListener(Vector3.Zero);
        rig.Tick();
        var pushed = rig.Fake.SetParametersCount;

        rig.Service.SetVoicePitch(voice, 0.4f);

        Assert.Equal(pushed + 1, rig.Fake.SetParametersCount);
        Assert.Equal(0.4f, rig.Service.GetVoicePitch(voice));
        Assert.Equal(0.4f, rig.Fake.GetParameters(voice).Pitch, 1e-4f);

        rig.Service.SetVoicePosition(voice, new Vector3(5f, 0f, -3f));
        rig.SetListener(Vector3.Zero);
        rig.Tick(3);

        Assert.Equal(0.4f, rig.Service.GetVoicePitch(voice));
        Assert.Equal(0.4f, rig.Fake.GetParameters(voice).Pitch, 1e-4f);

        rig.Service.SetVoicePitch(voice, 7f);
        Assert.Equal(1f, rig.Service.GetVoicePitch(voice));
        Assert.Equal(1f, rig.Fake.GetParameters(voice).Pitch, 1e-4f);

        rig.Service.SetVoicePitch(voice, -7f);
        Assert.Equal(-1f, rig.Service.GetVoicePitch(voice));
    }

    [Fact]
    public void VoicePitch_StaleHandle_IsIgnored()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);
        var voice = rig.PlayPlain();
        rig.Service.Stop(voice);

        rig.Service.SetVoicePitch(voice, 0.5f);

        Assert.Equal(0f, rig.Service.GetVoicePitch(voice));
        Assert.Equal(0f, rig.Service.GetVoicePitch(AudioVoiceHandle.None));
    }

    [Fact]
    public void VoicePitch_PlainVoice_IsSettable()
    {
        using var rig = new SpatialRig(SpatialBackendKind.Fallback);
        var voice = rig.PlayPlain();

        rig.Service.SetVoicePitch(voice, -0.5f);

        Assert.Equal(-0.5f, rig.Service.GetVoicePitch(voice));
        Assert.Equal(-0.5f, rig.Fake.GetParameters(voice).Pitch, 1e-4f);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void UpdateWithSixtyFourDopplerVoicesMovedEveryFrame_AllocatesNothing(SpatialBackendKind kind)
    {
        const int voiceCount = 64;
        using var rig = new SpatialRig(kind, voiceCount);
        var asset = CreateDopplerAsset(rig, 1f);
        var voices = new AudioVoiceHandle[voiceCount];
        rig.SetListener(Vector3.Zero);

        for (var i = 0; i < voiceCount; i++)
        {
            voices[i] = rig.Service.PlaySoundAt(asset, new Vector3(2f + (i * 0.1f), 0f, -3f), SoundPlaybackOverrides.None);
            Assert.True(voices[i].IsValid);
        }

        for (var frame = 0; frame < 20; frame++)
        {
            Move(rig, voices, frame);
            rig.Tick();
        }

        var before = AllocationWindow.Start();

        for (var frame = 20; frame < 120; frame++)
        {
            Move(rig, voices, frame);
            rig.Service.Update(SpatialRig.Frame);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    private static void Move(SpatialRig rig, AudioVoiceHandle[] voices, int frame)
    {
        for (var i = 0; i < voices.Length; i++)
        {
            var phase = (frame * 0.3f) + i;
            rig.Service.SetVoicePosition(voices[i], new Vector3(3f * MathF.Sin(phase), 0.5f, -4f - (2f * MathF.Cos(phase))));
        }

        rig.SetListener(new Vector3(0.2f * MathF.Sin(frame * 0.1f), 0f, 0f));
    }

    /// <summary>The speed ratio the backend got: the rate (capability) or the folded pitch offset (fallback).</summary>
    private static void AssertRatio(SpatialRig rig, AudioVoiceHandle voice, float expected)
    {
        if (rig.Kind == SpatialBackendKind.Capability)
        {
            Assert.Equal(expected, rig.Capability.GetModulation(voice).Rate, 0.002f);
            Assert.Equal(0f, rig.Fake.GetParameters(voice).Pitch);
        }
        else
        {
            Assert.Equal(MathF.Log2(expected), rig.Fake.GetParameters(voice).Pitch, 0.001f);
        }
    }
}
