using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Physics;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Physics;

public class PhysicsBroadphaseAabbTests
{
    [Fact]
    public void UpdateSingleAabb_RefreshesMovedRigidBodyBroadphaseBounds()
    {
        using var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: true);
        var collisionShape = new Box();

        Matrix worldMatrix = Matrix.Identity;
        var physicsDefinition = new PhysicsDefinition
        {
            Mass = 0f,
            PhysicsType = PhysicsType.Static,
        };

        using PhysicsBody rigidBody = physicsWorldContext.AddStaticObject(
            collisionShape,
            Vector3.One,
            ref worldMatrix,
            new object(),
            physicsDefinition,
            CollisionProfileIds.WorldStatic);

        rigidBody.WorldTransform = Matrix.CreateTranslation(10f, 0f, 0f);
        using var sweepShape = physicsWorldContext.CreateQueryShape(new Sphere { Radius = 0.25f }, Vector3.One);
        Matrix from = Matrix.CreateTranslation(8f, 0f, 0f);
        Matrix to = Matrix.CreateTranslation(12f, 0f, 0f);

        bool hitBeforeRefresh = physicsWorldContext.ShapeSweep(sweepShape, from, to, out _);
        physicsWorldContext.RefreshBodyAabb(rigidBody);
        bool hitAfterRefresh = physicsWorldContext.ShapeSweep(sweepShape, from, to, out HitResult result);

        Assert.False(hitBeforeRefresh);
        Assert.True(hitAfterRefresh);
        Assert.True(result.Succeeded);
    }
}