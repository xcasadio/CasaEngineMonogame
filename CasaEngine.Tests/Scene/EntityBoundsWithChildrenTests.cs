using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Core.Math.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// The bounds of an entity, which decide whether the world draws it (ADR-0152): they count the descendants that draw,
/// physics subtrees excluded, and the bounds of a camera stand on the camera. Every scene component is an
/// <see cref="IComponentDrawable"/>, so "the descendants that draw" are all the descendants outside physics.
/// </summary>
public sealed class EntityBoundsWithChildrenTests
{
    private static readonly Vector3 Far = new(400f, -250f, 90f);

    // ---- Unchanged: an entity with no camera and no descendant outside physics ----

    public enum RootKind
    {
        Inert,
        Plain,
        Sprite,
    }

    private static SceneComponent CreateRoot(RootKind kind) => kind switch
    {
        RootKind.Inert => new TransformComponent(),
        RootKind.Plain => new PlainComponent(),
        _ => new StaticSpriteComponent(),
    };

    //  What the bounds of an entity were before its children counted: its root, extended by its entity components.
    private static BoundingBox BoundsOfRootAndEntityComponents(Entity entity)
    {
        var boundingBox = entity.RootComponent?.BoundingBox ?? new BoundingBox();
        foreach (var component in entity.Components)
        {
            if (component is SceneComponent sceneComponent)
            {
                boundingBox.ExpandBy(sceneComponent.BoundingBox);
            }
        }

        return boundingBox;
    }

    [Theory]
    [InlineData(RootKind.Inert, false, false)]
    [InlineData(RootKind.Inert, true, false)]
    [InlineData(RootKind.Inert, false, true)]
    [InlineData(RootKind.Inert, true, true)]
    [InlineData(RootKind.Plain, false, false)]
    [InlineData(RootKind.Plain, true, false)]
    [InlineData(RootKind.Plain, false, true)]
    [InlineData(RootKind.Plain, true, true)]
    [InlineData(RootKind.Sprite, false, false)]
    [InlineData(RootKind.Sprite, true, false)]
    [InlineData(RootKind.Sprite, false, true)]
    [InlineData(RootKind.Sprite, true, true)]
    public void Invariant_AnEntityWithNoCameraAndNoDescendantOutsidePhysics_KeepsItsBounds(
        RootKind kind, bool withEntityComponent, bool withPhysicsChild)
    {
        SceneComponent root = CreateRoot(kind);
        root.LocalPosition = new Vector3(10f, 20f, 30f);
        var entity = new Entity { RootComponent = root };
        if (withEntityComponent)
        {
            entity.AddComponent(new PlainComponent { LocalPosition = new Vector3(-60f, 5f, 0f) });
        }

        if (withPhysicsChild)
        {
            root.AddChildComponent(new CollisionComponent { LocalPosition = Far });
        }

        Assert.Equal(BoundsOfRootAndEntityComponents(entity), entity.GetBoundingBox());
    }

    [Fact]
    public void Invariant_AnInertRootThatAlreadyCountsItsDescendants_KeepsItsBounds()
    {
        var root = new TransformComponent { LocalPosition = new Vector3(10f, 20f, 30f) };
        var entity = new Entity { RootComponent = root };
        var holder = new PlainComponent { LocalPosition = new Vector3(5f, 0f, 0f) };
        root.AddChildComponent(holder);
        holder.AddChildComponent(new PlainComponent { LocalPosition = Far });
        root.AddChildComponent(new CollisionComponent { LocalPosition = -Far });

        Assert.Equal(BoundsOfRootAndEntityComponents(entity), entity.GetBoundingBox());
        Assert.Equal(root.BoundingBox, entity.GetBoundingBox());
    }

    // ---- New: the descendants that draw count, physics does not ----

    [Fact]
    public void TheBounds_ContainADistantChildThatDraws()
    {
        var root = new PlainComponent { LocalPosition = new Vector3(10f, 20f, 30f) };
        var entity = new Entity { RootComponent = root };
        var holder = new PlainComponent();
        root.AddChildComponent(holder);
        var drawable = new PlainComponent { LocalPosition = Far };
        holder.AddChildComponent(drawable);

        BoundingBox bounds = entity.GetBoundingBox();

        Assert.Equal(ContainmentType.Contains, bounds.Contains(drawable.BoundingBox));
        Assert.Equal(ContainmentType.Contains, bounds.Contains(root.BoundingBox));
    }

    [Fact]
    public void TheBounds_ContainADistantChildThatDraws_UnderAnEntityComponent()
    {
        var entity = new Entity { RootComponent = new PlainComponent() };
        var entityComponent = new PlainComponent { LocalPosition = new Vector3(-5f, 0f, 0f) };
        entity.AddComponent(entityComponent);
        var drawable = new PlainComponent { LocalPosition = Far };
        entityComponent.AddChildComponent(drawable);

        Assert.Equal(ContainmentType.Contains, entity.GetBoundingBox().Contains(drawable.BoundingBox));
    }

    [Fact]
    public void ADistantPhysicsChild_AndWhatHangsUnderIt_DoNotChangeTheBounds()
    {
        var root = new PlainComponent { LocalPosition = new Vector3(10f, 20f, 30f) };
        var entity = new Entity { RootComponent = root };
        BoundingBox before = entity.GetBoundingBox();

        var body = new CollisionComponent { LocalPosition = Far };
        root.AddChildComponent(body);
        body.AddChildComponent(new PlainComponent { LocalPosition = Far });

        Assert.Equal(before, entity.GetBoundingBox());
    }

