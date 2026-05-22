using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Physics;

public class CharacterControllerAnimationBridgeTests
{
    [Fact]
    public void From_MapsHorizontalVelocityToLocomotionData()
    {
        var snapshot = CreateSnapshot(
            CharacterMovementState.Grounded,
            new Vector3(3f, 0f, 4f),
            Vector2.Zero,
            isGrounded: true);

        var data = CharacterControllerLocomotionAnimationData.From(snapshot, maxHorizontalSpeed: 5f);

        Assert.Equal(5f, data.HorizontalSpeed, precision: 5);
        Assert.Equal(1f, data.NormalizedSpeed, precision: 5);
        Assert.Equal(new Vector3(0.6f, 0f, 0.8f), data.MoveDirection);
        Assert.True(data.IsMoving);
        Assert.True(data.IsStopping);
        Assert.True(data.IsGrounded);
    }

    [Fact]
    public void From_UsesMoveIntentWhenVelocityIsZero()
    {
        var snapshot = CreateSnapshot(
            CharacterMovementState.Grounded,
            Vector3.Zero,
            new Vector2(0f, 1f),
            isGrounded: true);

        var data = CharacterControllerLocomotionAnimationData.From(snapshot, maxHorizontalSpeed: 5f);

        Assert.Equal(0f, data.HorizontalSpeed, precision: 5);
        Assert.Equal(0f, data.NormalizedSpeed, precision: 5);
        Assert.Equal(new Vector3(0f, 0f, -1f), data.MoveDirection);
        Assert.True(data.IsMoving);
        Assert.False(data.IsStopping);
    }

    [Fact]
    public void From_MapsJumpAndGroundVelocity()
    {
        var snapshot = CreateSnapshot(
            CharacterMovementState.Jumping,
            new Vector3(0f, 3f, 0f),
            Vector2.Zero,
            isGrounded: false,
            groundVelocity: new Vector3(2f, 0f, 0f));

        var data = CharacterControllerLocomotionAnimationData.From(snapshot, maxHorizontalSpeed: 5f);

        Assert.True(data.IsJumping);
        Assert.False(data.IsFalling);
        Assert.False(data.IsGrounded);
        Assert.Equal(new Vector3(2f, 0f, 0f), data.GroundVelocity);
    }

    [Fact]
    public void BridgeComponent_UpdatePublishesControllerLocomotionData()
    {
        var entity = new Entity();
        var controller = new CharacterControllerComponent();
        var bridge = new CharacterControllerAnimationBridgeComponent();
        entity.AddComponent(controller);
        entity.AddComponent(bridge);
        controller.SetMoveIntent(new Vector2(1f, 0f));

        bridge.Update(0.1f);

        Assert.Same(controller, bridge.Controller);
        Assert.True(bridge.LocomotionData.IsMoving);
        Assert.Equal(Vector3.Right, bridge.LocomotionData.MoveDirection);
    }

    private static CharacterControllerDebugSnapshot CreateSnapshot(
        CharacterMovementState movementState,
        Vector3 velocity,
        Vector2 moveIntent,
        bool isGrounded,
        Vector3? groundVelocity = null)
    {
        return new CharacterControllerDebugSnapshot(
            CharacterControlMode.Player,
            movementState,
            velocity,
            moveIntent,
            isGrounded,
            Vector3.Up,
            null,
            groundVelocity ?? Vector3.Zero,
            0f,
            default(HitResult),
            Vector3.Zero,
            Vector3.Zero);
    }
}