namespace CasaEngine.Engine.Physics;

/// <summary>
/// Backend handle of a convex shape kept alive for world queries (sweeps).
/// Created from a <see cref="CasaEngine.Engine.Geometry.Shape3d"/> by the physics world and owned by its caller.
/// </summary>
public sealed class PhysicsQueryShape : IDisposable
{
    private IPhysicsQueryShapeBackend _backend;

    internal PhysicsQueryShape(IPhysicsQueryShapeBackend backend)
    {
        _backend = backend;
    }

    internal IPhysicsQueryShapeBackend Backend => _backend ?? throw new ObjectDisposedException(nameof(PhysicsQueryShape));

    public void Dispose()
    {
        _backend?.Dispose();
        _backend = null;
    }
}

internal interface IPhysicsQueryShapeBackend : IDisposable
{
}
