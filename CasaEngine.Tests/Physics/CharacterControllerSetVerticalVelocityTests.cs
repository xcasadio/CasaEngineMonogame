using System.Reflection;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Physics;

/// <summary>
/// E4.0 acceptance: <see cref="CharacterControllerComponent.SetVerticalVelocity"/>, the additive,
/// policy-aware vertical-impulse API (port target: Alundra opcode 0x1B "Fly"). Reuses the
/// <see cref="TopDownElevationSimulationSpacePolicy"/> + <see cref="HeightGridCollisionField"/>
/// fixture pattern from <see cref="CharacterControllerFieldAwareMoverTests"/>.
/// </summary>
public class CharacterControllerSetVerticalVelocityTests
{
    private const float Tick = 1f / 50f;
    private const int MaxTicks = 2000;

    [Fact]
    public void SetVerticalVelocity_SubSnapUpwardImpulse_LiftsOffImmediately_AndRegrounds()
    {
        var (entity, component) = CreatePawn(CreateFlatField());
        entity.RootComponent!.Position = new Vector3(24f, 24f, 0f);

        component.Update(Tick);
        Assert.True(component.IsGrounded);

        component.SetVerticalVelocity(160f);
        component.Update(Tick);

        // 160 * Tick = 3.2px, strictly below GroundSnapDistance (4px): only the upward-velocity gate
        // at the head of UpdateGround (Dot(velocity, up) > 0) explains losing ground here.
        Assert.False(component.IsGrounded);
        Assert.True(entity.RootComponent!.Position.Z > 0f);

        var ticks = 0;
        while (!component.IsGrounded && ticks < MaxTicks)
        {
            component.Update(Tick);
            ticks++;
        }

        Assert.True(component.IsGrounded, "Expected the pawn to reground within the tick budget.");
        Assert.Equal(0f, entity.RootComponent.Position.Z, precision: 2);
    }

    [Fact]
    public void SetVerticalVelocity_DownwardImpulse_LandsSoonerThanFreeFall()
    {
        var (baselineEntity, baselineComponent) = CreatePawn(CreateFlatField());
        baselineEntity.RootComponent!.Position = new Vector3(24f, 24f, 400f);
        var baselineTicks = CountTicksUntilGrounded(baselineComponent);

        var (impulseEntity, impulseComponent) = CreatePawn(CreateFlatField());
        impulseEntity.RootComponent!.Position = new Vector3(24f, 24f, 400f);
        impulseComponent.SetVerticalVelocity(-800f);
        var impulseTicks = CountTicksUntilGrounded(impulseComponent);

        Assert.True(impulseTicks < baselineTicks,
            $"Expected the downward impulse to land sooner than free fall (impulse: {impulseTicks} ticks, baseline: {baselineTicks} ticks).");
    }

    [Fact]
    public void SetVerticalVelocity_WithoutWorld_UsesYUp_AndLeavesHorizontalVelocityUntouched()
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
        var horizontalBefore = component.Velocity;

        component.SetVerticalVelocity(5f);

        Assert.Equal(5f, component.Velocity.Y, precision: 5);
        Assert.Equal(horizontalBefore.X, component.Velocity.X, precision: 5);
        Assert.Equal(horizontalBefore.Z, component.Velocity.Z, precision: 5);
    }

    [Fact]
    public void SetVerticalVelocity_DisabledControlMode_IsNoOp()
    {
        var component = new CharacterControllerComponent();
        component.SetControlMode(CharacterControlMode.Disabled);

        component.SetVerticalVelocity(123f);

        Assert.Equal(Vector3.Zero, component.Velocity);
    }

    private static int CountTicksUntilGrounded(TestCharacterControllerComponent component)
    {
        var ticks = 0;
        while (!component.IsGrounded && ticks < MaxTicks)
        {
            component.Update(Tick);
            ticks++;
        }

        Assert.True(component.IsGrounded, "Expected the pawn to reground within the tick budget.");
        return ticks;
    }

    private static (Entity Entity, TestCharacterControllerComponent Component) CreatePawn(ICollisionField field)
    {
        var entity = CreateEntityWithRoot();
        var world = CreateWorld(new PhysicsWorld(useExternalViewManagement: false, spacePolicy: new TopDownElevationSimulationSpacePolicy()));
        world.CollisionField = field;
        SetWorld(entity, world);
        var collisionComponent = CreateBoxCollision();
        entity.AddComponent(collisionComponent);
        AttachToRoot(entity, collisionComponent);
        var component = new TestCharacterControllerComponent();
        ConfigureFieldAwareSettings(component.Settings);
        entity.AddComponent(component);
        return (entity, component);
    }

    private static void ConfigureFieldAwareSettings(CharacterControllerSettings settings)
    {
        settings.StepHeight = 3f;
        settings.GroundSnapDistance = 4f;
        settings.Gravity = 1250f;
        settings.MaxFallSpeed = 800f;
        settings.SkinWidth = 0.5f;
        settings.WalkabilityMask = 0u;
        settings.Radius = 8f;
        settings.Height = 32f;
    }

    private static void AttachToRoot(Entity entity, SceneComponent component)
    {
        component.Parent = entity.RootComponent;
        entity.RootComponent!.Children.Add(component);
    }

    /// <summary>Alundra's G2 body box: 16x16x32, centered 16 above the root, foot at up-offset 0.</summary>
    private static CollisionComponent CreateBoxCollision()
    {
        var component = new CollisionComponent();
        component.Fixtures.Add(new ColliderFixture(new Box { Size = new Vector3(16f, 16f, 32f) })
        {
            LocalPosition = new Vector3(0f, 0f, 16f),
        });
        return component;
    }

    private static HeightGridCollisionField CreateFlatField()
    {
        const int width = 8;
        const int depth = 8;
        var heights = new float[width * depth];
        return new HeightGridCollisionField(Vector3.Zero, 16f, width, depth, heights, up: Vector3.UnitZ);
    }

    private static Entity CreateEntityWithRoot()
    {
        return new Entity
        {
            RootComponent = new TestSceneComponent(),
        };
    }

    private static World CreateWorld(IPhysicsWorld physicsWorldContext)
    {
        var world = new World();
        typeof(World)
            .GetProperty(nameof(World.PhysicsWorld), BindingFlags.Instance | BindingFlags.Public)!
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
        public void SetVelocityForTest(Vector3 velocity)
        {
            SetVelocity(velocity);
        }
    }
}
