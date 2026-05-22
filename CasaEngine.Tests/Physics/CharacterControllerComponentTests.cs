using System.Reflection;
using CasaEngine.Framework.Application.Components.Physics;
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

    private static Entity CreateEntityWithRoot()
    {
        return new Entity
        {
            RootComponent = new TestSceneComponent(),
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
}