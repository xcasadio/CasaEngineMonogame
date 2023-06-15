using Microsoft.Xna.Framework;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace CasaEngine.Core.Helpers;

public static class MatrixExtensions
{
    // Therefore the extrinsic rotations to achieve this matrix is the reversed order of operations,
    // ie. Matrix.RotationZ(roll) * Matrix.RotationX(pitch) * Matrix.RotationY(yaw)
    public static void Decompose(this Matrix matrix, out float yaw, out float pitch, out float roll)
    {
        // Adapted from 'Euler Angle Formulas' by David Eberly - https://www.geometrictools.com/Documentation/EulerAngles.pdf
        // 2.3 Factor as Ry Rx Rz
        // License under CC BY 4.0 (https://creativecommons.org/licenses/by/4.0/)
        //
        // Note the Stride's matrix row/column ordering is swapped, indices starts at one,
        // and the if-statement ordering is written to minimize the number of operations to get to
        // the common case, and made to handle the +/- 1 cases better due to low precision in floats
        if (MathUtils.IsOne(Math.Abs(matrix.M32)))
        {
            if (matrix.M32 >= 0)
            {
                // Edge case where M32 == +1
                pitch = -MathHelper.PiOver2;
                yaw = MathF.Atan2(-matrix.M21, matrix.M11);
                roll = 0;
            }
            else
            {
                // Edge case where M32 == -1
                pitch = MathHelper.PiOver2;
                yaw = -MathF.Atan2(-matrix.M21, matrix.M11);
                roll = 0;
            }
        }
        else
        {
            // Common case
            pitch = MathF.Asin(-matrix.M32);
            yaw = MathF.Atan2(matrix.M31, matrix.M33);
            roll = MathF.Atan2(matrix.M12, matrix.M22);
        }
    }

    // Creates a matrix that contains both the X, Y and Z rotation, as well as scaling and translation. Note: This function is NOT thread safe.
    // rotation: Angle of rotation in radians. Angles are measured clockwise when looking along the rotation axis toward the origin
    public static Matrix Transformation(Vector3 scaling, Quaternion rotation, Vector3 translation)
    {
        return Transformation(ref scaling, ref rotation, ref translation);
    }

    // Creates a matrix that contains both the X, Y and Z rotation, as well as scaling and translation. Note: This function is NOT thread safe.
    // rotation: Angle of rotation in radians. Angles are measured clockwise when looking along the rotation axis toward the origin
    public static Matrix Transformation(ref Vector3 scaling, ref Quaternion rotation, ref Vector3 translation)
    {
        // Equivalent to:
        //result =
        //    Matrix.Scaling(scaling)
        //    *Matrix.RotationX(rotation.X)
        //    *Matrix.RotationY(rotation.Y)
        //    *Matrix.RotationZ(rotation.Z)
        //    *Matrix.Position(translation);

        Matrix result = new Matrix();

        // Rotation
        float xx = rotation.X * rotation.X;
        float yy = rotation.Y * rotation.Y;
        float zz = rotation.Z * rotation.Z;
        float xy = rotation.X * rotation.Y;
        float zw = rotation.Z * rotation.W;
        float zx = rotation.Z * rotation.X;
        float yw = rotation.Y * rotation.W;
        float yz = rotation.Y * rotation.Z;
        float xw = rotation.X * rotation.W;

        result.M11 = 1.0f - (2.0f * (yy + zz));
        result.M12 = 2.0f * (xy + zw);
        result.M13 = 2.0f * (zx - yw);
        result.M21 = 2.0f * (xy - zw);
        result.M22 = 1.0f - (2.0f * (zz + xx));
        result.M23 = 2.0f * (yz + xw);
        result.M31 = 2.0f * (zx + yw);
        result.M32 = 2.0f * (yz - xw);
        result.M33 = 1.0f - (2.0f * (yy + xx));

        // Position
        result.M41 = translation.X;
        result.M42 = translation.Y;
        result.M43 = translation.Z;

        // Scale
        if (scaling.X != 1.0f)
        {
            result.M11 *= scaling.X;
            result.M12 *= scaling.X;
            result.M13 *= scaling.X;
        }
        if (scaling.Y != 1.0f)
        {
            result.M21 *= scaling.Y;
            result.M22 *= scaling.Y;
            result.M23 *= scaling.Y;
        }
        if (scaling.Z != 1.0f)
        {
            result.M31 *= scaling.Z;
            result.M32 *= scaling.Z;
            result.M33 *= scaling.Z;
        }

        result.M14 = 0.0f;
        result.M24 = 0.0f;
        result.M34 = 0.0f;
        result.M44 = 1.0f;

        return result;
    }
}