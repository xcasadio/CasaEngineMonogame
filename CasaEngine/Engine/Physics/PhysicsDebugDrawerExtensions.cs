using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Physics;

/// <summary>Helpers on top of <see cref="IPhysicsDebugDrawer"/> for code that draws its own debug shapes.</summary>
public static class PhysicsDebugDrawerExtensions
{
    /// <summary>
    /// Draws the 12 edges of the axis-aligned box [<paramref name="min"/> ; <paramref name="max"/>] with
    /// <see cref="IPhysicsDebugDrawer.DrawLine"/>. No allocation.
    /// </summary>
    public static void DrawAabb(this IPhysicsDebugDrawer drawer, Vector3 min, Vector3 max, Color color)
    {
        var c000 = new Vector3(min.X, min.Y, min.Z);
        var c100 = new Vector3(max.X, min.Y, min.Z);
        var c110 = new Vector3(max.X, max.Y, min.Z);
        var c010 = new Vector3(min.X, max.Y, min.Z);
        var c001 = new Vector3(min.X, min.Y, max.Z);
        var c101 = new Vector3(max.X, min.Y, max.Z);
        var c111 = new Vector3(max.X, max.Y, max.Z);
        var c011 = new Vector3(min.X, max.Y, max.Z);

        drawer.DrawLine(ref c000, ref c100, color);
        drawer.DrawLine(ref c100, ref c110, color);
        drawer.DrawLine(ref c110, ref c010, color);
        drawer.DrawLine(ref c010, ref c000, color);

        drawer.DrawLine(ref c001, ref c101, color);
        drawer.DrawLine(ref c101, ref c111, color);
        drawer.DrawLine(ref c111, ref c011, color);
        drawer.DrawLine(ref c011, ref c001, color);

        drawer.DrawLine(ref c000, ref c001, color);
        drawer.DrawLine(ref c100, ref c101, color);
        drawer.DrawLine(ref c110, ref c111, color);
        drawer.DrawLine(ref c010, ref c011, color);
    }
}
