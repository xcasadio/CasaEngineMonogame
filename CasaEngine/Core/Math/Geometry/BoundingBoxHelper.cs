using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Core.Math.Geometry;

public static class BoundingBoxHelper
{
    public static BoundingBox Create()
    {
        var boundingBox = new BoundingBox();
        boundingBox.Initialize();
        return boundingBox;
    }

    public static void Initialize(this ref BoundingBox boundingBox)
    {
        boundingBox.Min.X = float.PositiveInfinity;
        boundingBox.Min.Y = float.PositiveInfinity;
        boundingBox.Min.Z = float.PositiveInfinity;

        boundingBox.Max.X = float.NegativeInfinity;
        boundingBox.Max.Y = float.NegativeInfinity;
        boundingBox.Max.Z = float.NegativeInfinity;
    }

    public static bool Valid(this ref BoundingBox boundingBox)
    {
        return boundingBox.Max.X >= boundingBox.Min.X &&
               boundingBox.Max.Y >= boundingBox.Min.Y &&
               boundingBox.Max.Z >= boundingBox.Min.Z;
    }

    public static void ExpandBy(this ref BoundingBox boundingBox, BoundingBox bb)
    {
        if (!bb.Valid())
        {
            return;
        }

        if (bb.Min.X < boundingBox.Min.X)
        {
            boundingBox.Min.X = bb.Min.X;
        }

        if (bb.Max.X > boundingBox.Max.X)
        {
            boundingBox.Max.X = bb.Max.X;
        }

        if (bb.Min.Y < boundingBox.Min.Y)
        {
            boundingBox.Min.Y = bb.Min.Y;
        }

        if (bb.Max.Y > boundingBox.Max.Y)
        {
            boundingBox.Max.Y = bb.Max.Y;
        }

        if (bb.Min.Z < boundingBox.Min.Z)
        {
            boundingBox.Min.Z = bb.Min.Z;
        }

        if (bb.Max.Z > boundingBox.Max.Z)
        {
            boundingBox.Max.Z = bb.Max.Z;
        }
    }

    public static void ExpandBy(this ref BoundingBox boundingBox, BoundingSphere sh)
    {
        if (!sh.Valid())
        {
            return;
        }

        if (sh.Center.X - sh.Radius < boundingBox.Min.X)
        {
            boundingBox.Min.X = sh.Center.X - sh.Radius;
        }

        if (sh.Center.X + sh.Radius > boundingBox.Max.X)
        {
            boundingBox.Max.X = sh.Center.X + sh.Radius;
        }

        if (sh.Center.Y - sh.Radius < boundingBox.Min.Y)
        {
            boundingBox.Min.Y = sh.Center.Y - sh.Radius;
        }

        if (sh.Center.Y + sh.Radius > boundingBox.Max.Y)
        {
            boundingBox.Max.Y = sh.Center.Y + sh.Radius;
        }

        if (sh.Center.Z - sh.Radius < boundingBox.Min.Z)
        {
            boundingBox.Min.Z = sh.Center.Z - sh.Radius;
        }

        if (sh.Center.Z + sh.Radius > boundingBox.Max.Z)
        {
            boundingBox.Max.Z = sh.Center.Z + sh.Radius;
        }
    }

    public static void ExpandBy(this ref BoundingBox boundingBox, Vector3 v)
    {
        if (v.X < boundingBox.Min.X)
        {
            boundingBox.Min.X = v.X;
        }

        if (v.X > boundingBox.Max.X)
        {
            boundingBox.Max.X = v.X;
        }

        if (v.Y < boundingBox.Min.Y)
        {
            boundingBox.Min.Y = v.Y;
        }

        if (v.Y > boundingBox.Max.Y)
        {
            boundingBox.Max.Y = v.Y;
        }

        if (v.Z < boundingBox.Min.Z)
        {
            boundingBox.Min.Z = v.Z;
        }

        if (v.Z > boundingBox.Max.Z)
        {
            boundingBox.Max.Z = v.Z;
        }
    }

    public static void ExpandBy(this ref BoundingBox boundingBox, float x, float y, float z)
    {
        if (x < boundingBox.Min.X)
        {
            boundingBox.Min.X = x;
        }

        if (x > boundingBox.Max.X)
        {
            boundingBox.Max.X = x;
        }

        if (y < boundingBox.Min.Y)
        {
            boundingBox.Min.Y = y;
        }

        if (y > boundingBox.Max.Y)
        {
            boundingBox.Max.Y = y;
        }

        if (z < boundingBox.Min.Z)
        {
            boundingBox.Min.Z = z;
        }

        if (z > boundingBox.Max.Z)
        {
            boundingBox.Max.Z = z;
        }
    }

    public static Vector3 GetCenter(this ref BoundingBox boundingBox) => (boundingBox.Min + boundingBox.Max) * 0.5f;

    public static float GetRadius(this ref BoundingBox boundingBox)
    {
        return System.MathF.Sqrt(boundingBox.GetRadiusSquared());
    }

    public static float GetRadiusSquared(this ref BoundingBox boundingBox)
    {
        return 0.25f * ((boundingBox.Max - boundingBox.Min).LengthSquared());
    }

    /// <summary>
    /// Returns a specific corner of the bounding box.
    /// pos specifies the corner as a number between 0 and 7.
    /// Each bit selects an axis, X, Y, or Z from least- to
    /// most-significant. Unset bits select the minimum value
    /// for that axis, and set bits select the maximum.
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    public static Vector3 Corner(this ref BoundingBox boundingBox, uint pos)
    {
        return new Vector3(
            (pos & 1) > 0 ? boundingBox.Max.X : boundingBox.Min.X,
            (pos & 2) > 0 ? boundingBox.Max.Y : boundingBox.Min.Y,
            (pos & 4) > 0 ? boundingBox.Max.Z : boundingBox.Min.Z);
    }

    public static Vector3 GetDimensions(this ref BoundingBox boundingBox)
    {
        return boundingBox.Max - boundingBox.Min;
    }

    public static BoundingBox Transform(this BoundingBox boundingBox, Matrix matrix)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        for (uint cornerIndex = 0; cornerIndex < 8; cornerIndex++)
        {
            var corner = Vector3.Transform(boundingBox.Corner(cornerIndex), matrix);
            min = Vector3.Min(min, corner);
            max = Vector3.Max(max, corner);
        }

        return new BoundingBox(min, max);
    }
    
    public static bool ContainsNaN(this ref BoundingBox boundingBox)
    {
        return float.IsNaN(boundingBox.Min.X) || float.IsNaN(boundingBox.Min.Y) || float.IsNaN(boundingBox.Min.Z)
               || float.IsNaN(boundingBox.Max.X) || float.IsNaN(boundingBox.Max.Y) || float.IsNaN(boundingBox.Max.Z);
    }
}