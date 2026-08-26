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

    public AudioVoiceParameters ApplyTo(in AudioVoiceParameters parameters)
    {
        return new AudioVoiceParameters(
            Volume ?? parameters.Volume,
            Pan ?? parameters.Pan,
            Pitch ?? parameters.Pitch,
            IsLooped ?? parameters.IsLooped);
    }

    public string ResolveBus(string assetBusName)
    {
        return string.IsNullOrWhiteSpace(BusName) ? assetBusName : BusName;
    }
}
