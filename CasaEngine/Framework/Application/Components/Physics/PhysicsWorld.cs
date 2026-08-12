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

    public PhysicsBody AddGhostObject(PhysicsShape collisionShape, ref Matrix worldMatrix, ICollideableComponent collideableComponent, int collisionProfileId, Color? color = null)
    {
        return _physicsEngine.AddGhostObject(collisionShape, ref worldMatrix, collideableComponent, collisionProfileId, color);
    }

    public PhysicsBody CreateGhostObject(Matrix worldMatrix, ICollideableComponent collideableComponent, PhysicsShape collisionShape, int collisionProfileId, Color? color = null)
    {
        return _physicsEngine.CreateGhostObject(worldMatrix, collideableComponent, collisionShape, collisionProfileId, color);
    }

    public PhysicsBody AddStaticObject(PhysicsShape collisionShape, Vector3 localScale, ref Matrix worldMatrix, object component, PhysicsDefinition physicsDefinition)
    {
        physicsDefinition.Mass = 0f;
        return AddRigidBody(collisionShape, localScale, ref worldMatrix, component, physicsDefinition);
    }

    public PhysicsBody AddRigidBody(PhysicsShape collisionShape, Vector3 localScale, ref Matrix worldMatrix, object component, PhysicsDefinition physicsDefinition)
    {
        collisionShape.LocalScaling = localScale;
        return AddRigidBody(collisionShape, ref worldMatrix, component, physicsDefinition);
    }

    public PhysicsBody AddRigidBody(PhysicsShape collisionShape, ref Matrix worldMatrix, object userObject, PhysicsDefinition physicsDefinition)
    {
        return _physicsEngine.AddRigidBody(collisionShape, ref worldMatrix, userObject, physicsDefinition, _useExternalViewManagement);
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

    public HitResult ShapeSweep(PhysicsShape shape, Matrix from, Matrix to, uint channelMask = ChannelMask.All, bool hitTriggers = false, ICollideableComponent ignoredComponent = null)
    {
        return _physicsEngine.ShapeSweep(shape, from, to, channelMask, hitTriggers, ignoredComponent);
    }

    public bool ShapeSweep(PhysicsShape shape, Matrix from, Matrix to, out HitResult result, uint channelMask = ChannelMask.All, bool hitTriggers = false, ICollideableComponent ignoredComponent = null)
    {
        return _physicsEngine.ShapeSweep(shape, from, to, out result, channelMask, hitTriggers, ignoredComponent);
    }

    public void ShapeSweepPenetrating(PhysicsShape shape, Matrix from, Matrix to, ICollection<HitResult> resultsOutput, uint channelMask = ChannelMask.All, bool hitTriggers = false, ICollideableComponent ignoredComponent = null)
    {
        _physicsEngine.ShapeSweepPenetrating(shape, from, to, resultsOutput, channelMask, hitTriggers, ignoredComponent);
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