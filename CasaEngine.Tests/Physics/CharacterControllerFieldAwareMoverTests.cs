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
/// E3.c acceptance: the character controller's mover under a policy whose up axis is not Y
/// (<see cref="TopDownElevationSimulationSpacePolicy"/>), and its ground/walkability resolution
/// against a <see cref="World.CollisionField"/> instead of the sweep-only path.
/// </summary>
public class CharacterControllerFieldAwareMoverTests
{
    private const float Tick = 1f / 50f;

    [Fact]
    public void Up_MatchesEachSimulationSpacePolicy()
    {
        Assert.Equal(Vector3.UnitZ, new TopDownElevationSimulationSpacePolicy().Up);
        Assert.Equal(Vector3.Up, new Identity3dSimulationSpacePolicy().Up);
        Assert.Equal(Vector3.Up, new Planar2dSimulationSpacePolicy().Up);
    }

    [Fact]
    public void Update_UnderZUp_WithoutField_AppliesGravityAlongUp_AndProgressesHorizontally()
    {
        var entity = CreateEntityWithRoot();
        SetWorld(entity, CreateWorld(new PhysicsWorld(useExternalViewManagement: false, spacePolicy: new TopDownElevationSimulationSpacePolicy())));
        entity.RootComponent!.Position = new Vector3(24f, 24f, 500f);
        var component = new TestCharacterControllerComponent();
        component.Settings.Gravity = 1250f;
        component.Settings.MaxHorizontalSpeed = 10f;
        component.Settings.Acceleration = 100f;
        entity.AddComponent(component);
        component.SetVelocityForTest(new Vector3(0f, 0f, -10f));
        component.SetMoveIntent(new Vector2(1f, 0f));

        component.Update(Tick);

        Assert.Equal(-10f - 1250f * Tick, component.Velocity.Z, precision: 3);
        Assert.True(component.Velocity.X > 0f);
        Assert.Equal(0f, component.Velocity.Y, precision: 5);
    }

    [Fact]
    public void MoveThenUpdate_OnFlatGround_Walks_AndGrounds()
    {
        var (entity, component) = CreatePawn(CreateFlatField());
        entity.RootComponent!.Position = new Vector3(24f, 24f, 0f);

        component.Move(new Vector3(8f, 0f, 0f));
        component.Update(Tick);

        AssertPosition(new Vector3(32f, 24f, 0f), entity.RootComponent!.Position);
        Assert.True(component.IsGrounded);
    }

    [Fact]
    public void MoveThenUpdate_BlockedByACliffTallerThanStepHeight()
    {
        var (entity, component) = CreatePawn(CreateColumnHeightField(32f));
        entity.RootComponent!.Position = new Vector3(48f, 24f, 0f);

        component.Move(new Vector3(20f, 0f, 0f));
        component.Update(Tick);

        AssertPosition(new Vector3(48f, 24f, 0f), entity.RootComponent!.Position);
    }

    [Fact]
    public void MoveThenUpdate_StepsOntoALedgeWithinStepHeight()
    {
        var (entity, component) = CreatePawn(CreateColumnHeightField(2f));
        entity.RootComponent!.Position = new Vector3(48f, 24f, 0f);

        component.Move(new Vector3(20f, 0f, 0f));
        component.Update(Tick);

        AssertPosition(new Vector3(68f, 24f, 2f), entity.RootComponent!.Position);
        Assert.True(component.IsGrounded);
    }

    [Fact]
    public void MoveThenUpdate_BlockedByNonWalkableCells()
    {
        var (entity, component) = CreatePawn(CreateNonWalkableColumnField());
        entity.RootComponent!.Position = new Vector3(48f, 24f, 0f);

        component.Move(new Vector3(20f, 0f, 0f));
        component.Update(Tick);

        AssertPosition(new Vector3(48f, 24f, 0f), entity.RootComponent!.Position);
    }

    [Fact]
    public void Update_FallsClampsAtTerminalVelocity_AndLands()
    {
        var (entity, component) = CreatePawn(CreateFlatField());
        entity.RootComponent!.Position = new Vector3(24f, 24f, 400f);

        var minVelocityZ = 0f;
        for (var i = 0; i < 100; i++)
        {
            component.Update(Tick);
            minVelocityZ = Math.Min(minVelocityZ, component.Velocity.Z);
        }

        Assert.Equal(-800f, minVelocityZ, precision: 0);
        AssertPosition(new Vector3(24f, 24f, 0f), entity.RootComponent!.Position);
        Assert.True(component.IsGrounded);
        Assert.Equal(0f, component.Velocity.Z, precision: 3);
    }

