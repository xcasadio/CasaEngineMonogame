using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Animations;

public static class Extensions
{
    // exensions

    public static Color ToColor(this Vector4 v)
    {
        return new Color(v.X, v.Y, v.Z, v.W);
    }
    public static Vector4 ToVector4(this Color v)
    {
        return new Vector4(1f / v.R, 1f / v.G, 1f / v.B, 1f / v.A);
    }

    public static string ToStringTrimed(this int v)
    {
        string d = "+0.000;-0.000"; // "0.00";
        int pamt = 8;
        return (v.ToString(d).PadRight(pamt));
    }
    public static string ToStringTrimed(this float v)
    {
        string d = "+0.000;-0.000"; // "0.00";
        int pamt = 8;
        return (v.ToString(d).PadRight(pamt));
    }
    public static string ToStringTrimed(this double v)
    {
        string d = "+0.000;-0.000"; // "0.00";
        int pamt = 8;
        return (v.ToString(d).PadRight(pamt));
    }
    public static string ToStringTrimed(this Vector3 v)
    {
        string d = "+0.000;-0.000"; // "0.00";
        int pamt = 8;
        return (v.X.ToString(d).PadRight(pamt) + ", " + v.Y.ToString(d).PadRight(pamt) + ", " + v.Z.ToString(d).PadRight(pamt));
    }
    public static string ToStringTrimed(this Vector4 v)
    {
        string d = "+0.000;-0.000"; // "0.00";
        int pamt = 8;
        return (v.X.ToString(d).PadRight(pamt) + ", " + v.Y.ToString(d).PadRight(pamt) + ", " + v.Z.ToString(d).PadRight(pamt) + ", " + v.W.ToString(d).PadRight(pamt));
    }
    public static string ToStringTrimed(this Quaternion q)
    {
        string d = "+0.000;-0.000"; // "0.00";
        int pamt = 8;
        return ("x: " + q.X.ToString(d).PadRight(pamt) + "y: " + q.Y.ToString(d).PadRight(pamt) + "z: " + q.Z.ToString(d).PadRight(pamt) + "w: " + q.W.ToString(d).PadRight(pamt));
    }

    public static Matrix Invert(this Matrix m)
    {
        return Matrix.Invert(m);
    }
}

