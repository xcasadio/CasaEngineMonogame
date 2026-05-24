using BulletSharp;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Physics;

public class PhysicsShapeSweepTests
{
    [Fact]
    public void ShapeSweep_ReturnsClosestHit_WhenConvexShapeReachesStaticBody()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);
        using var obstacleShape = new BoxShape(0.5f, 0.5f, 0.5f);
        Matrix obstacleTransform = Matrix.Identity;
        using RigidBody rigidBody = physicsWorldContext.AddStaticObject(
            obstacleShape,
            Vector3.One,
            ref obstacleTransform,
            new object(),
            CreateStaticPhysicsDefinition());
        using var sweepShape = new SphereShape(0.25f);

        Matrix from = Matrix.CreateTranslation(-3f, 0f, 0f);
        Matrix to = Matrix.CreateTranslation(3f, 0f, 0f);

        bool hit = physicsWorldContext.ShapeSweep(sweepShape, from, to, out HitResult result, filterFlags: CollisionFilterGroups.AllFilter);

        Assert.True(hit);
        Assert.True(result.Succeeded);
        Assert.InRange(result.HitFraction, 0f, 1f);
        Assert.True(result.Normal.X < -0.5f);
    }

    [Fact]
    public void ShapeSweep_ReturnsNoHit_WhenPathMissesStaticBody()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);
        using var obstacleShape = new BoxShape(0.5f, 0.5f, 0.5f);
        Matrix obstacleTransform = Matrix.Identity;
        using RigidBody rigidBody = physicsWorldContext.AddStaticObject(
            obstacleShape,
            Vector3.One,
            ref obstacleTransform,
            new object(),
            CreateStaticPhysicsDefinition());
        using var sweepShape = new SphereShape(0.25f);

        Matrix from = Matrix.CreateTranslation(-3f, 3f, 0f);
        Matrix to = Matrix.CreateTranslation(3f, 3f, 0f);

        bool hit = physicsWorldContext.ShapeSweep(sweepShape, from, to, out HitResult result, filterFlags: CollisionFilterGroups.AllFilter);

        Assert.False(hit);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ShapeSweep_RespectsTriggerFiltering()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);
        using var triggerShape = new BoxShape(0.5f, 0.5f, 0.5f);
        var triggerComponent = new TestCollideableComponent(PhysicsType.Kinetic);
        Matrix triggerTransform = Matrix.Identity;
        physicsWorldContext.AddGhostObject(triggerShape, ref triggerTransform, triggerComponent);
        using var sweepShape = new SphereShape(0.25f);

        Matrix from = Matrix.CreateTranslation(-3f, 0f, 0f);
        Matrix to = Matrix.CreateTranslation(3f, 0f, 0f);

        bool defaultHit = physicsWorldContext.ShapeSweep(sweepShape, from, to, out HitResult defaultResult);
        bool triggerHit = physicsWorldContext.ShapeSweep(sweepShape, from, to, out HitResult triggerResult, hitTriggers: true);

        Assert.False(defaultHit);
        Assert.False(defaultResult.Succeeded);
        Assert.True(triggerHit);
        Assert.True(triggerResult.Succeeded);
    }

    [Fact]
    public void ShapeSweep_IgnoresRequestedComponent()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);
        using var obstacleShape = new BoxShape(0.5f, 0.5f, 0.5f);
        var ignoredComponent = new TestCollideableComponent(PhysicsType.Static);
        Matrix obstacleTransform = Matrix.Identity;
        using RigidBody rigidBody = physicsWorldContext.AddStaticObject(
            obstacleShape,
            Vector3.One,
            ref obstacleTransform,
            ignoredComponent,
            CreateStaticPhysicsDefinition());
        using var sweepShape = new SphereShape(0.25f);

        Matrix from = Matrix.CreateTranslation(-3f, 0f, 0f);
        Matrix to = Matrix.CreateTranslation(3f, 0f, 0f);

        bool hit = physicsWorldContext.ShapeSweep(sweepShape, from, to, out HitResult result, filterFlags: CollisionFilterGroups.AllFilter, ignoredComponent: ignoredComponent);

        Assert.False(hit);
        Assert.False(result.Succeeded);
    }

    private static PhysicsDefinition CreateStaticPhysicsDefinition()
    {
        return new PhysicsDefinition
        {
            Mass = 0f,
            PhysicsType = PhysicsType.Static,
        };
    }

    private sealed class TestCollideableComponent : ICollideableComponent
    {
        public TestCollideableComponent(PhysicsType physicsType)
        {
            PhysicsType = physicsType;
        }

        public Entity? Owner => null;

        public PhysicsType PhysicsType { get; }

        public HashSet<Collision> Collisions { get; } = [];
    }
}