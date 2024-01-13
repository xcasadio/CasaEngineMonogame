using Microsoft.Xna.Framework;

namespace CasaEngine.Core.Helpers;

public static class BoundingSphereExtension
{
    public static BoundingSphere Create()
    {
        var boundingSphere = new BoundingSphere();
        boundingSphere.Init();
        return boundingSphere;
    }

    public static bool Valid(this ref BoundingSphere boundingSphere)
    {
        return boundingSphere.Radius >= 0.0f;
    }

    public static void Init(this ref BoundingSphere boundingSphere)
    {
        boundingSphere.Center.X = 0.0f;
        boundingSphere.Center.Y = 0.0f;
        boundingSphere.Center.Z = 0.0f;
        boundingSphere.Radius = -1.0f;
    }

    /// <summary>
    /// Expand bounding sphere to include sh. Repositions
    /// The sphere center to minimize the radius increase.
    /// </summary>
    /// <param name="sh"></param>
    public static void ExpandBy(this ref BoundingSphere boundingSphere, BoundingSphere sh)
    {
        // ignore operation if incoming BoundingSphere is invalid.
        if (!sh.Valid()) return;

        // This sphere is not set so use the inbound sphere
        if (!boundingSphere.Valid())
        {
            boundingSphere.Center = sh.Center;
            boundingSphere.Radius = sh.Radius;

            return;
        }


        // Calculate d == The distance between the sphere centers
        double d = (boundingSphere.Center - sh.Center).Length();

        // New sphere is already inside this one
        if (d + sh.Radius <= boundingSphere.Radius)
        {
            return;
        }

        //  New sphere completely contains this one
        if (d + boundingSphere.Radius <= sh.Radius)
        {
            boundingSphere.Center = sh.Center;
            boundingSphere.Radius = sh.Radius;
            return;
        }


        // Build a new sphere that completely contains the other two:
        //
        // The center point lies halfway along the line between the furthest
        // points on the edges of the two spheres.
        //
        // Computing those two points is ugly - so we'll use similar triangles
        var newRadius = (boundingSphere.Radius + d + sh.Radius) * 0.5;
        var ratio = (newRadius - boundingSphere.Radius) / d;

        boundingSphere.Center.X += (sh.Center.X - boundingSphere.Center.X) * (float)ratio;
        boundingSphere.Center.Y += (sh.Center.Y - boundingSphere.Center.Y) * (float)ratio;
        boundingSphere.Center.Z += (sh.Center.Z - boundingSphere.Center.Z) * (float)ratio;

        boundingSphere.Radius = (float)newRadius;
    }

    public static void ExpandRadiusBy(this ref BoundingSphere boundingSphere, BoundingSphere sh)
    {
        if (!sh.Valid())
        {
            return;
        }

        if (boundingSphere.Valid())
        {
            var r = (sh.Center - boundingSphere.Center).Length() + sh.Radius;
            if (r > boundingSphere.Radius)
            {
                boundingSphere.Radius = r;
            }
        }
        else
        {
            boundingSphere.Center = sh.Center;
            boundingSphere.Radius = sh.Radius;
        }
    }
}