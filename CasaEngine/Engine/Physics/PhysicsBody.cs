using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Physics;

public sealed class PhysicsBody : IDisposable
{
    private IPhysicsBodyBackend _backend;

    internal PhysicsBody(IPhysicsBodyBackend backend)
    {
        _backend = backend;
    }

    internal IPhysicsBodyBackend Backend => _backend ?? throw new ObjectDisposedException(nameof(PhysicsBody));

    public bool IsRigidBody => _backend?.IsRigidBody == true;

    public Matrix WorldTransform
    {
        get => Backend.WorldTransform;
        set => Backend.WorldTransform = value;
    }

    public Vector3 LinearVelocity
    {
        get => Backend.LinearVelocity;
        set => Backend.LinearVelocity = value;
    }

    public void ApplyImpulse(Vector3 impulse, Vector3 relativePosition)
    {
        Backend.ApplyImpulse(impulse, relativePosition);
    }

    public void RefreshAabb()
    {
        Backend.RefreshAabb();
    }

    public void Dispose()
    {
        _backend?.Dispose();
        _backend = null;
    }
}

internal interface IPhysicsBodyBackend : IDisposable
{
    bool IsRigidBody { get; }

    Matrix WorldTransform { get; set; }

    Vector3 LinearVelocity { get; set; }

    void ApplyImpulse(Vector3 impulse, Vector3 relativePosition);

    void RefreshAabb();
}