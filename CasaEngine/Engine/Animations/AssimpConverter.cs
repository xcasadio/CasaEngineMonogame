using Assimp;
using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Animations;

public static class AssimpConverter
{
    public static float CheckVal(float n)
    {
        if (float.IsNaN(n) || n == float.NaN || float.IsInfinity(n))
        {
            return 0.0f;
        }
        else
        {
            return n;
        }
    }

    public static Microsoft.Xna.Framework.Quaternion ToMonoGame(this Assimp.Quaternion aq)
    {
        //return new Microsoft.Xna.Framework.Quaternion(aq.X, aq.Y, aq.Z, aq.W);
        var m = aq.GetMatrix();
        var n = m.ToMonoGameTransposed();
        var q = Microsoft.Xna.Framework.Quaternion.CreateFromRotationMatrix(n);  //MatrixToQuaternion();
        return q;
    }

    public static Matrix ToMonoGame(this Matrix4x4 ma)
    {
        Matrix m = Matrix.Identity;
        m.M11 = CheckVal(ma.A1); m.M12 = CheckVal(ma.A2); m.M13 = CheckVal(ma.A3); m.M14 = CheckVal(ma.A4);
        m.M21 = CheckVal(ma.B1); m.M22 = CheckVal(ma.B2); m.M23 = CheckVal(ma.B3); m.M24 = CheckVal(ma.B4);
        m.M31 = CheckVal(ma.C1); m.M32 = CheckVal(ma.C2); m.M33 = CheckVal(ma.C3); m.M34 = CheckVal(ma.C4);
        m.M41 = CheckVal(ma.D1); m.M42 = CheckVal(ma.D2); m.M43 = CheckVal(ma.D3); m.M44 = CheckVal(ma.D4);
        return m;
    }
    public static Matrix ToMonoGameTransposed(this Matrix4x4 ma)
    {
        Matrix m = Matrix.Identity;
        m.M11 = CheckVal(ma.A1); m.M12 = CheckVal(ma.A2); m.M13 = CheckVal(ma.A3); m.M14 = CheckVal(ma.A4);
        m.M21 = CheckVal(ma.B1); m.M22 = CheckVal(ma.B2); m.M23 = CheckVal(ma.B3); m.M24 = CheckVal(ma.B4);
        m.M31 = CheckVal(ma.C1); m.M32 = CheckVal(ma.C2); m.M33 = CheckVal(ma.C3); m.M34 = CheckVal(ma.C4);
        m.M41 = CheckVal(ma.D1); m.M42 = CheckVal(ma.D2); m.M43 = CheckVal(ma.D3); m.M44 = CheckVal(ma.D4);
        m = Matrix.Transpose(m);
        return m;
    }
    public static Matrix ToMonoGameTransposed(this Matrix3x3 ma)
    {
        Matrix m = Matrix.Identity;
        ma.Transpose();
        m.M11 = CheckVal(ma.A1); m.M12 = CheckVal(ma.A2); m.M13 = CheckVal(ma.A3); m.M14 = 0;
        m.M21 = CheckVal(ma.B1); m.M22 = CheckVal(ma.B2); m.M23 = CheckVal(ma.B3); m.M24 = 0;
        m.M31 = CheckVal(ma.C1); m.M32 = CheckVal(ma.C2); m.M33 = CheckVal(ma.C3); m.M34 = 0;
        m.M41 = 0; m.M42 = 0; m.M43 = 0; m.M44 = 1;
        return m;
    }

