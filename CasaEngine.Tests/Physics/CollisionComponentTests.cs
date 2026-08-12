using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Physics;

public class CollisionComponentTests
{
    [Fact]
    public void ColliderFixtures_RoundTrip_ShapeProfileAndTag()
    {
        var component = new CollisionComponent();
        component.Fixtures.Add(new ColliderFixture(new Box { Size = new Vector3(2f, 3f, 4f) })
        {
            LocalPosition = new Vector3(1f, 2f, 3f),
            LocalRotation = Quaternion.CreateFromYawPitchRoll(0.5f, 0f, 0f),
            ProfileName = CollisionProfileNames.Trigger,
            Tag = "hitbox",
        });
        component.Fixtures.Add(new ColliderFixture(new Capsule { Radius = 0.4f, Length = 1.2f }));

        var node = new JObject();
        node.AddArray("fixtures", component.Fixtures, EditorEntityJsonSerializer.SaveColliderFixture);

        var loaded = new CollisionComponent();
        LoadFixtures(loaded, node);

        Assert.Equal(2, loaded.Fixtures.Count);

        var first = loaded.Fixtures[0];
        var loadedBox = Assert.IsType<Box>(first.Shape);
        Assert.Equal(new Vector3(2f, 3f, 4f), loadedBox.Size);
        Assert.Equal(new Vector3(1f, 2f, 3f), first.LocalPosition);
        Assert.Equal(Quaternion.CreateFromYawPitchRoll(0.5f, 0f, 0f), first.LocalRotation);
        Assert.Equal(CollisionProfileNames.Trigger, first.ProfileName);
        Assert.Equal("hitbox", first.Tag);

        var second = loaded.Fixtures[1];
        var loadedCapsule = Assert.IsType<Capsule>(second.Shape);
        Assert.Equal(0.4f, loadedCapsule.Radius);
        Assert.Equal(1.2f, loadedCapsule.Length);
        Assert.Equal(Vector3.Zero, second.LocalPosition);
        Assert.Equal(Quaternion.Identity, second.LocalRotation);
        Assert.True(string.IsNullOrEmpty(second.ProfileName));
        Assert.True(string.IsNullOrEmpty(second.Tag));
    }

