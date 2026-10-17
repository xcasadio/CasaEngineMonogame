using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.EditorServices;
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
/// Covers the RPGDemo crash where a thrown rock hits the player: a NullReferenceException out of
/// <c>Entity.Destroy</c> -> <c>AnimatedSpriteComponent.OnEnabledValueChange</c>, called from
/// <c>ScriptEnemyWeapon.HitWithPlayer</c> through <see cref="BepuPhysicsEngine.SendEvents"/>.
///
/// The physics engine clears its contact buffer only before a sub-step. With a fixed physics step and a
/// variable frame time, a frame often runs no sub-step at all, and <c>UpdateContacts</c> then re-reads the
/// records of the last step. Those records still name the bodies an <c>OnHit</c> of that step removed: the
/// rock destroyed itself on its hit, its body left the simulation, and the next frame without a step raised
/// the same hit again as a new collision. When the world had discarded the rock in between, its root
/// component was already detached, and the second <c>Destroy</c> dereferenced its null owner.
/// </summary>
public class BepuFrameWithoutStepTests
{
    private static float FixedTimeStep => GameSettings.PhysicsEngineSettings.FixedTimeStep;

    private sealed class FakeCollideableComponent : ICollideableComponent
    {
        public FakeCollideableComponent(Entity owner)
        {
            Owner = owner;
        }

        public Entity Owner { get; }
        public PhysicsType PhysicsType { get; set; }
        public HashSet<Collision> Collisions { get; } = new();
    }

    /// <summary>A one-shot hitbox: on its first hit it takes its own body out of the simulation, the
    /// sequence <c>PhysicsBaseComponent.DestroyPhysicsObject</c> runs when its entity is destroyed.</summary>
    private sealed class SelfRemovingHitboxProxy : GameplayProxy
    {
        public PhysicsWorld PhysicsWorld;
        public ICollideableComponent HitboxComponent;
        public PhysicsBody HitboxBody;
        public int Hits;

        public override void InitializeWithWorld(World world)
        {
        }

        public override void Update(float elapsedTime)
        {
        }

        public override void Draw()
        {
        }

        public override void OnHit(Collision collision)
        {
            if (!ReferenceEquals(collision.ColliderA, HitboxComponent) && !ReferenceEquals(collision.ColliderB, HitboxComponent))
            {
                return;
            }

            Hits++;

            if (HitboxBody != null)
            {
                PhysicsWorld.RemoveCollisionObject(HitboxBody);
                HitboxBody.Dispose();
                HitboxBody = null;
            }

            PhysicsWorld.ClearCollisionDataFrom(HitboxComponent);
        }

        public override void OnHitEnded(Collision collision)
        {
        }

        public override void OnBeginPlay(World world)
        {
        }

        public override void OnEndPlay(World world)
        {
        }

        public override IGameplayProxy Clone() => throw new NotSupportedException();
    }

    private sealed class HitCountingProxy : GameplayProxy
    {
        public int Hits;
        public int HitEnded;

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

        public override void OnHitEnded(Collision collision) => HitEnded++;

        public override void OnBeginPlay(World world)
        {
        }

        public override void OnEndPlay(World world)
        {
        }

        public override IGameplayProxy Clone() => throw new NotSupportedException();
    }

    /// <summary>Stands in for ScriptEnemyWeapon.HitWithPlayer: the weapon destroys its own entity when it
    /// hits something.</summary>
    private sealed class DestroyOnHitProxy : GameplayProxy
    {
        public int Hits;

        public override void InitializeWithWorld(World world)
        {
        }

        public override void Update(float elapsedTime)
        {
        }

        public override void Draw()
        {
        }

        public override void OnHit(Collision collision)
        {
            if (collision.ColliderA.Owner != Owner && collision.ColliderB.Owner != Owner)
            {
                return;
            }

            Hits++;
            Owner.Destroy();
        }

        public override void OnHitEnded(Collision collision)
        {
        }

