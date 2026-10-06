namespace CasaEngine.Framework.Audio.Psx;

/// <summary>
/// The default 39 tap reverb FIR of <see cref="PsxSpuHardwareTables.CreateDefault"/>, computed once by formula. It is
/// an approximation: no hardware value is used (decision D25).
/// Design: windowed-sinc low-pass of S. W. Smith, "The Scientist and Engineer's Guide to Digital Signal Processing",
/// chapter 16, with M = 38 (39 taps) and fc = 0.25 of the 44100 Hz rate (11025 Hz, the Nyquist frequency of the
/// 22050 Hz rate the reverb runs at).
/// Kernel, eq. 16-4 (https://www.dspguide.com/ch16/2.htm): h[i] = K sin(2 pi fc (i - M/2)) / (i - M/2) w[i], the centre
/// point i = M/2 being the limit 2 pi fc K (the text says the division by zero is handled apart).
/// Window, eq. 16-2 (https://www.dspguide.com/ch16/1.htm): Blackman, w[i] = 0.42 - 0.5 cos(2 pi i / M) + 0.08 cos(4 pi i / M).
/// The equations are images on those pages; the forms above are the standard ones the text names (sinc, M/2 shift,
/// Blackman window). K is applied by normalising the sum to 1, then the taps are rounded to Q15 and the centre tap
/// is adjusted so the integers sum to exactly 32768 (unity DC gain).
/// </summary>
internal static class PsxSpuDefaultReverbFir
{
    private const double CutoffFraction = 0.25;
    private const int Order = PsxSpuHardwareTables.ReverbFirTapCount - 1;

    /// <summary>The 39 Q15 coefficients (never modified).</summary>
    internal static readonly short[] Coefficients = Compute();

    private static short[] Compute()
    {
        var taps = PsxSpuHardwareTables.ReverbFirTapCount;
        var kernel = new double[taps];
        var sum = 0.0;
        for (var i = 0; i < taps; i++)
        {
            var shifted = i - Order / 2;
            var sinc = shifted == 0
                ? 2.0 * Math.PI * CutoffFraction
                : Math.Sin(2.0 * Math.PI * CutoffFraction * shifted) / shifted;
            var window = 0.42 - 0.5 * Math.Cos(2.0 * Math.PI * i / Order) + 0.08 * Math.Cos(4.0 * Math.PI * i / Order);
            kernel[i] = sinc * window;
            sum += kernel[i];
        }

        var result = new short[taps];
        var total = 0;
        for (var i = 0; i < taps; i++)
        {
            result[i] = (short)Math.Round(kernel[i] / sum * 32768.0);
            total += result[i];
        }

        result[Order / 2] += (short)(32768 - total);
        return result;
    }
}