    [Fact]
    public void SeveralFixtures_SharingOneProfile_CreateOneCompoundBody()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);
        var component = new CollisionComponent();
        component.PhysicsDefinition.PhysicsType = PhysicsType.Static;
        component.Fixtures.Add(new ColliderFixture(new Box()) { LocalPosition = new Vector3(-1f, 0f, 0f), Tag = "left" });
        component.Fixtures.Add(new ColliderFixture(new Box()) { LocalPosition = new Vector3(1f, 0f, 0f), Tag = "right" });

        CreateEntityWithComponent(physicsWorldContext, component);

        var body = Assert.Single(component.Bodies);
        Assert.True(body.IsCompound);
        Assert.Equal(2, body.FixtureCount);
        Assert.Equal(new Vector3(-1f, 0f, 0f), body.GetFixtureLocalTransform(0).Translation);
        Assert.Equal(new Vector3(1f, 0f, 0f), body.GetFixtureLocalTransform(1).Translation);
        Assert.Equal("left", body.GetFixtureTag(0));
        Assert.Equal("right", body.GetFixtureTag(1));

        component.DisablePhysics();
    }

    [Fact]
    public void FixturesUsingTwoProfiles_CreateOneBodyPerProfile()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);
        var component = new CollisionComponent();
        component.PhysicsDefinition.PhysicsType = PhysicsType.Static;
        component.Fixtures.Add(new ColliderFixture(new Box()));
        component.Fixtures.Add(new ColliderFixture(new Sphere()) { ProfileName = CollisionProfileNames.Trigger });

        CreateEntityWithComponent(physicsWorldContext, component);

        Assert.Equal(2, component.Bodies.Count);

        var profiles = GameSettings.PhysicsEngineSettings.CollisionProfiles;
        Assert.Equal(profiles.GetResolved(CollisionProfileIds.WorldStatic).GroupBit, component.Bodies[0].CollisionProfile.GroupBit);
        Assert.Equal(profiles.GetResolved(CollisionProfileIds.Trigger).GroupBit, component.Bodies[1].CollisionProfile.GroupBit);
        Assert.False(component.Bodies[0].IsCompound);
        Assert.False(component.Bodies[1].IsCompound);

        component.DisablePhysics();
    }

    [Fact]
    public void DynamicComponent_UsingSeveralProfiles_IsRejected()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);
        var component = new CollisionComponent();
        component.PhysicsDefinition.PhysicsType = PhysicsType.Dynamic;
        component.PhysicsDefinition.Mass = 1f;
        component.Fixtures.Add(new ColliderFixture(new Box()));
        component.Fixtures.Add(new ColliderFixture(new Sphere()) { ProfileName = CollisionProfileNames.Trigger });

        Assert.Throws<InvalidOperationException>(() => CreateEntityWithComponent(physicsWorldContext, component));
        Assert.Empty(component.Bodies);
    }

    [Fact]
    public void StaticComponent_NeverWritesBackToTheEntityTransform()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: false);
        var component = new CollisionComponent();
        component.PhysicsDefinition.PhysicsType = PhysicsType.Static;
        component.Fixtures.Add(new ColliderFixture(new Box()));
        component.Fixtures.Add(new ColliderFixture(new Sphere()) { ProfileName = CollisionProfileNames.Trigger });

        var root = CreateEntityWithComponent(physicsWorldContext, component, GameplayExecutionPolicies.Runtime);
        Assert.Equal(2, component.Bodies.Count);

        //The bodies stay at the origin: only gameplay may move a static component.
        root.LocalTransform.Position = new Vector3(5f, 0f, 0f);
        physicsWorldContext.Update(1f / 60f);
        root.Update(1f / 60f);

        Assert.Equal(new Vector3(5f, 0f, 0f), root.LocalTransform.Position);

        component.DisablePhysics();
    }

    [Fact]
    public void DynamicComponent_WritesBackToTheEntityTransform()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: false);
        var component = new CollisionComponent();
        component.PhysicsDefinition.PhysicsType = PhysicsType.Dynamic;
        component.PhysicsDefinition.Mass = 1f;
        component.Fixtures.Add(new ColliderFixture(new Box()));

        var root = CreateEntityWithComponent(physicsWorldContext, component, GameplayExecutionPolicies.Runtime);
        var body = Assert.Single(component.Bodies);

        body.LinearVelocity = new Vector3(0f, -10f, 0f);
        physicsWorldContext.Update(1f / 30f);
        root.Update(1f / 30f);

        Assert.True(root.LocalTransform.Position.Y < -0.01f);

        component.DisablePhysics();
    }

    [Fact]
    public void BoundingBox_IsTheUnionOfEveryFixture()
    {
        var component = new CollisionComponent();
        component.Fixtures.Add(new ColliderFixture(new Box()) { LocalPosition = new Vector3(-2f, 0f, 0f) });
        component.Fixtures.Add(new ColliderFixture(new Box()) { LocalPosition = new Vector3(2f, 0f, 0f) });

        var boundingBox = component.GetBoundingBox();

        Assert.Equal(-2.5f, boundingBox.Min.X, precision: 5);
        Assert.Equal(2.5f, boundingBox.Max.X, precision: 5);
        Assert.Equal(-0.5f, boundingBox.Min.Y, precision: 5);
        Assert.Equal(0.5f, boundingBox.Max.Y, precision: 5);
    }

    [Fact]
    public void ShapeSweep_ReportsTheTagOfTheFixtureItHit()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);
        var component = new CollisionComponent();
        component.PhysicsDefinition.PhysicsType = PhysicsType.Static;
        component.Fixtures.Add(new ColliderFixture(new Box()) { Tag = "hull" });

        CreateEntityWithComponent(physicsWorldContext, component);

        using var sweepShape = physicsWorldContext.CreateQueryShape(new Sphere { Radius = 0.25f }, Vector3.One);
        bool hit = physicsWorldContext.ShapeSweep(
            sweepShape,
            Matrix.CreateTranslation(-3f, 0f, 0f),
            Matrix.CreateTranslation(3f, 0f, 0f),
            out HitResult result);

        Assert.True(hit);
        Assert.Equal("hull", result.Tag);

        component.DisablePhysics();
    }

    /// <summary>
    /// The vendored BulletSharp does not fill LocalShapeInfo for compound children during a convex sweep,
    /// so a query hitting a compound body cannot name the fixture it touched.
    /// </summary>
    [Fact]
    public void ShapeSweep_ReportsNoTag_WhenItHitsACompoundBody()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);
        var component = new CollisionComponent();
        component.PhysicsDefinition.PhysicsType = PhysicsType.Static;
        component.Fixtures.Add(new ColliderFixture(new Box()) { LocalPosition = new Vector3(-4f, 0f, 0f), Tag = "left" });
        component.Fixtures.Add(new ColliderFixture(new Box()) { LocalPosition = new Vector3(4f, 0f, 0f), Tag = "right" });

        CreateEntityWithComponent(physicsWorldContext, component);

        using var sweepShape = physicsWorldContext.CreateQueryShape(new Sphere { Radius = 0.25f }, Vector3.One);
        bool hit = physicsWorldContext.ShapeSweep(
            sweepShape,
            Matrix.CreateTranslation(8f, 0f, 0f),
            Matrix.CreateTranslation(0f, 0f, 0f),
            out HitResult result);

        Assert.True(hit);
        Assert.Equal(4.5f, result.Point.X, precision: 3);
        Assert.Null(result.Tag);

        component.DisablePhysics();
    }

    [Fact]
    public void Contacts_CarryTheFixtureTagOfEachSide()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);

        var componentA = new CollisionComponent();
        componentA.PhysicsDefinition.PhysicsType = PhysicsType.Kinetic;
        componentA.Fixtures.Add(new ColliderFixture(new Box()) { Tag = "bodyA" });
        CreateEntityWithComponent(physicsWorldContext, componentA);

        var componentB = new CollisionComponent();
        componentB.PhysicsDefinition.PhysicsType = PhysicsType.Kinetic;
        componentB.Fixtures.Add(new ColliderFixture(new Box()) { Tag = "bodyB" });
        CreateEntityWithComponent(physicsWorldContext, componentB, position: new Vector3(0.5f, 0f, 0f));

        physicsWorldContext.Update(1f / 60f);

        var collision = Assert.Single(componentA.Collisions);
        var contactPoints = physicsWorldContext.GetContactPoints(collision);
        Assert.NotEmpty(contactPoints);

        foreach (var contactPoint in contactPoints)
        {
            Assert.Contains(contactPoint.FixtureTagA, new[] { "bodyA", "bodyB" });
            Assert.Contains(contactPoint.FixtureTagB, new[] { "bodyA", "bodyB" });
            Assert.NotEqual(contactPoint.FixtureTagA, contactPoint.FixtureTagB);
        }

        componentA.DisablePhysics();
        componentB.DisablePhysics();
    }

    [Fact]
    public void Contacts_NameTheCompoundChildTheyTouched()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);

        var compoundComponent = new CollisionComponent();
        compoundComponent.PhysicsDefinition.PhysicsType = PhysicsType.Kinetic;
        compoundComponent.Fixtures.Add(new ColliderFixture(new Box()) { LocalPosition = new Vector3(-4f, 0f, 0f), Tag = "left" });
        compoundComponent.Fixtures.Add(new ColliderFixture(new Box()) { LocalPosition = new Vector3(4f, 0f, 0f), Tag = "right" });
        CreateEntityWithComponent(physicsWorldContext, compoundComponent);

        var probeComponent = new CollisionComponent();
        probeComponent.PhysicsDefinition.PhysicsType = PhysicsType.Kinetic;
        probeComponent.Fixtures.Add(new ColliderFixture(new Box()) { Tag = "probe" });
        CreateEntityWithComponent(physicsWorldContext, probeComponent, position: new Vector3(4.5f, 0f, 0f));

        physicsWorldContext.Update(1f / 60f);

        var collision = Assert.Single(compoundComponent.Collisions);
        var contactPoints = physicsWorldContext.GetContactPoints(collision);
        Assert.NotEmpty(contactPoints);

        foreach (var contactPoint in contactPoints)
        {
            Assert.Equal("right", contactPoint.FixtureTagA);
            Assert.Equal("probe", contactPoint.FixtureTagB);
        }

        compoundComponent.DisablePhysics();
        probeComponent.DisablePhysics();
    }

    [Fact]
    public void SteeringCollisionRadius_ReadsSphereThenCapsuleFixtures()
    {
        var sphereComponent = new CollisionComponent();
        sphereComponent.Fixtures.Add(new ColliderFixture(new Box()));
        sphereComponent.Fixtures.Add(new ColliderFixture(new Sphere { Radius = 1f }));

        Assert.True(SteeringAgentComponent.TryGetCollisionRadius(sphereComponent, out float sphereRadius));
        Assert.Equal(1f, sphereRadius);

        var capsuleComponent = new CollisionComponent();
        capsuleComponent.Fixtures.Add(new ColliderFixture(new Capsule { Radius = 0.75f }));

        Assert.True(SteeringAgentComponent.TryGetCollisionRadius(capsuleComponent, out float capsuleRadius));
        Assert.Equal(0.75f, capsuleRadius);

        var boxComponent = new CollisionComponent();
        boxComponent.Fixtures.Add(new ColliderFixture(new Box()));

        Assert.False(SteeringAgentComponent.TryGetCollisionRadius(boxComponent, out float boxRadius));
        Assert.Equal(0f, boxRadius);

        Assert.False(SteeringAgentComponent.TryGetCollisionRadius(null, out _));
    }

    private static void LoadFixtures(CollisionComponent component, JObject node)
    {
        foreach (var fixtureNode in (JArray)node["fixtures"]!)
        {
            var fixture = new ColliderFixture();
            fixture.Load((JObject)fixtureNode);
            component.Fixtures.Add(fixture);
        }
    }

    private static SceneComponent CreateEntityWithComponent(
        IPhysicsWorld physicsWorldContext,
        CollisionComponent component,
        GameplayExecutionPolicy? executionPolicy = null,
        Vector3 position = default)
    {
        var world = new CasaEngine.Framework.Scene.World.World();
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        game.ExecutionPolicy = executionPolicy ?? GameplayExecutionPolicies.EditorPreview;
        SetProperty(world, nameof(CasaEngine.Framework.Scene.World.World.Game), game);
        SetProperty(world, nameof(CasaEngine.Framework.Scene.World.World.PhysicsWorld), physicsWorldContext);

        var entity = new Entity();
        SetProperty(entity, nameof(Entity.World), world);

        var root = new TestSceneComponent();
        entity.RootComponent = root;
        root.LocalTransform.Position = position;
        root.AddChildComponent(component);

        component.InitializeWithWorld(world);
        return root;
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
