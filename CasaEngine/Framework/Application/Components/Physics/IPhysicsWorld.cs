using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace CasaEngine.Framework.Application.Components.Physics;

public interface IPhysicsWorld
{
    int CollisionObjectCount { get; }

    void Update(float elapsedTime);

    PhysicsBody AddGhostObject(PhysicsShape collisionShape, ref Matrix worldMatrix, ICollideableComponent collideableComponent, Color? color = null);

    PhysicsBody CreateGhostObject(Matrix worldMatrix, ICollideableComponent collideableComponent, PhysicsShape collisionShape, Color? color = null);

    PhysicsBody AddStaticObject(PhysicsShape collisionShape, Vector3 localScale, ref Matrix worldMatrix, object component, PhysicsDefinition physicsDefinition);

    PhysicsBody AddRigidBody(PhysicsShape collisionShape, Vector3 localScale, ref Matrix worldMatrix, object component, PhysicsDefinition physicsDefinition);

    PhysicsBody AddRigidBody(PhysicsShape collisionShape, ref Matrix worldMatrix, object userObject, PhysicsDefinition physicsDefinition);

    void AddCollisionObject(PhysicsBody collisionObject);

    void RemoveCollisionObject(PhysicsBody collisionObject);

    void AddRigidBody(PhysicsBody rigidBody);

    void RemoveRigidBody(PhysicsBody rigidBody);

    void ClearCollisionDataFrom(ICollideableComponent component);

    HitResult ShapeSweep(PhysicsShape shape, Matrix from, Matrix to, PhysicsCollisionFilterGroups filterGroup = PhysicsCollisionFilterGroups.DefaultFilter, PhysicsCollisionFilterGroups filterFlags = PhysicsCollisionFilterGroups.DefaultFilter, bool hitTriggers = false, ICollideableComponent? ignoredComponent = null);

    bool ShapeSweep(PhysicsShape shape, Matrix from, Matrix to, out HitResult result, PhysicsCollisionFilterGroups filterGroup = PhysicsCollisionFilterGroups.DefaultFilter, PhysicsCollisionFilterGroups filterFlags = PhysicsCollisionFilterGroups.DefaultFilter, bool hitTriggers = false, ICollideableComponent? ignoredComponent = null);

    void ShapeSweepPenetrating(PhysicsShape shape, Matrix from, Matrix to, ICollection<HitResult> resultsOutput, PhysicsCollisionFilterGroups filterGroup = PhysicsCollisionFilterGroups.DefaultFilter, PhysicsCollisionFilterGroups filterFlags = PhysicsCollisionFilterGroups.DefaultFilter, bool hitTriggers = false, ICollideableComponent? ignoredComponent = null);

    bool WorldRayCast(ref Vector3 start, ref Vector3 end, Vector3 dir);

    bool NearBodyWorldRayCast(ref Vector3 position, ref Vector3 feelers, out Vector3 contactPoint, out Vector3 contactNormal);

    void RefreshBodyAabb(PhysicsBody body);

    void DrawDebugWorld(IPhysicsDebugDrawer debugDrawer);
}