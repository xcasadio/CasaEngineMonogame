using CasaEngine.Core.Helper;
using Microsoft.Xna.Framework;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace CasaEngine.Core;

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
}