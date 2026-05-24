using System.ComponentModel;
using CasaEngine.Core.Serialization;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Physics;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

[DisplayName("Character controller")]
public class CharacterControllerComponent : EntityComponent, IEntityPolicyDefaultsProvider, IWorldSystemDrivenComponent
{
    private const int MaxSweepIterations = 3;
    private const float MinMoveDistanceSquared = 0.000001f;
    private const float MinSweepShapeSize = 0.001f;

    private CharacterControllerSettings _settings = new();
    private Vector2 _moveIntent;
    private bool _jumpRequested;
    private float _jumpBufferRemainingSeconds;
    private float _coyoteTimeRemainingSeconds;
    private bool _dashRequested;
    private Vector2 _dashRequestedDirection;
    private Vector2 _dashDirection;
    private float _dashRemainingSeconds;
    private float _dashCooldownRemainingSeconds;
    private CharacterControllerGroundInfo _groundInfo = CharacterControllerGroundInfo.None;
    private HitResult _lastCollisionHit;
    private HitResult _stepSupportHit;
    private bool _hasStepSupportHit;
    private Vector3 _lastRequestedDisplacement;
    private Vector3 _lastActualDisplacement;
    private PhysicsShape? _sweepShape;
    private float _sweepShapeRadius;
    private float _sweepShapeCylinderHeight;

    public CharacterControllerComponent()
    {
    }

