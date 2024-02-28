using BulletSharp;
using CasaEngine.Core.Helpers;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Entities.Components;
using Microsoft.Xna.Framework;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace CasaEngine.Framework.Game.Components.Physics;

public class PhysicsEngineComponent : GameComponent
{
    private readonly CasaEngineGame? _casaEngineGame;
    public PhysicsEngine PhysicsEngine { get; private set; }

    public PhysicsEngineComponent(CasaEngineGame game) : base(game)
    {
        _casaEngineGame = Game as CasaEngineGame;
        game.Components.Add(this);
        UpdateOrder = (int)ComponentUpdateOrder.Physics;
    }

    public override void Initialize()
    {
        PhysicsEngine = new PhysicsEngine(GameSettings.PhysicsEngineSettings);
        base.Initialize();
    }

    public override void Update(GameTime gameTime)
    {
#if EDITOR
        if (_casaEngineGame.IsRunningInGameEditorMode)
        {
            return;
        }
#endif

        PhysicsEngine.Update(GameTimeHelper.ConvertElapsedTimeToSeconds(gameTime));
        PhysicsEngine.UpdateContacts();
        PhysicsEngine.SendEvents();
    }

    public CollisionObject AddGhostObject(CollisionShape collisionShape, ref Matrix worldMatrix, ICollideableComponent collideableComponent, Color? color = null)
    {
        var collisionObject = AddGhostObject(worldMatrix, collideableComponent, collisionShape, color);
        return collisionObject;
    }

    private PairCachingGhostObject AddGhostObject(Matrix worldMatrix, ICollideableComponent collideableComponent, CollisionShape collisionShape, Color? color = null)
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
        using var rbInfo = new RigidBodyConstructionInfo(physicsDefinition.Mass, physicsDefinition.MotionState, collisionShape);
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
        //rbInfo.LocalInertia = physicsDefinition.LocalInertia;
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

#if EDITOR
        body.CollisionFlags = CollisionFlags.None;
#else
        if (!isDynamic)
        {
            body.CollisionFlags |= CollisionFlags.StaticObject;
        }
#endif


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
}