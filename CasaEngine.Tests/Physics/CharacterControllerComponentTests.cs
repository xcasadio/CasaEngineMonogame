using System.Reflection;
using BulletSharp;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Physics;

public class CharacterControllerComponentTests
{
    [Fact]
    public void ValidateDependencies_Throws_WhenComponentIsNotAttached()
    {
        var component = new CharacterControllerComponent();

        Assert.Throws<InvalidOperationException>(component.ValidateDependencies);
    }

    [Fact]
    public void ValidateDependencies_Throws_WhenRootComponentIsMissing()
    {
        var entity = new Entity();
        var component = new CharacterControllerComponent();
        entity.AddComponent(component);

        Assert.Throws<InvalidOperationException>(component.ValidateDependencies);
    }

    [Fact]
    public void ValidateDependencies_Throws_WhenCapsuleCollisionIsMissing()
    {
        var entity = CreateEntityWithRoot();
        var component = new CharacterControllerComponent();
        entity.AddComponent(component);

        Assert.Throws<InvalidOperationException>(component.ValidateDependencies);
    }

    [Fact]
    public void ValidateDependencies_Succeeds_WhenRequiredDependenciesExist()
    {
        using var physicsWorldContext = new PhysicsWorldContext(useExternalViewManagement: false);
        var entity = CreateEntityWithRoot();
        SetWorld(entity, CreateWorld(physicsWorldContext));
        entity.AddComponent(new CapsuleCollisionComponent());
        var component = new CharacterControllerComponent();
        entity.AddComponent(component);

        component.ValidateDependencies();
    }

    [Fact]
    public void SetMoveIntent_ClampsDirectionToUnitLength()
    {
        var component = new CharacterControllerComponent();

        component.SetMoveIntent(new Vector2(2f, 0f));

        Assert.Equal(1f, component.MoveIntent.Length(), precision: 5);
    }

    [Fact]
    public void SetControlMode_DisabledStopsMovement()
    {
        var component = new CharacterControllerComponent();
        component.SetMoveIntent(new Vector2(1f, 0f));

        component.SetControlMode(CharacterControlMode.Disabled);

        Assert.Equal(CharacterControlMode.Disabled, component.ControlMode);
        Assert.Equal(CharacterMovementState.Disabled, component.MovementState);
        Assert.Equal(Vector2.Zero, component.MoveIntent);
        Assert.Equal(Vector3.Zero, component.Velocity);
    }

    [Fact]
    public void Teleport_UpdatesRootPosition_AndClearsMovement()
    {
        var entity = CreateEntityWithRoot();
        var component = new CharacterControllerComponent();
        entity.AddComponent(component);
        component.SetMoveIntent(new Vector2(1f, 0f));

        component.Teleport(new Vector3(1f, 2f, 3f));

        Assert.Equal(new Vector3(1f, 2f, 3f), entity.RootComponent!.Position);
        Assert.Equal(Vector2.Zero, component.MoveIntent);
        Assert.Equal(Vector3.Zero, component.Velocity);
    }

    [Fact]
    public void Update_AcceleratesHorizontalVelocity_AndMovesRoot()
    {
        var entity = CreateEntityWithRoot();
        var component = new TestCharacterControllerComponent();
        component.Settings.Gravity = 0f;
        component.Settings.MaxHorizontalSpeed = 10f;
        component.Settings.Acceleration = 10f;
        entity.AddComponent(component);
        component.SetMoveIntent(new Vector2(1f, 0f));

        component.Update(0.5f);

        Assert.Equal(5f, component.Velocity.X, precision: 5);
        Assert.Equal(2.5f, entity.RootComponent!.Position.X, precision: 5);
    }

    [Fact]
    public void Update_ClampsHorizontalVelocity_ToMaxSpeed()
    {
        var entity = CreateEntityWithRoot();
        var component = new TestCharacterControllerComponent();
        component.Settings.Gravity = 0f;
        component.Settings.MaxHorizontalSpeed = 5f;
        component.Settings.Acceleration = 50f;
        entity.AddComponent(component);
        component.SetMoveIntent(new Vector2(1f, 0f));

        component.Update(1f);

        Assert.Equal(5f, component.Velocity.X, precision: 5);
    }

