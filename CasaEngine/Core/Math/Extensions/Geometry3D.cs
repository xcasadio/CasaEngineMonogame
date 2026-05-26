using Microsoft.Xna.Framework;

namespace CasaEngine.Core.Math.Extensions;

public static class Geometry3D
{
    public static Vector3 AngleTo(Vector3 from, Vector3 location)
    {
        var angle = new Vector3();
        var v3 = Vector3.Normalize(location - from);
        angle.X = (float)System.Math.Asin(v3.Y);
        angle.Y = MathF.Atan2(-v3.Z, -v3.X);
        return angle;
    }

    public static float GetAngleBetweenVectors(Vector3 a, Vector3 b)
    {
        var dot = Vector3.Dot(a, b);
        dot = MathHelper.Clamp(dot, -1.0f, 1.0f);
        return MathF.Acos(dot);
    }

    /// <summary>
    /// Return angle between two vectors. Used for visibility testing and
    /// for checking angles between vectors for the road sign generation.
    /// </summary>
    /// <param name="axis"></param>
    /// <param name="vec1">Vector 1</param>
    /// <param name="vec2">Vector 2</param>
    /// <returns>Float</returns>
    public static float GetAngleBetweenVectors(Vector3 axis, Vector3 vec1, Vector3 vec2)
    {
        Matrix mat = new Matrix(vec1.X, vec1.Y, vec1.Z, 0.0f, vec2.X, vec2.Y, vec2.Z, 0.0f, axis.X, axis.Y, axis.Z, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);

        float coeff = 1.0f;

        if (mat.Determinant() < 0.0f)
        {
            coeff = -1.0f;
        }

        return coeff * MathF.Acos(Vector3.Dot(vec1, vec2));
    }

    /// <summary>
    /// Return matrix rotation between two vectors
    /// </summary>
    /// <param name="vec1">Vector 1</param>
    /// <param name="vec2">Vector 2</param>
    /// <returns></returns>
    public static Matrix GetRotationMatrixBetweenVectors(Vector3 vec1, Vector3 vec2)
    {
        Vector3 axis = Vector3.Cross(vec1, vec2);
        return GetRotationMatrixBetweenVectors(axis, vec1, vec2);
    }

    /// <summary>
    /// Return matrix rotation between two vectors
    /// </summary>
    /// <param name="axis"></param>
    /// <param name="vec1">Vector 1</param>
    /// <param name="vec2">Vector 2</param>
    /// <returns></returns>
    public static Matrix GetRotationMatrixBetweenVectors(Vector3 axis, Vector3 vec1, Vector3 vec2)
    {
        return Matrix.CreateFromAxisAngle(axis, GetAngleBetweenVectors(axis, vec1, vec2));
    }

    /// <summary>
    /// Return quaternion between two vectors
    /// </summary>
    /// <param name="vec1">Vector 1</param>
    /// <param name="vec2">Vector 2</param>
    /// <returns></returns>
    public static Quaternion GetQuaternionBetweenVectors(Vector3 vec1, Vector3 vec2)
    {
        Vector3 axis = Vector3.Cross(vec1, vec2);
        return GetQuaternionBetweenVectors(axis, vec1, vec2);
    }

    /// <summary>
    /// Return quaternion between two vectors
    /// </summary>
    /// <param name="vec1">Vector 1</param>
    /// <param name="vec2">Vector 2</param>
    /// <returns></returns>
    public static Quaternion GetQuaternionBetweenVectors(Vector3 axis, Vector3 vec1, Vector3 vec2)
    {
        return Quaternion.CreateFromAxisAngle(axis, GetAngleBetweenVectors(axis, vec1, vec2));
    }

