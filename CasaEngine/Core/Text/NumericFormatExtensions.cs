using Microsoft.Xna.Framework;

namespace CasaEngine.Core.Text;

/// <summary>
/// Formatting helpers for numeric and MonoGame math types.
/// Moved from Engine/Animations/Extensions.cs during architecture cleanup.
/// </summary>
public static class NumericFormatExtensions
{
    private const string FormatSpec = "+0.000;-0.000";
    private const int PadAmount = 8;

    public static string ToStringTrimed(this int v)
    {
        return v.ToString(FormatSpec).PadRight(PadAmount);
    }

    public static string ToStringTrimed(this float v)
    {
        return v.ToString(FormatSpec).PadRight(PadAmount);
    }

    public static string ToStringTrimed(this double v)
    {
        return v.ToString(FormatSpec).PadRight(PadAmount);
    }

    public static string ToStringTrimed(this Vector3 v)
    {
        return v.X.ToString(FormatSpec).PadRight(PadAmount) + ", "
             + v.Y.ToString(FormatSpec).PadRight(PadAmount) + ", "
             + v.Z.ToString(FormatSpec).PadRight(PadAmount);
    }

    public static string ToStringTrimed(this Vector4 v)
    {
        return v.X.ToString(FormatSpec).PadRight(PadAmount) + ", "
             + v.Y.ToString(FormatSpec).PadRight(PadAmount) + ", "
             + v.Z.ToString(FormatSpec).PadRight(PadAmount) + ", "
             + v.W.ToString(FormatSpec).PadRight(PadAmount);
    }

    public static string ToStringTrimed(this Quaternion q)
    {
        return "x: " + q.X.ToString(FormatSpec).PadRight(PadAmount)
             + "y: " + q.Y.ToString(FormatSpec).PadRight(PadAmount)
             + "z: " + q.Z.ToString(FormatSpec).PadRight(PadAmount)
             + "w: " + q.W.ToString(FormatSpec).PadRight(PadAmount);
    }
}
