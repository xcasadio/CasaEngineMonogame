using CasaEngine.Framework.Audio.Effects;

namespace CasaEngine.Framework.Audio.Mixing;

/// <summary>
/// An insert effect stored in an <see cref="AudioMixerAsset"/>. Values are kept as written: the engine
/// effects clamp them themselves when the asset is applied to the live mixer.
/// </summary>
public abstract record AudioMixerEffectData;

/// <summary>Defaults of the effect data, used when a field is missing from a document.</summary>
public static class AudioMixerEffectDefaults
{
    public const BiquadFilterType BiquadFilterType = Effects.BiquadFilterType.LowPass;
    public const float BiquadFrequencyHz = 1000f;

    /// <summary>Butterworth Q (1 / sqrt(2)); copied here because the engine's constant is private.</summary>
    public const float BiquadQ = 0.70710678f;
    public const float BiquadGainDb = 0f;

    public const float CompressorThresholdDb = -18f;
    public const float CompressorRatio = 4f;
    public const float CompressorKneeDb = 6f;
    public const float CompressorAttackSeconds = 0.01f;
    public const float CompressorReleaseSeconds = 0.1f;
    public const float CompressorMakeupGainDb = 0f;

    public const float ReverbRoomSize = 0.5f;
    public const float ReverbDamping = 0.5f;
    public const float ReverbWet = 1f;
    public const float ReverbDry = 0f;
    public const float ReverbStereoSeparation = 1f;

    public const float DuckingDepthDb = 12f;
    public const float DuckingThresholdDb = -40f;
    public const float DuckingAttackSeconds = 0.02f;
    public const float DuckingReleaseSeconds = 0.4f;
}

public sealed record AudioMixerBiquadEffectData(
    BiquadFilterType FilterType = AudioMixerEffectDefaults.BiquadFilterType,
    float FrequencyHz = AudioMixerEffectDefaults.BiquadFrequencyHz,
    float Q = AudioMixerEffectDefaults.BiquadQ,
    float GainDb = AudioMixerEffectDefaults.BiquadGainDb) : AudioMixerEffectData;

public sealed record AudioMixerCompressorEffectData(
    float ThresholdDb = AudioMixerEffectDefaults.CompressorThresholdDb,
    float Ratio = AudioMixerEffectDefaults.CompressorRatio,
    float KneeDb = AudioMixerEffectDefaults.CompressorKneeDb,
    float AttackSeconds = AudioMixerEffectDefaults.CompressorAttackSeconds,
    float ReleaseSeconds = AudioMixerEffectDefaults.CompressorReleaseSeconds,
    float MakeupGainDb = AudioMixerEffectDefaults.CompressorMakeupGainDb) : AudioMixerEffectData;

public sealed record AudioMixerReverbEffectData(
    float RoomSize = AudioMixerEffectDefaults.ReverbRoomSize,
    float Damping = AudioMixerEffectDefaults.ReverbDamping,
    float Wet = AudioMixerEffectDefaults.ReverbWet,
    float Dry = AudioMixerEffectDefaults.ReverbDry,
    float StereoSeparation = AudioMixerEffectDefaults.ReverbStereoSeparation) : AudioMixerEffectData;

/// <summary>Ducks the bus holding it while the <see cref="Source"/> bus is loud; the source is required.</summary>
public sealed record AudioMixerDuckingEffectData(
    string Source,
    float DepthDb = AudioMixerEffectDefaults.DuckingDepthDb,
    float ThresholdDb = AudioMixerEffectDefaults.DuckingThresholdDb,
    float AttackSeconds = AudioMixerEffectDefaults.DuckingAttackSeconds,
    float ReleaseSeconds = AudioMixerEffectDefaults.DuckingReleaseSeconds) : AudioMixerEffectData;
