using CasaEngine.Framework.Physics;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Scene.Entities.Components;

public readonly struct CharacterControllerDebugSnapshot
{
    public CharacterControllerDebugSnapshot(
        CharacterControlMode controlMode,
        CharacterMovementState movementState,
        Vector3 velocity,
        Vector2 moveIntent,
        bool isGrounded,
        Vector3 groundNormal,
        PhysicsBaseComponent? groundCollider,
        float groundSlopeAngle,
        HitResult lastCollisionHit,
        Vector3 lastRequestedDisplacement,
        Vector3 lastActualDisplacement)
    {
        ControlMode = controlMode;
        MovementState = movementState;
        Velocity = velocity;
        MoveIntent = moveIntent;
        IsGrounded = isGrounded;
        GroundNormal = groundNormal;
        GroundCollider = groundCollider;
        GroundSlopeAngle = groundSlopeAngle;
        LastCollisionHit = lastCollisionHit;
        LastRequestedDisplacement = lastRequestedDisplacement;
        LastActualDisplacement = lastActualDisplacement;
    }

    public CharacterControlMode ControlMode { get; }

    public CharacterMovementState MovementState { get; }

    public Vector3 Velocity { get; }

    public Vector2 MoveIntent { get; }

    public bool IsGrounded { get; }

    public Vector3 GroundNormal { get; }

    public PhysicsBaseComponent? GroundCollider { get; }

    public float GroundSlopeAngle { get; }

    public HitResult LastCollisionHit { get; }

    public Vector3 LastRequestedDisplacement { get; }

    public Vector3 LastActualDisplacement { get; }
}