using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Physics;

/// <summary>
/// Covers the simulation space policy of a world: the constraints it gives its bodies, the render
/// position it derives from a logical one, and the wiring that makes a world use the policy it names.
/// </summary>
public class SimulationSpacePolicyTests
{
    [Fact]
    public void Identity3d_RendersWhereItSimulates()
    {
        var policy = new Identity3dSimulationSpacePolicy();

        Assert.Equal(new Vector3(3f, -4f, 5f), policy.DeriveRenderPosition(new Vector3(3f, -4f, 5f)));
    }

    [Theory]
    [InlineData(10f, 20f, 0f, 10f, -20f)]
    [InlineData(10f, 36f, 16f, 10f, -20f)]
    [InlineData(-2f, 0f, 7f, -2f, 7f)]
    public void TopDownElevation_FoldsGroundDepthAndElevationIntoTheScreenRow(
        float x, float y, float z, float expectedX, float expectedY)
    {
        var policy = new TopDownElevationSimulationSpacePolicy();

        var renderPosition = policy.DeriveRenderPosition(new Vector3(x, y, z));

        Assert.Equal(expectedX, renderPosition.X, precision: 4);
        Assert.Equal(expectedY, renderPosition.Y, precision: 4);
        Assert.Equal(0f, renderPosition.Z, precision: 4);
    }

    [Theory]
    [InlineData(SimulationSpacePolicyNames.Identity3d, typeof(Identity3dSimulationSpacePolicy))]
    [InlineData(SimulationSpacePolicyNames.Planar2d, typeof(Planar2dSimulationSpacePolicy))]
    [InlineData(SimulationSpacePolicyNames.TopDownElevation, typeof(TopDownElevationSimulationSpacePolicy))]
    public void CreateByName_RoundTripsThePolicyName(string name, Type expectedType)
    {
        var policy = SimulationSpacePolicy.CreateByName(name);

        Assert.IsType(expectedType, policy);
        Assert.Equal(name, policy.Name);
        Assert.IsType(expectedType, SimulationSpacePolicy.CreateByName(policy.Name));
    }