    public static Vector3 ToMonoGame(this Vector3D v)
    {
        return new Vector3(v.X, v.Y, v.Z);
    }

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
        return ("x: " + q.X.ToString(d).PadRight(pamt) + "y: " + q.Y.ToString(d).PadRight(pamt) + "z: " + q.Z.ToString(d).PadRight(pamt) + "w: " + q.W.ToString(d).PadRight(pamt));
    }

    // ______________________

    /// <summary>
    /// just use the assimp version to get the info;
    /// </summary>
    public static string SrtInfoToString(this Matrix mat, string tabspaces)
    {
        Matrix4x4 m = mat.ToAssimpTransposed();
        return SrtInfoToString(m, tabspaces);
    }

    public static string SrtInfoToString(this Matrix4x4 m, string tabspaces)
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
            int padamt = 2;
            str += "\n" + tabspaces + "    " + "As Quaternion     ".PadRight(padamt) + rot.ToStringTrimed();
            str += "\n" + tabspaces + "    " + "Translation           ".PadRight(padamt) + trans.ToStringTrimed();
            if (scale.X != scale.Y || scale.Y != scale.Z || scale.Z != scale.X)
            {
                str += "\n" + tabspaces + "    " + "Scale                    ".PadRight(padamt) + scale.ToStringTrimed();
            }
            else
            {
                str += "\n" + tabspaces + "    " + "Scale                    ".PadRight(padamt) + scale.X.ToStringTrimed();
            }

            str += "\n" + tabspaces + "    " + "Rotation degrees ".PadRight(padamt) + rotDeg.ToStringTrimed();// + "   radians: " + rotAngles.ToStringTrimed();
            str += "\n";
        }
        return str;
    }
    public static string GetSrtFromMatrix(Matrix4x4 m, string tabspaces)
    {
        var checkdeterminatevalid = Math.Abs(m.Determinant()) < 1e-5;
        string str = "";
        int pamt = 12;
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
            str += "\n" + tabspaces + " Rot (deg)".PadRight(pamt) + ":" + rotDeg.ToStringTrimed();// + "   radians: " + rotAngles.ToStringTrimed();
            if (scale.X != scale.Y || scale.Y != scale.Z || scale.Z != scale.X)
            {
                str += "\n" + tabspaces + " Scale ".PadRight(pamt) + ":" + scale.ToStringTrimed();
            }
            else
            {
                str += "\n" + tabspaces + " Scale".PadRight(pamt) + ":" + scale.X.ToStringTrimed();
            }

            str += "\n" + tabspaces + " Position".PadRight(pamt) + ":" + trans.ToStringTrimed();
            str += "\n";
        }
        return str;
    }
    /// <summary>
    /// returns true if decomposed failed.
    /// </summary>
    public static bool GetSrtFromMatrix(Matrix mat, string tabspaces, out Vector3 scale, out Vector3 translation, out Vector3 degRot)
    {
        var m = mat.ToAssimpTransposed();
        var checkdeterminatevalid = Math.Abs(m.Determinant()) < 1e-5;
        string str = "";
        int pamt = 12;
        // this can fail if the determinante is invalid.
        if (checkdeterminatevalid == false)
        {
            Vector3D scale3d = new Vector3D();
            Assimp.Quaternion rot = new Assimp.Quaternion();
            Vector3D rotAngles = new Vector3D();
            Vector3D trans = new Vector3D();
            m.Decompose(out scale3d, out rot, out trans);
            QuatToEulerXyz(ref rot, out rotAngles);
            var rotDeg = rotAngles * (float)(180d / Math.PI);
            scale = scale3d.ToMonoGame();
            degRot = rotDeg.ToMonoGame();
            translation = trans.ToMonoGame();
        }
        else
        {
            Vector3D scale3d = new Vector3D();
            Assimp.Quaternion rot = new Assimp.Quaternion();
            Vector3D rotAngles = new Vector3D();
            Vector3D trans = new Vector3D();
            var rotDeg = rotAngles * (float)(180d / Math.PI);
            scale = scale3d.ToMonoGame();
            degRot = rotAngles.ToMonoGame();
            translation = trans.ToMonoGame();
        }
        return checkdeterminatevalid;
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
        { // singularity at north pole
            outVector.Z = (float)(2 * Math.Atan2(q1.X, q1.W));
            outVector.Y = (float)(Math.PI / 2);
            outVector.X = 0;
            return;
        }
        if (test < -0.499 * unit)
        { // singularity at south pole
            outVector.Z = (float)(-2 * Math.Atan2(q1.X, q1.W));
            outVector.Y = (float)(-Math.PI / 2);
            outVector.X = 0;
            return;
        }
        outVector.Z = (float)Math.Atan2(2 * q1.Y * q1.W - 2 * q1.X * q1.Z, sqx - sqy - sqz + sqw);
        outVector.Y = (float)Math.Asin(2 * test / unit);
        outVector.X = (float)Math.Atan2(2 * q1.X * q1.W - 2 * q1.Y * q1.Z, -sqx + sqy - sqz + sqw);
    }
    public static Matrix4x4 ToAssimpTransposed(this Matrix m)
    {
        Matrix4x4 ma = Matrix4x4.Identity;
        ma.A1 = m.M11; ma.A2 = m.M12; ma.A3 = m.M13; ma.A4 = m.M14;
        ma.B1 = m.M21; ma.B2 = m.M22; ma.B3 = m.M23; ma.B4 = m.M24;
        ma.C1 = m.M31; ma.C2 = m.M32; ma.C3 = m.M33; ma.C4 = m.M34;
        ma.D1 = m.M41; ma.D2 = m.M42; ma.D3 = m.M43; ma.D4 = m.M44;
        ma.Transpose();
        return ma;
    }

}