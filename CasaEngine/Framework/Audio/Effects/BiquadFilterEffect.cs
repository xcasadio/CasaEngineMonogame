namespace CasaEngine.Framework.Audio.Effects;

/// <summary>
/// Second order filter insert (low-pass, high-pass, band-pass, peaking, low shelf, high shelf), the same on both
/// channels. Coefficients come from the formulas of the "Audio EQ Cookbook" by Robert Bristow-Johnson, as published
/// by the W3C as a Working Group Note (2021): https://www.w3.org/TR/audio-eq-cookbook/ (W3C Software and Document
/// Notice and License). The filter runs in transposed direct form II, with double precision coefficients and state.
/// </summary>
/// <remarks>
/// Cookbook definitions, with Fs the output rate, f0 the frequency, Q the quality factor and dBgain the gain:
/// A = 10^(dBgain/40) (peaking and shelves only), w0 = 2 pi f0 / Fs, alpha = sin(w0) / (2 Q); the shelves use
/// 2 sqrt(A) alpha with that alpha, which is the Q form of the cookbook shelf (S is not exposed). Coefficients are
/// divided by a0. The frequency is limited to [10 Hz, 0.49 Fs] when the coefficients are computed. A change of
/// parameters takes effect at the next block without resetting the filter memory.
/// </remarks>
public sealed class BiquadFilterEffect : AudioEffect
{
    public const float MinFrequencyHz = 10f;
    public const float MaxFrequencyHz = 24000f;
    public const float MinQ = 0.1f;
    public const float MaxQ = 50f;
    public const float MaxGainDb = 40f;
    private const float Butterworth = 0.70710678f;

    // Immutable snapshot, replaced as a whole and read by the audio thread (last value, never queued).
    internal sealed record Parameters(BiquadFilterType Type, float FrequencyHz, float Q, float GainDb);

    private Parameters _parameters;

    public BiquadFilterEffect(BiquadFilterType type, float frequencyHz, float q = Butterworth, float gainDb = 0f)
    {
        _parameters = new Parameters(
            type,
            Sanitize(frequencyHz, 1000f, MinFrequencyHz, MaxFrequencyHz),
            Sanitize(q, Butterworth, MinQ, MaxQ),
            Sanitize(gainDb, 0f, -MaxGainDb, MaxGainDb));
    }

    public BiquadFilterType Type
    {
        get => Volatile.Read(ref _parameters).Type;
        set => Publish(Volatile.Read(ref _parameters) with { Type = value });
    }

    /// <summary>Cutoff, centre or corner frequency in Hz. NaN is ignored; out of range values are clamped.</summary>
    public float FrequencyHz
    {
        get => Volatile.Read(ref _parameters).FrequencyHz;
        set => Publish(Volatile.Read(ref _parameters) with { FrequencyHz = Sanitize(value, FrequencyHz, MinFrequencyHz, MaxFrequencyHz) });
    }

    /// <summary>Quality factor, 1/sqrt(2) for a Butterworth low-pass or high-pass.</summary>
    public float Q
    {
        get => Volatile.Read(ref _parameters).Q;
        set => Publish(Volatile.Read(ref _parameters) with { Q = Sanitize(value, Q, MinQ, MaxQ) });
    }

    /// <summary>Gain in dB, used by the peaking and shelving types only.</summary>
    public float GainDb
    {
        get => Volatile.Read(ref _parameters).GainDb;
        set => Publish(Volatile.Read(ref _parameters) with { GainDb = Sanitize(value, GainDb, -MaxGainDb, MaxGainDb) });
    }

    private static float Sanitize(float value, float fallback, float min, float max)
    {
        return float.IsNaN(value) ? fallback : Math.Clamp(value, min, max);
    }

    private void Publish(Parameters parameters)
    {
        Volatile.Write(ref _parameters, parameters);
    }

