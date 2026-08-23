using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Physics;

/// <summary>
/// Covers the root cause behind the RPGDemo "hit breakable grass" AccessViolationException: a
/// <c>GameplayProxy.OnHit</c> callback fired from <see cref="BepuPhysicsEngine.SendEvents"/> removes and
/// disposes a collidable that is still referenced by the current step's contact buffer (the ghost's own
/// hitbox is disabled after it registers its hit - a common one-shot-hitbox pattern - and the grass tile it
/// hit is torn down through <c>TileMapComponent.RemoveCollisionObject</c>'s exact sequence:
/// <c>ClearCollisionDataFrom</c>, then <c>RemoveRigidBody</c>/<c>RemoveCollisionObject</c>, then
/// <c>Dispose</c>). Bepu always reports a body-vs-static pair with the body as <c>CollidablePair.A</c>, and
/// the debug renderer/<see cref="BepuPhysicsEngine.LatestContactPointsFor"/> used to re-read that side's pose
/// straight out of <c>Simulation.Bodies</c>/<c>Simulation.Statics</c> after the step - confirmed to
/// AccessViolationException once the body handle is gone (reproduced against the pre-fix code while writing
/// this test: <c>BodyReference.get_Pose()</c> -> <c>BepuPhysicsEngine.GetPosition</c> ->
/// <c>BepuPhysicsDebugRenderer.DrawContacts</c>, killing the test host). <see cref="ContactRecord"/> is now a
/// self-contained snapshot taken at manifold-report time, so these reads never touch the simulation again.
/// </summary>
public class BepuContactRecordLifetimeTests
{
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

    private sealed class CountingDebugDrawer : IPhysicsDebugDrawer
    {
        public PhysicsDebugDrawModes DebugMode { get; set; }
        public List<Vector3> ContactPoints { get; } = new();

        public void Draw3dText(ref Vector3 location, string textString)
        {
        }

        public void DrawContactPoint(ref Vector3 pointOnB, ref Vector3 normalOnB, float distance, int lifeTime, Color color)
        {
            ContactPoints.Add(pointOnB);
        }

        public void DrawLine(ref Vector3 from, ref Vector3 to, Color color)
        {
        }

        public void ReportErrorWarning(string warningString)
        {
        }
    }

    /// <summary>
    /// Stands in for ScriptPlayerWeapon.HitWithGrass: the ghost's hitbox is a one-shot volume that, on its
    /// first hit, tears down the grass tile it hit through <c>TileMapComponent.RemoveCollisionObject</c>'s
    /// exact sequence, and disables itself (a one-shot hitbox never registers a second hit within the same
    /// swing) through the same sequence. Bepu reports a body-vs-static pair with the body - the ghost - as
    /// <c>CollidablePair.A</c>, so removing the ghost is what exercises the position-read hazard the fix
    /// closes; removing the tile matches the bug report's literal trigger.
    /// </summary>
    private sealed class SelfConsumingHitboxProxy : GameplayProxy
    {
        public PhysicsWorld PhysicsWorld;
        public ICollideableComponent TileComponent;
        public PhysicsBody TileBody;
        public ICollideableComponent GhostComponent;
        public PhysicsBody GhostBody;
        public bool AlsoAddReplacementStaticFarAway;

        public bool Hit;
        public Collision LastCollision;
        public List<ContactPoint> CapturedContactPoints;
        public PhysicsBody ReplacementBody;

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
            bool involvesTile = ReferenceEquals(collision.ColliderA, TileComponent) || ReferenceEquals(collision.ColliderB, TileComponent);
            if (!involvesTile || Hit)
            {
                return;
            }

            Hit = true;
            LastCollision = collision;

            // Read the contact info before tearing anything down, the way real gameplay code would to
            // decide the hit response (VFX placement, damage direction, ...).
            CapturedContactPoints = PhysicsWorld.GetContactPoints(collision).ToList();

            // Tear down the grass tile: TileMapComponent.RemoveCollisionObject's exact sequence.
            PhysicsWorld.ClearCollisionDataFrom(TileComponent);
            if (TileBody.IsRigidBody)
            {
                PhysicsWorld.RemoveRigidBody(TileBody);
            }
            else
            {
                PhysicsWorld.RemoveCollisionObject(TileBody);
            }
            TileBody.Dispose();

            if (AlsoAddReplacementStaticFarAway)
            {
                var farAwayMatrix = Matrix.CreateTranslation(FarAwayPosition);
                var replacementDefinition = new PhysicsDefinition { PhysicsType = PhysicsType.Static };
                var replacementComponent = new FakeCollideableComponent(new Entity());
                ReplacementBody = PhysicsWorld.AddStaticObject(new Box(), Vector3.One, ref farAwayMatrix, replacementComponent, replacementDefinition, CollisionProfileIds.WorldStatic);
            }

            // Disable the one-shot hitbox itself, the same sequence applied to its own (ghost) body.
            PhysicsWorld.ClearCollisionDataFrom(GhostComponent);
            PhysicsWorld.RemoveCollisionObject(GhostBody);
            GhostBody.Dispose();
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

    private static readonly Vector3 FarAwayPosition = new(1000f, 1000f, 1000f);

    private sealed class Fixture
    {
        public PhysicsWorld PhysicsWorld;
        public SelfConsumingHitboxProxy Proxy;
    }

