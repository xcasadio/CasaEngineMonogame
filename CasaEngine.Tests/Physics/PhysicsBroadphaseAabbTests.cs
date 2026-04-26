using BulletSharp;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application.Components.Physics;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Physics;

public class PhysicsBroadphaseAabbTests
{
    [Fact]
    public void UpdateSingleAabb_RefreshesMovedRigidBodyBroadphaseBounds()
    {
        using var physicsWorldContext = new PhysicsWorldContext(useExternalViewManagement: true);
        using var collisionShape = new BoxShape(0.5f, 0.5f, 0.5f);

        Matrix worldMatrix = Matrix.Identity;
        var physicsDefinition = new PhysicsDefinition
        {
            Mass = 0f,
            PhysicsType = PhysicsType.Static,
        };

        using RigidBody rigidBody = physicsWorldContext.AddStaticObject(
            collisionShape,
            Vector3.One,
            ref worldMatrix,
            new object(),
            physicsDefinition);

        Assert.InRange(rigidBody.BroadphaseHandle.AabbMin.X, -0.6f, -0.4f);
        Assert.InRange(rigidBody.BroadphaseHandle.AabbMax.X, 0.4f, 0.6f);

        rigidBody.WorldTransform = Matrix.CreateTranslation(10f, 0f, 0f);

        Assert.InRange(rigidBody.BroadphaseHandle.AabbMin.X, -0.6f, -0.4f);
        Assert.InRange(rigidBody.BroadphaseHandle.AabbMax.X, 0.4f, 0.6f);

        physicsWorldContext.PhysicsEngine.World.UpdateSingleAabb(rigidBody);

        Assert.InRange(rigidBody.BroadphaseHandle.AabbMin.X, 9.4f, 9.6f);
        Assert.InRange(rigidBody.BroadphaseHandle.AabbMax.X, 10.4f, 10.6f);
    }
}