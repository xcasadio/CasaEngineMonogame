using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Physics;

public sealed class PhysicsShape : IDisposable
{
    internal IPhysicsShapeBackend Backend { get; set; }

    private PhysicsShape(PhysicsShapeType type, Vector3 halfExtents, float radius, float height)
    {
        Type = type;
        HalfExtents = halfExtents;
        Radius = radius;
        Height = height;
    }

    public PhysicsShapeType Type { get; }

    public Vector3 HalfExtents { get; }

    public float Radius { get; }

    public float Height { get; }

    public Vector3 LocalScaling { get; set; } = Vector3.One;

    public static PhysicsShape CreateBox(float halfExtentX, float halfExtentY, float halfExtentZ)
    {
        return new PhysicsShape(PhysicsShapeType.Box, new Vector3(halfExtentX, halfExtentY, halfExtentZ), 0f, 0f);
    }

    public static PhysicsShape CreateSphere(float radius)
    {
        return new PhysicsShape(PhysicsShapeType.Sphere, Vector3.Zero, radius, 0f);
    }

    public static PhysicsShape CreateCapsule(float radius, float cylinderHeight)
    {
        return new PhysicsShape(PhysicsShapeType.Capsule, Vector3.Zero, radius, cylinderHeight);
    }

    public static PhysicsShape CreateCylinder(float radius)
    {
        return new PhysicsShape(PhysicsShapeType.Cylinder, Vector3.Zero, radius, 0f);
    }

    public void Dispose()
    {
        Backend?.Dispose();
        Backend = null;
    }
}

internal interface IPhysicsShapeBackend : IDisposable
{
}