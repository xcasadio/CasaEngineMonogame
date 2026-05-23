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
    public void Update_AccumulatesVelocityAcrossSmallRuntimeDeltas()
    {
        var entity = CreateEntityWithRoot();
        var component = new TestCharacterControllerComponent();
        component.Settings.Gravity = 0f;
        component.Settings.MaxHorizontalSpeed = 3.5f;
        component.Settings.Acceleration = 18f;
        entity.AddComponent(component);
        component.SetMoveIntent(new Vector2(1f, 0f));

        for (int frameIndex = 0; frameIndex < 120; frameIndex++)
        {
            component.Update(1f / 160f);
        }

        Assert.True(component.Velocity.X > 0.5f, $"Expected horizontal velocity to accumulate across small runtime deltas, but velocity stayed at {component.Velocity.X:F4}.");
        Assert.True(entity.RootComponent!.Position.X > 0.1f, $"Expected the root to start moving across small runtime deltas, but X stayed at {entity.RootComponent.Position.X:F4}.");
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
    public void RequestJump_WithinCoyoteTime_StartsJumpAfterLeavingGround()
    {
        var entity = CreateEntityWithRoot();
        var component = new TestCharacterControllerComponent();
        component.Settings.Gravity = 0f;
        component.Settings.JumpSpeed = 6f;
        component.Settings.CoyoteTimeSeconds = 0.2f;
        component.SetGroundedForTest();
        entity.AddComponent(component);
        component.Update(0.01f);
        component.SetAirborneForTest();

        component.RequestJump();
        component.Update(0.05f);

        Assert.False(component.IsGrounded);
        Assert.Equal(CharacterMovementState.Jumping, component.MovementState);
        Assert.Equal(6f, component.Velocity.Y, precision: 5);
    }

    [Fact]
    public void RequestJump_BuffersUntilGrounded()
    {
        var entity = CreateEntityWithRoot();
        var component = new TestCharacterControllerComponent();
        component.Settings.Gravity = 0f;
        component.Settings.JumpSpeed = 6f;
        component.Settings.JumpBufferSeconds = 0.2f;
        entity.AddComponent(component);

        component.RequestJump();
        component.Update(0.05f);
        component.SetGroundedForTest();
        component.Update(0.05f);

        Assert.False(component.IsGrounded);
        Assert.Equal(CharacterMovementState.Jumping, component.MovementState);
        Assert.Equal(6f, component.Velocity.Y, precision: 5);
    }

    [Fact]
    public void RequestDash_AppliesHorizontalDashAndCooldown()
    {
        var entity = CreateEntityWithRoot();
        var component = new TestCharacterControllerComponent();
        component.Settings.Gravity = 0f;
        component.Settings.DashSpeed = 12f;
        component.Settings.DashDurationSeconds = 0.2f;
        component.Settings.DashCooldownSeconds = 0.3f;
        entity.AddComponent(component);

        bool accepted = component.RequestDash(new Vector2(2f, 0f));
        component.Update(0.1f);

        Assert.True(accepted);
        Assert.True(component.IsDashing);
        Assert.Equal(12f, component.Velocity.X, precision: 5);
        Assert.Equal(1.2f, entity.RootComponent!.Position.X, precision: 5);

        component.Update(0.1f);

        Assert.False(component.IsDashing);
        Assert.Equal(0.3f, component.DashCooldownRemainingSeconds, precision: 5);
        Assert.False(component.RequestDash(new Vector2(1f, 0f)));

        component.Update(0.3f);

        Assert.Equal(0f, component.DashCooldownRemainingSeconds, precision: 5);
        Assert.True(component.RequestDash(new Vector2(1f, 0f)));
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
    public void Move_UsesCollisionSolver_ForExternalDisplacement()
    {
        var physicsWorldContext = new FakePhysicsWorldContext
        {
            HorizontalHit = CreateHit(Vector3.Left, 0.5f),
        };
        var entity = CreateControllerEntity(physicsWorldContext, out var component);
        component.Settings.Gravity = 0f;

        Vector3 actualDisplacement = component.Move(new Vector3(1f, 0f, 0f));

        Assert.True(component.LastCollisionHit.Succeeded);
        Assert.Equal(new Vector3(1f, 0f, 0f), component.LastRequestedDisplacement);
        Assert.Equal(actualDisplacement, component.LastActualDisplacement);
        Assert.True(entity.RootComponent!.Position.X > 0f);
        Assert.True(entity.RootComponent.Position.X < 1f);
    }

    [Fact]
    public void InputSnapshot_SaveLoadAndApply_RoundTripsCommands()
    {
        var source = new TestCharacterControllerComponent();
        source.Settings.JumpBufferSeconds = 0.25f;
        source.Settings.DashSpeed = 8f;
        source.Settings.DashDurationSeconds = 0.1f;
        source.SetMoveIntent(new Vector2(2f, 0f));
        source.RequestJump();
        Assert.True(source.RequestDash(new Vector2(0f, 1f)));

        CharacterControllerInputSnapshot snapshot = CharacterControllerInputSnapshot.Load(source.CaptureInputSnapshot().Save());
        var target = new TestCharacterControllerComponent();
        target.Settings.JumpBufferSeconds = 0.25f;
        target.Settings.DashSpeed = 8f;
        target.Settings.DashDurationSeconds = 0.1f;

        target.ApplyInputSnapshot(snapshot);

        CharacterControllerInputSnapshot appliedSnapshot = target.CaptureInputSnapshot();
        Assert.Equal(snapshot.MoveIntent, appliedSnapshot.MoveIntent);
        Assert.True(appliedSnapshot.JumpRequested);
        Assert.True(appliedSnapshot.DashRequested);
        Assert.Equal(snapshot.DashDirection, appliedSnapshot.DashDirection);
    }

    [Fact]
    public void StateSnapshot_SaveLoad_RoundTripsSerializableState()
    {
        var entity = CreateEntityWithRoot();
        var component = new TestCharacterControllerComponent();
        entity.RootComponent!.Position = new Vector3(1f, 2f, 3f);
        entity.RootComponent.Orientation = Quaternion.Identity;
        component.Settings.JumpBufferSeconds = 0.25f;
        component.Settings.DashSpeed = 8f;
        component.Settings.DashDurationSeconds = 0.1f;
        component.SetControlMode(CharacterControlMode.AI);
        component.SetGroundedForTest();
        component.SetVelocityForTest(new Vector3(4f, 5f, 6f));
        component.SetMoveIntent(new Vector2(0.5f, 0.25f));
        component.RequestJump();
        Assert.True(component.RequestDash(new Vector2(1f, 0f)));
        entity.AddComponent(component);

        CharacterControllerStateSnapshot snapshot = component.CaptureStateSnapshot();
        CharacterControllerStateSnapshot loaded = CharacterControllerStateSnapshot.Load(snapshot.Save());

        Assert.Equal(snapshot, loaded);
    }

    [Fact]
    public void RestoreStateSnapshot_RestoresControllerForCorrection()
    {
        var entity = CreateEntityWithRoot();
        var component = new TestCharacterControllerComponent();
        entity.AddComponent(component);
        entity.RootComponent!.Position = new Vector3(1f, 0f, 2f);
        component.SetControlMode(CharacterControlMode.AI);
        component.SetGroundedForTest();
        component.SetVelocityForTest(new Vector3(3f, 0f, 4f));
        component.SetMoveIntent(new Vector2(1f, 0f));
        CharacterControllerStateSnapshot snapshot = component.CaptureStateSnapshot();

        component.Teleport(Vector3.Zero);
        component.Stop();
        component.SetControlMode(CharacterControlMode.Disabled);

        component.RestoreStateSnapshot(snapshot);

        Assert.Equal(snapshot, component.CaptureStateSnapshot());
        Assert.Equal(new Vector3(1f, 0f, 2f), entity.RootComponent.Position);
        Assert.Equal(CharacterControlMode.AI, component.ControlMode);
        Assert.Equal(new Vector3(3f, 0f, 4f), component.Velocity);
    }

    [Fact]
    public void ApplyInputSnapshots_ReplaysDeterministicallyFromSameState()
    {
        var firstEntity = CreateEntityWithRoot();
        var first = new TestCharacterControllerComponent();
        ConfigureDeterministicController(first);
        firstEntity.AddComponent(first);

        var secondEntity = CreateEntityWithRoot();
        var second = new TestCharacterControllerComponent();
        ConfigureDeterministicController(second);
        secondEntity.AddComponent(second);

        CharacterControllerInputSnapshot[] frames =
        [
            new CharacterControllerInputSnapshot(new Vector2(1f, 0f), false, false, Vector2.Zero),
            new CharacterControllerInputSnapshot(new Vector2(1f, 0f), false, true, new Vector2(1f, 0f)),
            new CharacterControllerInputSnapshot(Vector2.Zero, false, false, Vector2.Zero),
        ];

        for (var frameIndex = 0; frameIndex < frames.Length; frameIndex++)
        {
            first.ApplyInputSnapshot(frames[frameIndex]);
            second.ApplyInputSnapshot(CharacterControllerInputSnapshot.Load(frames[frameIndex].Save()));
            first.Update(0.1f);
            second.Update(0.1f);
        }

        Assert.Equal(first.CaptureStateSnapshot(), second.CaptureStateSnapshot());
        Assert.Equal(firstEntity.RootComponent!.Position, secondEntity.RootComponent!.Position);
    }

    [Fact]
    public void Update_StepsOntoLowObstacle_WhenStepHeightAllowsIt()
    {
        var physicsWorldContext = new FakePhysicsWorldContext
        {
            ShapeSweepHandler = (from, to) =>
            {
                var fromPosition = from.Translation;
                var toPosition = to.Translation;
                var delta = toPosition - fromPosition;

                if (delta.Y < -0.0001f)
                {
                    return CreateHit(Vector3.Up, 0.5f);
                }

                if (Math.Abs(delta.Y) <= 0.0001f
                    && delta.X > 0.0001f
                    && fromPosition.Y < 0.1f)
                {
                    return CreateHit(Vector3.Left, 0.1f);
                }

                return null;
            },
        };
        var entity = CreateControllerEntity(physicsWorldContext, out var component);
        component.Settings.Gravity = 0f;
        component.Settings.MaxHorizontalSpeed = 1f;
        component.Settings.Acceleration = 10f;
        component.Settings.StepHeight = 0.25f;
        component.Settings.GroundSnapDistance = 0.05f;
        component.SetGroundedForTest();
        component.SetMoveIntent(new Vector2(1f, 0f));

        component.Update(1f);

        Assert.True(entity.RootComponent!.Position.X > 0.9f);
        Assert.True(entity.RootComponent.Position.Y > 0f);
        Assert.True(component.IsGrounded);
    }

    [Fact]
    public void Update_DoesNotStep_WhenObstacleIsTooHigh()
    {
        var physicsWorldContext = new FakePhysicsWorldContext
        {
            ShapeSweepHandler = (from, to) =>
            {
                var delta = to.Translation - from.Translation;

                if (Math.Abs(delta.Y) <= 0.0001f && delta.X > 0.0001f)
                {
                    return CreateHit(Vector3.Left, 0.1f);
                }

                if (delta.Y < -0.0001f)
                {
                    return CreateHit(Vector3.Up, 0.5f);
                }

                return null;
            },
        };
        var entity = CreateControllerEntity(physicsWorldContext, out var component);
        component.Settings.Gravity = 0f;
        component.Settings.MaxHorizontalSpeed = 1f;
        component.Settings.Acceleration = 10f;
        component.Settings.StepHeight = 0.25f;
        component.Settings.GroundSnapDistance = 0.05f;
        component.SetGroundedForTest();
        component.SetMoveIntent(new Vector2(1f, 0f));

        component.Update(1f);

        Assert.True(entity.RootComponent!.Position.X < 0.2f);
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
        Assert.Equal(component.GroundVelocity, snapshot.GroundVelocity);
        Assert.Equal(2.5f, snapshot.LastRequestedDisplacement.X, precision: 5);
        Assert.Equal(2.5f, snapshot.LastActualDisplacement.X, precision: 5);
    }

    [Fact]
    public void Update_InheritsTranslationFromMovingGround()
    {
        var groundCollider = new BoxCollisionComponent();
        groundCollider.PhysicsDefinition.PhysicsType = PhysicsType.Kinetic;
        groundCollider.Velocity = new Vector3(2f, 0f, 0f);
        var physicsWorldContext = new FakePhysicsWorldContext
        {
            GroundHit = CreateHit(Vector3.Up, 0.5f, groundCollider),
        };
        var entity = CreateControllerEntity(physicsWorldContext, out var component);
        component.Settings.Gravity = 0f;
        component.Settings.GroundSnapDistance = 0.5f;

        component.Update(0.1f);
        component.Update(1f);

        Assert.Equal(2f, entity.RootComponent!.Position.X, precision: 5);
        Assert.Equal(Vector3.Zero, component.Velocity);
        Assert.Equal(new Vector3(2f, 0f, 0f), component.GroundVelocity);
    }

    [Fact]
    public void Update_InheritsVerticalTranslationFromMovingGround()
    {
        var groundCollider = new BoxCollisionComponent();
        groundCollider.PhysicsDefinition.PhysicsType = PhysicsType.Kinetic;
        groundCollider.Velocity = new Vector3(0f, 1f, 0f);
        var entity = CreateEntityWithRoot();
        var component = new TestCharacterControllerComponent();
        component.Settings.Gravity = 0f;
        component.SetGroundedForTest(groundCollider);
        entity.AddComponent(component);

        component.Update(1f);

        Assert.Equal(1f, entity.RootComponent!.Position.Y, precision: 5);
        Assert.Equal(Vector3.Zero, component.Velocity);
        Assert.Equal(new Vector3(0f, 1f, 0f), component.GroundVelocity);
    }

    [Fact]
    public void RequestJump_FromMovingGround_DoesNotInheritGroundTranslationAfterDetach()
    {
        var groundCollider = new BoxCollisionComponent();
        groundCollider.PhysicsDefinition.PhysicsType = PhysicsType.Kinetic;
        groundCollider.Velocity = new Vector3(2f, 0f, 0f);
        var entity = CreateEntityWithRoot();
        var component = new TestCharacterControllerComponent();
        component.Settings.Gravity = 0f;
        component.Settings.JumpSpeed = 3f;
        component.SetGroundedForTest(groundCollider);
        entity.AddComponent(component);

        component.RequestJump();
        component.Update(1f);

        Assert.Equal(0f, entity.RootComponent!.Position.X, precision: 5);
        Assert.Equal(3f, component.Velocity.Y, precision: 5);
        Assert.False(component.IsGrounded);
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

    private static HitResult CreateHit(Vector3 normal, float hitFraction, PhysicsBaseComponent? collider = null)
    {
        return new HitResult
        {
            Succeeded = true,
            Normal = normal,
            HitFraction = hitFraction,
            Collider = collider,
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

    private static void ConfigureDeterministicController(CharacterControllerComponent controller)
    {
        controller.Settings.Gravity = 0f;
        controller.Settings.MaxHorizontalSpeed = 5f;
        controller.Settings.Acceleration = 10f;
        controller.Settings.Deceleration = 10f;
        controller.Settings.DashSpeed = 8f;
        controller.Settings.DashDurationSeconds = 0.1f;
        controller.Settings.DashCooldownSeconds = 0.2f;
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
        public void SetGroundedForTest(PhysicsBaseComponent? collider = null)
        {
            SetGroundInfo(new CharacterControllerGroundInfo(true, Vector3.Up, collider, 0f));
        }

        public void SetAirborneForTest()
        {
            SetGroundInfo(CharacterControllerGroundInfo.None);
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
        public Func<Matrix, Matrix, HitResult?>? ShapeSweepHandler;

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
            if (ShapeSweepHandler != null)
            {
                var handlerResult = ShapeSweepHandler(from, to);
                if (handlerResult.HasValue)
                {
                    result = handlerResult.Value;
                    return result.Succeeded;
                }

                result = default;
                return false;
            }

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