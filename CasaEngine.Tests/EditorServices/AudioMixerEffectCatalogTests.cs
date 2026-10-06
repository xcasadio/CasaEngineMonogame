using CasaEngine.EditorServices.Audio;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Effects;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Tests.Audio;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

/// <summary>
/// The parameters of the insert effects of the mixer asset (plan T10.7): bounds equal to the public bounds of the engine effects,
/// defaults equal to the ones of the asset model, new effects, and the uniform read and write of one parameter of a record.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class AudioMixerEffectCatalogTests
{
    private static readonly AudioMixerEffectKind[] AllKinds =
    {
        AudioMixerEffectKind.Biquad, AudioMixerEffectKind.Compressor, AudioMixerEffectKind.Reverb, AudioMixerEffectKind.Ducking,
    };

    public static IEnumerable<object[]> Kinds() => AllKinds.Select(kind => new object[] { kind });

    private static AudioMixerEffectData Default(AudioMixerEffectKind kind) => AudioMixerEffectCatalog.CreateDefault(kind, "Voice");

    private static AudioMixerEffectParameter Parameter(AudioMixerEffectKind kind, int index) => AudioMixerEffectCatalog.GetParameters(kind)[index];

    [Fact]
    public void TheKinds_AreTheFourEffectsOfTheAssetModel_InAStableOrder()
    {
        Assert.Equal(AllKinds, AudioMixerEffectCatalog.Kinds);
        Assert.Equal(Enum.GetValues<AudioMixerEffectKind>(), AudioMixerEffectCatalog.Kinds);
        Assert.Equal(
            new[] { "Biquad filter", "Compressor", "Reverb", "Ducking" },
            AudioMixerEffectCatalog.Kinds.Select(AudioMixerEffectCatalog.GetDisplayName));
    }

    [Fact]
    public void TheParameters_AreListedInTheOrderOfTheRecords_WithTheirUnits()
    {
        Assert.Equal(
            new[] { "Frequency (Hz)", "Q", "Gain (dB)" },
            AudioMixerEffectCatalog.GetParameters(AudioMixerEffectKind.Biquad).Select(parameter => parameter.Name));
        Assert.Equal(
            new[] { "Threshold (dB)", "Ratio", "Knee (dB)", "Attack (s)", "Release (s)", "Make-up gain (dB)" },
            AudioMixerEffectCatalog.GetParameters(AudioMixerEffectKind.Compressor).Select(parameter => parameter.Name));
        Assert.Equal(
            new[] { "Room size", "Damping", "Wet", "Dry", "Stereo separation" },
            AudioMixerEffectCatalog.GetParameters(AudioMixerEffectKind.Reverb).Select(parameter => parameter.Name));
        Assert.Equal(
            new[] { "Depth (dB)", "Threshold (dB)", "Attack (s)", "Release (s)" },
            AudioMixerEffectCatalog.GetParameters(AudioMixerEffectKind.Ducking).Select(parameter => parameter.Name));
    }

    [Fact]
    public void TheBounds_AreThePublicBoundsOfTheEngineEffects()
    {
        AssertBounds(
            AudioMixerEffectKind.Biquad,
            (BiquadFilterEffect.MinFrequencyHz, BiquadFilterEffect.MaxFrequencyHz),
            (BiquadFilterEffect.MinQ, BiquadFilterEffect.MaxQ),
            (-BiquadFilterEffect.MaxGainDb, BiquadFilterEffect.MaxGainDb));

        AssertBounds(
            AudioMixerEffectKind.Compressor,
            (CompressorEffect.MinThresholdDb, 0f),
            (CompressorEffect.MinRatio, CompressorEffect.MaxRatio),
            (0f, CompressorEffect.MaxKneeDb),
            (CompressorEffect.MinTimeSeconds, CompressorEffect.MaxTimeSeconds),
            (CompressorEffect.MinTimeSeconds, CompressorEffect.MaxTimeSeconds),
            (-CompressorEffect.MaxMakeupGainDb, CompressorEffect.MaxMakeupGainDb));

        AssertBounds(
            AudioMixerEffectKind.Reverb,
            (0f, 1f),
            (0f, 1f),
            (0f, 1f),
            (0f, 1f),
            (0f, 1f));

        AssertBounds(
            AudioMixerEffectKind.Ducking,
            (0f, DuckingEffect.MaxDepthDb),
            (DuckingEffect.MinThresholdDb, 0f),
            (DuckingEffect.MinTimeSeconds, DuckingEffect.MaxTimeSeconds),
            (DuckingEffect.MinTimeSeconds, DuckingEffect.MaxTimeSeconds));
    }

    private static void AssertBounds(AudioMixerEffectKind kind, params (float Min, float Max)[] expected)
    {
        var parameters = AudioMixerEffectCatalog.GetParameters(kind);
        Assert.Equal(expected.Length, parameters.Count);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.True(expected[index].Min == parameters[index].Min, $"{kind} {parameters[index].Name}: min {parameters[index].Min}");
            Assert.True(expected[index].Max == parameters[index].Max, $"{kind} {parameters[index].Name}: max {parameters[index].Max}");
        }
    }

    [Fact]
    public void TheBounds_AreTheOnesTheEngineEffectsClampTo()
    {
        // The bounds written here (a threshold at most 0 dB, a depth at least 0 dB, [0, 1] for the reverb) are not engine constants:
        // the engine effects themselves are the reference.
        var biquad = new BiquadFilterEffect(BiquadFilterType.LowPass, 1000f);
        var compressor = new CompressorEffect();
        var reverb = new ReverbEffect();
        using var service = new AudioService(new FakeAudioBackend());
        var ducking = new DuckingEffect(service.Mixer.GetBus(AudioBusNames.Voice));

        foreach (var kind in AllKinds)
        {
            var parameters = AudioMixerEffectCatalog.GetParameters(kind);
            for (int index = 0; index < parameters.Count; index++)
            {
                Assert.Equal(parameters[index].Min, Clamped(index, float.MinValue, biquad, compressor, reverb, ducking, (int)kind));
                Assert.Equal(parameters[index].Max, Clamped(index, float.MaxValue, biquad, compressor, reverb, ducking, (int)kind));
            }
        }
    }

    /// <summary>Writes <paramref name="value"/> to the parameter of one engine effect and reads back what the effect kept.</summary>
    private static float Clamped(int parameter, float value, BiquadFilterEffect biquad, CompressorEffect compressor, ReverbEffect reverb, DuckingEffect ducking, int which)
    {
        switch (which)
        {
            case 0:
                switch (parameter)
                {
                    case 0: biquad.FrequencyHz = value; return biquad.FrequencyHz;
                    case 1: biquad.Q = value; return biquad.Q;
                    default: biquad.GainDb = value; return biquad.GainDb;
                }
            case 1:
                switch (parameter)
                {
                    case 0: compressor.ThresholdDb = value; return compressor.ThresholdDb;
                    case 1: compressor.Ratio = value; return compressor.Ratio;
                    case 2: compressor.KneeDb = value; return compressor.KneeDb;
                    case 3: compressor.AttackSeconds = value; return compressor.AttackSeconds;
                    case 4: compressor.ReleaseSeconds = value; return compressor.ReleaseSeconds;
                    default: compressor.MakeupGainDb = value; return compressor.MakeupGainDb;
                }
            case 2:
                switch (parameter)
                {
                    case 0: reverb.RoomSize = value; return reverb.RoomSize;
                    case 1: reverb.Damping = value; return reverb.Damping;
                    case 2: reverb.Wet = value; return reverb.Wet;
                    case 3: reverb.Dry = value; return reverb.Dry;
                    default: reverb.StereoSeparation = value; return reverb.StereoSeparation;
                }
            default:
                switch (parameter)
                {
                    case 0: ducking.DepthDb = value; return ducking.DepthDb;
                    case 1: ducking.ThresholdDb = value; return ducking.ThresholdDb;
                    case 2: ducking.AttackSeconds = value; return ducking.AttackSeconds;
                    default: ducking.ReleaseSeconds = value; return ducking.ReleaseSeconds;
                }
        }
    }

    [Fact]
    public void TheDefaults_AreTheOnesOfTheAssetModel()
    {
        AssertDefaults(
            AudioMixerEffectKind.Biquad,
            AudioMixerEffectDefaults.BiquadFrequencyHz,
            AudioMixerEffectDefaults.BiquadQ,
            AudioMixerEffectDefaults.BiquadGainDb);

        AssertDefaults(
            AudioMixerEffectKind.Compressor,
            AudioMixerEffectDefaults.CompressorThresholdDb,
            AudioMixerEffectDefaults.CompressorRatio,
            AudioMixerEffectDefaults.CompressorKneeDb,
            AudioMixerEffectDefaults.CompressorAttackSeconds,
            AudioMixerEffectDefaults.CompressorReleaseSeconds,
            AudioMixerEffectDefaults.CompressorMakeupGainDb);

        AssertDefaults(
            AudioMixerEffectKind.Reverb,
            AudioMixerEffectDefaults.ReverbRoomSize,
            AudioMixerEffectDefaults.ReverbDamping,
            AudioMixerEffectDefaults.ReverbWet,
            AudioMixerEffectDefaults.ReverbDry,
            AudioMixerEffectDefaults.ReverbStereoSeparation);

        AssertDefaults(
            AudioMixerEffectKind.Ducking,
            AudioMixerEffectDefaults.DuckingDepthDb,
            AudioMixerEffectDefaults.DuckingThresholdDb,
            AudioMixerEffectDefaults.DuckingAttackSeconds,
            AudioMixerEffectDefaults.DuckingReleaseSeconds);
    }

    private static void AssertDefaults(AudioMixerEffectKind kind, params float[] expected)
    {
        var parameters = AudioMixerEffectCatalog.GetParameters(kind);
        Assert.Equal(expected.Length, parameters.Count);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.True(expected[index] == parameters[index].Default, $"{kind} {parameters[index].Name}: default {parameters[index].Default}");
        }
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void EveryDefault_IsWithinItsBounds_AndEveryStepIsAUsableFractionOfTheRange(AudioMixerEffectKind kind)
    {
        foreach (var parameter in AudioMixerEffectCatalog.GetParameters(kind))
        {
            Assert.False(string.IsNullOrWhiteSpace(parameter.Name));
            Assert.InRange(parameter.Default, parameter.Min, parameter.Max);
            Assert.True(parameter.Min < parameter.Max, parameter.Name);
            Assert.True(parameter.Step > 0f && parameter.Step <= (parameter.Max - parameter.Min) / 2f, parameter.Name);
        }
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void CreateDefault_GivesAnEffectOfThatKind_WithTheDefaultOfEveryParameter(AudioMixerEffectKind kind)
    {
        var data = AudioMixerEffectCatalog.CreateDefault(kind, "Voice");

        Assert.Equal(kind, AudioMixerEffectCatalog.GetKind(data));
        var parameters = AudioMixerEffectCatalog.GetParameters(kind);
        for (int index = 0; index < parameters.Count; index++)
        {
            Assert.Equal(parameters[index].Default, AudioMixerEffectCatalog.GetValue(data, index));
        }

        if (kind == AudioMixerEffectKind.Biquad)
        {
            Assert.Equal(AudioMixerEffectDefaults.BiquadFilterType, AudioMixerEffectCatalog.GetFilterType(data));
        }

        if (kind == AudioMixerEffectKind.Ducking)
        {
            Assert.Equal("Voice", AudioMixerEffectCatalog.GetDuckingSource(data));
        }
    }

    [Fact]
    public void CreateDefault_OfADucking_NeedsASource_TheOtherKindsIgnoreIt()
    {
        Assert.Throws<ArgumentNullException>(() => AudioMixerEffectCatalog.CreateDefault(AudioMixerEffectKind.Ducking, null));
        Assert.Throws<ArgumentException>(() => AudioMixerEffectCatalog.CreateDefault(AudioMixerEffectKind.Ducking, "  "));
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioMixerEffectCatalog.CreateDefault((AudioMixerEffectKind)99, "Voice"));

        Assert.IsType<AudioMixerBiquadEffectData>(AudioMixerEffectCatalog.CreateDefault(AudioMixerEffectKind.Biquad, null));
        Assert.IsType<AudioMixerCompressorEffectData>(AudioMixerEffectCatalog.CreateDefault(AudioMixerEffectKind.Compressor, null));
        Assert.IsType<AudioMixerReverbEffectData>(AudioMixerEffectCatalog.CreateDefault(AudioMixerEffectKind.Reverb, null));
    }

    [Fact]
    public void TheDefaultEffects_AppliedToTheLiveMixer_GiveEngineEffectsWithTheDefaultProperties()
    {
        using var service = new AudioService(new FakeAudioBackend());
        var applier = new AudioMixerAssetApplier(service);
        var asset = AudioMixerAsset.CreateDefault("Defaults");
        var sfx = asset.Buses.Single(bus => bus.Name == AudioBusNames.Sfx);
        foreach (var kind in AllKinds)
        {
            sfx.Effects.Add(AudioMixerEffectCatalog.CreateDefault(kind, AudioBusNames.Voice));
        }

        applier.Apply(asset, logProblems: false);

        Assert.Empty(applier.LastProblems);
        var live = service.Mixer.GetBus(AudioBusNames.Sfx).Effects;
        Assert.Equal(4, live.Count);

        // What the engine kept is the default clamped to the bounds of the parameter: the same value, the defaults being in range.
        var biquad = Assert.IsType<BiquadFilterEffect>(live[0]);
        Assert.Equal(AudioMixerEffectDefaults.BiquadFilterType, biquad.Type);
        Assert.Equal(Parameter(AudioMixerEffectKind.Biquad, 0).Default, biquad.FrequencyHz);
        Assert.Equal(Parameter(AudioMixerEffectKind.Biquad, 1).Default, biquad.Q);
        Assert.Equal(Parameter(AudioMixerEffectKind.Biquad, 2).Default, biquad.GainDb);

        var compressor = Assert.IsType<CompressorEffect>(live[1]);
        Assert.Equal(Parameter(AudioMixerEffectKind.Compressor, 0).Default, compressor.ThresholdDb);
        Assert.Equal(Parameter(AudioMixerEffectKind.Compressor, 1).Default, compressor.Ratio);
        Assert.Equal(Parameter(AudioMixerEffectKind.Compressor, 2).Default, compressor.KneeDb);
        Assert.Equal(Parameter(AudioMixerEffectKind.Compressor, 3).Default, compressor.AttackSeconds);
        Assert.Equal(Parameter(AudioMixerEffectKind.Compressor, 4).Default, compressor.ReleaseSeconds);
        Assert.Equal(Parameter(AudioMixerEffectKind.Compressor, 5).Default, compressor.MakeupGainDb);

        var reverb = Assert.IsType<ReverbEffect>(live[2]);
        Assert.Equal(Parameter(AudioMixerEffectKind.Reverb, 0).Default, reverb.RoomSize);
        Assert.Equal(Parameter(AudioMixerEffectKind.Reverb, 1).Default, reverb.Damping);
        Assert.Equal(Parameter(AudioMixerEffectKind.Reverb, 2).Default, reverb.Wet);
        Assert.Equal(Parameter(AudioMixerEffectKind.Reverb, 3).Default, reverb.Dry);
        Assert.Equal(Parameter(AudioMixerEffectKind.Reverb, 4).Default, reverb.StereoSeparation);

        var ducking = Assert.IsType<DuckingEffect>(live[3]);
        Assert.Equal(AudioBusNames.Voice, ducking.Source.Name);
        Assert.Equal(Parameter(AudioMixerEffectKind.Ducking, 0).Default, ducking.DepthDb);
        Assert.Equal(Parameter(AudioMixerEffectKind.Ducking, 1).Default, ducking.ThresholdDb);
        Assert.Equal(Parameter(AudioMixerEffectKind.Ducking, 2).Default, ducking.AttackSeconds);
        Assert.Equal(Parameter(AudioMixerEffectKind.Ducking, 3).Default, ducking.ReleaseSeconds);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void WithValue_ThenGetValue_RoundTrips_AndLeavesTheOtherParametersAlone(AudioMixerEffectKind kind)
    {
        var parameters = AudioMixerEffectCatalog.GetParameters(kind);
        var original = Default(kind);

        for (int index = 0; index < parameters.Count; index++)
        {
            var parameter = parameters[index];
            foreach (float value in new[] { parameter.Min, parameter.Max, (parameter.Min + parameter.Max) / 2f })
            {
                var changed = AudioMixerEffectCatalog.WithValue(original, index, value);

                Assert.Equal(value, AudioMixerEffectCatalog.GetValue(changed, index));
                Assert.Equal(kind, AudioMixerEffectCatalog.GetKind(changed));
                for (int other = 0; other < parameters.Count; other++)
                {
                    if (other != index)
                    {
                        Assert.Equal(AudioMixerEffectCatalog.GetValue(original, other), AudioMixerEffectCatalog.GetValue(changed, other));
                    }
                }
            }
        }

        // The records are values: the original is not touched.
        Assert.Equal(Default(kind), original);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void WithValue_ClampsToTheBounds_AndIgnoresANaN(AudioMixerEffectKind kind)
    {
        var parameters = AudioMixerEffectCatalog.GetParameters(kind);
        var original = Default(kind);

        for (int index = 0; index < parameters.Count; index++)
        {
            Assert.Equal(parameters[index].Min, AudioMixerEffectCatalog.GetValue(AudioMixerEffectCatalog.WithValue(original, index, float.MinValue), index));
            Assert.Equal(parameters[index].Max, AudioMixerEffectCatalog.GetValue(AudioMixerEffectCatalog.WithValue(original, index, float.MaxValue), index));
            Assert.Equal(parameters[index].Max, AudioMixerEffectCatalog.GetValue(AudioMixerEffectCatalog.WithValue(original, index, float.PositiveInfinity), index));
            Assert.Same(original, AudioMixerEffectCatalog.WithValue(original, index, float.NaN));
        }
    }

    [Fact]
    public void GetValue_AndWithValue_RefuseAnUnknownParameterOrANullEffect()
    {
        var biquad = Default(AudioMixerEffectKind.Biquad);

        Assert.Throws<ArgumentOutOfRangeException>(() => AudioMixerEffectCatalog.GetValue(biquad, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioMixerEffectCatalog.GetValue(biquad, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioMixerEffectCatalog.WithValue(biquad, 3, 1f));
        Assert.Throws<ArgumentNullException>(() => AudioMixerEffectCatalog.GetValue(null, 0));
        Assert.Throws<ArgumentNullException>(() => AudioMixerEffectCatalog.WithValue(null, 0, 1f));
        Assert.Throws<ArgumentNullException>(() => AudioMixerEffectCatalog.GetKind(null));
    }

    [Fact]
    public void TheFilterType_IsAParameterOfItsOwn_OfABiquadOnly()
    {
        var biquad = Default(AudioMixerEffectKind.Biquad);

        foreach (var type in AudioMixerEffectCatalog.FilterTypes)
        {
            var changed = AudioMixerEffectCatalog.WithFilterType(biquad, type);

            Assert.Equal(type, AudioMixerEffectCatalog.GetFilterType(changed));
            Assert.Equal(AudioMixerEffectCatalog.GetValue(biquad, 0), AudioMixerEffectCatalog.GetValue(changed, 0));
        }

        Assert.Equal(Enum.GetValues<BiquadFilterType>().OrderBy(type => type), AudioMixerEffectCatalog.FilterTypes.OrderBy(type => type));
        Assert.Equal(
            AudioMixerEffectCatalog.FilterTypes.Count,
            AudioMixerEffectCatalog.FilterTypes.Select(AudioMixerEffectCatalog.GetDisplayName).Distinct().Count());
        Assert.Throws<ArgumentException>(() => AudioMixerEffectCatalog.GetFilterType(Default(AudioMixerEffectKind.Reverb)));
        Assert.Throws<ArgumentException>(() => AudioMixerEffectCatalog.WithFilterType(Default(AudioMixerEffectKind.Ducking), BiquadFilterType.HighPass));
    }

    [Fact]
    public void TheDuckingSource_IsAParameterOfItsOwn_OfADuckingOnly()
    {
        var ducking = Default(AudioMixerEffectKind.Ducking);

        var changed = AudioMixerEffectCatalog.WithDuckingSource(ducking, "Music");

        Assert.Equal("Music", AudioMixerEffectCatalog.GetDuckingSource(changed));
        Assert.Equal("Voice", AudioMixerEffectCatalog.GetDuckingSource(ducking));
        Assert.Equal(AudioMixerEffectCatalog.GetValue(ducking, 0), AudioMixerEffectCatalog.GetValue(changed, 0));
        Assert.Throws<ArgumentException>(() => AudioMixerEffectCatalog.WithDuckingSource(ducking, " "));
        Assert.Throws<ArgumentException>(() => AudioMixerEffectCatalog.GetDuckingSource(Default(AudioMixerEffectKind.Biquad)));
        Assert.Throws<ArgumentException>(() => AudioMixerEffectCatalog.WithDuckingSource(Default(AudioMixerEffectKind.Reverb), "Music"));
    }
}
