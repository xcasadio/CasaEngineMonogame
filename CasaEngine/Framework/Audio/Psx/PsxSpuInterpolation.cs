namespace CasaEngine.Framework.Audio.Psx;

/// <summary>
/// Extension point of the SPU: turns the four most recent decoded ADPCM samples and the 8 bit fractional position
/// of the pitch counter into one output sample. Implementations must not allocate.
/// </summary>
public interface IPsxSpuInterpolator
{
    /// <param name="oldest">Fourth most recent sample.</param>
    /// <param name="older">Third most recent sample.</param>
    /// <param name="old">Second most recent sample.</param>
    /// <param name="newest">Most recent sample.</param>
    /// <param name="index">Interpolation index, 0..255 (bits 4 to 11 of the pitch counter).</param>
    /// <returns>A value inside the signed 16 bit range.</returns>
    int Interpolate(int oldest, int older, int old, int newest, int index);
}

/// <summary>
/// Engine default: 4 point cubic Hermite (Catmull-Rom, like the engine mixer) between <c>older</c> and <c>old</c>,
/// the index being the fraction (index / 256) of that segment. psx-spx does not define a cubic interpolation, so the
/// segment choice is an engine decision (the output lags the newest sample by one more sample).
/// </summary>
public sealed class PsxCubicInterpolator : IPsxSpuInterpolator
{
    public static PsxCubicInterpolator Instance { get; } = new();

    public int Interpolate(int oldest, int older, int old, int newest, int index)
    {
        if (index == 0)
        {
            return older;
        }

        var t = index * (1f / 256f);
        var value = older + 0.5f * t * (old - oldest
            + t * (2f * oldest - 5f * older + 4f * old - newest
            + t * (3f * (older - old) + newest - oldest)));
        var rounded = (int)MathF.Floor(value + 0.5f);
        return Math.Clamp(rounded, short.MinValue, short.MaxValue);
    }
}

/// <summary>
/// Gaussian interpolation from psx-spx ("4-Point Gaussian Interpolation"): each tap is the product of a table entry
/// and a sample, arithmetically shifted right by 15, the four terms being added.
/// </summary>
public sealed class PsxGaussianInterpolator : IPsxSpuInterpolator
{
    private readonly short[] _table;

    /// <param name="tables">Tables that provide the 512 entry interpolation table.</param>
    /// <exception cref="ArgumentException">The tables have no interpolation table.</exception>
    public PsxGaussianInterpolator(PsxSpuHardwareTables tables)
    {
        ArgumentNullException.ThrowIfNull(tables);
        _table = tables.GaussianTable ?? throw new ArgumentException("No interpolation table was supplied.", nameof(tables));
    }

    public int Interpolate(int oldest, int older, int old, int newest, int index)
    {
        var sum = ((_table[0xFF - index] * oldest) >> 15)
                  + ((_table[0x1FF - index] * older) >> 15)
                  + ((_table[0x100 + index] * old) >> 15)
                  + ((_table[index] * newest) >> 15);
        // AMBIGUOUS (psx-spx): the page gives no saturation of the interpolated sum; clamped to 16 bit here.
        return Math.Clamp(sum, short.MinValue, short.MaxValue);
    }
}
