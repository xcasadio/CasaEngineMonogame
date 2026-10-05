namespace CasaEngine.Framework.Audio.Effects;

/// <summary>
/// Audio-side state of one insert effect on one bus: the coefficients derived from the last parameter snapshot and the
/// memory of the filter or the envelope of the compressor. A plain struct held in a preallocated array of the mixer, so
/// adding an effect allocates nothing on the audio thread. One struct serves both effect kinds; each uses its own fields.
/// </summary>
internal struct EffectDspState
{
    /// <summary>
    /// Values below this magnitude are flushed to zero in every recursive state, so a long decaying tail never reaches
    /// the subnormal range, where arithmetic can be orders of magnitude slower. 1e-30 is -600 dB: inaudible.
    /// </summary>
    public const double DenormalFlush = 1e-30;

    /// <summary>The parameter snapshot the coefficients below were computed from (compared by reference).</summary>
    public object AppliedParameters;

    // Biquad, normalized by a0, with the transposed direct form II memory of each channel.
    public double B0;
    public double B1;
    public double B2;
    public double A1;
    public double A2;
    public double Z1Left;
    public double Z2Left;
    public double Z1Right;
    public double Z2Right;

    // Compressor.
    public double AttackCoefficient;
    public double ReleaseCoefficient;

    /// <summary>Smoothed gain reduction in dB (the detector output y_L of the paper, at least 0).</summary>
    public double GainReductionDb;
}