    public static bool TryGetRotationBetween(
        Vector3 from,
        Vector3 to,
        out Quaternion rotation)
    {
        rotation = Quaternion.Identity;

        if (from.LengthSquared() <= MathUtils.Epsilon ||
            to.LengthSquared() <= MathUtils.Epsilon)
        {
            return false;
        }

        from.Normalize();
        to.Normalize();

        var dot = MathHelper.Clamp(Vector3.Dot(from, to), -1f, 1f);

        if (dot > 1f - MathUtils.Epsilon)
        {
            rotation = Quaternion.Identity;
            return true;
        }

        if (dot < -1f + MathUtils.Epsilon)
        {
            var axis = Vector3.Cross(from, Vector3.Up);

            if (axis.LengthSquared() <= MathUtils.Epsilon)
                axis = Vector3.Cross(from, Vector3.Right);

            axis.Normalize();
            rotation = Quaternion.CreateFromAxisAngle(axis, MathHelper.Pi);
            return true;
        }

        var rotationAxis = Vector3.Cross(from, to);
        rotationAxis.Normalize();

        rotation = Quaternion.CreateFromAxisAngle(rotationAxis, MathF.Acos(dot));
        return true;
    }

    /// <summary>
    /// Distance from our point to the line described by linePos1 and linePos2.
    /// </summary>
    /// <param name="point">Point</param>
    /// <param name="linePos1">Line position 1</param>
    /// <param name="linePos2">Line position 2</param>
    /// <returns>Float</returns>
    public static float DistanceToLine(Vector3 point,
        Vector3 linePos1, Vector3 linePos2)
    {
        // For help check out this article:
        // http://mathworld.wolfram.com/Point-LineDistance3-Dimensional.html
        Vector3 lineVec = linePos2 - linePos1;
        Vector3 pointVec = linePos1 - point;
        return Vector3.Cross(lineVec, pointVec).Length() / lineVec.Length();
    }

    /// <summary>
    /// Signed distance to plane
    /// </summary>
    /// <param name="point">Point</param>
    /// <param name="planePosition">Plane position</param>
    /// <param name="planeNormal">Plane normal</param>
    /// <returns>Float</returns>
    public static float SignedDistanceToPlane(Vector3 point,
        Vector3 planePosition, Vector3 planeNormal)
    {
        return Vector3.Dot(point - planePosition, planeNormal);
    }


    /// <summary>
    /// Truncates a vector
    /// </summary>
    /// <param name="vector">Vector to truncate</param>
    /// <param name="max">Maximum value of the length of the vector</param>
    /// <returns>The new vector truncated</returns>
    public static Vector3 Truncate(this Vector3 vector, float max)
    {
        float len = vector.Length();
        if (len > max)
        {
            // Do it this way so we're only computing length once, instead of forcing Vector3 to do it too.
            vector *= max / len;
        }
        return vector;
    }

    /// <summary>
    /// Truncates a vector
    /// </summary>
    /// <param name="vector">Vector to truncate</param>
    /// <param name="max">Maximum value of the length of the vector</param>
    public static void Truncate(ref Vector3 vector, float max)
    {
        float len = vector.Length();
        if (len > max)
        {
            // Do it this way so we're only computing length once, instead of forcing Vector3 to do it too.
            vector *= max / len;
        }
    }

    public static Vector2 ToVector2(this Vector3 vector)
    {
        return new Vector2(vector.X, vector.Y);
    }


    /// <summary>
    /// Do a full perspective transform of the given vector by the given matrix,
    /// dividing out the w coordinate to return a Vector3 result.
    /// </summary>
    /// <param name="position">Vector3 of a point in space</param>
    /// <param name="matrix">4x4 matrix</param>
    /// <param name="result">Transformed vector after perspective divide</param>
    public static void PerspectiveTransform(ref Vector3 position, ref Matrix matrix, out Vector3 result)
    {
        float w = position.X * matrix.M14 + position.Y * matrix.M24 + position.Z * matrix.M34 + matrix.M44;
        float winv = 1.0f / w;

        float x = position.X * matrix.M11 + position.Y * matrix.M21 + position.Z * matrix.M31 + matrix.M41;
        float y = position.X * matrix.M12 + position.Y * matrix.M22 + position.Z * matrix.M32 + matrix.M42;
        float z = position.X * matrix.M13 + position.Y * matrix.M23 + position.Z * matrix.M33 + matrix.M43;

        result = new Vector3();
        result.X = x * winv;
        result.Y = y * winv;
        result.Z = z * winv;
    }
}