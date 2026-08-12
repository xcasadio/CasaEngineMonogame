using System.Reflection;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Physics;

public class PhysicsComponentBoundingBoxUpdateTests
{
    [Fact]
    public void RootBounds_FollowParentTranslation_WhenPhysicsChildRunsInEditorPreview()
    {
        var world = new World();
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        game.ExecutionPolicy = GameplayExecutionPolicies.EditorPreview;
        SetProperty(world, nameof(World.Game), game);

        var entity = new Entity();
        SetProperty(entity, nameof(Entity.World), world);

        var root = new StaticModelComponent();
        entity.RootComponent = root;

        root.AddChildComponent(new TestSceneComponent());
        var collisionComponent = new CollisionComponent();
        collisionComponent.Fixtures.Add(new ColliderFixture(new Box()));
        root.AddChildComponent(collisionComponent);

        var initialBounds = root.BoundingBox;
        Assert.InRange(initialBounds.Min.X, -0.6f, -0.4f);
        Assert.InRange(initialBounds.Max.X, 0.4f, 0.6f);

        root.Position = new Vector3(10f, 0f, 0f);
        root.Update(0f);

        var movedBounds = root.BoundingBox;
        Assert.InRange(movedBounds.Min.X, 9.4f, 9.6f);
        Assert.InRange(movedBounds.Max.X, 10.4f, 10.6f);
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var property = typeof(TTarget).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    private sealed class TestSceneComponent : SceneComponent
    {
        public override TestSceneComponent Clone() => new(this);

        public TestSceneComponent()
        {
        }

        private TestSceneComponent(TestSceneComponent other)
            : base(other)
        {
        }
    }
}