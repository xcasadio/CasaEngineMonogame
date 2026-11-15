using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Display-colour to linear-light conversion of the linear colour pipeline (ADR-0102). The engine's colour encoding is the
/// 2.2 power law, not the exact sRGB curve: <c>Decode(c) = c^2.2</c>, the inverse of the <c>pow(c, 1 / 2.2)</c> that
/// <c>Tonemap.fx</c> writes. The shaders apply the same function (<c>DecodeColor</c>) to the values they read.
/// </summary>
public static class ColorSpace
{
    /// <summary>The exponent of the display encoding.</summary>
    public const float DisplayGamma = 2.2f;

    /// <summary>Decodes one display-encoded channel to linear light. Negative values are decoded by magnitude, like the shader.</summary>
    public static float Decode(float value) => MathF.Pow(MathF.Abs(value), DisplayGamma);

    /// <summary>Decodes a display-encoded colour to linear light, channel by channel.</summary>
    public static Vector3 Decode(Vector3 color) => new(Decode(color.X), Decode(color.Y), Decode(color.Z));

    /// <summary>Decodes a display-encoded colour to linear light; alpha is left alone.</summary>
    public static Vector4 Decode(Vector4 color) => new(Decode(color.X), Decode(color.Y), Decode(color.Z), color.W);

    /// <summary>
    /// Decodes an 8-bit colour into a floating-point colour (an 8-bit result would quantise dark values to 0 or 1/255).
    /// Alpha is left alone.
    /// </summary>
    public static Vector4 Decode(Color color) => Decode(color.ToVector4());
}
