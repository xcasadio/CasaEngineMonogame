using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Physics;

public sealed class CharacterControllerRagdollBridgeTests
{
    [Fact]
    public void EnterRagdoll_DisablesControllerAndTransfersVelocityToBodies()
    {
        Entity entity = CreateEntity(out CharacterControllerComponent controller, out BoxCollisionComponent body, out CharacterControllerRagdollBridgeComponent bridge);
        controller.SetControlMode(CharacterControlMode.AI);
        controller.RestoreStateSnapshot(controller.CaptureStateSnapshot() with { Velocity = new Vector3(3f, 0f, 4f) });
        bridge.RegisterRagdollBody(body);

        bridge.EnterRagdoll();

        Assert.True(bridge.IsRagdollActive);
        Assert.Equal(CharacterControlMode.Disabled, controller.ControlMode);
        Assert.Equal(CharacterMovementState.Disabled, controller.MovementState);
        Assert.True(body.SimulatePhysics);
        Assert.Equal(new Vector3(3f, 0f, 4f), body.Velocity);
        Assert.Same(entity, bridge.Owner);
    }

    [Fact]
    public void ExitRagdoll_RestoresControlAndCopiesReferenceBodyTransform()
    {
        Entity entity = CreateEntity(out CharacterControllerComponent controller, out BoxCollisionComponent body, out CharacterControllerRagdollBridgeComponent bridge);
        entity.RootComponent!.Position = new Vector3(1f, 0f, 2f);
        controller.SetControlMode(CharacterControlMode.AI);
        controller.RestoreStateSnapshot(controller.CaptureStateSnapshot() with { Velocity = new Vector3(2f, 0f, 0f) });
        bridge.RegisterRagdollBody(body);
        bridge.EnterRagdoll();
        body.Position = new Vector3(5f, 0f, 6f);
        body.Orientation = Quaternion.CreateFromAxisAngle(Vector3.Up, 0.25f);

        bridge.ExitRagdoll();

        Assert.False(bridge.IsRagdollActive);
        Assert.Equal(CharacterControlMode.AI, controller.ControlMode);
        Assert.Equal(new Vector3(5f, 0f, 6f), entity.RootComponent.Position);
        Assert.Equal(body.Orientation, entity.RootComponent.Orientation);
        Assert.Equal(new Vector3(2f, 0f, 0f), controller.Velocity);
    }

    [Fact]
    public void ExitRagdoll_CanRestoreVelocityFromReferenceBody()
    {
        Entity entity = CreateEntity(out CharacterControllerComponent controller, out BoxCollisionComponent body, out CharacterControllerRagdollBridgeComponent bridge);
        bridge.RestoreVelocityFromReferenceBodyOnExit = true;
        bridge.RegisterRagdollBody(body);
        bridge.EnterRagdoll();
        body.Velocity = new Vector3(0f, 1f, 0f);

        bridge.ExitRagdoll();

        Assert.Equal(new Vector3(0f, 1f, 0f), controller.Velocity);
        Assert.Same(entity, bridge.Owner);
    }

    [Fact]
    public void RegisterRagdollBody_IgnoresDuplicates()
    {
        CreateEntity(out _, out BoxCollisionComponent body, out CharacterControllerRagdollBridgeComponent bridge);

        bridge.RegisterRagdollBody(body);
        bridge.RegisterRagdollBody(body);

        Assert.Single(bridge.RagdollBodies);
    }

    private static Entity CreateEntity(
        out CharacterControllerComponent controller,
        out BoxCollisionComponent body,
        out CharacterControllerRagdollBridgeComponent bridge)
    {
        var entity = new Entity
        {
            RootComponent = new TestSceneComponent(),
        };

        controller = new CharacterControllerComponent();
        body = new BoxCollisionComponent();
        body.PhysicsDefinition.PhysicsType = PhysicsType.Kinetic;
        bridge = new CharacterControllerRagdollBridgeComponent();

        entity.AddComponent(controller);
        entity.AddComponent(body);
        entity.AddComponent(bridge);
        return entity;
    }

    private sealed class TestSceneComponent : SceneComponent
    {
        public override EntityComponent Clone()
        {
            return new TestSceneComponent();
        }
    }
}