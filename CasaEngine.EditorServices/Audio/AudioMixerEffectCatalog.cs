using CasaEngine.Framework.Audio.Effects;
using CasaEngine.Framework.Audio.Mixing;

namespace CasaEngine.EditorServices.Audio;

/// <summary>The four kinds of insert effect an <see cref="AudioMixerAsset"/> can hold.</summary>
public enum AudioMixerEffectKind
{
    Biquad,
    Compressor,
    Reverb,
    Ducking,
}

/// <summary>One numeric parameter of an insert effect, as the mixer panel edits it.</summary>
/// <param name="Name">The label of the parameter, with its unit.</param>
/// <param name="Min">The lowest value the engine effect accepts.</param>
/// <param name="Max">The highest value the engine effect accepts.</param>
/// <param name="Step">What one click on the +/- buttons of the field adds or removes (an editor choice).</param>
/// <param name="Default">The value of a new effect (<see cref="AudioMixerEffectDefaults"/>).</param>
public sealed record AudioMixerEffectParameter(string Name, float Min, float Max, float Step, float Default);

/// <summary>
/// The parameters of the insert effects of the asset model (plan T10.7): what the mixer panel shows for each kind (name, bounds,
/// step, default), a new effect with its defaults, and a uniform read and write of one numeric parameter of an effect record.
/// Pure: no UI, no engine instance.
/// </summary>
/// <remarks>
/// The bounds are the public bounds of the engine effects (what <see cref="BiquadFilterEffect"/>, <see cref="CompressorEffect"/>,
/// <see cref="ReverbEffect"/> and <see cref="DuckingEffect"/> clamp to); the few bounds the engine keeps in its code rather than in
/// a constant (a threshold at most 0 dB, a knee or a depth at least 0, the reverb parameters in [0, 1]) are written here. The
/// defaults are the ones of the asset model, <see cref="AudioMixerEffectDefaults"/>. The steps are editor choices.
/// The biquad filter type and the ducking source are not numeric: they have their own accessors.
/// The parameters are listed in the order of the positional members of the record, the non-numeric one left out.
/// </remarks>
public static class AudioMixerEffectCatalog
{
    private static readonly AudioMixerEffectKind[] AllKinds =
    {
        AudioMixerEffectKind.Biquad,
        AudioMixerEffectKind.Compressor,
        AudioMixerEffectKind.Reverb,
        AudioMixerEffectKind.Ducking,
    };

    private static readonly BiquadFilterType[] AllFilterTypes =
    {
        BiquadFilterType.LowPass,
        BiquadFilterType.HighPass,
        BiquadFilterType.BandPass,
        BiquadFilterType.Peaking,
        BiquadFilterType.LowShelf,
        BiquadFilterType.HighShelf,
    };

    private static readonly IReadOnlyList<AudioMixerEffectParameter> BiquadParameters = Array.AsReadOnly(new[]
    {
        new AudioMixerEffectParameter("Frequency (Hz)", BiquadFilterEffect.MinFrequencyHz, BiquadFilterEffect.MaxFrequencyHz, 10f, AudioMixerEffectDefaults.BiquadFrequencyHz),
        new AudioMixerEffectParameter("Q", BiquadFilterEffect.MinQ, BiquadFilterEffect.MaxQ, 0.1f, AudioMixerEffectDefaults.BiquadQ),
        new AudioMixerEffectParameter("Gain (dB)", -BiquadFilterEffect.MaxGainDb, BiquadFilterEffect.MaxGainDb, 0.5f, AudioMixerEffectDefaults.BiquadGainDb),
    });

    private static readonly IReadOnlyList<AudioMixerEffectParameter> CompressorParameters = Array.AsReadOnly(new[]
    {
        new AudioMixerEffectParameter("Threshold (dB)", CompressorEffect.MinThresholdDb, 0f, 1f, AudioMixerEffectDefaults.CompressorThresholdDb),
        new AudioMixerEffectParameter("Ratio", CompressorEffect.MinRatio, CompressorEffect.MaxRatio, 0.5f, AudioMixerEffectDefaults.CompressorRatio),
        new AudioMixerEffectParameter("Knee (dB)", 0f, CompressorEffect.MaxKneeDb, 1f, AudioMixerEffectDefaults.CompressorKneeDb),
        new AudioMixerEffectParameter("Attack (s)", CompressorEffect.MinTimeSeconds, CompressorEffect.MaxTimeSeconds, 0.005f, AudioMixerEffectDefaults.CompressorAttackSeconds),
        new AudioMixerEffectParameter("Release (s)", CompressorEffect.MinTimeSeconds, CompressorEffect.MaxTimeSeconds, 0.01f, AudioMixerEffectDefaults.CompressorReleaseSeconds),
        new AudioMixerEffectParameter("Make-up gain (dB)", -CompressorEffect.MaxMakeupGainDb, CompressorEffect.MaxMakeupGainDb, 0.5f, AudioMixerEffectDefaults.CompressorMakeupGainDb),
    });

