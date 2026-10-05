namespace CasaEngine.Framework.Audio.Psx;

/// <summary>
/// The hardware constant tables of the PlayStation SPU, supplied by the caller (decision P17, open question O12):
/// the engine ships none of them. The ADPCM filter coefficients are mandatory; the reverb FIR coefficients and the
/// Gaussian interpolation table are optional (a missing Gaussian table selects the engine's cubic interpolation).
/// Every table is validated and copied at construction.
/// </summary>
public sealed class PsxSpuHardwareTables
{
    /// <summary>Number of ADPCM filters of the SPU (indices 0 to 4).</summary>
    public const int AdpcmFilterCount = 5;

    /// <summary>Number of taps of the reverb input/output FIR filter.</summary>
    public const int ReverbFirTapCount = 39;

    /// <summary>Number of entries of the interpolation table.</summary>
    public const int GaussianEntryCount = 512;

    /// <summary>Smallest accepted ADPCM coefficient (the coefficients are signed 8 bit weights in 1/64 units).</summary>
    public const int AdpcmCoefficientMin = sbyte.MinValue;

    /// <summary>Largest accepted ADPCM coefficient.</summary>
    public const int AdpcmCoefficientMax = sbyte.MaxValue;

    private readonly int[] _adpcmPositive;
    private readonly int[] _adpcmNegative;
    private readonly short[] _reverbFir;
    private readonly short[] _gaussian;

    /// <param name="adpcmPositive">Coefficient applied to the previous sample, one per filter (5 values, -128..127).</param>
    /// <param name="adpcmNegative">Coefficient applied to the sample before it, one per filter (5 values, -128..127).</param>
    /// <param name="reverbFir">Empty, or the 39 reverb FIR coefficients (consumed by the reverb unit of <see cref="PsxSpu"/>; without them the reverb is bypassed).</param>
    /// <param name="gaussian">Empty, or the 512 interpolation table entries.</param>
    /// <exception cref="ArgumentException">A table has the wrong size or a value out of range.</exception>
    public PsxSpuHardwareTables(
        ReadOnlySpan<int> adpcmPositive,
        ReadOnlySpan<int> adpcmNegative,
        ReadOnlySpan<short> reverbFir = default,
        ReadOnlySpan<short> gaussian = default)
    {
        _adpcmPositive = ValidateAdpcm(adpcmPositive, nameof(adpcmPositive));
        _adpcmNegative = ValidateAdpcm(adpcmNegative, nameof(adpcmNegative));

        if (!reverbFir.IsEmpty)
        {
            if (reverbFir.Length != ReverbFirTapCount)
            {
                throw new ArgumentException($"The reverb FIR table needs {ReverbFirTapCount} coefficients, got {reverbFir.Length}.", nameof(reverbFir));
            }

            _reverbFir = reverbFir.ToArray();
        }

        if (!gaussian.IsEmpty)
        {
            if (gaussian.Length != GaussianEntryCount)
            {
                throw new ArgumentException($"The interpolation table needs {GaussianEntryCount} entries, got {gaussian.Length}.", nameof(gaussian));
            }

            _gaussian = gaussian.ToArray();
        }
    }

    /// <summary>True when a Gaussian interpolation table was supplied.</summary>
    public bool HasGaussianTable => _gaussian != null;

    /// <summary>True when the reverb FIR coefficients were supplied.</summary>
    public bool HasReverbFir => _reverbFir != null;

    internal int AdpcmPositive(int filter) => _adpcmPositive[filter];

    internal int AdpcmNegative(int filter) => _adpcmNegative[filter];

    internal short[] GaussianTable => _gaussian;

    internal short[] ReverbFir => _reverbFir;

    private static int[] ValidateAdpcm(ReadOnlySpan<int> values, string name)
    {
        if (values.Length != AdpcmFilterCount)
        {
            throw new ArgumentException($"The ADPCM table needs {AdpcmFilterCount} coefficients, got {values.Length}.", name);
        }

        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] < AdpcmCoefficientMin || values[i] > AdpcmCoefficientMax)
            {
                throw new ArgumentException($"ADPCM coefficient {i} ({values[i]}) is outside {AdpcmCoefficientMin}..{AdpcmCoefficientMax}.", name);
            }
        }

        return values.ToArray();
    }
}
