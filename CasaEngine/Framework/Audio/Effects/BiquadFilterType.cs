namespace CasaEngine.Framework.Audio.Effects;

/// <summary>Response of a <see cref="BiquadFilterEffect"/>.</summary>
public enum BiquadFilterType
{
    LowPass = 0,
    HighPass,

    /// <summary>Band-pass with a constant 0 dB peak gain.</summary>
    BandPass,

    /// <summary>Peaking EQ: boosts or cuts by <see cref="BiquadFilterEffect.GainDb"/> around the frequency.</summary>
    Peaking,

    LowShelf,
    HighShelf,
}