    [Fact]
    public void CreateByName_UnknownName_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => SimulationSpacePolicy.CreateByName("Isometric"));
        Assert.Contains("Isometric", exception.Message);
    }

    [Fact]
    public void Planar2d_LocksTheFactorsLeftUnexpressed()
    {
        var definition = new PhysicsDefinition();

        new Planar2dSimulationSpacePolicy().ApplyDefaultConstraints(definition);

        Assert.Equal(new Vector3(1f, 1f, 0f), definition.LinearFactor);
        Assert.Equal(new Vector3(0f, 0f, 1f), definition.AngularFactor);
    }

    [Fact]
    public void Planar2d_KeepsTheFactorsAComponentAuthored()
    {
        var definition = new PhysicsDefinition
        {
            LinearFactor = new Vector3(1f, 0f, 1f),
            AngularFactor = new Vector3(0f, 1f, 0f)
        };

        new Planar2dSimulationSpacePolicy().ApplyDefaultConstraints(definition);

        Assert.Equal(new Vector3(1f, 0f, 1f), definition.LinearFactor);
        Assert.Equal(new Vector3(0f, 1f, 0f), definition.AngularFactor);
    }

    [Fact]
    public void Identity3dAndTopDownElevation_ConstrainNothing()
    {
        var identityDefinition = new PhysicsDefinition();
        var topDownDefinition = new PhysicsDefinition();

        new Identity3dSimulationSpacePolicy().ApplyDefaultConstraints(identityDefinition);
        new TopDownElevationSimulationSpacePolicy().ApplyDefaultConstraints(topDownDefinition);

        Assert.Equal(Vector3.One, identityDefinition.LinearFactor);
        Assert.Equal(Vector3.One, identityDefinition.AngularFactor);
        Assert.Equal(Vector3.One, topDownDefinition.LinearFactor);
        Assert.Equal(Vector3.One, topDownDefinition.AngularFactor);
    }

    [Fact]
    public void WorldSpacePolicyName_RoundTripsThroughTheWorldSerializer()
    {
        var world = new World { SpacePolicyName = SimulationSpacePolicyNames.TopDownElevation };

        var node = new JObject();
        EditorEntityJsonSerializer.SaveWorld(world, node);

        Assert.Equal(SimulationSpacePolicyNames.TopDownElevation, (string?)node["space_policy"]);

        var loaded = new World();
        loaded.Load(node);

        Assert.Equal(SimulationSpacePolicyNames.TopDownElevation, loaded.SpacePolicyName);
    }

    [Fact]
    public void WorldWithoutASpacePolicyField_FallsBackToTheProjectPolicy()
    {
        var node = new JObject();
        EditorEntityJsonSerializer.SaveWorld(new World(), node);
        node.Remove("space_policy");

        var world = new World();
        world.Load(node);

        Assert.Equal(string.Empty, world.SpacePolicyName);

        var physicsSystem = CreateDetachedPhysicsSystem();
        var context = physicsSystem.GetOrCreateContext(world);

        Assert.Same(GameSettings.PhysicsEngineSettings.SpacePolicy, context.SpacePolicy);
        physicsSystem.ReleaseContext(world);
    }

    /// <summary>
    /// Wiring: the single place a physics context is built must resolve the policy the world names.
    /// </summary>
    [Fact]
    public void PhysicsContext_UsesThePolicyItsWorldNames()
    {
        var world = new World { SpacePolicyName = SimulationSpacePolicyNames.TopDownElevation };

        var physicsSystem = CreateDetachedPhysicsSystem();
        var context = physicsSystem.GetOrCreateContext(world);

        Assert.IsType<TopDownElevationSimulationSpacePolicy>(context.SpacePolicy);
        Assert.Same(context, physicsSystem.GetOrCreateContext(world));

        physicsSystem.ReleaseContext(world);
    }

    /// <summary>
    /// Wiring: the constraints of the world must reach the definition before the body consumes it.
    /// </summary>
    [Fact]
    public void Planar2dWorld_LocksTheBodiesItCreates()
    {
        using var physicsWorldContext = new PhysicsWorld(false, new Planar2dSimulationSpacePolicy());
        var component = new CollisionComponent();
        component.PhysicsDefinition.PhysicsType = PhysicsType.Dynamic;
        component.PhysicsDefinition.Mass = 1f;
        component.PhysicsDefinition.ApplyGravity = false;
        component.Fixtures.Add(new ColliderFixture(new Box()));

        CreateEntityWithComponent(physicsWorldContext, component);

        Assert.Equal(new Vector3(1f, 1f, 0f), component.PhysicsDefinition.LinearFactor);
        Assert.Equal(new Vector3(0f, 0f, 1f), component.PhysicsDefinition.AngularFactor);

        //The body itself is locked: it was built after the constraints were applied, so the impulse
        //it receives along the axis the plane forbids is filtered out by its linear factor.
        var body = Assert.Single(component.Bodies);
        component.ApplyImpulse(new Vector3(0f, 0f, 10f), Vector3.Zero);
        physicsWorldContext.Update(1f / 30f);

        Assert.Equal(0f, body.WorldTransform.Translation.Z, precision: 4);

        component.DisablePhysics();
    }

    [Fact]
    public void Identity3dWorld_LeavesTheBodiesItCreatesUnconstrained()
    {
        using var physicsWorldContext = new PhysicsWorld(false, new Identity3dSimulationSpacePolicy());
        var component = new CollisionComponent();
        component.PhysicsDefinition.PhysicsType = PhysicsType.Dynamic;
        component.PhysicsDefinition.Mass = 1f;
        component.PhysicsDefinition.ApplyGravity = false;
        component.Fixtures.Add(new ColliderFixture(new Box()));

        CreateEntityWithComponent(physicsWorldContext, component);

        Assert.Equal(Vector3.One, component.PhysicsDefinition.LinearFactor);
        Assert.Equal(Vector3.One, component.PhysicsDefinition.AngularFactor);

        var body = Assert.Single(component.Bodies);
        component.ApplyImpulse(new Vector3(0f, 0f, 10f), Vector3.Zero);
        physicsWorldContext.Update(1f / 30f);

        Assert.True(body.WorldTransform.Translation.Z > 0.1f);

        component.DisablePhysics();
    }

    /// <summary>
    /// The point of the whole phase: two entities that overlap on screen at different elevations are far
    /// apart in the simulation, because the bodies live in the logical space of the entity roots.
    /// </summary>
    [Fact]
    public void TopDownElevationWorld_SeparatesTheBodiesTheScreenOverlaps()
    {
        using var physicsWorldContext = new PhysicsWorld(true, new TopDownElevationSimulationSpacePolicy());

        var groundLevel = CreateGhostBox(physicsWorldContext, new Vector3(10f, 20f, 0f));
        var elevated = CreateGhostBox(physicsWorldContext, new Vector3(10f, 36f, 16f));
        var coLocated = CreateGhostBox(physicsWorldContext, new Vector3(10f, 20f, 0f));

        //Both boxes derive the same screen row.
        var policy = physicsWorldContext.SpacePolicy;
        Assert.Equal(
            policy.DeriveRenderPosition(new Vector3(10f, 20f, 0f)),
            policy.DeriveRenderPosition(new Vector3(10f, 36f, 16f)));

        physicsWorldContext.Update(1f / 60f);

        Assert.DoesNotContain(elevated, CollidingComponents(groundLevel));
        Assert.Contains(coLocated, CollidingComponents(groundLevel));
        Assert.Empty(elevated.Collisions);

        groundLevel.DisablePhysics();
        elevated.DisablePhysics();
        coLocated.DisablePhysics();
    }

    [Fact]
    public void RenderProjectionComponent_PlacesItselfAtTheDerivedPosition()
    {
        using var physicsWorldContext = new PhysicsWorld(true, new TopDownElevationSimulationSpacePolicy());
        var projection = new RenderProjectionComponent();
        var root = CreateEntityWithProjection(physicsWorldContext, projection);

        root.LocalTransform.Position = new Vector3(10f, 36f, 16f);
        root.Update(0f);

        var renderPosition = projection.WorldMatrixNoScale.Translation;
        Assert.Equal(10f, renderPosition.X, precision: 4);
        Assert.Equal(-20f, renderPosition.Y, precision: 4);
        Assert.Equal(0f, renderPosition.Z, precision: 4);
    }

    [Fact]
    public void RenderProjectionComponent_IsInertUnderAnIdentityPolicy()
    {
        using var physicsWorldContext = new PhysicsWorld(true, new Identity3dSimulationSpacePolicy());
        var projection = new RenderProjectionComponent();
        var root = CreateEntityWithProjection(physicsWorldContext, projection);

        root.LocalTransform.Position = new Vector3(10f, 36f, 16f);
        root.Update(0f);

        Assert.Equal(Vector3.Zero, projection.LocalTransform.Position);
        Assert.Equal(new Vector3(10f, 36f, 16f), projection.WorldMatrixNoScale.Translation);
    }

    /// <summary>
    /// Default is off: a fractional logical position still renders fractionally, unchanged from before
    /// E5.b, so nothing that already relies on <see cref="RenderProjectionComponent"/> is disturbed.
    /// </summary>
    [Fact]
    public void RenderProjectionComponent_SnapToPixelOff_KeepsTheFractionalRenderPosition()
    {
        using var physicsWorldContext = new PhysicsWorld(true, new TopDownElevationSimulationSpacePolicy());
        var projection = new RenderProjectionComponent();
        var root = CreateEntityWithProjection(physicsWorldContext, projection);

        root.LocalTransform.Position = new Vector3(10.7f, 20.3f, 0f);
        root.Update(0f);

        var renderPosition = projection.WorldMatrixNoScale.Translation;
        Assert.Equal(10.7f, renderPosition.X, precision: 4);
        Assert.Equal(-20.3f, renderPosition.Y, precision: 4);
        Assert.Equal(0f, renderPosition.Z, precision: 4);
    }

    /// <summary>
    /// The discriminating case (plan-verifier's fixed convention): floor on X, ceiling on Y. A floor
    /// applied in render space would give (10, -21, 0) instead, and a round-to-nearest would give
    /// (11, -20, 0) instead - both were considered and rejected; this is the case that tells them apart
    /// from the fixed convention, and each is asserted not to match it below.
    /// </summary>
    [Fact]
    public void RenderProjectionComponent_SnapToPixelOn_FloorsXAndCeilsY()
    {
        using var physicsWorldContext = new PhysicsWorld(true, new TopDownElevationSimulationSpacePolicy());
        var projection = new RenderProjectionComponent { SnapToPixel = true };
        var root = CreateEntityWithProjection(physicsWorldContext, projection);

        root.LocalTransform.Position = new Vector3(10.7f, 20.3f, 0f);
        root.Update(0f);

        var renderPosition = projection.WorldMatrixNoScale.Translation;
        Assert.Equal(new Vector3(10f, -20f, 0f), renderPosition);

        //Both rejected conventions fail this case.
        Assert.NotEqual(new Vector3(10f, -21f, 0f), renderPosition); //floor in render space
        Assert.NotEqual(new Vector3(11f, -20f, 0f), renderPosition); //round to nearest
    }

    /// <summary>Elevation enters the row through Y - Z before the snap, not after.</summary>
    [Fact]
    public void RenderProjectionComponent_SnapToPixelOn_FoldsElevationBeforeSnapping()
    {
        using var physicsWorldContext = new PhysicsWorld(true, new TopDownElevationSimulationSpacePolicy());
        var projection = new RenderProjectionComponent { SnapToPixel = true };
        var root = CreateEntityWithProjection(physicsWorldContext, projection);

        //Y - Z = 36.2 - 16.9 = 19.3, negated: -19.3, ceiling: -19.
        root.LocalTransform.Position = new Vector3(-2.4f, 36.2f, 16.9f);
        root.Update(0f);

        var renderPosition = projection.WorldMatrixNoScale.Translation;
        Assert.Equal(new Vector3(-3f, -19f, 0f), renderPosition);
    }

    /// <summary>
    /// A slow continuous advance of the logical pose must not oscillate between two integer render
    /// rows: each step's snapped Y must be monotonically non-increasing, matching the monotonically
    /// increasing logical Y under the sign flip.
    /// </summary>
    [Fact]
    public void RenderProjectionComponent_SnapToPixelOn_AdvancesMonotonicallyWithoutOscillation()
    {
        using var physicsWorldContext = new PhysicsWorld(true, new TopDownElevationSimulationSpacePolicy());
        var projection = new RenderProjectionComponent { SnapToPixel = true };
        var root = CreateEntityWithProjection(physicsWorldContext, projection);

        float? previousY = null;

        for (var i = 0; i <= 40; i++)
        {
            var logicalY = 20f + i * 0.1f;
            root.LocalTransform.Position = new Vector3(10f, logicalY, 0f);
            root.Update(0f);

            var renderY = projection.WorldMatrixNoScale.Translation.Y;

            if (previousY.HasValue)
            {
                //A tolerance absorbs the float round-trip through WorldMatrixNoScale's invert/multiply,
                //not the snap itself: the snapped local position is exactly integral before that.
                Assert.True(renderY <= previousY.Value + 1e-3f, $"render Y regressed forward from {previousY} to {renderY} at logical Y {logicalY}.");
            }

            previousY = renderY;
        }
    }

    private static IEnumerable<ICollideableComponent> CollidingComponents(CollisionComponent component)
    {
        foreach (var collision in component.Collisions)
        {
            yield return ReferenceEquals(collision.ColliderA, component) ? collision.ColliderB : collision.ColliderA;
        }
    }

    private static CollisionComponent CreateGhostBox(IPhysicsWorld physicsWorldContext, Vector3 logicalPosition)
    {
        var component = new CollisionComponent();
        component.PhysicsDefinition.PhysicsType = PhysicsType.Kinetic;
        component.PhysicsDefinition.ProfileName = CollisionProfileNames.Trigger;
        component.Fixtures.Add(new ColliderFixture(new Box { Size = new Vector3(4f) }));

        var world = CreateWorld(physicsWorldContext);
        var entity = new Entity();
        SetProperty(entity, nameof(Entity.World), world);

        //The collision component is the root of the entity: its pose is the logical pose.
        entity.RootComponent = component;
        component.LocalTransform.Position = logicalPosition;
        component.InitializeWithWorld(world);

        return component;
    }

    private static SceneComponent CreateEntityWithComponent(IPhysicsWorld physicsWorldContext, CollisionComponent component)
    {
        var world = CreateWorld(physicsWorldContext);
        var entity = new Entity();
        SetProperty(entity, nameof(Entity.World), world);

        var root = new TestSceneComponent();
        entity.RootComponent = root;
        root.AddChildComponent(component);
        component.InitializeWithWorld(world);

        return root;
    }

    private static SceneComponent CreateEntityWithProjection(IPhysicsWorld physicsWorldContext, RenderProjectionComponent projection)
    {
        var world = CreateWorld(physicsWorldContext);
        var entity = new Entity();
        SetProperty(entity, nameof(Entity.World), world);

        var root = new TestSceneComponent();
        entity.RootComponent = root;
        root.AddChildComponent(projection);

        return root;
    }

    private static World CreateWorld(IPhysicsWorld physicsWorldContext)
    {
        var world = new World();
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        game.ExecutionPolicy = GameplayExecutionPolicies.Runtime;
        SetProperty(world, nameof(World.Game), game);
        SetProperty(world, nameof(World.PhysicsWorld), physicsWorldContext);
        return world;
    }

    /// <summary>
    /// A physics system component without a game behind it: only its context table is needed here.
    /// </summary>
    private static PhysicsSystemComponent CreateDetachedPhysicsSystem()
    {
        var component = (PhysicsSystemComponent)RuntimeHelpers.GetUninitializedObject(typeof(PhysicsSystemComponent));
        var contextsField = typeof(PhysicsSystemComponent).GetField("_physicsWorldContexts", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(contextsField);
        contextsField!.SetValue(component, new Dictionary<World, PhysicsWorld>());
        return component;
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var property = typeof(TTarget).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    private sealed class TestSceneComponent : SceneComponent
    {
        public TestSceneComponent()
        {
        }

        private TestSceneComponent(TestSceneComponent other) : base(other)
        {
        }

        public override TestSceneComponent Clone() => new(this);
    }
}
