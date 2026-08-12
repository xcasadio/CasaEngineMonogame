using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Physics;
using Microsoft.Xna.Framework;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace CasaEngine.Framework.Application.Components.Physics;

public sealed class PhysicsWorld : IPhysicsWorld, IDisposable
{
    private readonly bool _useExternalViewManagement;
    private readonly BulletPhysicsEngine _physicsEngine;

    public int CollisionObjectCount => _physicsEngine.CollisionObjectCount;

    public PhysicsWorld(bool useExternalViewManagement)
    {
        _useExternalViewManagement = useExternalViewManagement;
        _physicsEngine = new BulletPhysicsEngine(GameSettings.PhysicsEngineSettings);
    }

    public void Update(float elapsedTime)
    {
        _physicsEngine.Update(elapsedTime);
        _physicsEngine.UpdateContacts();
        _physicsEngine.SendEvents();
    }

    public PhysicsBody AddGhostObject(Shape3d shape, Vector3 localScale, ref Matrix worldMatrix, ICollideableComponent collideableComponent, int collisionProfileId, string fixtureTag = null, Color? color = null)
    {
        return _physicsEngine.AddGhostObject(shape, localScale, ref worldMatrix, collideableComponent, collisionProfileId, fixtureTag, color);
    }

    public PhysicsBody AddGhostObject(IReadOnlyList<ColliderFixture> fixtures, Vector3 localScale, ref Matrix worldMatrix, ICollideableComponent collideableComponent, int collisionProfileId, Color? color = null)
    {
        return _physicsEngine.AddGhostObject(fixtures, localScale, ref worldMatrix, collideableComponent, collisionProfileId, color);
    }

    public PhysicsBody CreateGhostObject(Matrix worldMatrix, ICollideableComponent collideableComponent, Shape3d shape, Vector3 localScale, int collisionProfileId, string fixtureTag = null, Color? color = null)
    {
        return _physicsEngine.CreateGhostObject(worldMatrix, collideableComponent, shape, localScale, collisionProfileId, fixtureTag, color);
    }

    public PhysicsBody CreateGhostObject(Matrix worldMatrix, ICollideableComponent collideableComponent, IReadOnlyList<ColliderFixture> fixtures, Vector3 localScale, int collisionProfileId, Color? color = null)
    {
        return _physicsEngine.CreateGhostObject(worldMatrix, collideableComponent, fixtures, localScale, collisionProfileId, color);
    }

    public PhysicsBody AddStaticObject(Shape3d shape, Vector3 localScale, ref Matrix worldMatrix, object component, PhysicsDefinition physicsDefinition, int collisionProfileId, string fixtureTag = null)
    {
        physicsDefinition.Mass = 0f;
        return AddRigidBody(shape, localScale, ref worldMatrix, component, physicsDefinition, collisionProfileId, fixtureTag);
    }

    public PhysicsBody AddStaticObject(IReadOnlyList<ColliderFixture> fixtures, Vector3 localScale, ref Matrix worldMatrix, object component, PhysicsDefinition physicsDefinition, int collisionProfileId)
    {
        physicsDefinition.Mass = 0f;
        return AddRigidBody(fixtures, localScale, ref worldMatrix, component, physicsDefinition, collisionProfileId);
    }

    public PhysicsBody AddRigidBody(Shape3d shape, Vector3 localScale, ref Matrix worldMatrix, object component, PhysicsDefinition physicsDefinition, int collisionProfileId, string fixtureTag = null)
    {
        return _physicsEngine.AddRigidBody(shape, localScale, ref worldMatrix, component, physicsDefinition, collisionProfileId, _useExternalViewManagement, fixtureTag);
    }

    public PhysicsBody AddRigidBody(IReadOnlyList<ColliderFixture> fixtures, Vector3 localScale, ref Matrix worldMatrix, object component, PhysicsDefinition physicsDefinition, int collisionProfileId)
    {
        return _physicsEngine.AddRigidBody(fixtures, localScale, ref worldMatrix, component, physicsDefinition, collisionProfileId, _useExternalViewManagement);
    }

    public void AddCollisionObject(PhysicsBody collisionObject)
    {
        _physicsEngine.AddCollisionObject(collisionObject);
    }

