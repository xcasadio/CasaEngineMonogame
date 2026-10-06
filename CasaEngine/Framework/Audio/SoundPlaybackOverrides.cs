namespace CasaEngine.Framework.Audio;

/// <summary>
/// Per-call overrides applied on top of what a <see cref="SoundAsset"/> declares.
/// Every field left null keeps the asset value.
/// </summary>
public readonly struct SoundPlaybackOverrides
{
    public static readonly SoundPlaybackOverrides None = default;

    public SoundPlaybackOverrides(
        float? volume = null,
        float? pan = null,
        float? pitch = null,
        bool? isLooped = null,
        string busName = null)
    {
        Volume = volume;
        Pan = pan;
        Pitch = pitch;
        IsLooped = isLooped;
        BusName = busName;
    }

    public float? Volume { get; }

    public float? Pan { get; }

    public float? Pitch { get; }

    public bool? IsLooped { get; }

    /// <summary>Null or empty keeps the bus declared by the asset.</summary>
    public string BusName { get; }

    /// <summary>
    /// Replaces the overridden fields of <paramref name="parameters"/>. Every field that is not
    /// overridden, the loop region and the rate multiplier included, keeps its input value.
    /// </summary>
    public AudioVoiceParameters ApplyTo(in AudioVoiceParameters parameters)
    {
        var result = parameters;

        if (Volume.HasValue)
        {
            result = result.WithVolume(Volume.Value);
        }

        if (Pan.HasValue)
        {
            result = result.WithPan(Pan.Value);
        }

        if (Pitch.HasValue)
        {
            result = result.WithPitch(Pitch.Value);
        }

        if (IsLooped.HasValue)
        {
            result = result.WithLooping(IsLooped.Value);
        }

        return result;
    }

    /// <summary>
    /// Steal priority replacing the one of the asset. Null keeps the asset value; 0 removes any priority
    /// (the voice neither steals nor can be stolen); values are clamped to [0, <see cref="SoundAsset.MaxPriority"/>].
    /// </summary>
    public int? Priority { get; init; }

    /// <summary>Priority of the voice to start: the override when set, otherwise <paramref name="assetPriority"/>, clamped.</summary>
    public int ResolvePriority(int assetPriority) => Math.Clamp(Priority ?? assetPriority, 0, SoundAsset.MaxPriority);

    public string ResolveBus(string assetBusName)
    {
        return string.IsNullOrWhiteSpace(BusName) ? assetBusName : BusName;
    }
}
