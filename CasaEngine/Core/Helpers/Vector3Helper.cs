//-----------------------------------------------------------------------------
// Vector3Helper.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

using Microsoft.Xna.Framework;

namespace CasaEngine.Core.Helpers;

public static class Vector3Helper
{
    public static Vector3 AngleTo(Vector3 from, Vector3 location)
    {
        var angle = new Vector3();
        var v3 = Vector3.Normalize(location - from);
        angle.X = (float)Math.Asin(v3.Y);
        angle.Y = TrigonometryHelper.ArcTanAngle(-v3.Z, -v3.X);
        return angle;
    }

    /// <summary>
    /// Return angle between two vectors. Used for visibility testing and
    /// for checking angles between vectors for the road sign generation.
    /// </summary>
    /// <param name="vec1">Vector 1</param>
    /// <param name="vec2">Vector 2</param>
    /// <returns>Float</returns>
    public static float GetAngleBetweenVectors(Vector3 vec1, Vector3 vec2)
    {
        // See http://en.wikipedia.org/wiki/Vector_(spatial)
        // for help and check out the Dot Product section ^^
        // Both vectors are normalized so we can save deviding through the
        // lengths.
        return MathUtils.Acos(Vector3.Dot(vec1, vec2));
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

        return coeff * MathUtils.Acos(Vector3.Dot(vec1, vec2));
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
        /*Matrix mat = new Matrix( vec1_.X, vec1_.Y, vec1_.Z, 0.0f, vec2_.X, vec2_.Y, vec2_.Z, 0.0f, axis_.X, axis_.Y, axis_.Z, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f );

        float coeff = 1.0f;

        if ( mat.Determinant() < 0.0f )
        {
            coeff = -1.0f;
        }*/

        return Matrix.CreateFromAxisAngle(axis,/* coeff * */GetAngleBetweenVectors(axis, vec1, vec2));
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
        /*Matrix mat = new Matrix(vec1_.X, vec1_.Y, vec1_.Z, 0.0f, vec2_.X, vec2_.Y, vec2_.Z, 0.0f, axis_.X, axis_.Y, axis_.Z, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);

        float coeff = 1.0f;

        if (mat.Determinant() < 0.0f)
        {
            coeff = -1.0f;
        }
        
        return Quaternion.CreateFromAxisAngle(axis_, coeff * GetAngleBetweenVectors(vec1_, vec2_));
        */

        //float fDot = Vector3.Dot(vec1_, vec2_);
        //return new Quaternion(axis_.X, axis_.Y, axis_.Z, fDot);
        return Quaternion.CreateFromAxisAngle(axis, GetAngleBetweenVectors(axis, vec1, vec2));
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
        Vector3 pointVec = planePosition - point;
        return Vector3.Dot(planeNormal, pointVec);
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