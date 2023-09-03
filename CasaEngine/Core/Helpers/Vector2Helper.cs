using Microsoft.Xna.Framework;

namespace CasaEngine.Core.Helpers;

public static class Vector2Helper
{
    /// <summary>
    /// Return angle between two vectors. Used for visibility testing and
    /// for checking angles between vectors for the road sign generation.
    /// </summary>
    /// <param name="vec1">Vector 1</param>
    /// <param name="vec2">Vector 2</param>
    /// <returns>Float</returns>
    public static float GetAngleBetweenVectors(Vector2 vec1, Vector2 vec2)
    {
        // See http://en.wikipedia.org/wiki/Vector_(spatial)
        // for help and check out the Dot Product section ^^
        // Both vectors are normalized so we can save deviding through the
        // lengths.
        return MathUtils.Acos(Vector2.Dot(vec1, vec2));
    }

    public static void Cross(ref Vector2 a, ref Vector2 b, out float c)
    {
        c = a.X * b.Y - a.Y * b.X;
    }

    public static float Cross(ref Vector2 a, ref Vector2 b)
    {
        return a.X * b.Y - a.Y * b.X;
    }

    public static float Cross(Vector2 a, Vector2 b)
    {
        return Cross(ref a, ref b);
    }
    public static Vector2 Cross(Vector2 a, float s)
    {
        return new Vector2(s * a.Y, -s * a.X);
    }

    public static Vector2 Cross(float s, Vector2 a)
    {
        return new Vector2(-s * a.Y, s * a.X);
    }


    /// <summary>Returns a positive number if c is to the left of the line going from a to b.</summary>
    /// <returns>Positive number if point is left, negative if point is right, and 0 if points are collinear.</returns>
    public static float Area(Vector2 a, Vector2 b, Vector2 c)
    {
        return Area(ref a, ref b, ref c);
    }

    /// <summary>Returns a positive number if c is to the left of the line going from a to b.</summary>
    /// <returns>Positive number if point is left, negative if point is right, and 0 if points are collinear.</returns>
    public static float Area(ref Vector2 a, ref Vector2 b, ref Vector2 c)
    {
        return a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y);
    }

    /// <summary>Determines if three vertices are collinear (ie. on a straight line)</summary>
    public static bool IsCollinear(ref Vector2 a, ref Vector2 b, ref Vector2 c, float tolerance = 0)
    {
        return MathUtils.FloatInRange(Area(ref a, ref b, ref c), -tolerance, tolerance);
    }

    public static Vector3 ToVector3(this Vector2 vector)
    {
        return new Vector3(vector, 0f);
    }
}