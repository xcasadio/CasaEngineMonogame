using Assimp;
using Microsoft.Xna.Framework;
using System.Runtime.CompilerServices;
using XNA = Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Animations.SkinModelHelpers;

// C L A S S  -  L O A D E R   E X T E N S I O N S   
public static class LoaderExtensions
{
    // T E S T   V A L  (check if value can be used [else return 0])
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float TestVal(float n)
    {
        if (float.IsNaN(n) || n == float.NaN || float.IsInfinity(n))
        {
            return 0.0f;
        }

        return n;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFinite(float n)
    {
        if (float.IsNaN(n) || n == float.NaN || float.IsInfinity(n))
        {
            return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFinite(this Vector3 v)
    {
        return IsFinite(v.X) && IsFinite(v.Y) && IsFinite(v.Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)] // (I don't know if this will actually inline but it's worth a shot) 
    public static Vector3 TestVec(Vector3 v)
    {
        if (float.IsNaN(v.X) || v.X == float.NaN || float.IsInfinity(v.X))
        {
            v.X = 0f;
        }

        if (float.IsNaN(v.Y) || v.Y == float.NaN || float.IsInfinity(v.Y))
        {
            v.Y = 0f;
        }

        if (float.IsNaN(v.Z) || v.Z == float.NaN || float.IsInfinity(v.Z))
        {
            v.Z = 0f;
        }

        return v;
    }

    public static XNA.Quaternion ToMg(this Assimp.Quaternion aq)
    {
        var m = aq.GetMatrix();
        var n = m.ToMgTransposed();
        var q = XNA.Quaternion.CreateFromRotationMatrix(n);
        return q;
    }

    public static Matrix ToMg(this Matrix4x4 ma)
    {
        Matrix m = Matrix.Identity;
        m.M11 = TestVal(ma.A1); m.M12 = TestVal(ma.A2); m.M13 = TestVal(ma.A3); m.M14 = TestVal(ma.A4);
        m.M21 = TestVal(ma.B1); m.M22 = TestVal(ma.B2); m.M23 = TestVal(ma.B3); m.M24 = TestVal(ma.B4);
        m.M31 = TestVal(ma.C1); m.M32 = TestVal(ma.C2); m.M33 = TestVal(ma.C3); m.M34 = TestVal(ma.C4);
        m.M41 = TestVal(ma.D1); m.M42 = TestVal(ma.D2); m.M43 = TestVal(ma.D3); m.M44 = TestVal(ma.D4);
        return m;
    }

    public static Matrix ToMgTransposed(this Matrix4x4 ma)
    {
        Matrix m = Matrix.Identity;
        m.M11 = TestVal(ma.A1); m.M12 = TestVal(ma.A2); m.M13 = TestVal(ma.A3); m.M14 = TestVal(ma.A4);
        m.M21 = TestVal(ma.B1); m.M22 = TestVal(ma.B2); m.M23 = TestVal(ma.B3); m.M24 = TestVal(ma.B4);
        m.M31 = TestVal(ma.C1); m.M32 = TestVal(ma.C2); m.M33 = TestVal(ma.C3); m.M34 = TestVal(ma.C4);
        m.M41 = TestVal(ma.D1); m.M42 = TestVal(ma.D2); m.M43 = TestVal(ma.D3); m.M44 = TestVal(ma.D4);
        m = Matrix.Transpose(m);
        return m;
    }

    public static Matrix ToMgTransposed(this Matrix3x3 ma)
    {
        Matrix m = Matrix.Identity;
        ma.Transpose();
        m.M11 = TestVal(ma.A1); m.M12 = TestVal(ma.A2); m.M13 = TestVal(ma.A3); m.M14 = 0;
        m.M21 = TestVal(ma.B1); m.M22 = TestVal(ma.B2); m.M23 = TestVal(ma.B3); m.M24 = 0;
        m.M31 = TestVal(ma.C1); m.M32 = TestVal(ma.C2); m.M33 = TestVal(ma.C3); m.M34 = 0;
        m.M41 = 0; m.M42 = 0; m.M43 = 0; m.M44 = 1;
        return m;
    }

    public static Vector3 ToMg(this Vector3D v)
    {
        return new Vector3(v.X, v.Y, v.Z);
    }

    public static Vector4 ToMg(this Color4D v)
    {
        return new Vector4(v.R, v.G, v.B, v.A);
    }

    // These are used by LoadDebugInfo to format certain types of console output
    // T O   S T R I N G   T R I M E D  
    public static string ToStringTrimed(this Vector3D v)
    {
        string d = "+0.000;-0.000"; // "0.00";
        int pamt = 8;
        return (v.X.ToString(d).PadRight(pamt) + ", " + v.Y.ToString(d).PadRight(pamt) + ", " + v.Z.ToString(d).PadRight(pamt));
    }
    public static string ToStringTrimed(this Assimp.Quaternion q)
    {
        string d = "+0.000;-0.000"; // "0.00";
        int pamt = 8;
        return (q.X.ToString(d).PadRight(pamt) + ", " + q.Y.ToString(d).PadRight(pamt) + ", " + q.Z.ToString(d).PadRight(pamt) + "w " + q.W.ToString(d).PadRight(pamt));
    }

    // SRT INFO TO STRING (for Assimp Matrix4x4)
    public static string SrtInfoToString(this Matrix4x4 m, string tabspaces)
    {
        return QsrtInfoToString(m, tabspaces, true);
    }

    // QSRT INFO TO STRING
    private static string QsrtInfoToString(this Matrix4x4 m, string tabspaces, bool showQuaternions)
    {
        var checkdeterminatevalid = Math.Abs(m.Determinant()) < 1e-5;
        string str = "";
        // this can fail if the determinante is invalid.
        if (checkdeterminatevalid == false)
        {
            Vector3D scale;
            Assimp.Quaternion rot;
            Vector3D rotAngles;
            Vector3D trans;
            m.Decompose(out scale, out rot, out trans);
            QuatToEulerXyz(ref rot, out rotAngles);
            var rotDeg = rotAngles * (float)(180d / Math.PI);
            int padamt = 20;
            if (showQuaternions)
            {
                str += "\n" + tabspaces + "    " + "As Quaternion     ".PadRight(padamt) + rot.ToStringTrimed();
            }

            str += "\n" + tabspaces + "    " + "Translation          ".PadRight(padamt) + trans.ToStringTrimed();
            if (scale.X != scale.Y || scale.Y != scale.Z || scale.Z != scale.X)
            {
                str += "\n" + tabspaces + "    " + "Scale                  ".PadRight(padamt) + scale.ToStringTrimed();
            }
            else
            {
                str += "\n" + tabspaces + "    " + "Scale                  ".PadRight(padamt) + scale.X.ToString();//scale.X.ToStringTrimed();
            }

            str += "\n" + tabspaces + "    " + "Rotation degrees  ".PadRight(padamt) + rotDeg.ToStringTrimed();// + "   radians: " + rotAngles.ToStringTrimed();
            str += "\n";
        }
        return str;
    }

    // quat4 -> (roll, pitch, yaw)
    private static void QuatToEulerXyz(ref Assimp.Quaternion q1, out Vector3D outVector)
    {
        // http://www.euclideanspace.com/maths/geometry/rotations/conversions/quaternionToEuler/
        double sqw = q1.W * q1.W;
        double sqx = q1.X * q1.X;
        double sqy = q1.Y * q1.Y;
        double sqz = q1.Z * q1.Z;
        double unit = sqx + sqy + sqz + sqw; // if normalised is one, otherwise is correction factor
        double test = q1.X * q1.Y + q1.Z * q1.W;
        if (test > 0.499 * unit)
        {           // singularity at north pole
            outVector.Z = (float)(2 * Math.Atan2(q1.X, q1.W));
            outVector.Y = (float)(Math.PI / 2);
            outVector.X = 0;
            return;
        }
        if (test < -0.499 * unit)
        {         // singularity at south pole
            outVector.Z = (float)(-2 * Math.Atan2(q1.X, q1.W));
            outVector.Y = (float)(-Math.PI / 2);
            outVector.X = 0;
            return;
        }
        outVector.Z = (float)Math.Atan2(2 * q1.Y * q1.W - 2 * q1.X * q1.Z, sqx - sqy - sqz + sqw);
        outVector.Y = (float)Math.Asin(2 * test / unit);
        outVector.X = (float)Math.Atan2(2 * q1.X * q1.W - 2 * q1.Y * q1.Z, -sqx + sqy - sqz + sqw);
    }
}