    [Fact]
    public void Update_DeceleratesHorizontalVelocity_WhenIntentStops()
    {
        var entity = CreateEntityWithRoot();
        var component = new TestCharacterControllerComponent();
        component.Settings.Gravity = 0f;
        component.Settings.Deceleration = 10f;
        component.SetVelocityForTest(new Vector3(5f, 0f, 0f));
        entity.AddComponent(component);

        component.Update(0.25f);

        Assert.Equal(2.5f, component.Velocity.X, precision: 5);
    }

    [Fact]
    public void Update_AppliesGravity_WhenAirborne()
    {
        var entity = CreateEntityWithRoot();
        var component = new TestCharacterControllerComponent();
        component.Settings.Gravity = 10f;
        entity.AddComponent(component);

        component.Update(0.5f);

        Assert.Equal(-5f, component.Velocity.Y, precision: 5);
        Assert.Equal(CharacterMovementState.Falling, component.MovementState);
    }

    [Fact]
    public void RequestJump_FromGround_StartsJumpAndClearsGround()
    {
        var entity = CreateEntityWithRoot();
        var component = new TestCharacterControllerComponent();
        var jumpStartedCount = 0;
        component.Settings.Gravity = 0f;
        component.Settings.JumpSpeed = 6f;
        component.SetGroundedForTest();
        component.JumpStarted += (_, _) => jumpStartedCount++;
        entity.AddComponent(component);

        component.RequestJump();
        component.Update(0.1f);

        Assert.Equal(1, jumpStartedCount);
        Assert.False(component.IsGrounded);
        Assert.Equal(CharacterMovementState.Jumping, component.MovementState);
        Assert.Equal(6f, component.Velocity.Y, precision: 5);
    }

    [Fact]
    public void Update_DisabledControlMode_DoesNotMoveRoot()
    {
        var entity = CreateEntityWithRoot();
        var component = new TestCharacterControllerComponent();
        component.Settings.Gravity = 10f;
        entity.AddComponent(component);
        component.SetControlMode(CharacterControlMode.Disabled);

        component.Update(1f);

        Assert.Equal(Vector3.Zero, entity.RootComponent!.Position);
        Assert.Equal(Vector3.Zero, component.Velocity);
        Assert.Equal(CharacterMovementState.Disabled, component.MovementState);
    }

    [Fact]
    public void Update_SnapsToWalkableGround()
    {
        var physicsWorldContext = new FakePhysicsWorldContext
        {
            GroundHit = CreateHit(Vector3.Up, 0.5f),
        };
        var entity = CreateControllerEntity(physicsWorldContext, out var component);
        entity.RootComponent!.Position = new Vector3(0f, 1.2f, 0f);
        component.Settings.Gravity = 0f;
        component.Settings.GroundSnapDistance = 0.5f;

        component.Update(0.1f);

        Assert.True(component.IsGrounded);
        Assert.Equal(CharacterMovementState.Grounded, component.MovementState);
        Assert.Equal(Vector3.Up, component.GroundNormal);
        Assert.True(entity.RootComponent.Position.Y < 1.2f);
    }

    [Fact]
    public void Update_SlidesAgainstWall_AndStoresLastHit()
    {
        var physicsWorldContext = new FakePhysicsWorldContext
        {
            HorizontalHit = CreateHit(Vector3.Left, 0.1f),
        };
        var entity = CreateControllerEntity(physicsWorldContext, out var component);
        component.Settings.Gravity = 0f;
        component.Settings.MaxHorizontalSpeed = 10f;
        component.Settings.Acceleration = 10f;
        component.SetMoveIntent(new Vector2(1f, 0f));

        component.Update(1f);

        Assert.True(component.LastCollisionHit.Succeeded);
        Assert.True(entity.RootComponent!.Position.X < 1.1f);
    }

