using BulletSharp;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Physics;
using Microsoft.Xna.Framework;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace CasaEngine.Framework.Application.Components.Physics;

public interface IPhysicsWorld
{
    BulletPhysicsEngine BulletPhysicsEngine { get; }

    void Update(float elapsedTime);

    CollisionObject AddGhostObject(CollisionShape collisionShape, ref Matrix worldMatrix, ICollideableComponent collideableComponent, Color? color = null);

    PairCachingGhostObject CreateGhostObject(Matrix worldMatrix, ICollideableComponent collideableComponent, CollisionShape collisionShape, Color? color = null);

    RigidBody AddStaticObject(CollisionShape collisionShape, Vector3 localScale, ref Matrix worldMatrix, object component, PhysicsDefinition physicsDefinition);

    RigidBody AddRigidBody(CollisionShape collisionShape, Vector3 localScale, ref Matrix worldMatrix, object component, PhysicsDefinition physicsDefinition);

    RigidBody AddRigidBody(CollisionShape collisionShape, ref Matrix worldMatrix, object userObject, PhysicsDefinition physicsDefinition);

    void AddCollisionObject(CollisionObject collisionObject);

    void RemoveCollisionObject(CollisionObject collisionObject);

    void AddRigidBody(RigidBody rigidBody);

    void RemoveRigidBody(RigidBody rigidBody);

    void ClearCollisionDataFrom(ICollideableComponent component);

    HitResult ShapeSweep(ConvexShape shape, Matrix from, Matrix to, CollisionFilterGroups filterGroup = CollisionFilterGroups.DefaultFilter, CollisionFilterGroups filterFlags = CollisionFilterGroups.DefaultFilter, bool hitTriggers = false, ICollideableComponent? ignoredComponent = null);

    bool ShapeSweep(ConvexShape shape, Matrix from, Matrix to, out HitResult result, CollisionFilterGroups filterGroup = CollisionFilterGroups.DefaultFilter, CollisionFilterGroups filterFlags = CollisionFilterGroups.DefaultFilter, bool hitTriggers = false, ICollideableComponent? ignoredComponent = null);

    void ShapeSweepPenetrating(ConvexShape shape, Matrix from, Matrix to, ICollection<HitResult> resultsOutput, CollisionFilterGroups filterGroup = CollisionFilterGroups.DefaultFilter, CollisionFilterGroups filterFlags = CollisionFilterGroups.DefaultFilter, bool hitTriggers = false, ICollideableComponent? ignoredComponent = null);

    bool WorldRayCast(ref Vector3 start, ref Vector3 end, Vector3 dir);

    bool NearBodyWorldRayCast(ref Vector3 position, ref Vector3 feelers, out Vector3 contactPoint, out Vector3 contactNormal);
}