using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Physics;

/// <summary>
/// Covers <see cref="TransformComponent"/>: the inert scene component usable as the root of an entity
/// whose visuals live under a <see cref="RenderProjectionComponent"/> child (E3.0 of
/// docs/plan-e3-collisions.md).
/// </summary>
public class TransformComponentTests
{
    [Fact]
    public void ElementFactory_ResolvesTransformComponentBySimpleName()
    {
        var component = ElementFactory.Create<SceneComponent>("TransformComponent");

        Assert.IsType<TransformComponent>(component);
    }

    [Fact]
    public void Clone_PreservesTheHierarchy()
    {
        var root = new TransformComponent();
        var projection = new RenderProjectionComponent();
        var sprite = new AnimatedSpriteComponent();
        root.AddChildComponent(projection);
        projection.AddChildComponent(sprite);

        var clone = root.Clone();

        var clonedProjection = Assert.IsType<RenderProjectionComponent>(Assert.Single(clone.Children));
        Assert.IsType<AnimatedSpriteComponent>(Assert.Single(clonedProjection.Children));
    }

    [Fact]
    public void JsonRoundTrip_PreservesTheHierarchy()
    {
        var entity = new Entity();
        var root = new TransformComponent();
        entity.RootComponent = root;
        root.LocalTransform.Position = new Vector3(1f, 2f, 3f);

        var projection = new RenderProjectionComponent();
        root.AddChildComponent(projection);

        var sprite = new AnimatedSpriteComponent();
        projection.AddChildComponent(sprite);

        var node = new JObject();
        EditorEntityJsonSerializer.SaveEntity(entity, node);

        var loaded = new Entity();
        loaded.Load(node);

        var loadedRoot = Assert.IsType<TransformComponent>(loaded.RootComponent);
        Assert.Equal(new Vector3(1f, 2f, 3f), loadedRoot.LocalTransform.Position);

        var loadedProjection = Assert.IsType<RenderProjectionComponent>(Assert.Single(loadedRoot.Children));
        Assert.IsType<AnimatedSpriteComponent>(Assert.Single(loadedProjection.Children));
    }

    /// <summary>
    /// A root TransformComponent under a TopDownElevation world: after one World.Update the entity
    /// bounds follow the render position derived from the projection, not the logical position of the
    /// root, and adding a physics component under the root does not change them (physics components are
    /// excluded from the union).
    /// </summary>
    [Fact]
    public void GetBoundingBox_AfterUpdate_FollowsTheRenderPositionAndExcludesPhysics()
    {
        var logicalPosition = new Vector3(0f, 952f, 0f);
        var expectedRenderPosition = new Vector3(0f, -952f, 0f);

        using var physicsWorldContext = new PhysicsWorld(false, new TopDownElevationSimulationSpacePolicy());
        var (world, entity, root, _, _) = CreateProjectedEntity(physicsWorldContext, logicalPosition);

        world.AddEntity(entity);
        world.Update(1f / 50f);

        var boundingBox = entity.GetBoundingBox();
        Assert.NotEqual(ContainmentType.Disjoint, boundingBox.Contains(expectedRenderPosition));
        Assert.Equal(ContainmentType.Disjoint, boundingBox.Contains(logicalPosition));

        var collision = new CollisionComponent();
        root.AddChildComponent(collision);

        Assert.Equal(boundingBox, entity.GetBoundingBox());
    }

    /// <summary>
    /// A child probe under a TransformComponent root observes exactly one Update and one Draw per call
    /// on the root: "inert" means no own state or visual, not that propagation stops.
    /// </summary>
    [Fact]
    public void Update_And_Draw_PropagateExactlyOnceToChildren()
    {
        var entity = new Entity();
        var root = new TransformComponent();
        entity.RootComponent = root;

        var probe = new ProbeComponent();
        root.AddChildComponent(probe);

        root.Update(1f / 50f);
        root.Draw(1f / 50f);

        Assert.Equal(1, probe.UpdateCount);
        Assert.Equal(1, probe.DrawCount);
    }