    [Fact]
    public void Update_AcceptsSlope_WhenAngleIsBelowLimit()
    {
        var physicsWorldContext = new FakePhysicsWorldContext
        {
            GroundHit = CreateHit(Vector3.Normalize(new Vector3(0f, 0.8660254f, 0.5f)), 0.25f),
        };
        var entity = CreateControllerEntity(physicsWorldContext, out var component);
        entity.RootComponent!.Position = new Vector3(0f, 1.2f, 0f);
        component.Settings.Gravity = 0f;
        component.Settings.MaxSlopeAngle = 45f;
        component.Settings.GroundSnapDistance = 0.5f;

        component.Update(0.1f);

        Assert.True(component.IsGrounded);
        Assert.Equal(CharacterMovementState.Grounded, component.MovementState);
    }

    [Fact]
    public void Update_RejectsSlope_WhenAngleIsAboveLimit()
    {
        var physicsWorldContext = new FakePhysicsWorldContext
        {
            GroundHit = CreateHit(Vector3.Normalize(new Vector3(0f, 0.5f, 0.8660254f)), 0.25f),
        };
        var entity = CreateControllerEntity(physicsWorldContext, out var component);
        entity.RootComponent!.Position = new Vector3(0f, 1.2f, 0f);
        component.Settings.Gravity = 0f;
        component.Settings.MaxSlopeAngle = 45f;
        component.Settings.GroundSnapDistance = 0.5f;

        component.Update(0.1f);

        Assert.False(component.IsGrounded);
        Assert.Equal(CharacterMovementState.Falling, component.MovementState);
    }

    [Fact]
    public void DebugSnapshot_ReflectsCurrentStateAndLastDisplacements()
    {
        var entity = CreateEntityWithRoot();
        var component = new TestCharacterControllerComponent();
        component.Settings.Gravity = 0f;
        component.Settings.MaxHorizontalSpeed = 10f;
        component.Settings.Acceleration = 10f;
        entity.AddComponent(component);
        component.SetMoveIntent(new Vector2(1f, 0f));

        component.Update(0.5f);
        var snapshot = component.DebugSnapshot;

        Assert.Equal(component.ControlMode, snapshot.ControlMode);
        Assert.Equal(component.MovementState, snapshot.MovementState);
        Assert.Equal(component.Velocity, snapshot.Velocity);
        Assert.Equal(component.MoveIntent, snapshot.MoveIntent);
        Assert.Equal(component.IsGrounded, snapshot.IsGrounded);
        Assert.Equal(2.5f, snapshot.LastRequestedDisplacement.X, precision: 5);
        Assert.Equal(2.5f, snapshot.LastActualDisplacement.X, precision: 5);
    }

    private static Entity CreateEntityWithRoot()
    {
        return new Entity
        {
            RootComponent = new TestSceneComponent(),
        };
    }

    private static Entity CreateControllerEntity(IPhysicsWorldContext physicsWorldContext, out TestCharacterControllerComponent component)
    {
        var entity = CreateEntityWithRoot();
        SetWorld(entity, CreateWorld(physicsWorldContext));
        entity.AddComponent(new CapsuleCollisionComponent());
        component = new TestCharacterControllerComponent();
        entity.AddComponent(component);
        return entity;
    }

    private static HitResult CreateHit(Vector3 normal, float hitFraction)
    {
        return new HitResult
        {
            Succeeded = true,
            Normal = normal,
            HitFraction = hitFraction,
        };
    }