    /// <summary>
    /// E3.c-bis acceptance: a box fixture 15 px deep, centred in a 16-px cell via a 0.5 local-position
    /// offset (matching the original's <c>[Pos + Mod, Pos + Mod + size - 1/65536)</c> collision box, as
    /// Alundra's G2 fixture does on each horizontal axis), lands its far corner exactly on the boundary
    /// with the neighbour cell. Without the far-corner epsilon that neighbour cell would be sampled too;
    /// raising it well above step height would then block grounding even though the fixture's own cell
    /// is flat.
    /// </summary>
    [Fact]
    public void FootprintFarCorner_OnCellBoundary_SamplesOnlyItsOwnCell()
    {
        const int width = 4;
        const int depth = 4;
        var heights = new float[width * depth];
        heights[1 * width + 2] = 100f; // cell (x=2, y=1): the h1-far neighbour of cell (1, 1).
        var field = new HeightGridCollisionField(Vector3.Zero, 16f, width, depth, heights, up: Vector3.UnitZ);

        var entity = CreateEntityWithRoot();
        var world = CreateWorld(new PhysicsWorld(useExternalViewManagement: false, spacePolicy: new TopDownElevationSimulationSpacePolicy()));
        world.CollisionField = field;
        SetWorld(entity, world);

        var collisionComponent = new CollisionComponent();
        collisionComponent.Fixtures.Add(new ColliderFixture(new Box { Size = new Vector3(15f, 15f, 32f) })
        {
            LocalPosition = new Vector3(0.5f, 0.5f, 16f),
        });
        entity.AddComponent(collisionComponent);
        AttachToRoot(entity, collisionComponent);

        var component = new TestCharacterControllerComponent();
        ConfigureFieldAwareSettings(component.Settings);
        entity.AddComponent(component);

        // Cell (1, 1) spans [16, 32) on both axes; its centre is (24, 24). The fixture's far corner on
        // X lands at 24 + 0.5 + 7.5 = 32.0, exactly the boundary with the raised cell (2, 1).
        entity.RootComponent!.Position = new Vector3(24f, 24f, 8f);
        component.SetVelocityForTest(new Vector3(0f, 0f, -800f));

        component.Update(Tick);

        AssertPosition(new Vector3(24f, 24f, 0f), entity.RootComponent!.Position);
        Assert.True(component.IsGrounded);
    }

    [Fact]
    public void Update_CrossesGroundWithinOneStep_AndLands()
    {
        var (entity, component) = CreatePawn(CreateFlatField());
        entity.RootComponent!.Position = new Vector3(24f, 24f, 8f);
        component.SetVelocityForTest(new Vector3(0f, 0f, -800f));

        component.Update(Tick);

        AssertPosition(new Vector3(24f, 24f, 0f), entity.RootComponent!.Position);
        Assert.True(component.IsGrounded);
        Assert.Equal(0f, component.Velocity.Z, precision: 3);
    }

    private static void AssertPosition(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, precision: 3);
        Assert.Equal(expected.Y, actual.Y, precision: 3);
        Assert.Equal(expected.Z, actual.Z, precision: 3);
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

    /// <summary>
    /// Wires a fixture-carrying collision component as a scene-graph child of the entity's root, the
    /// way the converter places it (E3.a: <c>CollisionComponent</c> as a direct child of the root),
    /// so that <see cref="SceneComponent.Position"/> composes with the root's position the way the
    /// mover's footprint resolution expects.
    /// </summary>
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

    /// <summary>Flat at 0, non-walkable for every cell at x &gt;= 64 (column index &gt;= 4).</summary>
    private static HeightGridCollisionField CreateNonWalkableColumnField()
    {
        const int width = 8;
        const int depth = 8;
        var heights = new float[width * depth];
        var walkable = new bool[width * depth];
        for (var i = 0; i < walkable.Length; i++)
        {
            walkable[i] = true;
        }

        for (var b = 0; b < depth; b++)
        {
            for (var a = 4; a < width; a++)
            {
                walkable[b * width + a] = false;
            }
        }

        return new HeightGridCollisionField(Vector3.Zero, 16f, width, depth, heights, walkable, up: Vector3.UnitZ);
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