    public void RemoveCollisionObject(PhysicsBody collisionObject)
    {
        _physicsEngine.RemoveCollisionObject(collisionObject);
    }

    public void AddRigidBody(PhysicsBody rigidBody)
    {
        _physicsEngine.AddRigidBody(rigidBody);
    }

    public void RemoveRigidBody(PhysicsBody rigidBody)
    {
        _physicsEngine.RemoveRigidBody(rigidBody);
    }

    public void ClearCollisionDataFrom(ICollideableComponent component)
    {
        _physicsEngine.ClearCollisionDataOf(component);
    }

    public IReadOnlyCollection<ContactPoint> GetContactPoints(Collision collision)
    {
        return _physicsEngine.LatestContactPointsFor(collision);
    }

    public PhysicsQueryShape CreateQueryShape(Shape3d shape, Vector3 localScale)
    {
        return _physicsEngine.CreateQueryShape(shape, localScale);
    }

    public HitResult ShapeSweep(PhysicsQueryShape shape, Matrix from, Matrix to, uint channelMask = ChannelMask.All, bool hitTriggers = false, ICollideableComponent ignoredComponent = null)
    {
        return _physicsEngine.ShapeSweep(shape, from, to, channelMask, hitTriggers, ignoredComponent);
    }

    public bool ShapeSweep(PhysicsQueryShape shape, Matrix from, Matrix to, out HitResult result, uint channelMask = ChannelMask.All, bool hitTriggers = false, ICollideableComponent ignoredComponent = null)
    {
        return _physicsEngine.ShapeSweep(shape, from, to, out result, channelMask, hitTriggers, ignoredComponent);
    }

    public HitResult ShapeSweep(Shape3d shape, Vector3 localScale, Matrix from, Matrix to, uint channelMask = ChannelMask.All, bool hitTriggers = false, ICollideableComponent ignoredComponent = null)
    {
        return _physicsEngine.ShapeSweep(shape, localScale, from, to, channelMask, hitTriggers, ignoredComponent);
    }

    public bool ShapeSweep(Shape3d shape, Vector3 localScale, Matrix from, Matrix to, out HitResult result, uint channelMask = ChannelMask.All, bool hitTriggers = false, ICollideableComponent ignoredComponent = null)
    {
        return _physicsEngine.ShapeSweep(shape, localScale, from, to, out result, channelMask, hitTriggers, ignoredComponent);
    }

    public void ShapeSweepPenetrating(PhysicsQueryShape shape, Matrix from, Matrix to, ICollection<HitResult> resultsOutput, uint channelMask = ChannelMask.All, bool hitTriggers = false, ICollideableComponent ignoredComponent = null)
    {
        _physicsEngine.ShapeSweepPenetrating(shape, from, to, resultsOutput, channelMask, hitTriggers, ignoredComponent);
    }

    public void ShapeSweepPenetrating(Shape3d shape, Vector3 localScale, Matrix from, Matrix to, ICollection<HitResult> resultsOutput, uint channelMask = ChannelMask.All, bool hitTriggers = false, ICollideableComponent ignoredComponent = null)
    {
        _physicsEngine.ShapeSweepPenetrating(shape, localScale, from, to, resultsOutput, channelMask, hitTriggers, ignoredComponent);
    }

    public bool WorldRayCast(ref Vector3 start, ref Vector3 end, Vector3 dir)
    {
        return _physicsEngine.WorldRayCast(ref start, ref end, dir);
    }

    public bool NearBodyWorldRayCast(ref Vector3 position, ref Vector3 feelers, out Vector3 contactPoint, out Vector3 contactNormal)
    {
        return _physicsEngine.NearBodyWorldRayCast(ref position, ref feelers, out contactPoint, out contactNormal);
    }

    public void RefreshBodyAabb(PhysicsBody body)
    {
        _physicsEngine.RefreshBodyAabb(body);
    }

    public void DrawDebugWorld(IPhysicsDebugDrawer debugDrawer)
    {
        _physicsEngine.DrawDebugWorld(debugDrawer);
    }

    public void Dispose()
    {
        _physicsEngine.Dispose();
    }
}
