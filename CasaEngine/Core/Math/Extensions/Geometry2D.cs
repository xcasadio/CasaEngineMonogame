using Microsoft.Xna.Framework;

namespace CasaEngine.Core.Math.Extensions;

public static class Geometry2D
{
    public static float GetAngleBetweenVectors(Vector2 a, Vector2 b)
    {
        if (a.LengthSquared() <= MathUtils.Epsilon || b.LengthSquared() <= MathUtils.Epsilon)
        {
            return 0f;
        }

        a.Normalize();
        b.Normalize();

        return MathF.Acos(MathHelper.Clamp(Vector2.Dot(a, b), -1f, 1f));
    }

    public static float GetAngleBetweenNormalizedVectors(Vector2 a, Vector2 b)
    {
        if (a.LengthSquared() <= MathUtils.Epsilon || b.LengthSquared() <= MathUtils.Epsilon)
        {
            return 0f;
        }

        return MathF.Acos(MathHelper.Clamp(Vector2.Dot(a, b), -1f, 1f));
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