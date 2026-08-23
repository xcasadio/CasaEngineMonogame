using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Physics;

/// <summary>
/// A component keeps a reference to its physics world and may tear its bodies down after the
/// world itself was released (world reload, game shutdown): that late removal must be a no-op.
/// </summary>
public class PhysicsWorldDisposalTests
{
    [Fact]
    public void DisposingBodiesAfterTheWorld_DoesNotThrow()
    {
        var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: false);
        Matrix worldMatrix = Matrix.Identity;

        var staticBody = physicsWorldContext.AddStaticObject(
            new Box { Size = Vector3.One },
            Vector3.One,
            ref worldMatrix,
            new object(),
            new PhysicsDefinition { PhysicsType = PhysicsType.Static },
            CollisionProfileIds.WorldStatic);

        var dynamicBody = physicsWorldContext.AddRigidBody(
            new Sphere { Radius = 0.5f },
            Vector3.One,
            ref worldMatrix,
            new object(),
            new PhysicsDefinition { PhysicsType = PhysicsType.Dynamic, Mass = 1f },
            CollisionProfileIds.WorldDynamic);

        physicsWorldContext.Dispose();

        physicsWorldContext.RemoveRigidBody(staticBody);
        physicsWorldContext.RemoveRigidBody(dynamicBody);
        staticBody.Dispose();
        dynamicBody.Dispose();
    }

    [Fact]
    public void DisposingAGhostAfterTheWorld_DoesNotThrow()
    {
        var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: false);
        Matrix worldMatrix = Matrix.Identity;
        var component = new CollisionComponent();
        component.PhysicsDefinition.PhysicsType = PhysicsType.Kinetic;

        var ghost = physicsWorldContext.AddGhostObject(
            new Box { Size = Vector3.One },
            Vector3.One,
            ref worldMatrix,
            component,
            CollisionProfileIds.Trigger);

        physicsWorldContext.Dispose();

        physicsWorldContext.RemoveCollisionObject(ghost);
        ghost.Dispose();
    }

    [Fact]
    public void DisposingTheWorldTwice_DoesNotThrow()
    {
        var physicsWorldContext = new PhysicsWorld(useExternalViewManagement: false);
        physicsWorldContext.Dispose();
        physicsWorldContext.Dispose();
    }
}