    // ---- New: the bounds of a camera stand on the camera ----

    [Fact]
    public void TheBoundsOfACamera_StandOnTheCamera()
    {
        var camera = new PerspectiveCameraComponent { LocalPosition = new Vector3(500f, 0f, 300f) };

        BoundingBox bounds = camera.GetBoundingBox();

        Assert.Equal(new Vector3(499f, -1f, 299f), bounds.Min);
        Assert.Equal(new Vector3(501f, 1f, 301f), bounds.Max);
    }

    // ---- New: through the visibility query of the world ----

    [Fact]
    public void AChildThatDraws_OfACameraFarFromTheOrigin_IsReturnedByTheWorldIndex_AndFollowsTheCamera()
    {
        using var physicsWorld = new PhysicsWorld(false, new TopDownElevationSimulationSpacePolicy());
        World world = CreateWorld(physicsWorld);
        //  The camera stands at (500, 0, 300) and looks down -z: the origin of the world is far outside what it frames.
        var camera = new PerspectiveCameraComponent { LocalPosition = new Vector3(500f, 0f, 300f) };
        var entity = new Entity { Name = "camera", RootComponent = camera };
        var plate = new PlainComponent { LocalPosition = new Vector3(0f, 0f, -50f) };
        camera.AddChildComponent(plate);

        world.AddEntity(entity);
        world.Update(1f / 50f);

        Assert.True(EntityPolicyResolver.ResolveRuntimePolicies(entity).UsesDynamicSpatialMaintenance);
        AssertReturnedAround(world, entity, new Vector3(500f, 0f, 250f));

        //  The camera moves: its child follows, and the index with it.
        camera.LocalPosition = new Vector3(-800f, 40f, 300f);
        world.Update(1f / 50f);

        AssertReturnedAround(world, entity, new Vector3(-800f, 40f, 250f));
        AssertNotReturnedAround(world, entity, new Vector3(500f, 0f, 250f));
    }

    [Fact]
    public void AChildThatDraws_AndMovesAlone_MakesTheWorldIndexFollowIt()
    {
        using var physicsWorld = new PhysicsWorld(false, new TopDownElevationSimulationSpacePolicy());
        World world = CreateWorld(physicsWorld);
        var root = new PlainComponent();
        var entity = new Entity { Name = "holder", RootComponent = root };
        var drawable = new PlainComponent { LocalPosition = new Vector3(0f, 0f, -50f) };
        root.AddChildComponent(drawable);
        entity.ApplyExplicitPolicies(EntityPolicySet.DynamicDefault);

        world.AddEntity(entity);
        world.Update(1f / 50f);
        AssertReturnedAround(world, entity, new Vector3(0f, 0f, -50f));

        drawable.LocalPosition = new Vector3(900f, 0f, -50f);
        world.Update(1f / 50f);

        AssertReturnedAround(world, entity, new Vector3(900f, 0f, -50f));
    }

    // ---- Harness ----

    private static void AssertReturnedAround(World world, Entity entity, Vector3 point)
    {
        var found = new List<Entity>();
        world.SpatialServices.WorldIndex.Query(CreateFrustumAroundPoint(point, 5f), found);
        Assert.Contains(entity, found);
    }

    private static void AssertNotReturnedAround(World world, Entity entity, Vector3 point)
    {
        var found = new List<Entity>();
        world.SpatialServices.WorldIndex.Query(CreateFrustumAroundPoint(point, 5f), found);
        Assert.DoesNotContain(entity, found);
    }

    private static BoundingFrustum CreateFrustumAroundPoint(Vector3 point, float halfExtent)
    {
        var eye = point + new Vector3(0f, 0f, halfExtent * 4f);
        var view = Matrix.CreateLookAt(eye, point, Vector3.Up);
        var projection = Matrix.CreateOrthographic(halfExtent * 2f, halfExtent * 2f, 0.1f, halfExtent * 8f);
        return new BoundingFrustum(view * projection);
    }

    //  The world of TransformComponentTests: a game built without its constructor, enough for World.Update.
    private static World CreateWorld(PhysicsWorld physicsWorld)
    {
        var world = new World();
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        //  An editor policy: the screen size then comes from two fields, so a camera can join the world with no device.
        game.ExecutionPolicy = GameplayExecutionPolicies.EditorPreview;
        typeof(CasaEngineGame).GetField("_screenSizeWidth", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, 1280);
        typeof(CasaEngineGame).GetField("_screenSizeHeight", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, 720);

        var componentsField = typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!;
        componentsField.SetValue(game, new GameComponentCollection());

        var gameManager = (GameManager)RuntimeHelpers.GetUninitializedObject(typeof(GameManager));
        var viewManagerField = typeof(GameManager).GetField("<ViewManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        viewManagerField.SetValue(gameManager, new CasaEngine.Framework.Rendering.ViewManager());
        var gameManagerField = typeof(CasaEngineGame).GetField("<GameManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        gameManagerField.SetValue(game, gameManager);

        SetProperty(world, nameof(World.Game), game);
        SetProperty(world, nameof(World.PhysicsWorld), physicsWorld);
        return world;
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var property = typeof(TTarget).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    //  A scene component with nothing of its own: like every scene component, it is an IComponentDrawable, with the
    //  unit box of a scene component at its world place.
    private sealed class PlainComponent : SceneComponent
    {
        public PlainComponent()
        {
        }

        private PlainComponent(PlainComponent other) : base(other)
        {
        }

        public override PlainComponent Clone() => new(this);
    }
}