        public override void OnBeginPlay(World world)
        {
        }

        public override void OnEndPlay(World world)
        {
        }

        public override IGameplayProxy Clone() => throw new NotSupportedException();
    }

    [Fact]
    public void AFrameWithoutAStep_DoesNotRaiseAgainTheHitOfABodyRemovedDuringOnHit_AndKeepsTheOtherCollisions()
    {
        AssertFrameWithoutStepIsReachable();
        using var physicsWorld = new PhysicsWorld(useExternalViewManagement: true);

        // The hitbox overlaps a sensor tile and removes its own body on its first hit.
        var tileComponent = AddStaticTile(physicsWorld, Vector3.Zero);
        var hitboxEntity = new Entity();
        var proxy = new SelfRemovingHitboxProxy { PhysicsWorld = physicsWorld };
        SetGameplayProxy(hitboxEntity, proxy);
        proxy.Initialize(hitboxEntity);
        proxy.HitboxComponent = new FakeCollideableComponent(hitboxEntity) { PhysicsType = PhysicsType.Kinetic };
        var hitboxMatrix = Matrix.CreateTranslation(0.4f, 0f, 0f);
        proxy.HitboxBody = physicsWorld.AddGhostObject(new Box(), Vector3.One, ref hitboxMatrix, proxy.HitboxComponent, CollisionProfileIds.Pawn);

        // An unrelated pair, far away, that keeps touching for the whole test.
        var otherTileComponent = AddStaticTile(physicsWorld, new Vector3(50f, 0f, 0f));
        var otherGhostEntity = new Entity();
        var otherProxy = new HitCountingProxy();
        SetGameplayProxy(otherGhostEntity, otherProxy);
        otherProxy.Initialize(otherGhostEntity);
        var otherGhostComponent = new FakeCollideableComponent(otherGhostEntity) { PhysicsType = PhysicsType.Kinetic };
        var otherGhostMatrix = Matrix.CreateTranslation(50.4f, 0f, 0f);
        physicsWorld.AddGhostObject(new Box(), Vector3.One, ref otherGhostMatrix, otherGhostComponent, CollisionProfileIds.Pawn);

        physicsWorld.Update(FixedTimeStep);

        Assert.Equal(1, proxy.Hits);
        Assert.Null(proxy.HitboxBody);
        Assert.Equal(1, otherProxy.Hits);
        var otherCollision = Assert.Single(otherGhostComponent.Collisions);
        Assert.Contains(otherCollision, otherTileComponent.Collisions);

        // A frame shorter than the fixed step runs no sub-step: the contact buffer still holds the
        // records of the step above, the removed hitbox included.
        physicsWorld.Update(FixedTimeStep * 0.25f);

        Assert.Equal(1, proxy.Hits);
        Assert.Empty(proxy.HitboxComponent.Collisions);
        Assert.Empty(tileComponent.Collisions);

        // The bodies still in the simulation keep their collision through the frame without a step: it
        // neither ends nor starts again.
        Assert.Equal(1, otherProxy.Hits);
        Assert.Equal(0, otherProxy.HitEnded);
        Assert.Equal(otherCollision, Assert.Single(otherGhostComponent.Collisions));
        Assert.Contains(otherCollision, otherTileComponent.Collisions);
    }

