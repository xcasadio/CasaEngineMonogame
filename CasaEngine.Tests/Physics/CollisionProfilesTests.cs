using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Physics;
using CasaEngine.Engine.Geometry;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Physics;

public class CollisionProfilesTests
{
    private static CollisionProfileTable Profiles => GameSettings.PhysicsEngineSettings.CollisionProfiles;

    [Fact]
    public void ReservedProfiles_ResolveToExpectedMasks()
    {
        var worldStatic = Profiles.GetResolved(CollisionProfileIds.WorldStatic);
        Assert.Equal(1u << CollisionChannels.WorldStatic, worldStatic.GroupBit);
        Assert.Equal(ChannelMask.All & ~(1u << CollisionChannels.WorldStatic), worldStatic.BlockMask);
        Assert.Equal(ChannelMask.None, worldStatic.OverlapMask);
        Assert.Equal(worldStatic.BlockMask, worldStatic.BroadphaseMask);
        Assert.False(worldStatic.IsSensor);

        //Static against static stays filtered out, like the Bullet defaults it replaces.
        Assert.Equal(
            CollisionResponse.Ignore,
            Profiles.GetProfile(CollisionProfileIds.WorldStatic).GetResponse(CollisionChannels.WorldStatic));

        var worldDynamic = Profiles.GetResolved(CollisionProfileIds.WorldDynamic);
        Assert.Equal(1u << CollisionChannels.WorldDynamic, worldDynamic.GroupBit);
        Assert.Equal(ChannelMask.All, worldDynamic.BlockMask);
        Assert.Equal(ChannelMask.None, worldDynamic.OverlapMask);
        Assert.False(worldDynamic.IsSensor);

        var pawn = Profiles.GetResolved(CollisionProfileIds.Pawn);
        Assert.Equal(1u << CollisionChannels.Pawn, pawn.GroupBit);
        Assert.Equal(ChannelMask.All, pawn.BlockMask);
        Assert.Equal(ChannelMask.None, pawn.OverlapMask);
        Assert.False(pawn.IsSensor);

        var trigger = Profiles.GetResolved(CollisionProfileIds.Trigger);
        Assert.Equal(1u << CollisionChannels.Trigger, trigger.GroupBit);
        Assert.Equal(ChannelMask.None, trigger.BlockMask);
        Assert.Equal(ChannelMask.All, trigger.OverlapMask);
        Assert.Equal(ChannelMask.All, trigger.BroadphaseMask);
        Assert.True(trigger.IsSensor);

        //Triggers overlap each other.
        Assert.Equal(
            CollisionResponse.Overlap,
            Profiles.GetProfile(CollisionProfileIds.Trigger).GetResponse(CollisionChannels.Trigger));
    }

    [Fact]
    public void ProfileId_FallsBackOnPhysicsType_WhenDefinitionHasNoProfileName()
    {
        Assert.Equal(CollisionProfileIds.WorldStatic, Profiles.ResolveProfileId(new PhysicsDefinition { PhysicsType = PhysicsType.Static }));
        Assert.Equal(CollisionProfileIds.Pawn, Profiles.ResolveProfileId(new PhysicsDefinition { PhysicsType = PhysicsType.Kinetic }));
        Assert.Equal(CollisionProfileIds.WorldDynamic, Profiles.ResolveProfileId(new PhysicsDefinition { PhysicsType = PhysicsType.Dynamic }));

        var explicitProfile = new PhysicsDefinition { PhysicsType = PhysicsType.Static, ProfileName = CollisionProfileNames.Trigger };
        Assert.Equal(CollisionProfileIds.Trigger, Profiles.ResolveProfileId(explicitProfile));
    }

