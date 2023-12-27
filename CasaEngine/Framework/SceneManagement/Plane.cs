﻿using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.SceneManagement;

public class Plane
{
    private Microsoft.Xna.Framework.Plane _internalPlane;
    private uint _upperBBCorner = 0;
    private uint _lowerBBCorner = 0;

    public float Nx => _internalPlane.Normal.X;
    public float Ny => _internalPlane.Normal.Y;
    public float Nz => _internalPlane.Normal.Z;
    public float D => _internalPlane.D;

    public static Plane Create(float nX, float nY, float nZ, float D)
    {
        return new Plane(nX, nY, nZ, D);
    }

    public static Plane Create(Plane other)
    {
        return new Plane(other.Nx, other.Ny, other.Nz, other.D);
    }

    protected Plane(float nX, float nY, float nZ, float D)
    {
        _internalPlane = new Microsoft.Xna.Framework.Plane(nX, nY, nZ, D);
        _internalPlane.Normalize();
        ComputeBBCorners();
    }

    public void Transform(Matrix matrix)
    {
        _internalPlane = Microsoft.Xna.Framework.Plane.Normalize(Microsoft.Xna.Framework.Plane.Transform(_internalPlane, matrix));
        ComputeBBCorners();
    }

    private void ComputeBBCorners()
    {
        _upperBBCorner = (_internalPlane.Normal.X >= 0.0f ? 1 : (uint)0) |
                         (_internalPlane.Normal.Y >= 0.0f ? 2 : (uint)0) |
                         (_internalPlane.Normal.Z >= 0.0f ? 4 : (uint)0);

        _lowerBBCorner = (~_upperBBCorner) & 7;
    }

    public float Distance(Vector3 v)
    {
        return _internalPlane.Normal.X * v.X +
               _internalPlane.Normal.Y * v.Y +
               _internalPlane.Normal.Z * v.Z +
               _internalPlane.D;
    }

    /// <summary>
    /// Intersection test between plane and bounding sphere.
    /// </summary>
    /// <param name="bb"></param>
    /// <returns>
    /// return 1 if the bb is completely above plane,
    /// return 0 if the bb intersects the plane,
    /// return -1 if the bb is completely below the plane.
    /// </returns>
    public int Intersect(BoundingBox bb)
    {
        var lowerBBCorner = bb.Corner(_lowerBBCorner);
        var distLower = Distance(lowerBBCorner);

        // If lowest point above plane than all above.
        if (Distance(bb.Corner(_lowerBBCorner)) > 0.0f)
        {
            return 1;
        }

        var upperBBCorner = bb.Corner(_upperBBCorner);
        var distUpper = Distance(upperBBCorner);

        // If highest point is below plane then all below.
        if (Distance(bb.Corner(_upperBBCorner)) < 0.0f)
        {
            return -1;
        }

        // Otherwise, must be crossing a plane
        return 0;
    }
}