using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace CasaEngine.Framework.Application.Components.Physics;

public interface IPhysicsWorld
{
    int CollisionObjectCount { get; }

    /// <summary>Simulation space of this world: shape lowering, body constraints and render mapping.</summary>
    SimulationSpacePolicy SpacePolicy { get; }

    void Update(float elapsedTime);

    PhysicsBody AddGhostObject(Shape3d shape, Vector3 localScale, ref Matrix worldMatrix, ICollideableComponent collideableComponent, int collisionProfileId, string fixtureTag = null, Color? color = null);

    PhysicsBody AddGhostObject(IReadOnlyList<ColliderFixture> fixtures, Vector3 localScale, ref Matrix worldMatrix, ICollideableComponent collideableComponent, int collisionProfileId, Color? color = null);

    PhysicsBody CreateGhostObject(Matrix worldMatrix, ICollideableComponent collideableComponent, Shape3d shape, Vector3 localScale, int collisionProfileId, string fixtureTag = null, Color? color = null);

    PhysicsBody CreateGhostObject(Matrix worldMatrix, ICollideableComponent collideableComponent, IReadOnlyList<ColliderFixture> fixtures, Vector3 localScale, int collisionProfileId, Color? color = null);

    PhysicsBody AddStaticObject(Shape3d shape, Vector3 localScale, ref Matrix worldMatrix, object component, PhysicsDefinition physicsDefinition, int collisionProfileId, string fixtureTag = null);

    PhysicsBody AddStaticObject(IReadOnlyList<ColliderFixture> fixtures, Vector3 localScale, ref Matrix worldMatrix, object component, PhysicsDefinition physicsDefinition, int collisionProfileId);

    PhysicsBody AddRigidBody(Shape3d shape, Vector3 localScale, ref Matrix worldMatrix, object component, PhysicsDefinition physicsDefinition, int collisionProfileId, string fixtureTag = null);

    PhysicsBody AddRigidBody(IReadOnlyList<ColliderFixture> fixtures, Vector3 localScale, ref Matrix worldMatrix, object component, PhysicsDefinition physicsDefinition, int collisionProfileId);

    void AddCollisionObject(PhysicsBody collisionObject);

    void RemoveCollisionObject(PhysicsBody collisionObject);

    void AddRigidBody(PhysicsBody rigidBody);

    void RemoveRigidBody(PhysicsBody rigidBody);

    void ClearCollisionDataFrom(ICollideableComponent component);

    /// <summary>
    /// Contact points of a collision, carrying the fixture tags the backend reported for them.
    /// </summary>
    IReadOnlyCollection<ContactPoint> GetContactPoints(Collision collision);

    PhysicsQueryShape CreateQueryShape(Shape3d shape, Vector3 localScale);

    HitResult ShapeSweep(PhysicsQueryShape shape, Matrix from, Matrix to, uint channelMask = ChannelMask.All, bool hitTriggers = false, ICollideableComponent ignoredComponent = null);

    bool ShapeSweep(PhysicsQueryShape shape, Matrix from, Matrix to, out HitResult result, uint channelMask = ChannelMask.All, bool hitTriggers = false, ICollideableComponent ignoredComponent = null);

    HitResult ShapeSweep(Shape3d shape, Vector3 localScale, Matrix from, Matrix to, uint channelMask = ChannelMask.All, bool hitTriggers = false, ICollideableComponent ignoredComponent = null);

    bool ShapeSweep(Shape3d shape, Vector3 localScale, Matrix from, Matrix to, out HitResult result, uint channelMask = ChannelMask.All, bool hitTriggers = false, ICollideableComponent ignoredComponent = null);

    void ShapeSweepPenetrating(PhysicsQueryShape shape, Matrix from, Matrix to, ICollection<HitResult> resultsOutput, uint channelMask = ChannelMask.All, bool hitTriggers = false, ICollideableComponent ignoredComponent = null);

    void ShapeSweepPenetrating(Shape3d shape, Vector3 localScale, Matrix from, Matrix to, ICollection<HitResult> resultsOutput, uint channelMask = ChannelMask.All, bool hitTriggers = false, ICollideableComponent ignoredComponent = null);

    bool WorldRayCast(ref Vector3 start, ref Vector3 end, Vector3 dir);

    bool NearBodyWorldRayCast(ref Vector3 position, ref Vector3 feelers, out Vector3 contactPoint, out Vector3 contactNormal);

    void RefreshBodyAabb(PhysicsBody body);

    void DrawDebugWorld(IPhysicsDebugDrawer debugDrawer);
}