    public CharacterControllerComponent(CharacterControllerComponent other) : base(other)
    {
        _settings = other._settings.Clone();
        _moveIntent = other._moveIntent;
        _jumpRequested = other._jumpRequested;
        _jumpBufferRemainingSeconds = other._jumpBufferRemainingSeconds;
        _coyoteTimeRemainingSeconds = other._coyoteTimeRemainingSeconds;
        _dashRequested = other._dashRequested;
        _dashRequestedDirection = other._dashRequestedDirection;
        _dashDirection = other._dashDirection;
        _dashRemainingSeconds = other._dashRemainingSeconds;
        _dashCooldownRemainingSeconds = other._dashCooldownRemainingSeconds;
        _groundInfo = other._groundInfo;
        _lastCollisionHit = other._lastCollisionHit;
        _stepSupportHit = other._stepSupportHit;
        _hasStepSupportHit = other._hasStepSupportHit;
        _lastRequestedDisplacement = other._lastRequestedDisplacement;
        _lastActualDisplacement = other._lastActualDisplacement;
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

    public float JumpBufferRemainingSeconds => _jumpBufferRemainingSeconds;

    public float CoyoteTimeRemainingSeconds => _coyoteTimeRemainingSeconds;

    public bool IsDashing => _dashRemainingSeconds > 0f;

    public float DashRemainingSeconds => _dashRemainingSeconds;

    public float DashCooldownRemainingSeconds => _dashCooldownRemainingSeconds;

    public bool IsGrounded => _groundInfo.IsGrounded;

    public Vector3 GroundNormal => _groundInfo.Normal;

    public PhysicsBaseComponent? GroundCollider => _groundInfo.Collider;

    public Vector3 GroundVelocity => _groundInfo.Collider?.Velocity ?? Vector3.Zero;

    public float GroundSlopeAngle => _groundInfo.SlopeAngle;

    public HitResult LastCollisionHit => _lastCollisionHit;

    public Vector3 LastRequestedDisplacement => _lastRequestedDisplacement;

    public Vector3 LastActualDisplacement => _lastActualDisplacement;

    public Vector2 MoveIntent => _moveIntent;

    public CharacterControllerDebugSnapshot DebugSnapshot => new(
        ControlMode,
        MovementState,
        Velocity,
        MoveIntent,
        IsGrounded,
        GroundNormal,
        GroundCollider,
        GroundVelocity,
        GroundSlopeAngle,
        LastCollisionHit,
        LastRequestedDisplacement,
        LastActualDisplacement);

    public void ApplyEntityPolicyDefaults(Entity owner, ref EntityPolicyDefaultsBuilder defaults)
    {
        defaults.Apply(EntityPolicySet.DynamicTransformAnimated);
    }

    public event EventHandler? JumpStarted;

    public event EventHandler<CharacterControllerGroundInfo>? Landed;

    public event EventHandler<CharacterControllerGroundInfo>? GroundChanged;

    public override CharacterControllerComponent Clone()
    {
        return new CharacterControllerComponent(this);
    }

    public override void Detach()
    {
        _sweepShape?.Dispose();
        _sweepShape = null;
        base.Detach();
    }

    public override void Update(float elapsedTime)
    {
        base.Update(elapsedTime);

        if (elapsedTime <= 0f)
        {
            _lastRequestedDisplacement = Vector3.Zero;
            _lastActualDisplacement = Vector3.Zero;
            return;
        }

        if (ControlMode == CharacterControlMode.Disabled)
        {
            MovementState = CharacterMovementState.Disabled;
            _lastRequestedDisplacement = Vector3.Zero;
            _lastActualDisplacement = Vector3.Zero;
            return;
        }

        var rootComponent = Owner?.RootComponent;
        if (rootComponent == null)
        {
            _lastRequestedDisplacement = Vector3.Zero;
            _lastActualDisplacement = Vector3.Zero;
            return;
        }

        _settings.Validate();
        UpdateTimedState(elapsedTime);
        TryStartDash();

        var velocity = Velocity;
        var inheritedGroundDisplacement = GetGroundDisplacement(elapsedTime);
        if (IsDashing)
        {
            ApplyDashVelocity(ref velocity, elapsedTime);
        }
        else
        {
            ApplyHorizontalVelocity(ref velocity, elapsedTime);
        }

        ApplyVerticalVelocity(ref velocity, elapsedTime);

        var actualGroundDisplacement = Vector3.Zero;
        if (inheritedGroundDisplacement.LengthSquared() > MinMoveDistanceSquared)
        {
            actualGroundDisplacement = MoveWithCollisions(rootComponent, inheritedGroundDisplacement);
        }

        var requestedDisplacement = velocity * elapsedTime;
        bool appliedRequestedDisplacement = requestedDisplacement.LengthSquared() > MinMoveDistanceSquared;
        var actualDisplacement = appliedRequestedDisplacement
            ? MoveWithCollisions(rootComponent, requestedDisplacement)
            : Vector3.Zero;
        _lastRequestedDisplacement = inheritedGroundDisplacement + requestedDisplacement;
        _lastActualDisplacement = actualGroundDisplacement + actualDisplacement;

        if (elapsedTime > 0f && appliedRequestedDisplacement)
        {
            velocity = actualDisplacement / elapsedTime;
        }

        UpdateGround(rootComponent, ref velocity);

        Velocity = velocity;
        UpdatePostMovementTimers(elapsedTime);
    }

    private Vector3 GetGroundDisplacement(float elapsedTime)
    {
        if (elapsedTime <= 0f || !IsGrounded || _jumpRequested)
        {
            return Vector3.Zero;
        }

        return GroundVelocity * elapsedTime;
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

        if (owner.World.PhysicsWorld == null)
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
        _jumpBufferRemainingSeconds = Math.Max(0f, _settings.JumpBufferSeconds);
    }

    public bool RequestDash(Vector2 direction)
    {
        if (ControlMode == CharacterControlMode.Disabled
            || _settings.DashSpeed <= 0f
            || _settings.DashDurationSeconds <= 0f
            || IsDashing
            || _dashCooldownRemainingSeconds > 0f)
        {
            return false;
        }

        if (direction.LengthSquared() <= 0f)
        {
            direction = _moveIntent;
        }

        if (direction.LengthSquared() <= 0f)
        {
            direction = new Vector2(Velocity.X, -Velocity.Z);
        }

        if (direction.LengthSquared() <= 0f)
        {
            return false;
        }

        if (direction.LengthSquared() > 1f)
        {
            direction.Normalize();
        }

        _dashRequested = true;
        _dashRequestedDirection = direction;
        return true;
    }

    public void Stop()
    {
        _moveIntent = Vector2.Zero;
        _jumpRequested = false;
        _jumpBufferRemainingSeconds = 0f;
        _coyoteTimeRemainingSeconds = 0f;
        _dashRequested = false;
        _dashRequestedDirection = Vector2.Zero;
        _dashDirection = Vector2.Zero;
        _dashRemainingSeconds = 0f;
        _dashCooldownRemainingSeconds = 0f;
        Velocity = Vector3.Zero;
        _lastRequestedDisplacement = Vector3.Zero;
        _lastActualDisplacement = Vector3.Zero;
    }

    public Vector3 Move(Vector3 requestedDisplacement)
    {
        if (ControlMode == CharacterControlMode.Disabled || requestedDisplacement.LengthSquared() <= MinMoveDistanceSquared)
        {
            _lastRequestedDisplacement = requestedDisplacement;
            _lastActualDisplacement = Vector3.Zero;
            return Vector3.Zero;
        }

        var rootComponent = Owner?.RootComponent;
        if (rootComponent == null)
        {
            _lastRequestedDisplacement = requestedDisplacement;
            _lastActualDisplacement = Vector3.Zero;
            return Vector3.Zero;
        }

        _settings.Validate();
        var actualDisplacement = MoveWithCollisions(rootComponent, requestedDisplacement);
        _lastRequestedDisplacement = requestedDisplacement;
        _lastActualDisplacement = actualDisplacement;
        return actualDisplacement;
    }

    public CharacterControllerInputSnapshot CaptureInputSnapshot()
    {
        return new CharacterControllerInputSnapshot(
            _moveIntent,
            _jumpRequested,
            _dashRequested,
            _dashRequestedDirection);
    }

    public void ApplyInputSnapshot(CharacterControllerInputSnapshot inputSnapshot)
    {
        SetMoveIntent(inputSnapshot.MoveIntent);

        if (inputSnapshot.JumpRequested)
        {
            RequestJump();
        }

        if (inputSnapshot.DashRequested)
        {
            RequestDash(inputSnapshot.DashDirection);
        }
    }

    public CharacterControllerStateSnapshot CaptureStateSnapshot()
    {
        var rootComponent = Owner?.RootComponent;

        return new CharacterControllerStateSnapshot(
            rootComponent?.Position ?? Vector3.Zero,
            rootComponent?.Orientation ?? Quaternion.Identity,
            ControlMode,
            MovementState,
            Velocity,
            _moveIntent,
            _jumpRequested,
            _jumpBufferRemainingSeconds,
            _coyoteTimeRemainingSeconds,
            _dashRequested,
            _dashRequestedDirection,
            _dashDirection,
            _dashRemainingSeconds,
            _dashCooldownRemainingSeconds,
            IsGrounded,
            GroundNormal,
            GroundSlopeAngle,
            _lastRequestedDisplacement,
            _lastActualDisplacement);
    }

    public void RestoreStateSnapshot(CharacterControllerStateSnapshot stateSnapshot)
    {
        var rootComponent = Owner?.RootComponent;
        if (rootComponent != null)
        {
            rootComponent.Position = stateSnapshot.Position;
            rootComponent.Orientation = stateSnapshot.Orientation;
        }

        ControlMode = stateSnapshot.ControlMode;
        MovementState = stateSnapshot.MovementState;
        Velocity = stateSnapshot.Velocity;
        _moveIntent = NormalizeDirection(stateSnapshot.MoveIntent);
        _jumpRequested = stateSnapshot.JumpRequested;
        _jumpBufferRemainingSeconds = Math.Max(0f, stateSnapshot.JumpBufferRemainingSeconds);
        _coyoteTimeRemainingSeconds = Math.Max(0f, stateSnapshot.CoyoteTimeRemainingSeconds);
        _dashRequested = stateSnapshot.DashRequested;
        _dashRequestedDirection = NormalizeDirection(stateSnapshot.DashRequestedDirection);
        _dashDirection = NormalizeDirection(stateSnapshot.DashDirection);
        _dashRemainingSeconds = Math.Max(0f, stateSnapshot.DashRemainingSeconds);
        _dashCooldownRemainingSeconds = Math.Max(0f, stateSnapshot.DashCooldownRemainingSeconds);
        _groundInfo = stateSnapshot.IsGrounded
            ? new CharacterControllerGroundInfo(true, NormalizeGroundNormal(stateSnapshot.GroundNormal), null, Math.Max(0f, stateSnapshot.GroundSlopeAngle))
            : CharacterControllerGroundInfo.None;
        _lastCollisionHit = default;
        _hasStepSupportHit = false;
        _stepSupportHit = default;
        _lastRequestedDisplacement = stateSnapshot.LastRequestedDisplacement;
        _lastActualDisplacement = stateSnapshot.LastActualDisplacement;
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
        _jumpBufferRemainingSeconds = 0f;
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
        _jumpBufferRemainingSeconds = 0f;
        _coyoteTimeRemainingSeconds = 0f;
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

    private void ApplyDashVelocity(ref Vector3 velocity, float elapsedTime)
    {
        var dashTime = Math.Min(elapsedTime, _dashRemainingSeconds);
        var dashScale = elapsedTime > 0f ? dashTime / elapsedTime : 0f;
        var dashVelocity = new Vector3(_dashDirection.X, 0f, -_dashDirection.Y) * _settings.DashSpeed * dashScale;

        velocity.X = dashVelocity.X;
        velocity.Z = dashVelocity.Z;
        _dashRemainingSeconds = Math.Max(0f, _dashRemainingSeconds - elapsedTime);
        if (_dashRemainingSeconds <= 0f)
        {
            _dashDirection = Vector2.Zero;
            _dashCooldownRemainingSeconds = _settings.DashCooldownSeconds;
        }
    }

    private void ApplyVerticalVelocity(ref Vector3 velocity, float elapsedTime)
    {
        if (HasBufferedJump() && CanStartJump())
        {
            velocity.Y = _settings.JumpSpeed;
            SetGroundInfo(CharacterControllerGroundInfo.None);
            MarkJumpStarted();
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

    private void UpdateTimedState(float elapsedTime)
    {
        if (_dashCooldownRemainingSeconds > 0f)
        {
            _dashCooldownRemainingSeconds = Math.Max(0f, _dashCooldownRemainingSeconds - elapsedTime);
        }

        if (IsGrounded)
        {
            _coyoteTimeRemainingSeconds = _settings.CoyoteTimeSeconds;
        }
    }

    private void UpdatePostMovementTimers(float elapsedTime)
    {
        if (!IsGrounded && _coyoteTimeRemainingSeconds > 0f)
        {
            _coyoteTimeRemainingSeconds = Math.Max(0f, _coyoteTimeRemainingSeconds - elapsedTime);
        }

        if (!_jumpRequested)
        {
            return;
        }

        if (_settings.JumpBufferSeconds <= 0f)
        {
            _jumpRequested = false;
            _jumpBufferRemainingSeconds = 0f;
            return;
        }

        _jumpBufferRemainingSeconds = Math.Max(0f, _jumpBufferRemainingSeconds - elapsedTime);
        if (_jumpBufferRemainingSeconds <= 0f)
        {
            _jumpRequested = false;
        }
    }

    private void TryStartDash()
    {
        if (!_dashRequested || IsDashing || _dashCooldownRemainingSeconds > 0f)
        {
            return;
        }

        _dashRequested = false;
        _dashDirection = _dashRequestedDirection;
        _dashRequestedDirection = Vector2.Zero;
        _dashRemainingSeconds = _settings.DashDurationSeconds;
    }

    private bool HasBufferedJump()
    {
        return _jumpRequested && (_settings.JumpBufferSeconds <= 0f || _jumpBufferRemainingSeconds > 0f);
    }

    private bool CanStartJump()
    {
        return IsGrounded || _coyoteTimeRemainingSeconds > 0f;
    }

    private static Vector2 NormalizeDirection(Vector2 direction)
    {
        if (direction.LengthSquared() > 1f)
        {
            direction.Normalize();
        }

        return direction;
    }

    private static Vector3 NormalizeGroundNormal(Vector3 normal)
    {
        if (normal.LengthSquared() <= 0f)
        {
            return Vector3.Up;
        }

        normal.Normalize();
        return normal;
    }

    private Vector3 MoveWithCollisions(SceneComponent rootComponent, Vector3 requestedDisplacement)
    {
        _lastCollisionHit = default;
        _hasStepSupportHit = false;
        _stepSupportHit = default;

        if (requestedDisplacement.LengthSquared() <= MinMoveDistanceSquared
            || !TryResolveCollisionDependencies(out var physicsWorldContext, out var capsuleCollisionComponent))
        {
            rootComponent.Position += requestedDisplacement;
            return requestedDisplacement;
        }

        var startPosition = rootComponent.Position;
        var currentPosition = startPosition;
        var remainingDisplacement = requestedDisplacement;
        var sweepShape = GetSweepShape();

        for (var iteration = 0; iteration < MaxSweepIterations; iteration++)
        {
            if (remainingDisplacement.LengthSquared() <= MinMoveDistanceSquared)
            {
                break;
            }

            var targetPosition = currentPosition + remainingDisplacement;
            if (!Sweep(physicsWorldContext, sweepShape, currentPosition, targetPosition, capsuleCollisionComponent, out var hitResult))
            {
                currentPosition = targetPosition;
                break;
            }

            _lastCollisionHit = hitResult;

            if (TryStepMove(physicsWorldContext, capsuleCollisionComponent, sweepShape, currentPosition, remainingDisplacement, hitResult, out var steppedPosition, out var stepSupportHit))
            {
                currentPosition = steppedPosition;
                _hasStepSupportHit = true;
                _stepSupportHit = stepSupportHit;
                _lastCollisionHit = stepSupportHit;
                break;
            }

            var displacementLength = remainingDisplacement.Length();
            if (displacementLength <= 0f)
            {
                break;
            }

            var moveDirection = remainingDisplacement / displacementLength;
            var allowedDistance = Math.Max(0f, hitResult.HitFraction * displacementLength - _settings.SkinWidth);
            currentPosition += moveDirection * allowedDistance;

            var remainingFraction = MathHelper.Clamp(1f - hitResult.HitFraction, 0f, 1f);
            remainingDisplacement *= remainingFraction;
            Slide(ref remainingDisplacement, hitResult.Normal);
        }

        rootComponent.Position = currentPosition;
        return currentPosition - startPosition;
    }

    private bool TryStepMove(
        IPhysicsWorld physicsWorldContext,
        CapsuleCollisionComponent capsuleCollisionComponent,
        PhysicsShape sweepShape,
        Vector3 currentPosition,
        Vector3 remainingDisplacement,
        HitResult blockingHit,
        out Vector3 steppedPosition,
        out HitResult stepSupportHit)
    {
        steppedPosition = currentPosition;
        stepSupportHit = default;

        if (_settings.StepHeight <= 0f || !IsGrounded)
        {
            return false;
        }

        var horizontalDisplacement = new Vector3(remainingDisplacement.X, 0f, remainingDisplacement.Z);
        if (horizontalDisplacement.LengthSquared() <= MinMoveDistanceSquared)
        {
            return false;
        }

        if (TryGetWalkableGround(blockingHit.Normal, out _))
        {
            return false;
        }

        var raisedPosition = currentPosition + Vector3.Up * _settings.StepHeight;
        if (Sweep(physicsWorldContext, sweepShape, currentPosition, raisedPosition, capsuleCollisionComponent, out _))
        {
            return false;
        }

        var forwardPosition = raisedPosition + horizontalDisplacement;
        if (Sweep(physicsWorldContext, sweepShape, raisedPosition, forwardPosition, capsuleCollisionComponent, out _))
        {
            return false;
        }

        var stepDownDistance = _settings.StepHeight + Math.Max(_settings.GroundSnapDistance, _settings.SkinWidth);
        var downTarget = forwardPosition - Vector3.Up * stepDownDistance;
        if (!Sweep(physicsWorldContext, sweepShape, forwardPosition, downTarget, capsuleCollisionComponent, out stepSupportHit)
            || !TryGetWalkableGround(stepSupportHit.Normal, out _))
        {
            stepSupportHit = default;
            return false;
        }

        var supportDistance = Math.Max(0f, stepSupportHit.HitFraction * stepDownDistance - _settings.SkinWidth);
        steppedPosition = forwardPosition - Vector3.Up * supportDistance;
        return true;
    }

    private void UpdateGround(SceneComponent rootComponent, ref Vector3 velocity)
    {
        if (_hasStepSupportHit && TryGetWalkableGround(_stepSupportHit.Normal, out var stepSlopeAngle))
        {
            _hasStepSupportHit = false;
            _lastCollisionHit = _stepSupportHit;
            velocity.Y = 0f;
            SetGroundInfo(new CharacterControllerGroundInfo(true, _stepSupportHit.Normal, _stepSupportHit.Collider, stepSlopeAngle));
            return;
        }

        _hasStepSupportHit = false;

        if (velocity.Y > 0f)
        {
            SetGroundInfo(CharacterControllerGroundInfo.None);
            return;
        }

        if (_settings.GroundSnapDistance <= 0f
            || !TryResolveCollisionDependencies(out var physicsWorldContext, out var capsuleCollisionComponent))
        {
            return;
        }

        var sweepShape = GetSweepShape();
        var startPosition = rootComponent.Position;
        var targetPosition = startPosition - Vector3.Up * _settings.GroundSnapDistance;

        if (!Sweep(physicsWorldContext, sweepShape, startPosition, targetPosition, capsuleCollisionComponent, out var hitResult)
            || !TryGetWalkableGround(hitResult.Normal, out var slopeAngle))
        {
            SetGroundInfo(CharacterControllerGroundInfo.None);
            if (MovementState != CharacterMovementState.Jumping || velocity.Y <= 0f)
            {
                MovementState = CharacterMovementState.Falling;
            }

            return;
        }

        var snapDistance = Math.Max(0f, hitResult.HitFraction * _settings.GroundSnapDistance - _settings.SkinWidth);
        if (snapDistance > 0f)
        {
            rootComponent.Position -= Vector3.Up * snapDistance;
        }

        if (velocity.Y < 0f)
        {
            velocity.Y = 0f;
        }

        SetGroundInfo(new CharacterControllerGroundInfo(true, hitResult.Normal, hitResult.Collider, slopeAngle));
    }

    private bool TryResolveCollisionDependencies(out IPhysicsWorld? physicsWorldContext, out CapsuleCollisionComponent? capsuleCollisionComponent)
    {
        physicsWorldContext = null;
        capsuleCollisionComponent = null;

        var owner = Owner;
        if (owner?.World?.PhysicsWorld == null)
        {
            return false;
        }

        capsuleCollisionComponent = owner.GetComponent<CapsuleCollisionComponent>();
        if (capsuleCollisionComponent == null)
        {
            return false;
        }

        physicsWorldContext = owner.World.PhysicsWorld;
        return true;
    }

    private PhysicsShape GetSweepShape()
    {
        var radius = Math.Max(MinSweepShapeSize, _settings.Radius - _settings.SkinWidth);
        var cylinderHeight = Math.Max(MinSweepShapeSize, _settings.Height - _settings.Radius * 2f);

        if (_sweepShape == null || radius != _sweepShapeRadius || cylinderHeight != _sweepShapeCylinderHeight)
        {
            _sweepShape?.Dispose();
            _sweepShape = PhysicsShape.CreateCapsule(radius, cylinderHeight);
            _sweepShapeRadius = radius;
            _sweepShapeCylinderHeight = cylinderHeight;
        }

        return _sweepShape;
    }

    private bool Sweep(
        IPhysicsWorld physicsWorldContext,
        PhysicsShape sweepShape,
        Vector3 startPosition,
        Vector3 targetPosition,
        CapsuleCollisionComponent capsuleCollisionComponent,
        out HitResult hitResult)
    {
        var from = Matrix.CreateTranslation(startPosition);
        var to = Matrix.CreateTranslation(targetPosition);
        return physicsWorldContext.ShapeSweep(
            sweepShape,
            from,
            to,
            out hitResult,
            _settings.CollisionGroup,
            _settings.CollisionMask,
            _settings.HitTriggers,
            capsuleCollisionComponent);
    }

    private bool TryGetWalkableGround(Vector3 normal, out float slopeAngle)
    {
        slopeAngle = 90f;

        if (normal == Vector3.Zero)
        {
            return false;
        }

        normal.Normalize();
        var upDot = MathHelper.Clamp(Vector3.Dot(normal, Vector3.Up), -1f, 1f);
        slopeAngle = MathHelper.ToDegrees(MathF.Acos(upDot));
        return slopeAngle <= _settings.MaxSlopeAngle;
    }

    private static void Slide(ref Vector3 displacement, Vector3 normal)
    {
        if (normal == Vector3.Zero)
        {
            displacement = Vector3.Zero;
            return;
        }

        normal.Normalize();
        var intoNormal = Vector3.Dot(displacement, normal);
        if (intoNormal < 0f)
        {
            displacement -= normal * intoNormal;
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