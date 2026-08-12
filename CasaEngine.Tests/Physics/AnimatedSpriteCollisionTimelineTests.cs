using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.Textures;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Physics;

public class AnimatedSpriteCollisionTimelineTests
{
    [Fact]
    public void CollisionBodies_ArePooledAcrossLoopCycles()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);

        var animation = new Animation2dData { AnimationType = AnimationType.Loop };
        animation.CollisionKeyframes.Add(BuildKeyframe(0f, new ColliderFixture(new Box()) { Tag = "startup" }));
        animation.CollisionKeyframes.Add(BuildKeyframe(0.5f, new ColliderFixture(new Box()) { Tag = "active" }));
        animation.Events.Add(new AnimationEventAsset(1f, "End"));

        var component = CreateAnimatedSprite(physicsWorldContext, animation, out _);

        var firstCycleBodies = new List<PhysicsBody>();
        var secondCycleBodies = new List<PhysicsBody>();

        //Two full cycles of 1s, sampled every 0.25s.
        for (int step = 0; step < 8; step++)
        {
            component.Update(0.25f);
            var bodies = step < 4 ? firstCycleBodies : secondCycleBodies;
            bodies.Add(Assert.Single(GetActiveCollisionBodies(component)));
        }

        //Both keyframes are visited in each cycle, and the second cycle reuses the very same bodies.
        Assert.Equal(2, firstCycleBodies.Distinct().Count());
        Assert.Equal(firstCycleBodies, secondCycleBodies);
        Assert.Equal(2, CountCachedBodies(component));

        component.Detach();
    }

    [Fact]
    public void CollisionSet_CreatesOneBodyPerCollisionProfile()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);

        var animation = new Animation2dData();
        animation.CollisionKeyframes.Add(BuildKeyframe(0f,
            new ColliderFixture(new Box()) { ProfileName = CollisionProfileNames.AttackVolume, Tag = "sword" },
            new ColliderFixture(new Box()) { ProfileName = CollisionProfileNames.DamageableVolume, Tag = "head" },
            new ColliderFixture(new Box()) { ProfileName = CollisionProfileNames.AttackVolume, Tag = "shield_bash" }));

        var component = CreateAnimatedSprite(physicsWorldContext, animation, out _);

        var bodies = GetActiveCollisionBodies(component);
        Assert.Equal(2, bodies.Count);

        var profiles = GameSettings.PhysicsEngineSettings.CollisionProfiles;
        Assert.Equal(profiles.GetResolved(CollisionProfileIds.AttackVolume).GroupBit, bodies[0].CollisionProfile.GroupBit);
        Assert.Equal(profiles.GetResolved(CollisionProfileIds.DamageableVolume).GroupBit, bodies[1].CollisionProfile.GroupBit);

        //The two attack fixtures share one compound body, the damageable one stands alone.
        Assert.True(bodies[0].IsCompound);
        Assert.Equal(2, bodies[0].FixtureCount);
        Assert.Equal("sword", bodies[0].GetFixtureTag(0));
        Assert.Equal("shield_bash", bodies[0].GetFixtureTag(1));
        Assert.False(bodies[1].IsCompound);

        component.Detach();
    }

    [Fact]
    public void CollisionBodies_StayInTheLogicalSpaceUnderARenderProjection()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true, new TopDownElevationSimulationSpacePolicy());

        var animation = new Animation2dData();
        animation.CollisionKeyframes.Add(BuildKeyframe(0f,
            new ColliderFixture(new Box()) { LocalPosition = new Vector3(0f, 0f, 1.5f), Tag = "hurt" }));

        var logicalPosition = new Vector3(10f, 4f, 2f);
        var component = CreateAnimatedSprite(physicsWorldContext, animation, out var root,
            logicalPosition: logicalPosition, underRenderProjection: true);

        var body = Assert.Single(GetActiveCollisionBodies(component));

        //The body sits on the logical pose of the entity root, fixture pose included.
        Assert.Equal(logicalPosition, body.WorldTransform.Translation);
        Assert.Equal(new Vector3(0f, 0f, 1.5f), body.GetFixtureLocalTransform(0).Translation);
        Assert.Equal(logicalPosition, root.WorldMatrixNoScale.Translation);

        //While the visual under the projection renders somewhere else entirely: (X, -(Y - Z), 0).
        Assert.Equal(new Vector3(10f, -2f, 0f), component.WorldMatrixNoScale.Translation);

        component.Detach();
    }

    [Fact]
    public void Contacts_CarryTheTagOfTheTimelineFixture()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);

        var animation = new Animation2dData();
        animation.CollisionKeyframes.Add(BuildKeyframe(0f,
            new ColliderFixture(new Box()) { ProfileName = CollisionProfileNames.AttackVolume, Tag = "sword" }));

        var component = CreateAnimatedSprite(physicsWorldContext, animation, out _);

        var target = new CollisionComponent();
        target.PhysicsDefinition.PhysicsType = PhysicsType.Kinetic;
        target.Fixtures.Add(new ColliderFixture(new Box()) { Tag = "torso" });
        CreateEntityWithCollisionComponent(physicsWorldContext, target, new Vector3(0.5f, 0f, 0f));

        physicsWorldContext.Update(1f / 60f);

        var collision = Assert.Single(target.Collisions);
        var contactPoints = physicsWorldContext.GetContactPoints(collision);
        Assert.NotEmpty(contactPoints);

        foreach (var contactPoint in contactPoints)
        {
            Assert.Contains(contactPoint.FixtureTagA, new[] { "sword", "torso" });
            Assert.Contains(contactPoint.FixtureTagB, new[] { "sword", "torso" });
            Assert.NotEqual(contactPoint.FixtureTagA, contactPoint.FixtureTagB);
        }

        component.Detach();
        target.DisablePhysics();
    }

    [Fact]
    public void CreatePhysicsForEachFrame_False_ActivatesNoCollisionSet()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);

        var animation = new Animation2dData();
        animation.CollisionKeyframes.Add(BuildKeyframe(0f, new ColliderFixture(new Box())));

        var component = CreateAnimatedSprite(physicsWorldContext, animation, out _, createPhysicsForEachFrame: false);

        Assert.Null(GetActiveCollisionBodiesOrNull(component));
        Assert.Equal(0, CountCachedBodies(component));

        component.Detach();
    }

    [Fact]
    public void UpdateCurrentSprite_TracksTheDrawnSpriteAndDirtiesTheBoundingBox()
    {
        var firstSpriteId = Guid.NewGuid();
        var secondSpriteId = Guid.NewGuid();

        var animation = new Animation2dData();
        animation.Parts.Add(new Animation2dPartData { Id = "body", DefaultVisible = true, DefaultSpriteId = firstSpriteId });
        var spriteTrack = new Animation2dTrackData
        {
            TargetPartId = "body",
            Property = Animation2dTrackProperty.Sprite,
        };
        spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0f, firstSpriteId));
        spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0.5f, secondSpriteId));
        animation.Tracks.Add(spriteTrack);

        var component = new AnimatedSpriteComponent();
        component.AddAnimation(new Animation2d(animation));
        component.SetCurrentAnimation(0, true);

        Assert.Equal(firstSpriteId, GetPrivateField<Guid>(component, "_currentSpriteId"));

        GetSampler(component).Seek(0.5f);
        component.ClearBoundingBoxDirtyRecursive();

        InvokePrivate(component, "UpdateCurrentSprite");

        Assert.Equal(secondSpriteId, GetPrivateField<Guid>(component, "_currentSpriteId"));
        Assert.True(component.IsBoundingBoxDirty);
    }

    /// <summary>
    /// The editor hot-reload path still refreshes the cached sprite of an animation, and does no
    /// collision work: collision volumes come from the timeline now.
    /// </summary>
    [Fact]
    public void ReloadSpriteAsset_RefreshesTheCachedSpriteOfAnOwnedSpriteId()
    {
        var spriteAssetId = Guid.NewGuid();
        var assetContentManager = CreateAssetContentManagerWithOneTexture(out var spriteSheetAssetId);

        var component = new AnimatedSpriteComponent();
        SetPrivateField(component, "_assetContentManager", assetContentManager);
        GetPrivateField<Dictionary<Guid, SpriteData>>(component, "_spriteDataById")
            .Add(spriteAssetId, new SpriteData { SpriteSheetAssetId = spriteSheetAssetId });

        var reloadedSpriteData = new SpriteData { Name = "reloaded", SpriteSheetAssetId = spriteSheetAssetId };

        Assert.False(component.ReloadSpriteAsset(Guid.NewGuid(), reloadedSpriteData));
        Assert.True(component.ReloadSpriteAsset(spriteAssetId, reloadedSpriteData));

        Assert.Same(reloadedSpriteData, GetPrivateField<Dictionary<Guid, SpriteData>>(component, "_spriteDataById")[spriteAssetId]);
        Assert.NotNull(GetPrivateField<Dictionary<Guid, Sprite>>(component, "_spriteById")[spriteAssetId]);
    }

    private static AssetContentManager CreateAssetContentManagerWithOneTexture(out Guid spriteSheetAssetId)
    {
        var assetContentManager = new AssetContentManager();

        var texture2dAssetId = Guid.NewGuid();
        assetContentManager.AddAsset(texture2dAssetId, "texture2d",
            RuntimeHelpers.GetUninitializedObject(typeof(Texture2D)));

        var texture = new CasaEngine.Framework.Assets.Textures.Texture();
        SetPrivateField(texture, "_texture2dAssetId", texture2dAssetId);

        spriteSheetAssetId = Guid.NewGuid();
        assetContentManager.AddAsset(spriteSheetAssetId, "spritesheet", texture);
        return assetContentManager;
    }

    private static Animation2dCollisionKeyframeData BuildKeyframe(float timeSeconds, params ColliderFixture[] fixtures)
    {
        var keyframe = new Animation2dCollisionKeyframeData { TimeSeconds = timeSeconds };
        keyframe.Fixtures.AddRange(fixtures);
        return keyframe;
    }

    private static AnimatedSpriteComponent CreateAnimatedSprite(
        IPhysicsWorld physicsWorldContext,
        Animation2dData animation,
        out SceneComponent root,
        Vector3 logicalPosition = default,
        bool underRenderProjection = false,
        bool createPhysicsForEachFrame = true)
    {
        var world = new CasaEngine.Framework.Scene.World.World();
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        game.ExecutionPolicy = GameplayExecutionPolicies.Runtime;
        SetProperty(world, nameof(CasaEngine.Framework.Scene.World.World.Game), game);
        SetProperty(world, nameof(CasaEngine.Framework.Scene.World.World.PhysicsWorld), physicsWorldContext);

        var entity = new Entity();
        SetProperty(entity, nameof(Entity.World), world);

        var entityRoot = new TestSceneComponent();
        entity.RootComponent = entityRoot;
        entityRoot.LocalTransform.Position = logicalPosition;

        var component = new AnimatedSpriteComponent { CreatePhysicsForEachFrame = createPhysicsForEachFrame };

        if (underRenderProjection)
        {
            var projection = new RenderProjectionComponent();
            entityRoot.AddChildComponent(projection);
            projection.AddChildComponent(component);
            projection.UpdateProjection();
        }
        else
        {
            entityRoot.AddChildComponent(component);
        }

        SetPrivateField(component, "_physicsWorldContext", physicsWorldContext);
        component.AddAnimation(new Animation2d(animation));
        component.SetCurrentAnimation(0, true);

        root = entityRoot;
        return component;
    }

    private static void CreateEntityWithCollisionComponent(IPhysicsWorld physicsWorldContext, CollisionComponent component, Vector3 position)
    {
        var world = new CasaEngine.Framework.Scene.World.World();
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        game.ExecutionPolicy = GameplayExecutionPolicies.EditorPreview;
        SetProperty(world, nameof(CasaEngine.Framework.Scene.World.World.Game), game);
        SetProperty(world, nameof(CasaEngine.Framework.Scene.World.World.PhysicsWorld), physicsWorldContext);

        var entity = new Entity();
        SetProperty(entity, nameof(Entity.World), world);

        var root = new TestSceneComponent();
        entity.RootComponent = root;
        root.LocalTransform.Position = position;
        root.AddChildComponent(component);

        component.InitializeWithWorld(world);
    }

    private static List<PhysicsBody> GetActiveCollisionBodies(AnimatedSpriteComponent component)
    {
        var bodies = GetActiveCollisionBodiesOrNull(component);
        Assert.NotNull(bodies);
        return bodies!;
    }

    private static List<PhysicsBody> GetActiveCollisionBodiesOrNull(AnimatedSpriteComponent component)
    {
        return GetPrivateField<List<PhysicsBody>>(component, "_activeCollisionBodies");
    }

    private static int CountCachedBodies(AnimatedSpriteComponent component)
    {
        var cache = GetPrivateField<Dictionary<Animation2dCompositionSampler, List<PhysicsBody>[]>>(component, "_collisionBodiesBySampler");
        int count = 0;

        foreach (var bodiesByKeyframe in cache.Values)
        {
            foreach (var bodies in bodiesByKeyframe)
            {
                count += bodies?.Count ?? 0;
            }
        }

        return count;
    }

    private static Animation2dCompositionSampler GetSampler(AnimatedSpriteComponent component)
    {
        return GetPrivateField<Animation2dCompositionSampler>(component, "_currentCompositionSampler");
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (T)field!.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(target, null);
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