    private static readonly IReadOnlyList<AudioMixerEffectParameter> ReverbParameters = Array.AsReadOnly(new[]
    {
        new AudioMixerEffectParameter("Room size", 0f, 1f, 0.05f, AudioMixerEffectDefaults.ReverbRoomSize),
        new AudioMixerEffectParameter("Damping", 0f, 1f, 0.05f, AudioMixerEffectDefaults.ReverbDamping),
        new AudioMixerEffectParameter("Wet", 0f, 1f, 0.05f, AudioMixerEffectDefaults.ReverbWet),
        new AudioMixerEffectParameter("Dry", 0f, 1f, 0.05f, AudioMixerEffectDefaults.ReverbDry),
        new AudioMixerEffectParameter("Stereo separation", 0f, 1f, 0.05f, AudioMixerEffectDefaults.ReverbStereoSeparation),
    });

    private static readonly IReadOnlyList<AudioMixerEffectParameter> DuckingParameters = Array.AsReadOnly(new[]
    {
        new AudioMixerEffectParameter("Depth (dB)", 0f, DuckingEffect.MaxDepthDb, 1f, AudioMixerEffectDefaults.DuckingDepthDb),
        new AudioMixerEffectParameter("Threshold (dB)", DuckingEffect.MinThresholdDb, 0f, 1f, AudioMixerEffectDefaults.DuckingThresholdDb),
        new AudioMixerEffectParameter("Attack (s)", DuckingEffect.MinTimeSeconds, DuckingEffect.MaxTimeSeconds, 0.005f, AudioMixerEffectDefaults.DuckingAttackSeconds),
        new AudioMixerEffectParameter("Release (s)", DuckingEffect.MinTimeSeconds, DuckingEffect.MaxTimeSeconds, 0.05f, AudioMixerEffectDefaults.DuckingReleaseSeconds),
    });

    /// <summary>The kinds of effect, in the order the panel offers them.</summary>
    public static IReadOnlyList<AudioMixerEffectKind> Kinds => AllKinds;

    /// <summary>The filter types of a biquad, in the order the panel offers them.</summary>
    public static IReadOnlyList<BiquadFilterType> FilterTypes => AllFilterTypes;

    public static string GetDisplayName(AudioMixerEffectKind kind)
    {
        return kind switch
        {
            AudioMixerEffectKind.Biquad => "Biquad filter",
            AudioMixerEffectKind.Compressor => "Compressor",
            AudioMixerEffectKind.Reverb => "Reverb",
            AudioMixerEffectKind.Ducking => "Ducking",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown effect kind."),
        };
    }

    public static string GetDisplayName(BiquadFilterType type)
    {
        return type switch
        {
            BiquadFilterType.LowPass => "Low-pass",
            BiquadFilterType.HighPass => "High-pass",
            BiquadFilterType.BandPass => "Band-pass",
            BiquadFilterType.Peaking => "Peaking",
            BiquadFilterType.LowShelf => "Low shelf",
            BiquadFilterType.HighShelf => "High shelf",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown filter type."),
        };
    }

    /// <summary>The kind of an effect record. An argument error for a record this catalog does not know.</summary>
    public static AudioMixerEffectKind GetKind(AudioMixerEffectData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return data switch
        {
            AudioMixerBiquadEffectData => AudioMixerEffectKind.Biquad,
            AudioMixerCompressorEffectData => AudioMixerEffectKind.Compressor,
            AudioMixerReverbEffectData => AudioMixerEffectKind.Reverb,
            AudioMixerDuckingEffectData => AudioMixerEffectKind.Ducking,
            _ => throw new ArgumentException($"Unknown effect data '{data.GetType().Name}'.", nameof(data)),
        };
    }

    /// <summary>The numeric parameters of a kind of effect, in a stable order.</summary>
    public static IReadOnlyList<AudioMixerEffectParameter> GetParameters(AudioMixerEffectKind kind)
    {
        return kind switch
        {
            AudioMixerEffectKind.Biquad => BiquadParameters,
            AudioMixerEffectKind.Compressor => CompressorParameters,
            AudioMixerEffectKind.Reverb => ReverbParameters,
            AudioMixerEffectKind.Ducking => DuckingParameters,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown effect kind."),
        };
    }

    /// <summary>
    /// A new effect with the defaults of the asset model. A ducking needs its <paramref name="sourceBus"/>; the other kinds ignore it.
    /// </summary>
    public static AudioMixerEffectData CreateDefault(AudioMixerEffectKind kind, string sourceBus)
    {
        switch (kind)
        {
            case AudioMixerEffectKind.Biquad:
                return new AudioMixerBiquadEffectData();
            case AudioMixerEffectKind.Compressor:
                return new AudioMixerCompressorEffectData();
            case AudioMixerEffectKind.Reverb:
                return new AudioMixerReverbEffectData();
            case AudioMixerEffectKind.Ducking:
                ArgumentException.ThrowIfNullOrWhiteSpace(sourceBus);
                return new AudioMixerDuckingEffectData(sourceBus);
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown effect kind.");
        }
    }

