using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Physics;

/// <summary>
/// The live preview policy: the physics component of the game skips a world whose own policy says something else steps
/// it, and a collision of such a world calls no game code. Only the named policies are used.
/// </summary>
public class LivePreviewPolicyTests
{
    private static CasaEngineGame CreateGame(GameplayExecutionPolicy policy, World? currentWorld = null)
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        game.ExecutionPolicy = policy;

        var gameManager = new GameManager(null);
        if (currentWorld != null)
        {
            gameManager.RestoreWorld(currentWorld);
        }

        typeof(CasaEngineGame).GetField("<GameManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(game, gameManager);
        return game;
    }

    private static PhysicsSystemComponent CreatePhysicsSystem(CasaEngineGame game)
    {
        var component = (PhysicsSystemComponent)RuntimeHelpers.GetUninitializedObject(typeof(PhysicsSystemComponent));
        SetField(component, "_casaEngineGame", game);
        SetField(component, "_physicsWorldContexts", new Dictionary<World, PhysicsWorld>());
        SetField(component, "_worldsToUpdate", new List<World>());
        return component;
    }

    private static int ContextCount(PhysicsSystemComponent component)
    {
        var contexts = (Dictionary<World, PhysicsWorld>)typeof(PhysicsSystemComponent)
            .GetField("_physicsWorldContexts", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(component)!;
        return contexts.Count;
    }

    private static void SetField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var property = typeof(TTarget).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    /// <summary>A world with its own policy (set before its game, as before <c>LoadContent</c>) and a game.</summary>
    private static World CreateWorld(GameplayExecutionPolicy gamePolicy, GameplayExecutionPolicy? ownPolicy, IPhysicsWorld physicsWorld)
    {
        var world = new World { Name = "LivePreviewWorld" };
        if (ownPolicy != null)
        {
            world.ExecutionPolicyOverride = ownPolicy;
        }

        SetProperty(world, nameof(World.Game), CreateGame(gamePolicy));
        SetProperty(world, nameof(World.PhysicsWorld), physicsWorld);
        return world;
    }

    /// <summary>A dynamic collision component, child of a root component with a null local pose, as in a real entity.</summary>
    private static (SceneComponent Root, CollisionComponent Collision) AddDynamicBox(World world)
    {
        var collision = new CollisionComponent();
        collision.PhysicsDefinition.PhysicsType = PhysicsType.Dynamic;
        collision.PhysicsDefinition.Mass = 1f;
        collision.Fixtures.Add(new ColliderFixture(new Box()));
        var root = AttachToNewEntity(world, collision);
        collision.Bodies[0].LinearVelocity = new Vector3(0f, -10f, 0f);
        return (root, collision);
    }

    private static SceneComponent AttachToNewEntity(World world, CollisionComponent collision, Vector3 position = default)
    {
        var entity = new Entity();
        SetProperty(entity, nameof(Entity.World), world);
        var root = new TestSceneComponent();
        entity.RootComponent = root;
        root.LocalTransform.Position = position;
        root.AddChildComponent(collision);
        collision.InitializeWithWorld(world);
        return root;
    }

    // 1/30 s: a step of exactly 1/60 s can fall just under the fixed step of the engine and run nothing.
    private static GameTime OneStep() => new(TimeSpan.Zero, TimeSpan.FromSeconds(1d / 30d));

    private sealed class HitRecorder : GameplayProxy
    {
        public int Hits;
        public int HitsEnded;

        public override void InitializeWithWorld(World world)
        {
        }

        public override void Update(float elapsedTime)
        {
        }

        public override void Draw()
        {
        }

        public override void OnHit(Collision collision) => Hits++;

        public override void OnHitEnded(Collision collision) => HitsEnded++;

        public override void OnBeginPlay(World world)
        {
        }

        public override void OnEndPlay(World world)
        {
        }

        public override IGameplayProxy Clone() => throw new NotSupportedException();
    }

    [Fact]
    public void ThePhysicsComponentOfTheGame_StepsAnOrdinaryWorld_AndSkipsAWorldUnderTheLivePreviewPolicy()
    {
        var system = CreatePhysicsSystem(CreateGame(GameplayExecutionPolicies.EditorSimulation));
        using var livePhysics = new PhysicsWorld(useExternalViewManagement: true);
        var ordinaryWorld = CreateWorld(GameplayExecutionPolicies.EditorSimulation, ownPolicy: null, physicsWorld: null!);
        // The context the game component will step is the one the world builds its bodies in.
        SetProperty(ordinaryWorld, nameof(World.PhysicsWorld), system.GetOrCreateContext(ordinaryWorld));
        var liveWorld = CreateWorld(GameplayExecutionPolicies.EditorSimulation, GameplayExecutionPolicies.EditorLivePreview, livePhysics);
        var (_, ordinaryBox) = AddDynamicBox(ordinaryWorld);
        var (_, liveBox) = AddDynamicBox(liveWorld);
        float ordinaryStart = ordinaryBox.Bodies[0].WorldTransform.Translation.Y;
        float liveStart = liveBox.Bodies[0].WorldTransform.Translation.Y;

        // The game steps its current world: each world is made current in turn.
        SetCurrentWorld(system, liveWorld);
        system.Update(OneStep());
        SetCurrentWorld(system, ordinaryWorld);
        system.Update(OneStep());

        Assert.True(ordinaryBox.Bodies[0].WorldTransform.Translation.Y < ordinaryStart - 0.001f);
        Assert.Equal(liveStart, liveBox.Bodies[0].WorldTransform.Translation.Y);
        // The live world was skipped before its context was requested: only the ordinary world got one.
        Assert.Equal(1, ContextCount(system));

        ordinaryBox.DisablePhysics();
        liveBox.DisablePhysics();
    }

    [Fact]
    public void AWorldUnderTheLivePreviewPolicy_AdvancesWhenItsPhysicsWorldIsUpdatedDirectly_AndItsComponentReportsTheBody()
    {
        using var livePhysics = new PhysicsWorld(useExternalViewManagement: true);
        var liveWorld = CreateWorld(GameplayExecutionPolicies.EditorPreview, GameplayExecutionPolicies.EditorLivePreview, livePhysics);
        var (root, collision) = AddDynamicBox(liveWorld);
        float start = root.LocalTransform.Position.Y;

        liveWorld.PhysicsWorld.Update(1f / 30f);
        Assert.True(collision.Bodies[0].WorldTransform.Translation.Y < start - 0.01f);
        // The entity shows the pose of its body at the next update of its components.
        root.Update(1f / 30f);

        Assert.True(root.LocalTransform.Position.Y < start - 0.01f);

        collision.DisablePhysics();
    }

    private static void SetCurrentWorld(PhysicsSystemComponent system, World world)
    {
        var game = (CasaEngineGame)typeof(PhysicsSystemComponent)
            .GetField("_casaEngineGame", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(system)!;
        game.GameManager.RestoreWorld(world);
    }

    private static (HitRecorder RecorderA, CollisionComponent ComponentA, CollisionComponent ComponentB) CreateTouchingKineticBoxes(World world)
    {
        var componentA = new CollisionComponent();
        componentA.PhysicsDefinition.PhysicsType = PhysicsType.Kinetic;
        componentA.Fixtures.Add(new ColliderFixture(new Box()));
        var rootA = AttachToNewEntity(world, componentA);
        var recorder = new HitRecorder();
        SetProperty(rootA.Owner, nameof(Entity.GameplayProxy), recorder);

        var componentB = new CollisionComponent();
        componentB.PhysicsDefinition.PhysicsType = PhysicsType.Kinetic;
        componentB.Fixtures.Add(new ColliderFixture(new Box()));
        AttachToNewEntity(world, componentB, new Vector3(0.5f, 0f, 0f));

        return (recorder, componentA, componentB);
    }

    [Fact]
    public void ACollisionInAWorldUnderTheLivePreviewPolicy_CallsNoScript_ButFillsTheCollisionList()
    {
        using var physics = new PhysicsWorld(useExternalViewManagement: true);
        var world = CreateWorld(GameplayExecutionPolicies.Runtime, GameplayExecutionPolicies.EditorLivePreview, physics);
        var (recorder, componentA, componentB) = CreateTouchingKineticBoxes(world);

        physics.Update(1f / 60f);

        Assert.Single(componentA.Collisions);
        Assert.Equal(0, recorder.Hits);

        componentB.DisablePhysics();

        Assert.Empty(componentA.Collisions);
        Assert.Equal(0, recorder.HitsEnded);

        componentA.DisablePhysics();
    }

    [Fact]
    public void ACollisionInARuntimeWorld_CallsTheScript()
    {
        using var physics = new PhysicsWorld(useExternalViewManagement: false);
        var world = CreateWorld(GameplayExecutionPolicies.Runtime, ownPolicy: null, physics);
        var (recorder, componentA, componentB) = CreateTouchingKineticBoxes(world);

        physics.Update(1f / 60f);

        Assert.Single(componentA.Collisions);
        Assert.Equal(1, recorder.Hits);

        componentB.DisablePhysics();

        Assert.Empty(componentA.Collisions);
        Assert.Equal(1, recorder.HitsEnded);

        componentA.DisablePhysics();
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
