using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Physics;

public sealed class PhysicsBody : IDisposable
{
    private IPhysicsBodyBackend _backend;

    internal PhysicsBody(IPhysicsBodyBackend backend, ResolvedCollisionProfile collisionProfile)
    {
        _backend = backend;
        CollisionProfile = collisionProfile;
    }

    internal IPhysicsBodyBackend Backend => _backend ?? throw new ObjectDisposedException(nameof(PhysicsBody));

    public bool IsRigidBody => _backend?.IsRigidBody == true;

    /// <summary>True when this body lowered several fixtures into a compound shape.</summary>
    public bool IsCompound => Backend.IsCompound;

    /// <summary>Number of fixtures this body was created from, in fixture order.</summary>
    public int FixtureCount => Backend.FixtureCount;

    /// <summary>Local transform of a fixture inside this body, identity for a single fixture body.</summary>
    public Matrix GetFixtureLocalTransform(int index) => Backend.GetFixtureLocalTransform(index);

    /// <summary>Tag of a fixture of this body.</summary>
    public string GetFixtureTag(int index) => Backend.GetFixtureTag(index);

    /// <summary>
    /// Filtering data of this body, resolved from its collision profile when the body was created.
    /// </summary>
    public ResolvedCollisionProfile CollisionProfile { get; }

    /// <summary>
    /// False when the body only raises overlap events instead of pushing other bodies.
    /// </summary>
    public bool HasContactResponse => Backend.HasContactResponse;

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

    bool IsCompound { get; }

    int FixtureCount { get; }

    Matrix GetFixtureLocalTransform(int index);

    string GetFixtureTag(int index);

    bool HasContactResponse { get; }

    Matrix WorldTransform { get; set; }

    Vector3 LinearVelocity { get; set; }

    void ApplyImpulse(Vector3 impulse, Vector3 relativePosition);

    void RefreshAabb();
}