    internal override void Process(ref EffectDspState state, Span<float> interleavedStereo, int frameCount, int sampleRate)
    {
        var parameters = Volatile.Read(ref _parameters);

        if (!ReferenceEquals(state.AppliedParameters, parameters))
        {
            ComputeCoefficients(parameters.Type, parameters.FrequencyHz, parameters.Q, parameters.GainDb, sampleRate, ref state);
            state.AppliedParameters = parameters;
        }

        var b0 = state.B0;
        var b1 = state.B1;
        var b2 = state.B2;
        var a1 = state.A1;
        var a2 = state.A2;
        var z1L = state.Z1Left;
        var z2L = state.Z2Left;
        var z1R = state.Z1Right;
        var z2R = state.Z2Right;

        for (var i = 0; i < frameCount * 2; i += 2)
        {
            // Transposed direct form II: y = b0 x + z1; z1 = b1 x - a1 y + z2; z2 = b2 x - a2 y.
            double x = interleavedStereo[i];
            var y = (b0 * x) + z1L;
            z1L = (b1 * x) - (a1 * y) + z2L;
            z2L = (b2 * x) - (a2 * y);
            interleavedStereo[i] = (float)y;

            x = interleavedStereo[i + 1];
            y = (b0 * x) + z1R;
            z1R = (b1 * x) - (a1 * y) + z2R;
            z2R = (b2 * x) - (a2 * y);
            interleavedStereo[i + 1] = (float)y;

            // Denormal guard: a decaying tail is flushed to zero long before the subnormal range.
            if (Math.Abs(z1L) < EffectDspState.DenormalFlush) { z1L = 0; }
            if (Math.Abs(z2L) < EffectDspState.DenormalFlush) { z2L = 0; }
            if (Math.Abs(z1R) < EffectDspState.DenormalFlush) { z1R = 0; }
            if (Math.Abs(z2R) < EffectDspState.DenormalFlush) { z2R = 0; }
        }

        state.Z1Left = z1L;
        state.Z2Left = z2L;
        state.Z1Right = z1R;
        state.Z2Right = z2R;
    }

    /// <summary>Cookbook coefficients (https://www.w3.org/TR/audio-eq-cookbook/), normalized by a0, into <paramref name="state"/>.</summary>
    internal static void ComputeCoefficients(BiquadFilterType type, float frequencyHz, float q, float gainDb, int sampleRate, ref EffectDspState state)
    {
        var f0 = Math.Min((double)frequencyHz, 0.49 * sampleRate);
        var w0 = 2.0 * Math.PI * f0 / sampleRate;
        var cosW0 = Math.Cos(w0);
        var alpha = Math.Sin(w0) / (2.0 * q);
        var a = Math.Pow(10.0, gainDb / 40.0);
        var twoSqrtAAlpha = 2.0 * Math.Sqrt(a) * alpha;
        double b0, b1, b2, a0, a1, a2;

        switch (type)
        {
            case BiquadFilterType.HighPass:
                b0 = (1 + cosW0) / 2;
                b1 = -(1 + cosW0);
                b2 = (1 + cosW0) / 2;
                a0 = 1 + alpha;
                a1 = -2 * cosW0;
                a2 = 1 - alpha;
                break;
            case BiquadFilterType.BandPass:
                b0 = alpha;
                b1 = 0;
                b2 = -alpha;
                a0 = 1 + alpha;
                a1 = -2 * cosW0;
                a2 = 1 - alpha;
                break;
            case BiquadFilterType.Peaking:
                b0 = 1 + (alpha * a);
                b1 = -2 * cosW0;
                b2 = 1 - (alpha * a);
                a0 = 1 + (alpha / a);
                a1 = -2 * cosW0;
                a2 = 1 - (alpha / a);
                break;
            case BiquadFilterType.LowShelf:
                b0 = a * ((a + 1) - ((a - 1) * cosW0) + twoSqrtAAlpha);
                b1 = 2 * a * ((a - 1) - ((a + 1) * cosW0));
                b2 = a * ((a + 1) - ((a - 1) * cosW0) - twoSqrtAAlpha);
                a0 = (a + 1) + ((a - 1) * cosW0) + twoSqrtAAlpha;
                a1 = -2 * ((a - 1) + ((a + 1) * cosW0));
                a2 = (a + 1) + ((a - 1) * cosW0) - twoSqrtAAlpha;
                break;
            case BiquadFilterType.HighShelf:
                b0 = a * ((a + 1) + ((a - 1) * cosW0) + twoSqrtAAlpha);
                b1 = -2 * a * ((a - 1) + ((a + 1) * cosW0));
                b2 = a * ((a + 1) + ((a - 1) * cosW0) - twoSqrtAAlpha);
                a0 = (a + 1) - ((a - 1) * cosW0) + twoSqrtAAlpha;
                a1 = 2 * ((a - 1) - ((a + 1) * cosW0));
                a2 = (a + 1) - ((a - 1) * cosW0) - twoSqrtAAlpha;
                break;
            default: // LowPass
                b0 = (1 - cosW0) / 2;
                b1 = 1 - cosW0;
                b2 = (1 - cosW0) / 2;
                a0 = 1 + alpha;
                a1 = -2 * cosW0;
                a2 = 1 - alpha;
                break;
        }

        state.B0 = b0 / a0;
        state.B1 = b1 / a0;
        state.B2 = b2 / a0;
        state.A1 = a1 / a0;
        state.A2 = a2 / a0;
    }
}
