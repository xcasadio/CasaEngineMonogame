using BulletSharp;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Physics;
using Microsoft.Xna.Framework;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace CasaEngine.Framework.Application.Components.Physics;

public sealed class PhysicsWorldContext : IPhysicsWorldContext, IDisposable
{
    private readonly bool _useExternalViewManagement;

    public PhysicsEngine PhysicsEngine { get; }

    public PhysicsWorldContext(bool useExternalViewManagement)
    {
        _useExternalViewManagement = useExternalViewManagement;
        PhysicsEngine = new PhysicsEngine(GameSettings.PhysicsEngineSettings);
    }

    public void Update(float elapsedTime)
    {
        PhysicsEngine.Update(elapsedTime);
        PhysicsEngine.UpdateContacts();
        PhysicsEngine.SendEvents();
    }

    public CollisionObject AddGhostObject(CollisionShape collisionShape, ref Matrix worldMatrix, ICollideableComponent collideableComponent, Color? color = null)
    {
        var collisionObject = CreateGhostObject(worldMatrix, collideableComponent, collisionShape, color);
        PhysicsEngine.World.AddCollisionObject(collisionObject);
        return collisionObject;
    }

    public PairCachingGhostObject CreateGhostObject(Matrix worldMatrix, ICollideableComponent collideableComponent, CollisionShape collisionShape, Color? color = null)
    {
        var ghostObject = new PairCachingGhostObject
        {
            CollisionShape = collisionShape,
            UserObject = collideableComponent,
            WorldTransform = worldMatrix
        };
        ghostObject.CollisionFlags |= CollisionFlags.NoContactResponse;

        if (color.HasValue)
        {
            ghostObject.SetCustomDebugColor(color.Value.ToVector3());
        }

        return ghostObject;
    }

    public RigidBody AddStaticObject(CollisionShape collisionShape, Vector3 localScale, ref Matrix worldMatrix, object component, PhysicsDefinition physicsDefinition)
    {
        physicsDefinition.Mass = 0f;
        return AddRigidBody(collisionShape, localScale, ref worldMatrix, component, physicsDefinition);
    }

    public RigidBody AddRigidBody(CollisionShape collisionShape, Vector3 localScale, ref Matrix worldMatrix, object component, PhysicsDefinition physicsDefinition)
    {
        return AddRigidBody(collisionShape, ref worldMatrix, component, physicsDefinition);
    }

    public RigidBody AddRigidBody(CollisionShape collisionShape, ref Matrix worldMatrix, object userObject, PhysicsDefinition physicsDefinition)
    {
        using var rbInfo = new RigidBodyConstructionInfo(physicsDefinition.Mass, null, collisionShape);
        rbInfo.AdditionalAngularDampingFactor = physicsDefinition.AdditionalAngularDampingFactor;
        rbInfo.AdditionalAngularDampingThresholdSqr = physicsDefinition.AdditionalAngularDampingThresholdSqr;
        rbInfo.AdditionalDamping = physicsDefinition.AdditionalDamping;
        rbInfo.AdditionalDampingFactor = physicsDefinition.AdditionalDampingFactor;
        rbInfo.AdditionalLinearDampingThresholdSqr = physicsDefinition.AdditionalLinearDampingThresholdSqr;
        rbInfo.AngularDamping = physicsDefinition.AngularDamping;
        rbInfo.AngularSleepingThreshold = physicsDefinition.AngularSleepingThreshold;
        rbInfo.Friction = physicsDefinition.Friction;
        rbInfo.LinearDamping = physicsDefinition.LinearDamping;
        rbInfo.LinearSleepingThreshold = physicsDefinition.LinearSleepingThreshold;
        rbInfo.Restitution = physicsDefinition.Restitution;
        rbInfo.RollingFriction = physicsDefinition.RollingFriction;

        bool isDynamic = physicsDefinition.Mass != 0.0f;
        if (isDynamic)
        {
            rbInfo.LocalInertia = collisionShape.CalculateLocalInertia(physicsDefinition.Mass);
            rbInfo.MotionState = new DefaultMotionState(worldMatrix);
        }

        var body = new RigidBody(rbInfo)
        {
            Gravity = physicsDefinition.ApplyGravity is true ? GameSettings.PhysicsEngineSettings.Gravity : Vector3.Zero,
            UserObject = userObject,
            WorldTransform = worldMatrix,
            LinearFactor = physicsDefinition.LinearFactor,
            AngularFactor = physicsDefinition.AngularFactor
        };

        if (!isDynamic && !_useExternalViewManagement)
        {
            body.CollisionFlags |= CollisionFlags.StaticObject;
        }

        if (physicsDefinition.DebugColor.HasValue)
        {
            body.SetCustomDebugColor(physicsDefinition.DebugColor.Value.ToVector3());
        }

        PhysicsEngine.World.AddRigidBody(body);

        if (physicsDefinition.ApplyGravity is false)
        {
            body.Gravity = Vector3.Zero;
        }

        return body;
    }

    public void AddCollisionObject(CollisionObject collisionObject)
    {
        if (!PhysicsEngine.World.CollisionObjectArray.Contains(collisionObject))
        {
            PhysicsEngine.World.AddCollisionObject(collisionObject);
        }
    }

    public void RemoveCollisionObject(CollisionObject collisionObject)
    {
        if (PhysicsEngine.World.CollisionObjectArray.Contains(collisionObject))
        {
            PhysicsEngine.World.RemoveCollisionObject(collisionObject);
        }
    }

    public void AddRigidBody(RigidBody rigidBody)
    {
        PhysicsEngine.World.AddRigidBody(rigidBody);
    }

    public void RemoveRigidBody(RigidBody rigidBody)
    {
        PhysicsEngine.World.RemoveRigidBody(rigidBody);
    }

    public void ClearCollisionDataFrom(ICollideableComponent component)
    {
        PhysicsEngine.ClearCollisionDataOf(component);
    }

    public bool WorldRayCast(ref Vector3 start, ref Vector3 end, Vector3 dir)
    {
        return PhysicsEngine.WorldRayCast(ref start, ref end, dir);
    }

    public bool NearBodyWorldRayCast(ref Vector3 position, ref Vector3 feelers, out Vector3 contactPoint, out Vector3 contactNormal)
    {
        return PhysicsEngine.NearBodyWorldRayCast(ref position, ref feelers, out contactPoint, out contactNormal);
    }

    public void Dispose()
    {
        PhysicsEngine.Dispose();
    }
}