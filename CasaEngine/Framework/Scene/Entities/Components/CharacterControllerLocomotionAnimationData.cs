using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Scene.Entities.Components;

public readonly struct CharacterControllerLocomotionAnimationData
{
    private const float MovementEpsilon = 0.0001f;

    private CharacterControllerLocomotionAnimationData(
        CharacterControlMode controlMode,
        CharacterMovementState movementState,
        Vector3 velocity,
        Vector3 groundVelocity,
        Vector2 moveIntent,
        Vector3 moveDirection,
        float horizontalSpeed,
        float normalizedSpeed,
        bool isMoving,
        bool isStopping,
        bool isGrounded,
        bool isJumping,
        bool isFalling)
    {
        ControlMode = controlMode;
        MovementState = movementState;
        Velocity = velocity;
        GroundVelocity = groundVelocity;
        MoveIntent = moveIntent;
        MoveDirection = moveDirection;
        HorizontalSpeed = horizontalSpeed;
        NormalizedSpeed = normalizedSpeed;
        IsMoving = isMoving;
        IsStopping = isStopping;
        IsGrounded = isGrounded;
        IsJumping = isJumping;
        IsFalling = isFalling;
    }

    public static CharacterControllerLocomotionAnimationData Empty { get; } = new(
        CharacterControlMode.Disabled,
        CharacterMovementState.Disabled,
        Vector3.Zero,
        Vector3.Zero,
        Vector2.Zero,
        Vector3.Zero,
        0f,
        0f,
        false,
        false,
        false,
        false,
        false);

    public CharacterControlMode ControlMode { get; }

    public CharacterMovementState MovementState { get; }

    public Vector3 Velocity { get; }

    public Vector3 GroundVelocity { get; }

    public Vector2 MoveIntent { get; }

    public Vector3 MoveDirection { get; }

    public float HorizontalSpeed { get; }

    public float NormalizedSpeed { get; }

    public bool IsMoving { get; }

    public bool IsStopping { get; }

    public bool IsGrounded { get; }

    public bool IsJumping { get; }

    public bool IsFalling { get; }

    
    public static CharacterControllerLocomotionAnimationData From(CharacterControllerComponent controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        return From(controller.DebugSnapshot, controller.Settings.MaxHorizontalSpeed);
    }

    public static CharacterControllerLocomotionAnimationData From(CharacterControllerDebugSnapshot snapshot, float maxHorizontalSpeed)
    {
        var horizontalVelocity = new Vector3(snapshot.Velocity.X, 0f, snapshot.Velocity.Z);
        var horizontalSpeed = horizontalVelocity.Length();
        var moveDirection = ResolveMoveDirection(horizontalVelocity, snapshot.MoveIntent);
        var normalizedSpeed = maxHorizontalSpeed <= MovementEpsilon
            ? 0f
            : Math.Clamp(horizontalSpeed / maxHorizontalSpeed, 0f, 1f);
        var hasMoveIntent = snapshot.MoveIntent.LengthSquared() > MovementEpsilon;
        var isMoving = horizontalSpeed > MovementEpsilon || hasMoveIntent;
        var isStopping = !hasMoveIntent && horizontalSpeed > MovementEpsilon;

        return new CharacterControllerLocomotionAnimationData(
            snapshot.ControlMode,
            snapshot.MovementState,
            snapshot.Velocity,
            snapshot.GroundVelocity,
            snapshot.MoveIntent,
            moveDirection,
            horizontalSpeed,
            normalizedSpeed,
            isMoving,
            isStopping,
            snapshot.IsGrounded,
            snapshot.MovementState == CharacterMovementState.Jumping,
            snapshot.MovementState == CharacterMovementState.Falling);
    }

    private static Vector3 ResolveMoveDirection(Vector3 horizontalVelocity, Vector2 moveIntent)
    {
        if (horizontalVelocity.LengthSquared() > MovementEpsilon)
        {
            horizontalVelocity.Normalize();
            return horizontalVelocity;
        }

        if (moveIntent.LengthSquared() <= MovementEpsilon)
        {
            return Vector3.Zero;
        }

        var direction = new Vector3(moveIntent.X, 0f, -moveIntent.Y);
        direction.Normalize();
        return direction;
    }
}