    /// <summary>Overlapping (static sensor tile) + (kinematic hitbox ghost) pair, wired so the ghost's
    /// entity carries the <see cref="SelfConsumingHitboxProxy"/> and gets <c>OnHit</c> from
    /// <see cref="BepuPhysicsEngine.SendEvents"/>.</summary>
    private static Fixture BuildOverlappingTileAndGhost(bool alsoAddReplacement)
    {
        var physicsWorld = new PhysicsWorld(useExternalViewManagement: true);

        var tileEntity = new Entity();
        var tileComponent = new FakeCollideableComponent(tileEntity) { PhysicsType = PhysicsType.Static };
        var staticMatrix = Matrix.Identity;
        var staticDefinition = new PhysicsDefinition { PhysicsType = PhysicsType.Static };
        var tileBody = physicsWorld.AddStaticObject(new Box(), Vector3.One, ref staticMatrix, tileComponent, staticDefinition, CollisionProfileIds.Trigger);

        var proxy = new SelfConsumingHitboxProxy
        {
            PhysicsWorld = physicsWorld,
            TileComponent = tileComponent,
            TileBody = tileBody,
            AlsoAddReplacementStaticFarAway = alsoAddReplacement
        };

        var ghostEntity = new Entity();
        SetGameplayProxy(ghostEntity, proxy);
        proxy.Initialize(ghostEntity);

        var ghostComponent = new FakeCollideableComponent(ghostEntity) { PhysicsType = PhysicsType.Kinetic };
        var ghostMatrix = Matrix.CreateTranslation(0.4f, 0f, 0f);
        var ghostBody = physicsWorld.AddGhostObject(new Box(), Vector3.One, ref ghostMatrix, ghostComponent, CollisionProfileIds.Pawn);

        proxy.GhostComponent = ghostComponent;
        proxy.GhostBody = ghostBody;

        return new Fixture { PhysicsWorld = physicsWorld, Proxy = proxy };
    }

    private static void SetGameplayProxy(Entity entity, IGameplayProxy proxy)
    {
        var property = typeof(Entity).GetProperty(nameof(Entity.GameplayProxy), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(entity, proxy);
    }

    private static bool IsFinite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

    [Fact]
    public void RemovingTheOtherBodyDuringOnHit_KeepsContactPointsAndDebugDrawSafe()
    {
        var fixture = BuildOverlappingTileAndGhost(alsoAddReplacement: false);

        var exception = Record.Exception(() => fixture.PhysicsWorld.Update(1f / 60f));
        Assert.Null(exception);
        Assert.True(fixture.Proxy.Hit, "OnHit should have fired for the overlapping ghost/tile pair.");

        // Contact info captured from within OnHit, before either body was torn down: it must already be a
        // real, finite point near the overlap, not garbage read off a half-removed body.
        Assert.NotEmpty(fixture.Proxy.CapturedContactPoints);
        foreach (var point in fixture.Proxy.CapturedContactPoints)
        {
            Assert.True(IsFinite(point.PositionOnA), $"PositionOnA {point.PositionOnA} is not finite.");
            Assert.True(point.PositionOnA.Length() < 5f, $"PositionOnA {point.PositionOnA} is not near the overlap.");
        }

        // The debug renderer walks the raw (now stale) contact buffer after both the tile and the ghost
        // that hit it are gone: this used to AccessViolationException reading the removed body's pose.
        var drawer = new CountingDebugDrawer { DebugMode = PhysicsDebugDrawModes.DrawContactPoints };
        var drawException = Record.Exception(() => fixture.PhysicsWorld.DrawDebugWorld(drawer));
        Assert.Null(drawException);

        foreach (var point in drawer.ContactPoints)
        {
            Assert.True(IsFinite(point), $"Debug-drawn contact point {point} is not finite.");
        }

        // Querying contact points for the now-ended collision must stay safe too (it simply reports none).
        var afterRemoval = Record.Exception(() => fixture.PhysicsWorld.GetContactPoints(fixture.Proxy.LastCollision));
        Assert.Null(afterRemoval);
    }

    [Fact]
    public void HandleReusedInTheSameFrame_DoesNotMisattributeContacts()
    {
        var fixture = BuildOverlappingTileAndGhost(alsoAddReplacement: true);

        var exception = Record.Exception(() => fixture.PhysicsWorld.Update(1f / 60f));
        Assert.Null(exception);
        Assert.True(fixture.Proxy.Hit);
        Assert.NotNull(fixture.Proxy.ReplacementBody);

        // Captured before the removal/replacement: still the original components, positioned at the
        // overlap, never the far-away replacement.
        Assert.NotEmpty(fixture.Proxy.CapturedContactPoints);
        foreach (var point in fixture.Proxy.CapturedContactPoints)
        {
            Assert.True(IsFinite(point.PositionOnA));
            Assert.True(point.PositionOnA.Length() < 5f);
            Assert.NotEqual(FarAwayPosition, point.PositionOnA);
        }

        var drawer = new CountingDebugDrawer { DebugMode = PhysicsDebugDrawModes.DrawContactPoints };
        var drawException = Record.Exception(() => fixture.PhysicsWorld.DrawDebugWorld(drawer));
        Assert.Null(drawException);

        foreach (var point in drawer.ContactPoints)
        {
            Assert.True(IsFinite(point), $"Debug-drawn contact point {point} is not finite.");
            // The stale record's snapshot position must stay near the original overlap: it must never
            // jump to the replacement static's position just because it reused the same Bepu handle.
            Assert.True(Vector3.Distance(point, FarAwayPosition) > 100f, $"Contact point {point} was drawn at the replacement static's position.");
        }

        var afterRemoval = Record.Exception(() => fixture.PhysicsWorld.GetContactPoints(fixture.Proxy.LastCollision));
        Assert.Null(afterRemoval);
    }
}