    [Fact]
    public void ARockDestroyedOnItsHit_ThenDiscardedByTheWorld_IsNotHitAgainOnAFrameWithoutAStep()
    {
        AssertFrameWithoutStepIsReachable();
        using var physicsWorld = new PhysicsWorld(useExternalViewManagement: false);
        var world = CreateWorld(physicsWorld);

        // weapon_rock of RPGDemo: an animated sprite root with a collision component under it, and a
        // script that destroys the rock when it hits something.
        var rockSprite = new AnimatedSpriteComponent();
        var rock = new Entity { Name = "weapon_rock", RootComponent = rockSprite };
        var rockCollision = CreateKineticBoxCollision();
        rockSprite.AddChildComponent(rockCollision);
        var rockProxy = new DestroyOnHitProxy();
        SetGameplayProxy(rock, rockProxy);
        EditorWorldEditingService.AddEntityReference(world, new EntityReference { AssetId = Guid.Empty, Entity = rock });
        Assert.NotEmpty(rockCollision.Bodies);

        // The player it flies into, kept out of the world's entity list: only the rock goes through the
        // world's removal.
        var player = new Entity { Name = "character_link", RootComponent = new TestSceneComponent() };
        SetProperty(player, nameof(Entity.World), world);
        player.RootComponent.LocalTransform.Position = new Vector3(0.5f, 0f, 0f);
        var playerCollision = CreateKineticBoxCollision();
        player.RootComponent.AddChildComponent(playerCollision);
        playerCollision.InitializeWithWorld(world);
        Assert.NotEmpty(playerCollision.Bodies);

        physicsWorld.Update(FixedTimeStep);

        Assert.Equal(1, rockProxy.Hits);
        Assert.True(rock.ToBeRemoved);
        Assert.Empty(rockCollision.Bodies);

        // The world discards the destroyed rock: its root component is detached.
        world.Update(FixedTimeStep);

        Assert.DoesNotContain(rock, world.Entities);
        Assert.Null(rockSprite.Owner);

        var exception = Record.Exception(() => physicsWorld.Update(FixedTimeStep * 0.25f));

        Assert.Null(exception);
        Assert.Equal(1, rockProxy.Hits);
        Assert.Empty(playerCollision.Collisions);
    }

    /// <summary>The two tests rely on a frame without a sub-step: the physics settings must step at a
    /// fixed rate (MaxSubSteps &gt; 0), as the engine defaults do.</summary>
    private static void AssertFrameWithoutStepIsReachable()
    {
        Assert.True(GameSettings.PhysicsEngineSettings.MaxSubSteps > 0, "The physics engine steps every frame: no frame runs without a sub-step.");
        Assert.True(FixedTimeStep > 0f);
    }

    private static FakeCollideableComponent AddStaticTile(PhysicsWorld physicsWorld, Vector3 position)
    {
        var tileComponent = new FakeCollideableComponent(new Entity()) { PhysicsType = PhysicsType.Static };
        var tileMatrix = Matrix.CreateTranslation(position);
        var tileDefinition = new PhysicsDefinition { PhysicsType = PhysicsType.Static };
        physicsWorld.AddStaticObject(new Box(), Vector3.One, ref tileMatrix, tileComponent, tileDefinition, CollisionProfileIds.Trigger);
        return tileComponent;
    }

    private static CollisionComponent CreateKineticBoxCollision()
    {
        var collision = new CollisionComponent();
        collision.PhysicsDefinition.PhysicsType = PhysicsType.Kinetic;
        collision.Fixtures.Add(new ColliderFixture(new Box()));
        return collision;
    }

    private static World CreateWorld(PhysicsWorld physicsWorld)
    {
        var world = new World { Name = "RockHitWorld" };

        // RuntimeHelpers.GetUninitializedObject skips Game's constructor: Components (backed by _components)
        // would be null, and AnimatedSpriteComponent.InitializeWithWorld looks up a SpriteRendererComponent
        // through it.
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var componentsField = typeof(Microsoft.Xna.Framework.Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!;
        componentsField.SetValue(game, new GameComponentCollection());
        game.ExecutionPolicy = GameplayExecutionPolicies.Runtime;
        SetProperty(world, nameof(World.Game), game);
        SetProperty(world, nameof(World.PhysicsWorld), physicsWorld);
        return world;
    }

    private static void SetGameplayProxy(Entity entity, IGameplayProxy proxy)
    {
        var property = typeof(Entity).GetProperty(nameof(Entity.GameplayProxy), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(entity, proxy);
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