    private static World CreateWorld(IPhysicsWorldContext physicsWorldContext)
    {
        var world = new World();
        typeof(World)
            .GetProperty(nameof(World.PhysicsWorldContext), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(world, physicsWorldContext);
        return world;
    }

    private static void SetWorld(Entity entity, World world)
    {
        typeof(Entity)
            .GetProperty(nameof(Entity.World), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(entity, world);
    }

    private sealed class TestSceneComponent : SceneComponent
    {
        public override EntityComponent Clone()
        {
            return new TestSceneComponent();
        }
    }

    private sealed class TestCharacterControllerComponent : CharacterControllerComponent
    {
        public void SetGroundedForTest()
        {
            SetGroundInfo(new CharacterControllerGroundInfo(true, Vector3.Up, null, 0f));
        }

        public void SetVelocityForTest(Vector3 velocity)
        {
            SetVelocity(velocity);
        }
    }

    private sealed class FakePhysicsWorldContext : IPhysicsWorldContext
    {
        public HitResult HorizontalHit;
        public HitResult GroundHit;

        public PhysicsEngine PhysicsEngine => throw new NotSupportedException();

        public void Update(float elapsedTime)
        {
        }

        public CollisionObject AddGhostObject(CollisionShape collisionShape, ref Matrix worldMatrix, ICollideableComponent collideableComponent, Color? color = null)
        {
            throw new NotSupportedException();
        }

        public PairCachingGhostObject CreateGhostObject(Matrix worldMatrix, ICollideableComponent collideableComponent, CollisionShape collisionShape, Color? color = null)
        {
            throw new NotSupportedException();
        }

        public RigidBody AddStaticObject(CollisionShape collisionShape, Vector3 localScale, ref Matrix worldMatrix, object component, PhysicsDefinition physicsDefinition)
        {
            throw new NotSupportedException();
        }

        public RigidBody AddRigidBody(CollisionShape collisionShape, Vector3 localScale, ref Matrix worldMatrix, object component, PhysicsDefinition physicsDefinition)
        {
            throw new NotSupportedException();
        }

        public RigidBody AddRigidBody(CollisionShape collisionShape, ref Matrix worldMatrix, object userObject, PhysicsDefinition physicsDefinition)
        {
            throw new NotSupportedException();
        }

        public void AddCollisionObject(CollisionObject collisionObject)
        {
            throw new NotSupportedException();
        }

        public void RemoveCollisionObject(CollisionObject collisionObject)
        {
            throw new NotSupportedException();
        }

        public void AddRigidBody(RigidBody rigidBody)
        {
            throw new NotSupportedException();
        }

        public void RemoveRigidBody(RigidBody rigidBody)
        {
            throw new NotSupportedException();
        }

        public void ClearCollisionDataFrom(ICollideableComponent component)
        {
            throw new NotSupportedException();
        }

        public HitResult ShapeSweep(ConvexShape shape, Matrix from, Matrix to, CollisionFilterGroups filterGroup = CollisionFilterGroups.DefaultFilter, CollisionFilterGroups filterFlags = CollisionFilterGroups.DefaultFilter, bool hitTriggers = false, ICollideableComponent? ignoredComponent = null)
        {
            ShapeSweep(shape, from, to, out var result, filterGroup, filterFlags, hitTriggers, ignoredComponent);
            return result;
        }

        public bool ShapeSweep(ConvexShape shape, Matrix from, Matrix to, out HitResult result, CollisionFilterGroups filterGroup = CollisionFilterGroups.DefaultFilter, CollisionFilterGroups filterFlags = CollisionFilterGroups.DefaultFilter, bool hitTriggers = false, ICollideableComponent? ignoredComponent = null)
        {
            var delta = to.Translation - from.Translation;
            if ((Math.Abs(delta.X) > 0.0001f || Math.Abs(delta.Z) > 0.0001f) && HorizontalHit.Succeeded)
            {
                result = HorizontalHit;
                return true;
            }

            if (delta.Y < -0.0001f && GroundHit.Succeeded)
            {
                result = GroundHit;
                return true;
            }

            result = default;
            return false;
        }

        public void ShapeSweepPenetrating(ConvexShape shape, Matrix from, Matrix to, ICollection<HitResult> resultsOutput, CollisionFilterGroups filterGroup = CollisionFilterGroups.DefaultFilter, CollisionFilterGroups filterFlags = CollisionFilterGroups.DefaultFilter, bool hitTriggers = false, ICollideableComponent? ignoredComponent = null)
        {
            throw new NotSupportedException();
        }

        public bool WorldRayCast(ref Vector3 start, ref Vector3 end, Vector3 dir)
        {
            throw new NotSupportedException();
        }

        public bool NearBodyWorldRayCast(ref Vector3 position, ref Vector3 feelers, out Vector3 contactPoint, out Vector3 contactNormal)
        {
            throw new NotSupportedException();
        }
    }
}