    /// <summary>
    /// The point of the whole slice: without physics, a projected entity is still returned by the world
    /// index around its render pose and not around its logical pose after one World.Update, and it
    /// resolves to dynamic spatial maintenance. This fails without the dirty marking in
    /// RenderProjectionComponent.UpdateProjection (the index would keep the pre-projection box) and it
    /// fails without RenderProjectionComponent contributing EntityPolicySet.DynamicDefault (the entity
    /// would resolve to a static index / conditional tick and never even run Update).
    /// </summary>
    [Fact]
    public void WorldIndex_ReturnsTheEntityAroundItsRenderPose_NotItsLogicalPose()
    {
        var logicalPosition = new Vector3(0f, 952f, 0f);
        var renderPosition = new Vector3(0f, -952f, 0f);

        using var physicsWorldContext = new PhysicsWorld(false, new TopDownElevationSimulationSpacePolicy());
        var (world, entity, _, _, _) = CreateProjectedEntity(physicsWorldContext, logicalPosition);

        world.AddEntity(entity);
        world.Update(1f / 50f);

        var resolvedPolicies = EntityPolicyResolver.ResolveRuntimePolicies(entity);
        Assert.True(resolvedPolicies.UsesDynamicSpatialMaintenance);

        var aroundRenderPose = new List<Entity>();
        world.SpatialServices.WorldIndex.Query(CreateFrustumAroundPoint(renderPosition, 5f), aroundRenderPose);
        Assert.Contains(entity, aroundRenderPose);

        var aroundLogicalPose = new List<Entity>();
        world.SpatialServices.WorldIndex.Query(CreateFrustumAroundPoint(logicalPosition, 5f), aroundLogicalPose);
        Assert.DoesNotContain(entity, aroundLogicalPose);
    }

    [Fact]
    public void MarkBoundingBoxDirty_SetsIsBoundingBoxDirty()
    {
        var root = new TransformComponent();
        root.ClearBoundingBoxDirtyRecursive();
        Assert.False(root.IsBoundingBoxDirty);

        root.MarkBoundingBoxDirty();

        Assert.True(root.IsBoundingBoxDirty);
    }

    private static (World World, Entity Entity, TransformComponent Root, RenderProjectionComponent Projection, AnimatedSpriteComponent Sprite)
        CreateProjectedEntity(PhysicsWorld physicsWorldContext, Vector3 logicalPosition)
    {
        var world = new World();
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        game.ExecutionPolicy = GameplayExecutionPolicies.Runtime;

        //RuntimeHelpers.GetUninitializedObject skips Game's constructor, so Components (backed by
        //_components) is otherwise null: AnimatedSpriteComponent.InitializeWithWorld looks up a
        //SpriteRendererComponent through it.
        var componentsField = typeof(Microsoft.Xna.Framework.Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!;
        componentsField.SetValue(game, new Microsoft.Xna.Framework.GameComponentCollection());

        //Likewise, GameManager (whose ViewManager World.Update reaches for on-demand view invalidation)
        //is only ever assigned in CasaEngineGame's constructor.
        var gameManager = (CasaEngine.Framework.Application.GameManager)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngine.Framework.Application.GameManager));
        var viewManagerField = typeof(CasaEngine.Framework.Application.GameManager).GetField("<ViewManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        viewManagerField.SetValue(gameManager, new CasaEngine.Framework.Rendering.ViewManager());
        var gameManagerField = typeof(CasaEngineGame).GetField("<GameManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        gameManagerField.SetValue(game, gameManager);

        SetProperty(world, nameof(World.Game), game);
        SetProperty(world, nameof(World.PhysicsWorld), physicsWorldContext);

        var entity = new Entity();
        var root = new TransformComponent();
        entity.RootComponent = root;
        root.LocalTransform.Position = logicalPosition;

        var projection = new RenderProjectionComponent();
        root.AddChildComponent(projection);

        var sprite = new AnimatedSpriteComponent();
        projection.AddChildComponent(sprite);

        return (world, entity, root, projection, sprite);
    }

    private static BoundingFrustum CreateFrustumAroundPoint(Vector3 point, float halfExtent)
    {
        var eye = point + new Vector3(0f, 0f, halfExtent * 4f);
        var view = Matrix.CreateLookAt(eye, point, Vector3.Up);
        var projection = Matrix.CreateOrthographic(halfExtent * 2f, halfExtent * 2f, 0.1f, halfExtent * 8f);
        return new BoundingFrustum(view * projection);
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var property = typeof(TTarget).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    private sealed class ProbeComponent : SceneComponent
    {
        public int UpdateCount { get; private set; }
        public int DrawCount { get; private set; }

        public ProbeComponent()
        {
        }

        private ProbeComponent(ProbeComponent other) : base(other)
        {
        }

        public override ProbeComponent Clone() => new(this);

        public override void Update(float elapsedTime)
        {
            UpdateCount++;
            base.Update(elapsedTime);
        }

        public override void Draw(float elapsedTime)
        {
            DrawCount++;
            base.Draw(elapsedTime);
        }
    }
}
