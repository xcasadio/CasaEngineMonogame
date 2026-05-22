using System.ComponentModel;
using BulletSharp;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Physics;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

[DisplayName("Character controller")]
public class CharacterControllerComponent : EntityComponent
{
    private const int MaxSweepIterations = 3;
    private const float MinMoveDistanceSquared = 0.000001f;
    private const float MinSweepShapeSize = 0.001f;

    private CharacterControllerSettings _settings = new();
    private Vector2 _moveIntent;
    private bool _jumpRequested;
    private CharacterControllerGroundInfo _groundInfo = CharacterControllerGroundInfo.None;
    private HitResult _lastCollisionHit;
    private Vector3 _lastRequestedDisplacement;
    private Vector3 _lastActualDisplacement;
    private CapsuleShape? _sweepShape;
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
        _groundInfo = other._groundInfo;
        _lastCollisionHit = other._lastCollisionHit;
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

    public bool IsGrounded => _groundInfo.IsGrounded;

    public Vector3 GroundNormal => _groundInfo.Normal;

    public PhysicsBaseComponent? GroundCollider => _groundInfo.Collider;

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
        GroundSlopeAngle,
        LastCollisionHit,
        LastRequestedDisplacement,
        LastActualDisplacement);

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

        var velocity = Velocity;
        ApplyHorizontalVelocity(ref velocity, elapsedTime);
        ApplyVerticalVelocity(ref velocity, elapsedTime);

        var requestedDisplacement = velocity * elapsedTime;
        var actualDisplacement = MoveWithCollisions(rootComponent, requestedDisplacement);
    _lastRequestedDisplacement = requestedDisplacement;
    _lastActualDisplacement = actualDisplacement;

        if (elapsedTime > 0f)
        {
            velocity = actualDisplacement / elapsedTime;
        }

        UpdateGround(rootComponent, ref velocity);

        Velocity = velocity;
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
        _lastRequestedDisplacement = Vector3.Zero;
        _lastActualDisplacement = Vector3.Zero;
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

    private Vector3 MoveWithCollisions(SceneComponent rootComponent, Vector3 requestedDisplacement)
    {
        _lastCollisionHit = default;

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

    private void UpdateGround(SceneComponent rootComponent, ref Vector3 velocity)
    {
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

    private bool TryResolveCollisionDependencies(out IPhysicsWorldContext? physicsWorldContext, out CapsuleCollisionComponent? capsuleCollisionComponent)
    {
        physicsWorldContext = null;
        capsuleCollisionComponent = null;

        var owner = Owner;
        if (owner?.World?.PhysicsWorldContext == null)
        {
            return false;
        }

        capsuleCollisionComponent = owner.GetComponent<CapsuleCollisionComponent>();
        if (capsuleCollisionComponent == null)
        {
            return false;
        }

        physicsWorldContext = owner.World.PhysicsWorldContext;
        return true;
    }

    private CapsuleShape GetSweepShape()
    {
        var radius = Math.Max(MinSweepShapeSize, _settings.Radius - _settings.SkinWidth);
        var cylinderHeight = Math.Max(MinSweepShapeSize, _settings.Height - _settings.Radius * 2f);

        if (_sweepShape == null || radius != _sweepShapeRadius || cylinderHeight != _sweepShapeCylinderHeight)
        {
            _sweepShape?.Dispose();
            _sweepShape = new CapsuleShape(radius, cylinderHeight);
            _sweepShapeRadius = radius;
            _sweepShapeCylinderHeight = cylinderHeight;
        }

        return _sweepShape;
    }

    private bool Sweep(
        IPhysicsWorldContext physicsWorldContext,
        ConvexShape sweepShape,
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