    /// <summary>The value of the numeric parameter <paramref name="parameterIndex"/> of <paramref name="data"/>, as stored.</summary>
    public static float GetValue(AudioMixerEffectData data, int parameterIndex)
    {
        CheckParameterIndex(data, parameterIndex);

        switch (data)
        {
            case AudioMixerBiquadEffectData biquad:
                return parameterIndex switch
                {
                    0 => biquad.FrequencyHz,
                    1 => biquad.Q,
                    _ => biquad.GainDb,
                };
            case AudioMixerCompressorEffectData compressor:
                return parameterIndex switch
                {
                    0 => compressor.ThresholdDb,
                    1 => compressor.Ratio,
                    2 => compressor.KneeDb,
                    3 => compressor.AttackSeconds,
                    4 => compressor.ReleaseSeconds,
                    _ => compressor.MakeupGainDb,
                };
            case AudioMixerReverbEffectData reverb:
                return parameterIndex switch
                {
                    0 => reverb.RoomSize,
                    1 => reverb.Damping,
                    2 => reverb.Wet,
                    3 => reverb.Dry,
                    _ => reverb.StereoSeparation,
                };
            default:
                var ducking = (AudioMixerDuckingEffectData)data;
                return parameterIndex switch
                {
                    0 => ducking.DepthDb,
                    1 => ducking.ThresholdDb,
                    2 => ducking.AttackSeconds,
                    _ => ducking.ReleaseSeconds,
                };
        }
    }

    /// <summary>
    /// A copy of <paramref name="data"/> with the numeric parameter <paramref name="parameterIndex"/> set to <paramref name="value"/>,
    /// clamped to the bounds of the parameter. A NaN leaves the effect as it is (the same instance comes back).
    /// </summary>
    public static AudioMixerEffectData WithValue(AudioMixerEffectData data, int parameterIndex, float value)
    {
        CheckParameterIndex(data, parameterIndex);

        if (float.IsNaN(value))
        {
            return data;
        }

        var parameter = GetParameters(GetKind(data))[parameterIndex];
        float clamped = Math.Clamp(value, parameter.Min, parameter.Max);

        switch (data)
        {
            case AudioMixerBiquadEffectData biquad:
                return parameterIndex switch
                {
                    0 => biquad with { FrequencyHz = clamped },
                    1 => biquad with { Q = clamped },
                    _ => biquad with { GainDb = clamped },
                };
            case AudioMixerCompressorEffectData compressor:
                return parameterIndex switch
                {
                    0 => compressor with { ThresholdDb = clamped },
                    1 => compressor with { Ratio = clamped },
                    2 => compressor with { KneeDb = clamped },
                    3 => compressor with { AttackSeconds = clamped },
                    4 => compressor with { ReleaseSeconds = clamped },
                    _ => compressor with { MakeupGainDb = clamped },
                };
            case AudioMixerReverbEffectData reverb:
                return parameterIndex switch
                {
                    0 => reverb with { RoomSize = clamped },
                    1 => reverb with { Damping = clamped },
                    2 => reverb with { Wet = clamped },
                    3 => reverb with { Dry = clamped },
                    _ => reverb with { StereoSeparation = clamped },
                };
            default:
                var ducking = (AudioMixerDuckingEffectData)data;
                return parameterIndex switch
                {
                    0 => ducking with { DepthDb = clamped },
                    1 => ducking with { ThresholdDb = clamped },
                    2 => ducking with { AttackSeconds = clamped },
                    _ => ducking with { ReleaseSeconds = clamped },
                };
        }
    }

    /// <summary>The filter type of a biquad effect.</summary>
    public static BiquadFilterType GetFilterType(AudioMixerEffectData data)
    {
        return RequireBiquad(data).FilterType;
    }

    /// <summary>A copy of a biquad effect with another filter type.</summary>
    public static AudioMixerEffectData WithFilterType(AudioMixerEffectData data, BiquadFilterType type)
    {
        return RequireBiquad(data) with { FilterType = type };
    }

    /// <summary>The source bus of a ducking effect.</summary>
    public static string GetDuckingSource(AudioMixerEffectData data)
    {
        return RequireDucking(data).Source;
    }

    /// <summary>A copy of a ducking effect with another source bus.</summary>
    public static AudioMixerEffectData WithDuckingSource(AudioMixerEffectData data, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        return RequireDucking(data) with { Source = source };
    }

    private static void CheckParameterIndex(AudioMixerEffectData data, int parameterIndex)
    {
        int count = GetParameters(GetKind(data)).Count;
        if (parameterIndex < 0 || parameterIndex >= count)
        {
            throw new ArgumentOutOfRangeException(nameof(parameterIndex), parameterIndex, $"The effect has {count} numeric parameters.");
        }
    }

    private static AudioMixerBiquadEffectData RequireBiquad(AudioMixerEffectData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return data as AudioMixerBiquadEffectData
               ?? throw new ArgumentException($"A filter type belongs to a biquad filter, not to '{data.GetType().Name}'.", nameof(data));
    }

    private static AudioMixerDuckingEffectData RequireDucking(AudioMixerEffectData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return data as AudioMixerDuckingEffectData
               ?? throw new ArgumentException($"A source bus belongs to a ducking effect, not to '{data.GetType().Name}'.", nameof(data));
    }
}
