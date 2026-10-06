namespace CasaEngine.Framework.Audio.Decoding;

/// <summary>
/// Sample conversions shared by the decoders, so every format maps float to 16 bit the same way.
/// </summary>
internal static class PcmConversion
{
    /// <summary>NaN becomes silence, then the value is clamped to [-1, 1], scaled by 32767 and rounded.</summary>
    public static short FloatToInt16(float value)
    {
        // NaN compares false with everything: map it to silence rather than an arbitrary value.
        if (float.IsNaN(value))
        {
            return 0;
        }

        return (short)MathF.Round(Math.Clamp(value, -1f, 1f) * short.MaxValue);
    }
}
