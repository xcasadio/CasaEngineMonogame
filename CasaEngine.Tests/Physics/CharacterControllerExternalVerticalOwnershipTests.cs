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
/// E4.g acceptance: <see cref="CharacterControllerComponent.IsVerticalOwnedExternally"/> and
/// <see cref="CharacterControllerComponent.SetExternalVerticalDisplacement"/> - the additive API
/// that replaces the DLL's symbolic 1e-6 <c>SetVerticalVelocity</c> signal (port target: Alundra's
/// tick-quantized <c>ForceZ</c> for scripted NPCs). Reuses the
/// <see cref="TopDownElevationSimulationSpacePolicy"/> + <see cref="HeightGridCollisionField"/>
/// fixture pattern from <see cref="CharacterControllerFieldAwareMoverTests"/>.
/// </summary>
public class CharacterControllerExternalVerticalOwnershipTests
{
    private const float Tick50 = 1f / 50f;
    private const float Tick240 = 1f / 240f;

    /// <summary>
    /// (a) PERSISTENCE: a rising declaration must survive every subsequent Update with no further
    /// call - it is a LATCH, not a one-shot signal. Fails if Update ever clears it (the pawn would
    /// re-ground on some later frame instead of staying airborne on every one of them).
    /// </summary>
    [Fact]
    public void RisingDeclaration_PersistsAcrossManyUpdatesWithoutFurtherCalls()
    {
        var (entity, component) = CreatePawn(CreateFlatField());
        entity.RootComponent!.Position = new Vector3(24f, 24f, 0f);
        component.Update(Tick50);
        Assert.True(component.IsGrounded);

        component.IsVerticalOwnedExternally = true;
        component.SetExternalVerticalDisplacement(2f); // inside GroundSnapDistance (4px)

        for (var i = 0; i < 60; i++)
        {
            component.Update(Tick240);
            Assert.False(component.IsGrounded, $"Expected still airborne at frame {i}: the latch must not be cleared by Update.");
        }
    }

    /// <summary>
    /// (b) A same-frame horizontal Move with a zero vertical component must not overwrite the
    /// latched declaration. Fails under a "last Move wins" implementation.
    /// </summary>
    [Fact]
    public void RisingDeclaration_SurvivesAZeroVerticalMoveInTheSameFrame()
    {
        var (entity, component) = CreatePawn(CreateFlatField());
        entity.RootComponent!.Position = new Vector3(24f, 24f, 0f);
        component.Update(Tick50);
        Assert.True(component.IsGrounded);

        component.IsVerticalOwnedExternally = true;
        component.SetExternalVerticalDisplacement(2f);
        component.Move(new Vector3(4f, 0f, 0f));
        component.Update(Tick50);

        Assert.False(component.IsGrounded);
    }

    // (c) A step-support hit from Move (TryStepMove) must not re-ground the pawn on the same
    // Update when a rising declaration is latched. Covered by
    // CharacterControllerComponentTests.Update_UnderExternalVerticalOwnership_RisingDeclaration_PreventsRegroundViaStepSupportHit,
    // which reuses that file's FakePhysicsWorldContext to reliably trigger TryStepMove (a
    // HeightGridCollisionField alone never produces a step-support hit, since TryStepMove only
    // fires off a blocking Sweep hit against a physics collider, not the field).

    /// <summary>
    /// (d) A residual positive vertical velocity must never produce engine-side vertical motion
    /// while the flag is set: no gravity integration and the up-axis component is excluded from
    /// the velocity-driven displacement. Fails without either half of that exclusion.
    /// </summary>
    [Fact]
    public void ResidualVerticalVelocity_NeverMovesTheUpCoordinate_NorGrows()
    {
        var (entity, component) = CreatePawn(CreateFlatField());
        entity.RootComponent!.Position = new Vector3(24f, 24f, 100f);
        component.IsVerticalOwnedExternally = true;
        component.SetVelocityForTest(new Vector3(0f, 0f, 50f));

        var startZ = entity.RootComponent!.Position.Z;

        for (var i = 0; i < 60; i++)
        {
            component.Update(Tick50);
        }

        Assert.Equal(startZ, entity.RootComponent!.Position.Z, precision: 4);
        Assert.Equal(50f, component.Velocity.Z, precision: 4);
    }

    /// <summary>(e) A descending (non-positive) declaration lets ground resolution find ground again.</summary>
    [Fact]
    public void NonPositiveDeclaration_AllowsGroundToBeFoundAgain()
    {
        var (entity, component) = CreatePawn(CreateFlatField());
        entity.RootComponent!.Position = new Vector3(24f, 24f, 0f);
        component.Update(Tick50);
        Assert.True(component.IsGrounded);

        component.IsVerticalOwnedExternally = true;
        component.SetExternalVerticalDisplacement(2f);
        component.Update(Tick50);
        Assert.False(component.IsGrounded);

        component.SetExternalVerticalDisplacement(-1f);
        component.Update(Tick50);

        Assert.True(component.IsGrounded);
    }

    [Fact]
    public void DefaultsToFalse_NoBehaviorChangeForExistingUsers()
    {
        var component = new CharacterControllerComponent();
        Assert.False(component.IsVerticalOwnedExternally);
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

    /// <summary>Flat at 0, raised to <paramref name="raisedHeight"/> for every cell at x &gt;= 64 (column index &gt;= 4).</summary>
    private static HeightGridCollisionField CreateColumnHeightField(float raisedHeight)
    {
        const int width = 8;
        const int depth = 8;
        var heights = new float[width * depth];
        for (var b = 0; b < depth; b++)
        {
            for (var a = 4; a < width; a++)
            {
                heights[b * width + a] = raisedHeight;
            }
        }

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