    [Fact]
    public void ShapeSweep_WithDefaultChannelMask_HitsStaticBody()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);
        var obstacleShape = new Box();
        Matrix obstacleTransform = Matrix.Identity;
        using PhysicsBody staticBody = physicsWorldContext.AddStaticObject(
            obstacleShape,
            Vector3.One,
            ref obstacleTransform,
            new object(),
            new PhysicsDefinition { Mass = 0f, PhysicsType = PhysicsType.Static },
            CollisionProfileIds.WorldStatic);
        using var sweepShape = physicsWorldContext.CreateQueryShape(new Sphere { Radius = 0.25f }, Vector3.One);

        bool hit = physicsWorldContext.ShapeSweep(
            sweepShape,
            Matrix.CreateTranslation(-3f, 0f, 0f),
            Matrix.CreateTranslation(3f, 0f, 0f),
            out HitResult result);

        Assert.True(hit);
        Assert.True(result.Succeeded);
        Assert.Equal(Profiles.GetResolved(CollisionProfileIds.WorldStatic).GroupBit, staticBody.CollisionProfile.GroupBit);
    }

    [Fact]
    public void RigidBody_WithSensorProfile_HasNoContactResponse()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);
        var shape = new Box();
        Matrix transform = Matrix.Identity;

        using PhysicsBody sensorBody = physicsWorldContext.AddStaticObject(
            shape,
            Vector3.One,
            ref transform,
            new object(),
            new PhysicsDefinition { PhysicsType = PhysicsType.Static, ProfileName = CollisionProfileNames.Trigger },
            CollisionProfileIds.Trigger);

        Assert.True(sensorBody.CollisionProfile.IsSensor);
        Assert.False(sensorBody.HasContactResponse);
    }

    [Fact]
    public void KineticComponent_CreatesPawnGhost_ThatDefaultSweepsIgnore()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);
        var world = CreateWorldWithPhysics(physicsWorldContext);

        var entity = new Entity();
        SetProperty(entity, nameof(Entity.World), world);

        var component = new KineticBoxComponent();
        component.PhysicsDefinition.PhysicsType = PhysicsType.Kinetic;
        entity.RootComponent = component;
        component.InitializeWithWorld(world);

        var ghost = component.CollisionObject;
        Assert.NotNull(ghost);

        var pawn = Profiles.GetResolved(CollisionProfileIds.Pawn);
        Assert.Equal(pawn.GroupBit, ghost!.CollisionProfile.GroupBit);
        Assert.Equal(pawn.BroadphaseMask, ghost.CollisionProfile.BroadphaseMask);
        Assert.False(ghost.CollisionProfile.IsSensor);

        //A ghost never answers contacts, whatever its profile blocks.
        Assert.False(ghost.HasContactResponse);

        using var sweepShape = physicsWorldContext.CreateQueryShape(new Sphere { Radius = 0.1f }, Vector3.One);
        bool hit = physicsWorldContext.ShapeSweep(
            sweepShape,
            Matrix.CreateTranslation(-3f, 0f, 0f),
            Matrix.CreateTranslation(3f, 0f, 0f),
            out HitResult _);

        Assert.False(hit);

        component.DisablePhysics();
    }

    [Fact]
    public void PhysicsDefinition_RoundTrips_CollisionProfile()
    {
        var definition = new PhysicsDefinition
        {
            PhysicsType = PhysicsType.Static,
            ProfileName = CollisionProfileNames.Trigger,
        };

        var node = new JObject();
        EditorEntityJsonSerializer.SavePhysicsDefinition(definition, node);

        var loaded = new PhysicsDefinition();
        loaded.Load(node);

        Assert.Equal(CollisionProfileNames.Trigger, loaded.ProfileName);
        Assert.Equal(CollisionProfileIds.Trigger, Profiles.ResolveProfileId(loaded));
    }

    [Fact]
    public void SpriteTriggerVolumes_LandInTriggerChannel_AndOverlapEachOther()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);

        var componentA = new TestCollideableComponent(PhysicsType.Kinetic);
        var componentB = new TestCollideableComponent(PhysicsType.Kinetic);

        using var bodyA = CreateSpriteTriggerVolume(physicsWorldContext, componentA, Matrix.Identity);
        using var bodyB = CreateSpriteTriggerVolume(physicsWorldContext, componentB, Matrix.CreateTranslation(0.5f, 0f, 0f));

        var trigger = Profiles.GetResolved(CollisionProfileIds.Trigger);
        Assert.Equal(trigger.GroupBit, bodyA.CollisionProfile.GroupBit);
        Assert.Equal(trigger.BroadphaseMask, bodyA.CollisionProfile.BroadphaseMask);

        physicsWorldContext.Update(1f / 60f);

        Assert.NotEmpty(componentA.Collisions);
        Assert.NotEmpty(componentB.Collisions);
    }

    [Fact]
    public void TileTriggerVolume_OverlapsPawnBody()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);

        var tileComponent = new TestCollideableComponent(PhysicsType.Kinetic);
        var pawnComponent = new TestCollideableComponent(PhysicsType.Kinetic);

        var tileShape = new Box();
        Matrix tileTransform = Matrix.Identity;
        using var tileBody = physicsWorldContext.AddGhostObject(tileShape, Vector3.One, ref tileTransform, tileComponent, CollisionProfileIds.Trigger);

        var pawnShape = new Box();
        Matrix pawnTransform = Matrix.CreateTranslation(0.5f, 0f, 0f);
        using var pawnBody = physicsWorldContext.AddGhostObject(pawnShape, Vector3.One, ref pawnTransform, pawnComponent, CollisionProfileIds.Pawn);

        Assert.Equal(Profiles.GetResolved(CollisionProfileIds.Trigger).GroupBit, tileBody.CollisionProfile.GroupBit);
        Assert.Equal(Profiles.GetResolved(CollisionProfileIds.Pawn).GroupBit, pawnBody.CollisionProfile.GroupBit);

        physicsWorldContext.Update(1f / 60f);

        Assert.NotEmpty(tileComponent.Collisions);
        Assert.NotEmpty(pawnComponent.Collisions);
    }

    private static PhysicsBody CreateSpriteTriggerVolume(IPhysicsWorld physicsWorldContext, ICollideableComponent component, Matrix worldMatrix)
    {
        var collision2d = new Collision2d { Shape = new ShapeRectangle(1f, 1f) };
        var body = SpriteCollisionHelper.CreateCollisionBody(collision2d, Vector3.One, worldMatrix, physicsWorldContext, component);
        physicsWorldContext.AddCollisionObject(body);
        return body;
    }

    private static CasaEngine.Framework.Scene.World.World CreateWorldWithPhysics(IPhysicsWorld physicsWorldContext)
    {
        var world = new CasaEngine.Framework.Scene.World.World();
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        game.ExecutionPolicy = GameplayExecutionPolicies.EditorPreview;
        SetProperty(world, nameof(CasaEngine.Framework.Scene.World.World.Game), game);
        SetProperty(world, nameof(CasaEngine.Framework.Scene.World.World.PhysicsWorld), physicsWorldContext);
        return world;
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var property = typeof(TTarget).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    private sealed class KineticBoxComponent : CollisionComponent
    {
        public KineticBoxComponent()
        {
            Fixtures.Add(new ColliderFixture(new Box()));
        }

        public PhysicsBody? CollisionObject => _bodies.Count == 0 ? null : _bodies[0];
    }

    private sealed class TestCollideableComponent : ICollideableComponent
    {
        public TestCollideableComponent(PhysicsType physicsType)
        {
            PhysicsType = physicsType;
        }

        public Entity Owner { get; } = new();

        public PhysicsType PhysicsType { get; }

        public HashSet<Collision> Collisions { get; } = [];
    }
}
