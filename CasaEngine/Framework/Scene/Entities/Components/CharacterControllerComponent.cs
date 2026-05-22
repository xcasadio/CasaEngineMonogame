using System.ComponentModel;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Physics;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

[DisplayName("Character controller")]
public class CharacterControllerComponent : EntityComponent
{
    private CharacterControllerSettings _settings = new();
    private Vector2 _moveIntent;
    private bool _jumpRequested;
    private CharacterControllerGroundInfo _groundInfo = CharacterControllerGroundInfo.None;
    private HitResult _lastCollisionHit;

    public CharacterControllerComponent()
    {
    }

    public CharacterControllerComponent(CharacterControllerComponent other) : base(other)
    {
        _settings = other._settings.Clone();
        _moveIntent = other._moveIntent;
        _jumpRequested = other._jumpRequested;
        _groundInfo = other._groundInfo;
        _lastCollisionHit = other._lastCollisionHit;
        ControlMode = other.ControlMode;
        MovementState = other.MovementState;
        Velocity = other.Velocity;
    }

    public CharacterControllerSettings Settings
    {
        get => _settings;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            value.Validate();
            _settings = value.Clone();
        }
    }

    public CharacterControlMode ControlMode { get; private set; } = CharacterControlMode.Player;

    public CharacterMovementState MovementState { get; private set; } = CharacterMovementState.Falling;

    public Vector3 Velocity { get; private set; }

    public bool IsGrounded => _groundInfo.IsGrounded;

    public Vector3 GroundNormal => _groundInfo.Normal;

    public PhysicsBaseComponent? GroundCollider => _groundInfo.Collider;

    public HitResult LastCollisionHit => _lastCollisionHit;

    public Vector2 MoveIntent => _moveIntent;

    public event EventHandler? JumpStarted;

    public event EventHandler<CharacterControllerGroundInfo>? Landed;

    public event EventHandler<CharacterControllerGroundInfo>? GroundChanged;

    public override CharacterControllerComponent Clone()
    {
        return new CharacterControllerComponent(this);
    }

    public override void Update(float elapsedTime)
    {
        base.Update(elapsedTime);

        if (elapsedTime <= 0f)
        {
            return;
        }

        if (ControlMode == CharacterControlMode.Disabled)
        {
            MovementState = CharacterMovementState.Disabled;
            return;
        }

        var rootComponent = Owner?.RootComponent;
        if (rootComponent == null)
        {
            return;
        }

        _settings.Validate();

        var velocity = Velocity;
        ApplyHorizontalVelocity(ref velocity, elapsedTime);
        ApplyVerticalVelocity(ref velocity, elapsedTime);

        Velocity = velocity;
        rootComponent.Position += Velocity * elapsedTime;
    }

    public void ValidateDependencies()
    {
        var owner = Owner;
        if (owner == null)
        {
            throw new InvalidOperationException("Character controller must be attached to an entity.");
        }

        if (owner.RootComponent == null)
        {
            throw new InvalidOperationException("Character controller requires an owner root component.");
        }

        if (owner.GetComponent<CapsuleCollisionComponent>() == null)
        {
            throw new InvalidOperationException("Character controller requires a CapsuleCollisionComponent on the owner entity.");
        }

        if (owner.World == null)
        {
            throw new InvalidOperationException("Character controller requires an owner world.");
        }

        if (owner.World.PhysicsWorldContext == null)
        {
            throw new InvalidOperationException("Character controller requires a physics world context.");
        }

        _settings.Validate();
    }

    public void SetMoveIntent(Vector2 direction)
    {
        if (ControlMode == CharacterControlMode.Disabled)
        {
            _moveIntent = Vector2.Zero;
            return;
        }

        if (direction.LengthSquared() > 1f)
        {
            direction.Normalize();
        }

        _moveIntent = direction;
    }

    public void RequestJump()
    {
        if (ControlMode == CharacterControlMode.Disabled)
        {
            return;
        }

        _jumpRequested = true;
    }

    public void Stop()
    {
        _moveIntent = Vector2.Zero;
        _jumpRequested = false;
        Velocity = Vector3.Zero;
    }

    public void Teleport(Vector3 position)
    {
        var rootComponent = ResolveRootComponent();
        rootComponent.Position = position;
        Stop();
        _lastCollisionHit = default;
    }

    public void SetControlMode(CharacterControlMode mode)
    {
        ControlMode = mode;

        if (ControlMode == CharacterControlMode.Disabled)
        {
            Stop();
            MovementState = CharacterMovementState.Disabled;
            return;
        }

        if (MovementState == CharacterMovementState.Disabled)
        {
            MovementState = IsGrounded ? CharacterMovementState.Grounded : CharacterMovementState.Falling;
        }
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        if (element["settings"] is JObject settingsElement)
        {
            Settings.Load(settingsElement);
        }

        if (element.ContainsKey("control_mode"))
        {
            SetControlMode(element["control_mode"]!.GetEnum<CharacterControlMode>());
        }
    }

    protected bool ConsumeJumpRequest()
    {
        var result = _jumpRequested;
        _jumpRequested = false;
        return result;
    }

    protected void SetVelocity(Vector3 velocity)
    {
        Velocity = velocity;
    }

    protected void SetMovementState(CharacterMovementState movementState)
    {
        MovementState = movementState;
    }

    protected void SetLastCollisionHit(HitResult hitResult)
    {
        _lastCollisionHit = hitResult;
    }

    protected void MarkJumpStarted()
    {
        _jumpRequested = false;
        MovementState = CharacterMovementState.Jumping;
        JumpStarted?.Invoke(this, EventArgs.Empty);
    }

    protected void SetGroundInfo(CharacterControllerGroundInfo groundInfo)
    {
        var previousGroundInfo = _groundInfo;
        _groundInfo = groundInfo;

        if (GroundHasChanged(previousGroundInfo, groundInfo))
        {
            GroundChanged?.Invoke(this, groundInfo);
        }

        if (!previousGroundInfo.IsGrounded && groundInfo.IsGrounded)
        {
            MovementState = CharacterMovementState.Grounded;
            Landed?.Invoke(this, groundInfo);
        }
    }

    private void ApplyHorizontalVelocity(ref Vector3 velocity, float elapsedTime)
    {
        var horizontalVelocity = new Vector3(velocity.X, 0f, velocity.Z);
        var desiredHorizontalVelocity = GetDesiredHorizontalVelocity();
        var horizontalAcceleration = _moveIntent == Vector2.Zero ? _settings.Deceleration : _settings.Acceleration;

        horizontalVelocity = MoveTowards(horizontalVelocity, desiredHorizontalVelocity, horizontalAcceleration * elapsedTime);

        velocity.X = horizontalVelocity.X;
        velocity.Z = horizontalVelocity.Z;
    }

    private void ApplyVerticalVelocity(ref Vector3 velocity, float elapsedTime)
    {
        if (_jumpRequested)
        {
            if (IsGrounded)
            {
                velocity.Y = _settings.JumpSpeed;
                SetGroundInfo(CharacterControllerGroundInfo.None);
                MarkJumpStarted();
            }

            _jumpRequested = false;
        }

        if (IsGrounded)
        {
            if (velocity.Y < 0f)
            {
                velocity.Y = 0f;
            }

            MovementState = CharacterMovementState.Grounded;
            return;
        }

        velocity.Y -= _settings.Gravity * elapsedTime;

        if (MovementState != CharacterMovementState.Jumping || velocity.Y <= 0f)
        {
            MovementState = CharacterMovementState.Falling;
        }
    }

    private Vector3 GetDesiredHorizontalVelocity()
    {
        return new Vector3(_moveIntent.X, 0f, -_moveIntent.Y) * _settings.MaxHorizontalSpeed;
    }

    private static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDelta)
    {
        var difference = target - current;
        var distanceSquared = difference.LengthSquared();

        if (distanceSquared == 0f || maxDelta <= 0f)
        {
            return current;
        }

        var maxDeltaSquared = maxDelta * maxDelta;
        if (distanceSquared <= maxDeltaSquared)
        {
            return target;
        }

        difference.Normalize();
        return current + difference * maxDelta;
    }

    private SceneComponent ResolveRootComponent()
    {
        var owner = Owner;
        if (owner == null)
        {
            throw new InvalidOperationException("Character controller must be attached to an entity.");
        }

        if (owner.RootComponent == null)
        {
            throw new InvalidOperationException("Character controller requires an owner root component.");
        }

        return owner.RootComponent;
    }

    private static bool GroundHasChanged(CharacterControllerGroundInfo previousGroundInfo, CharacterControllerGroundInfo groundInfo)
    {
        return previousGroundInfo.IsGrounded != groundInfo.IsGrounded
               || !ReferenceEquals(previousGroundInfo.Collider, groundInfo.Collider)
               || previousGroundInfo.Normal != groundInfo.Normal;
    }
}