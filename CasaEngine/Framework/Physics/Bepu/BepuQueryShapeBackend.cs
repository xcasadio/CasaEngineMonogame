using System;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using Microsoft.Xna.Framework;
using GeometryBox = CasaEngine.Engine.Geometry.Box;
using GeometrySphere = CasaEngine.Engine.Geometry.Sphere;
using GeometryCapsule = CasaEngine.Engine.Geometry.Capsule;
using GeometryCylinder = CasaEngine.Engine.Geometry.Cylinder;

namespace CasaEngine.Framework.Physics.Bepu;

/// <summary>
/// Convex shape kept alive for world queries (sweeps). Stored by value, cooked the same way a body
/// fixture would be (scale baked in); it never needs the shared <c>Shapes</c> registry since queries
/// never insert a collidable into the simulation.
/// </summary>
internal enum BepuQueryShapeType
{
    Box,
    Sphere,
    Capsule,
    Cylinder
}

internal sealed class BepuPhysicsQueryShapeBackend : IPhysicsQueryShapeBackend
{
    public BepuQueryShapeType ShapeType { get; }

    public BepuPhysics.Collidables.Box Box { get; }
    public BepuPhysics.Collidables.Sphere Sphere { get; }
    public BepuPhysics.Collidables.Capsule Capsule { get; }
    public BepuPhysics.Collidables.Cylinder Cylinder { get; }

    private BepuPhysicsQueryShapeBackend(BepuQueryShapeType type, Vector3 dimensions)
    {
        ShapeType = type;
        switch (type)
        {
            case BepuQueryShapeType.Box:
                Box = new BepuPhysics.Collidables.Box(dimensions.X, dimensions.Y, dimensions.Z);
                break;
            case BepuQueryShapeType.Sphere:
                Sphere = new BepuPhysics.Collidables.Sphere(dimensions.X);
                break;
            case BepuQueryShapeType.Capsule:
                Capsule = new BepuPhysics.Collidables.Capsule(dimensions.X, dimensions.Y);
                break;
            case BepuQueryShapeType.Cylinder:
                Cylinder = new BepuPhysics.Collidables.Cylinder(dimensions.X, dimensions.Y);
                break;
        }
    }

    public static BepuPhysicsQueryShapeBackend Create(Shape3d shape, Vector3 localScale)
    {
        ArgumentNullException.ThrowIfNull(shape);

        var dimensions = BepuShapeCache.ComputeDimensions(shape, localScale);
        return shape switch
        {
            GeometryBox => new BepuPhysicsQueryShapeBackend(BepuQueryShapeType.Box, dimensions),
            GeometrySphere => new BepuPhysicsQueryShapeBackend(BepuQueryShapeType.Sphere, dimensions),
            GeometryCapsule => new BepuPhysicsQueryShapeBackend(BepuQueryShapeType.Capsule, dimensions),
            GeometryCylinder => new BepuPhysicsQueryShapeBackend(BepuQueryShapeType.Cylinder, dimensions),
            _ => throw new ArgumentException("This kind of shape cannot be used for a world query.", nameof(shape))
        };
    }

    public void Dispose()
    {
    }
}
