using System.ComponentModel;
using CasaEngine.Core.Serialization;
using CasaEngine.Engine.Geometry;
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
    private CharacterControllerContactReport _lastContact;
    private float _externalVerticalDisplacement;
    private PhysicsQueryShape _sweepShape;
    private bool _sweepShapeIsBox;
    private Vector3 _sweepShapeBoxSize;
    private float _sweepShapeRadius;
    private float _sweepShapeCylinderHeight;

    public CharacterControllerComponent()
    {
    }

    /// <summary>Additive constructor for callers assigning a deterministic id (see <see cref="ObjectBase(Guid)"/>).</summary>
    public CharacterControllerComponent(Guid id) : base(id)
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
        _lastContact = other._lastContact;
        // _externalVerticalDisplacement is intentionally NOT copied: the copy constructor resets
        // the latch to 0, consistent with Stop()/RestoreStateSnapshot also clearing it.
        IsVerticalOwnedExternally = other.IsVerticalOwnedExternally;
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

    /// <summary>
    /// When true, the owner (not the controller) is responsible for this entity's vertical motion
    /// for every rendered <see cref="Update"/>: gravity, vertical velocity integration and the
    /// grounded down-clamp in <c>ApplyVerticalVelocity</c> are skipped, the up-axis component is
    /// excluded from the velocity-driven displacement, and ground resolution in
    /// <c>UpdateGround</c> treats a positive <see cref="SetExternalVerticalDisplacement"/> latch as
    /// airborne. Ground resolution for <see cref="IsGrounded"/>, the downward snap, support and
    /// stairs on non-rising ticks are unaffected. Default <c>false</c>: no behavior change for
    /// existing users. Port target: lets a scripted-motion owner (e.g. an Alundra NPC driven by its
    /// own tick-quantized <c>ForceZ</c>) declare vertical displacement without the controller's
    /// per-render-frame gravity fighting a per-logic-tick value.
    /// </summary>
    public bool IsVerticalOwnedExternally { get; set; }

    public CharacterMovementState MovementState { get; private set; } = CharacterMovementState.Falling;

    public Vector3 Velocity { get; private set; }

    public float JumpBufferRemainingSeconds => _jumpBufferRemainingSeconds;

    public float CoyoteTimeRemainingSeconds => _coyoteTimeRemainingSeconds;

    public bool IsDashing => _dashRemainingSeconds > 0f;

    public float DashRemainingSeconds => _dashRemainingSeconds;

    public float DashCooldownRemainingSeconds => _dashCooldownRemainingSeconds;

    public bool IsGrounded => _groundInfo.IsGrounded;

    public Vector3 GroundNormal => _groundInfo.Normal;

    public PhysicsBaseComponent GroundCollider => _groundInfo.Collider;

    public Vector3 GroundVelocity => _groundInfo.Collider?.Velocity ?? Vector3.Zero;

    public float GroundSlopeAngle => _groundInfo.SlopeAngle;

    public HitResult LastCollisionHit => _lastCollisionHit;

    public Vector3 LastRequestedDisplacement => _lastRequestedDisplacement;

    public Vector3 LastActualDisplacement => _lastActualDisplacement;

    /// <summary>
    /// Per-axis contact report of the last collision resolution: requested vs. resolved
    /// displacement (projected on up/h1/h2), which axes were curtailed, whether the sweep path
    /// hit something, and the ground state as of the last <see cref="Update"/>. See
    /// <see cref="CharacterControllerContactReport"/> for the exact freshness and per-path
    /// semantics contract - additive and coexists with <see cref="LastRequestedDisplacement"/>,
    /// <see cref="LastActualDisplacement"/>, <see cref="LastCollisionHit"/> and
    /// <see cref="GroundCollider"/>, which it decomposes rather than replaces.
    /// </summary>
    public CharacterControllerContactReport LastContact => _lastContact;

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

    public event EventHandler JumpStarted;

    public event EventHandler<CharacterControllerGroundInfo> Landed;

    public event EventHandler<CharacterControllerGroundInfo> GroundChanged;

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

        // M2 reset contract: the displacement half of the contact report is zeroed at the ENTRY
        // of every Update, including every early-return path below - the ground half (last
        // resolved by a previous Update) is preserved.
        _lastContact = _lastContact.WithDisplacementReset();

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

        var up = ResolveUp();
        var (h1, h2) = ResolveHorizontalAxes(up);

        _settings.Validate();
        UpdateTimedState(elapsedTime);
        TryStartDash();

        var velocity = Velocity;
        var inheritedGroundDisplacement = GetGroundDisplacement(elapsedTime);
        if (IsDashing)
        {
            ApplyDashVelocity(ref velocity, elapsedTime, up, h1, h2);
        }
        else
        {
            ApplyHorizontalVelocity(ref velocity, elapsedTime, up, h1, h2);
        }

        ApplyVerticalVelocity(ref velocity, elapsedTime, up);

        var footUpBeforeStep = ResolveFootUpCoordinate(rootComponent, up, h1, h2);

        // M2 composition rule: this step can resolve up to two displacements (inherited ground
        // displacement, then velocity-driven displacement) - the contact report's curtailed-axis
        // flags and sweep-hit indicator are the OR of both calls, exactly like
        // LastRequestedDisplacement/LastActualDisplacement publish their SUM rather than only the
        // second call.
        bool h1Curtailed = false;
        bool h2Curtailed = false;
        bool sweepHit = false;

        var actualGroundDisplacement = Vector3.Zero;
        if (inheritedGroundDisplacement.LengthSquared() > MinMoveDistanceSquared)
        {
            actualGroundDisplacement = MoveWithCollisions(rootComponent, inheritedGroundDisplacement, up, h1, h2, out var groundH1Curtailed, out var groundH2Curtailed, out var groundSweepHit);
            h1Curtailed |= groundH1Curtailed;
            h2Curtailed |= groundH2Curtailed;
            sweepHit |= groundSweepHit;
        }

        var requestedDisplacement = velocity * elapsedTime;
        if (IsVerticalOwnedExternally)
        {
            // The up-axis component is excluded so a residual vertical velocity (e.g. after a
            // step's recomputation below, an external SetVerticalVelocity call, or a restored
            // snapshot) can never produce engine-side vertical motion while the owner is external.
            requestedDisplacement -= up * Vector3.Dot(requestedDisplacement, up);
        }

        bool appliedRequestedDisplacement = requestedDisplacement.LengthSquared() > MinMoveDistanceSquared;
        var actualDisplacement = Vector3.Zero;
        if (appliedRequestedDisplacement)
        {
            actualDisplacement = MoveWithCollisions(rootComponent, requestedDisplacement, up, h1, h2, out var velocityH1Curtailed, out var velocityH2Curtailed, out var velocitySweepHit);
            h1Curtailed |= velocityH1Curtailed;
            h2Curtailed |= velocityH2Curtailed;
            sweepHit |= velocitySweepHit;
        }

        _lastRequestedDisplacement = inheritedGroundDisplacement + requestedDisplacement;
        _lastActualDisplacement = actualGroundDisplacement + actualDisplacement;
        _lastContact = _lastContact.WithDisplacement(
            Vector3.Dot(_lastRequestedDisplacement, up), Vector3.Dot(_lastRequestedDisplacement, h1), Vector3.Dot(_lastRequestedDisplacement, h2),
            Vector3.Dot(_lastActualDisplacement, up), Vector3.Dot(_lastActualDisplacement, h1), Vector3.Dot(_lastActualDisplacement, h2),
            h1Curtailed, h2Curtailed, sweepHit);

        if (elapsedTime > 0f && appliedRequestedDisplacement)
        {
            velocity = actualDisplacement / elapsedTime;
        }

        UpdateGround(rootComponent, ref velocity, up, h1, h2, footUpBeforeStep);

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

        var collisionComponent = owner.GetComponent<CollisionComponent>();
        if (collisionComponent == null || !TryFindCharacterFixture(collisionComponent, out _, out _))
        {
            throw new InvalidOperationException("Character controller requires a CollisionComponent with a box or capsule fixture on the owner entity.");
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
        _lastContact = default;
        _externalVerticalDisplacement = 0f;
    }

    public Vector3 Move(Vector3 requestedDisplacement)
    {
        // M2 reset contract: same as Update - the displacement half is zeroed at entry, on every
        // early-return path, keeping the ground half (last resolved by Update, unaffected by
        // Move) unchanged.
        _lastContact = _lastContact.WithDisplacementReset();

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
        var up = ResolveUp();
        var (h1, h2) = ResolveHorizontalAxes(up);
        var actualDisplacement = MoveWithCollisions(rootComponent, requestedDisplacement, up, h1, h2, out var h1Curtailed, out var h2Curtailed, out var sweepHit);
        _lastRequestedDisplacement = requestedDisplacement;
        _lastActualDisplacement = actualDisplacement;
        _lastContact = _lastContact.WithDisplacement(
            Vector3.Dot(requestedDisplacement, up), Vector3.Dot(requestedDisplacement, h1), Vector3.Dot(requestedDisplacement, h2),
            Vector3.Dot(actualDisplacement, up), Vector3.Dot(actualDisplacement, h1), Vector3.Dot(actualDisplacement, h2),
            h1Curtailed, h2Curtailed, sweepHit);
        return actualDisplacement;
    }

    /// <summary>
    /// Replaces the vertical component of the controller's velocity (the projection onto the
    /// resolved up axis, see <see cref="ResolveUp"/>) with <paramref name="velocityAlongUp"/>,
    /// leaving the two horizontal components strictly unchanged. No-op when <see cref="ControlMode"/>
    /// is <see cref="CharacterControlMode.Disabled"/> (same gate as <see cref="Move"/>).
    /// <para>
    /// Additive API: lets gameplay code apply an arbitrary signed vertical impulse (port target:
    /// Alundra opcode 0x1B "Fly") without touching ground-state bookkeeping directly. Liftoff for a
    /// positive impulse still comes from the existing upward-velocity gate at the head of
    /// <c>UpdateGround</c> on the next <see cref="Update"/>, exactly as it does for a jump.
    /// </para>
    /// <para>
    /// While <see cref="IsVerticalOwnedExternally"/> is true, the vertical component set here is
    /// no longer integrated into displacement by <see cref="Update"/>: it is still stored on
    /// <see cref="Velocity"/>, but the up-axis component of the velocity-driven displacement is
    /// excluded, and <c>ApplyVerticalVelocity</c> does not touch it either. Use
    /// <see cref="SetExternalVerticalDisplacement"/> to drive vertical motion instead.
    /// </para>
    /// </summary>
    public void SetVerticalVelocity(float velocityAlongUp)
    {
        if (ControlMode == CharacterControlMode.Disabled)
        {
            return;
        }

        var up = ResolveUp();
        Velocity = Velocity - up * Vector3.Dot(Velocity, up) + up * velocityAlongUp;
    }

    /// <summary>
    /// Declares this tick's vertical displacement along the resolved up axis (see
    /// <see cref="ResolveUp"/>), consumed by <see cref="Update"/> only while
    /// <see cref="IsVerticalOwnedExternally"/> is true. The value is LATCHED: it persists across
    /// every subsequent <see cref="Update"/> call - including render frames that carry no matching
    /// logic tick - until the next call to this method. <see cref="Update"/> never clears it.
    /// <para>
    /// <see cref="Stop"/> (and therefore <see cref="Teleport"/> and <see cref="SetControlMode"/>
    /// with <see cref="CharacterControlMode.Disabled"/>), the copy constructor, and
    /// <see cref="RestoreStateSnapshot"/> reset the latch to 0 - the same places that already wipe
    /// the rest of the motion state.
    /// </para>
    /// <para>
    /// Additive API: port target for Alundra's tick-quantized <c>ForceZ</c> for scripted NPCs,
    /// replacing a symbolic near-zero <c>SetVerticalVelocity</c> signal that only worked around
    /// <c>UpdateGround</c>'s upward-velocity gate.
    /// </para>
    /// </summary>
    public void SetExternalVerticalDisplacement(float displacementAlongUp)
    {
        _externalVerticalDisplacement = displacementAlongUp;
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
            ? new CharacterControllerGroundInfo(true, NormalizeGroundNormal(stateSnapshot.GroundNormal, ResolveUp()), null, Math.Max(0f, stateSnapshot.GroundSlopeAngle))
            : CharacterControllerGroundInfo.None;
        _lastCollisionHit = default;
        _hasStepSupportHit = false;
        _stepSupportHit = default;
        _lastRequestedDisplacement = stateSnapshot.LastRequestedDisplacement;
        _lastActualDisplacement = stateSnapshot.LastActualDisplacement;
        _lastContact = default;
        _externalVerticalDisplacement = 0f;
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

    /// <summary>
    /// Sets the resolved ground state. <paramref name="surfaceTag"/> feeds
    /// <see cref="LastContact"/>'s ground half (<see cref="CharacterControllerContactReport.GroundSurfaceTag"/>);
    /// it defaults to <c>null</c>, which is correct for every call site except the field-path
    /// ground resolution (<see cref="TryUpdateGroundFromField"/>), which passes the tag of the
    /// max-height footprint corner.
    /// </summary>
    protected void SetGroundInfo(CharacterControllerGroundInfo groundInfo, string surfaceTag = null)
    {
        var previousGroundInfo = _groundInfo;
        _groundInfo = groundInfo;
        _lastContact = _lastContact.WithGround(groundInfo.IsGrounded, groundInfo.Normal, surfaceTag, groundInfo.Collider);

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

    /// <summary>
    /// Elevation axis the controller moves along: the owner world's simulation space policy, or
    /// Y-up when the controller has no world (matches the physics backend's native convention).
    /// </summary>
    private Vector3 ResolveUp()
    {
        return Owner?.World?.PhysicsWorld?.SpacePolicy?.Up ?? Vector3.Up;
    }

    /// <summary>
    /// The two horizontal axes for <paramref name="up"/>, in X -&gt; Y -&gt; Z order (skipping
    /// <paramref name="up"/>'s own axis). For Y-up this is exactly (X, Z), the base's previous
    /// fixed-axis behaviour.
    /// </summary>
    private static (Vector3 H1, Vector3 H2) ResolveHorizontalAxes(Vector3 up)
    {
        var upAxis = AxisIndexOf(up);
        Span<int> horizontal = stackalloc int[2];
        var count = 0;
        for (var axis = 0; axis < 3; axis++)
        {
            if (axis != upAxis)
            {
                horizontal[count++] = axis;
            }
        }

        return (AxisVector(horizontal[0]), AxisVector(horizontal[1]));
    }

    private static int AxisIndexOf(Vector3 up)
    {
        if (up == Vector3.UnitX || up == -Vector3.UnitX)
        {
            return 0;
        }

        if (up == Vector3.UnitZ || up == -Vector3.UnitZ)
        {
            return 2;
        }

        // Unknown/non-axis-aligned up: default to Y, matching the base policy and every known
        // simulation space policy that is not explicitly X or Z up.
        return 1;
    }

    private static Vector3 AxisVector(int axis) => axis switch
    {
        0 => Vector3.UnitX,
        2 => Vector3.UnitZ,
        _ => Vector3.UnitY,
    };

    private void ApplyHorizontalVelocity(ref Vector3 velocity, float elapsedTime, Vector3 up, Vector3 h1, Vector3 h2)
    {
        var verticalComponent = Vector3.Dot(velocity, up);
        var horizontalVelocity = velocity - up * verticalComponent;
        var desiredHorizontalVelocity = GetDesiredHorizontalVelocity(h1, h2);
        var horizontalAcceleration = _moveIntent == Vector2.Zero ? _settings.Deceleration : _settings.Acceleration;

        horizontalVelocity = MoveTowards(horizontalVelocity, desiredHorizontalVelocity, horizontalAcceleration * elapsedTime);

        velocity = horizontalVelocity + up * verticalComponent;
    }

    private void ApplyDashVelocity(ref Vector3 velocity, float elapsedTime, Vector3 up, Vector3 h1, Vector3 h2)
    {
        var dashTime = Math.Min(elapsedTime, _dashRemainingSeconds);
        var dashScale = elapsedTime > 0f ? dashTime / elapsedTime : 0f;
        var dashVelocity = (h1 * _dashDirection.X - h2 * _dashDirection.Y) * _settings.DashSpeed * dashScale;
        var verticalComponent = Vector3.Dot(velocity, up);

        velocity = dashVelocity + up * verticalComponent;
        _dashRemainingSeconds = Math.Max(0f, _dashRemainingSeconds - elapsedTime);
        if (_dashRemainingSeconds <= 0f)
        {
            _dashDirection = Vector2.Zero;
            _dashCooldownRemainingSeconds = _settings.DashCooldownSeconds;
        }
    }

    private void ApplyVerticalVelocity(ref Vector3 velocity, float elapsedTime, Vector3 up)
    {
        if (IsVerticalOwnedExternally)
        {
            // No gravity, no vertical velocity integration, no grounded down-clamp: the owner
            // drives vertical motion via SetExternalVerticalDisplacement instead.
            return;
        }

        var vertical = Vector3.Dot(velocity, up);

        if (HasBufferedJump() && CanStartJump())
        {
            vertical = _settings.JumpSpeed;
            velocity = velocity - up * Vector3.Dot(velocity, up) + up * vertical;
            SetGroundInfo(CharacterControllerGroundInfo.None);
            MarkJumpStarted();
        }

        if (IsGrounded)
        {
            if (vertical < 0f)
            {
                velocity -= up * vertical;
            }

            MovementState = CharacterMovementState.Grounded;
            return;
        }

        vertical -= _settings.Gravity * elapsedTime;

        if (_settings.MaxFallSpeed > 0f && vertical < -_settings.MaxFallSpeed)
        {
            vertical = -_settings.MaxFallSpeed;
        }

        velocity = velocity - up * Vector3.Dot(velocity, up) + up * vertical;

        if (MovementState != CharacterMovementState.Jumping || vertical <= 0f)
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

    private static Vector3 NormalizeGroundNormal(Vector3 normal, Vector3 up)
    {
        if (normal.LengthSquared() <= 0f)
        {
            return up;
        }

        normal.Normalize();
        return normal;
    }

    /// <summary>Up-coordinate of the character's foot (bottom of its fixture), or the root's own
    /// up-coordinate when no character fixture can be resolved.</summary>
    private float ResolveFootUpCoordinate(SceneComponent rootComponent, Vector3 up, Vector3 h1, Vector3 h2)
    {
        if (TryResolveCollisionDependencies(out _, out var collisionComponent, out var fixture, out var isCapsule))
        {
            ResolveFootprint(rootComponent, collisionComponent, fixture, isCapsule, up, h1, h2, out var footUpOffset);
            return Vector3.Dot(rootComponent.Position, up) + footUpOffset;
        }

        return Vector3.Dot(rootComponent.Position, up);
    }

    /// <summary>
    /// Horizontal footprint of the character's fixture: a world position whose component along
    /// <c>up</c> is zero (only its h1/h2 components are meaningful — combine with a probe's own
    /// up-coordinate to get a full 3d corner), plus the fixture's half-extents on each horizontal
    /// axis and the fixture kind (box footprint = 4 corners, capsule footprint = 4 radius points).
    /// </summary>
    private readonly struct CharacterFootprint
    {
        public CharacterFootprint(Vector3 footHorizontalCenter, float halfExtentH1, float halfExtentH2, bool isCapsule)
        {
            FootHorizontalCenter = footHorizontalCenter;
            HalfExtentH1 = halfExtentH1;
            HalfExtentH2 = halfExtentH2;
            IsCapsule = isCapsule;
        }

        public Vector3 FootHorizontalCenter { get; }

        public float HalfExtentH1 { get; }

        public float HalfExtentH2 { get; }

        public bool IsCapsule { get; }

        /// <summary>
        /// E3.c-bis: the original defines an entity's collision box as
        /// <c>[Pos + Mod, Pos + Mod + size - 1/65536)</c> (decompiled <c>EntityManager.SetEntityDimensions</c>,
        /// where <c>Width = (size &lt;&lt; 16) - 1</c> in 16.16 fixed point) - the far edge on each axis is
        /// exclusive by exactly one 16.16 fixed-point unit, at ANY magnitude, because fixed-point
        /// spacing is constant. Float32 spacing is not constant: it grows with magnitude (its ULP), so a
        /// fixed subtraction like <c>size - 1/65536f</c> is only exclusive near zero - at Alundra's real
        /// map coordinates (up to roughly 928 px) the float32 ULP is about 6.1e-5, larger than
        /// <c>1/65536f</c> (about 1.53e-5), and <c>928f - 1/65536f</c> rounds straight back to <c>928f</c>:
        /// the far edge silently becomes inclusive again. The faithful float translation of "one
        /// representable unit below the boundary" is one ULP below, via <see cref="MathF.BitDecrement"/> -
        /// and it must be applied to the FINAL far-corner coordinate, i.e. <c>centre + half-extent</c>,
        /// not to the half-extent before the addition: decrementing the half-extent first can be
        /// completely absorbed by the rounding of the later addition (the very same failure mode this
        /// fix exists to avoid), whereas decrementing the already-rounded sum always yields the exact
        /// float below it. This stays exclusive at any magnitude. Near corners (negative half-extent)
        /// are unchanged.
        /// </summary>
        public void GetCorners(Vector3 center, Vector3 h1, Vector3 h2, Span<(float H1, float H2)> corners)
        {
            var centerH1 = Vector3.Dot(center, h1);
            var centerH2 = Vector3.Dot(center, h2);

            var nearH1 = centerH1 - HalfExtentH1;
            var nearH2 = centerH2 - HalfExtentH2;
            var farH1 = MathF.BitDecrement(centerH1 + HalfExtentH1);
            var farH2 = MathF.BitDecrement(centerH2 + HalfExtentH2);

            if (IsCapsule)
            {
                corners[0] = (farH1, centerH2);
                corners[1] = (nearH1, centerH2);
                corners[2] = (centerH1, farH2);
                corners[3] = (centerH1, nearH2);
            }
            else
            {
                corners[0] = (farH1, farH2);
                corners[1] = (farH1, nearH2);
                corners[2] = (nearH1, farH2);
                corners[3] = (nearH1, nearH2);
            }
        }
    }

    /// <summary>
    /// Resolves the character's footprint from its collider fixture, expressed in the root's local
    /// space (fixture local position composed with the collision component's own offset from the
    /// root). Capsule: foot = center - Height/2, footprint = 4 points at Radius. Box: foot = center
    /// - half-extent along up, footprint = the 4 corners of the box (full fixture, not shrunk by
    /// skin width).
    /// </summary>
    private CharacterFootprint ResolveFootprint(
        SceneComponent rootComponent,
        CollisionComponent collisionComponent,
        ColliderFixture fixture,
        bool isCapsule,
        Vector3 up,
        Vector3 h1,
        Vector3 h2,
        out float footUpOffset)
    {
        var fixtureOffsetFromRoot = (collisionComponent.Position - rootComponent.Position) + fixture.LocalPosition;
        var worldFixtureCenter = rootComponent.Position + fixtureOffsetFromRoot;
        var horizontalCenter = worldFixtureCenter - up * Vector3.Dot(worldFixtureCenter, up);

        if (isCapsule)
        {
            var halfHeight = _settings.Height * 0.5f;
            footUpOffset = Vector3.Dot(fixtureOffsetFromRoot, up) - halfHeight;
            return new CharacterFootprint(horizontalCenter, _settings.Radius, _settings.Radius, true);
        }

        var box = (Box)fixture.Shape;
        var halfSize = box.Size * 0.5f;
        var halfExtentUp = Math.Abs(Vector3.Dot(halfSize, up));
        var halfExtentH1 = Math.Abs(Vector3.Dot(halfSize, h1));
        var halfExtentH2 = Math.Abs(Vector3.Dot(halfSize, h2));
        footUpOffset = Vector3.Dot(fixtureOffsetFromRoot, up) - halfExtentUp;
        return new CharacterFootprint(horizontalCenter, halfExtentH1, halfExtentH2, false);
    }

    /// <summary>
    /// Resolves <paramref name="requestedDisplacement"/> against field and sweep collisions.
    /// <paramref name="h1Curtailed"/>/<paramref name="h2Curtailed"/> are authoritative only for
    /// the field-based horizontal resolution (M2) - they are <c>false</c> whenever no field is
    /// installed, i.e. on the pure sweep path, which has no per-axis notion of "blocked" and is
    /// reported instead by <paramref name="sweepHit"/> (whether any sweep iteration hit
    /// something).
    /// </summary>
    private Vector3 MoveWithCollisions(SceneComponent rootComponent, Vector3 requestedDisplacement, Vector3 up, Vector3 h1, Vector3 h2, out bool h1Curtailed, out bool h2Curtailed, out bool sweepHit)
    {
        _lastCollisionHit = default;
        _hasStepSupportHit = false;
        _stepSupportHit = default;
        h1Curtailed = false;
        h2Curtailed = false;
        sweepHit = false;

        if (requestedDisplacement.LengthSquared() <= MinMoveDistanceSquared
            || !TryResolveCollisionDependencies(out var physicsWorldContext, out var collisionComponent, out var fixture, out var isCapsule))
        {
            rootComponent.Position += requestedDisplacement;
            return requestedDisplacement;
        }

        var startPosition = rootComponent.Position;
        var fieldAdjustedDisplacement = ResolveHorizontalDisplacementAgainstField(
            rootComponent, collisionComponent, fixture, isCapsule, requestedDisplacement, up, h1, h2, out h1Curtailed, out h2Curtailed);

        var currentPosition = startPosition;
        var remainingDisplacement = fieldAdjustedDisplacement;
        var sweepShape = GetSweepShape(physicsWorldContext, fixture, isCapsule);

        for (var iteration = 0; iteration < MaxSweepIterations; iteration++)
        {
            if (remainingDisplacement.LengthSquared() <= MinMoveDistanceSquared)
            {
                break;
            }

            var targetPosition = currentPosition + remainingDisplacement;
            if (!Sweep(physicsWorldContext, sweepShape, currentPosition, targetPosition, collisionComponent, out var hitResult))
            {
                currentPosition = targetPosition;
                break;
            }

            _lastCollisionHit = hitResult;
            sweepHit = true;

            if (TryStepMove(physicsWorldContext, collisionComponent, sweepShape, currentPosition, remainingDisplacement, hitResult, up, out var steppedPosition, out var stepSupportHit))
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

    /// <summary>
    /// C5: resolves the horizontal (h1, h2) part of a requested displacement against
    /// <see cref="World.CollisionField"/>, axis by axis (h1 then h2). An axis is blocked (its
    /// displacement set to zero, no partial displacement) when any corner of the footprint at the
    /// candidate position has no ground, non-walkable ground, or ground higher than foot + step
    /// height. Passes the displacement through unchanged when no field is installed.
    /// <paramref name="h1Curtailed"/>/<paramref name="h2Curtailed"/> (M2) report exactly those
    /// blocked-axis decisions, authoritative only here (the field path) - both are <c>false</c>
    /// when no field is installed.
    /// </summary>
    private Vector3 ResolveHorizontalDisplacementAgainstField(
        SceneComponent rootComponent,
        CollisionComponent collisionComponent,
        ColliderFixture fixture,
        bool isCapsule,
        Vector3 requestedDisplacement,
        Vector3 up,
        Vector3 h1,
        Vector3 h2,
        out bool h1Curtailed,
        out bool h2Curtailed)
    {
        h1Curtailed = false;
        h2Curtailed = false;

        var field = Owner?.World?.CollisionField;
        if (field == null)
        {
            return requestedDisplacement;
        }

        var footprint = ResolveFootprint(rootComponent, collisionComponent, fixture, isCapsule, up, h1, h2, out var footUpOffset);
        var footUpCoordinate = Vector3.Dot(rootComponent.Position, up) + footUpOffset;

        var h1Amount = Vector3.Dot(requestedDisplacement, h1);
        var h2Amount = Vector3.Dot(requestedDisplacement, h2);
        var otherComponent = requestedDisplacement - h1 * h1Amount - h2 * h2Amount;

        var footHorizontalCenter = footprint.FootHorizontalCenter;

        if (h1Amount != 0f)
        {
            var candidateCenter = footHorizontalCenter + h1 * h1Amount;
            if (IsHorizontalMoveBlocked(field, footprint, candidateCenter, footUpCoordinate, up, h1, h2))
            {
                h1Amount = 0f;
                h1Curtailed = true;
            }
            else
            {
                footHorizontalCenter = candidateCenter;
            }
        }

        if (h2Amount != 0f)
        {
            var candidateCenter = footHorizontalCenter + h2 * h2Amount;
            if (IsHorizontalMoveBlocked(field, footprint, candidateCenter, footUpCoordinate, up, h1, h2))
            {
                h2Amount = 0f;
                h2Curtailed = true;
            }
        }

        return h1 * h1Amount + h2 * h2Amount + otherComponent;
    }

    private bool IsHorizontalMoveBlocked(
        ICollisionField field,
        in CharacterFootprint footprint,
        Vector3 candidateFootHorizontalCenter,
        float footUpCoordinate,
        Vector3 up,
        Vector3 h1,
        Vector3 h2)
    {
        Span<(float H1, float H2)> corners = stackalloc (float, float)[4];
        footprint.GetCorners(candidateFootHorizontalCenter, h1, h2, corners);

        var probeUpCoordinate = footUpCoordinate + _settings.StepHeight;

        for (var i = 0; i < corners.Length; i++)
        {
            var cornerPosition = h1 * corners[i].H1 + h2 * corners[i].H2 + up * probeUpCoordinate;

            if (!field.TrySampleGround(cornerPosition, float.MaxValue, _settings.WalkabilityMask, out var sample))
            {
                return true;
            }

            if (!sample.IsWalkable)
            {
                return true;
            }

            if (sample.GroundHeight > footUpCoordinate + _settings.StepHeight)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryStepMove(
        IPhysicsWorld physicsWorldContext,
        CollisionComponent collisionComponent,
        PhysicsQueryShape sweepShape,
        Vector3 currentPosition,
        Vector3 remainingDisplacement,
        HitResult blockingHit,
        Vector3 up,
        out Vector3 steppedPosition,
        out HitResult stepSupportHit)
    {
        steppedPosition = currentPosition;
        stepSupportHit = default;

        if (_settings.StepHeight <= 0f || !IsGrounded)
        {
            return false;
        }

        var horizontalDisplacement = remainingDisplacement - up * Vector3.Dot(remainingDisplacement, up);
        if (horizontalDisplacement.LengthSquared() <= MinMoveDistanceSquared)
        {
            return false;
        }

        if (TryGetWalkableGround(blockingHit.Normal, up, out _))
        {
            return false;
        }

        var raisedPosition = currentPosition + up * _settings.StepHeight;
        if (Sweep(physicsWorldContext, sweepShape, currentPosition, raisedPosition, collisionComponent, out _))
        {
            return false;
        }

        var forwardPosition = raisedPosition + horizontalDisplacement;
        if (Sweep(physicsWorldContext, sweepShape, raisedPosition, forwardPosition, collisionComponent, out _))
        {
            return false;
        }

        var stepDownDistance = _settings.StepHeight + Math.Max(_settings.GroundSnapDistance, _settings.SkinWidth);
        var downTarget = forwardPosition - up * stepDownDistance;
        if (!Sweep(physicsWorldContext, sweepShape, forwardPosition, downTarget, collisionComponent, out stepSupportHit)
            || !TryGetWalkableGround(stepSupportHit.Normal, up, out _))
        {
            stepSupportHit = default;
            return false;
        }

        var supportDistance = Math.Max(0f, stepSupportHit.HitFraction * stepDownDistance - _settings.SkinWidth);
        steppedPosition = forwardPosition - up * supportDistance;
        return true;
    }

    private void UpdateGround(SceneComponent rootComponent, ref Vector3 velocity, Vector3 up, Vector3 h1, Vector3 h2, float footUpBeforeStep)
    {
        if (IsVerticalOwnedExternally && _externalVerticalDisplacement > 0f)
        {
            // A positive latched declaration means airborne. Evaluated before the
            // _hasStepSupportHit branch below (which would otherwise re-ground and return before
            // reaching this gate), and _hasStepSupportHit is deliberately left untouched here -
            // MoveWithCollisions already resets it on every call, and the owner relies on that
            // branch to keep following stairs on ticks where it does not declare a rise.
            SetGroundInfo(CharacterControllerGroundInfo.None);
            return;
        }

        if (_hasStepSupportHit && TryGetWalkableGround(_stepSupportHit.Normal, up, out var stepSlopeAngle))
        {
            _hasStepSupportHit = false;
            _lastCollisionHit = _stepSupportHit;
            velocity -= up * Vector3.Dot(velocity, up);
            SetGroundInfo(new CharacterControllerGroundInfo(true, _stepSupportHit.Normal, _stepSupportHit.Collider, stepSlopeAngle));
            return;
        }

        _hasStepSupportHit = false;

        if (Vector3.Dot(velocity, up) > 0f)
        {
            SetGroundInfo(CharacterControllerGroundInfo.None);
            return;
        }

        var field = Owner?.World?.CollisionField;
        if (field != null
            && TryResolveCollisionDependencies(out _, out var fieldCollisionComponent, out var fieldFixture, out var fieldIsCapsule))
        {
            if (TryUpdateGroundFromField(rootComponent, ref velocity, up, h1, h2, field, fieldCollisionComponent, fieldFixture, fieldIsCapsule, footUpBeforeStep))
            {
                return;
            }

            SetGroundInfo(CharacterControllerGroundInfo.None);
            if (MovementState != CharacterMovementState.Jumping || Vector3.Dot(velocity, up) <= 0f)
            {
                MovementState = CharacterMovementState.Falling;
            }

            return;
        }

        if (_settings.GroundSnapDistance <= 0f
            || !TryResolveCollisionDependencies(out var physicsWorldContext, out var collisionComponent, out var fixture, out var isCapsule))
        {
            return;
        }

        var sweepShape = GetSweepShape(physicsWorldContext, fixture, isCapsule);
        var startPosition = rootComponent.Position;
        var targetPosition = startPosition - up * _settings.GroundSnapDistance;

        if (!Sweep(physicsWorldContext, sweepShape, startPosition, targetPosition, collisionComponent, out var hitResult)
            || !TryGetWalkableGround(hitResult.Normal, up, out var slopeAngle))
        {
            SetGroundInfo(CharacterControllerGroundInfo.None);
            if (MovementState != CharacterMovementState.Jumping || Vector3.Dot(velocity, up) <= 0f)
            {
                MovementState = CharacterMovementState.Falling;
            }

            return;
        }

        var snapDistance = Math.Max(0f, hitResult.HitFraction * _settings.GroundSnapDistance - _settings.SkinWidth);
        if (snapDistance > 0f)
        {
            rootComponent.Position -= up * snapDistance;
        }

        if (Vector3.Dot(velocity, up) < 0f)
        {
            velocity -= up * Vector3.Dot(velocity, up);
        }

        SetGroundInfo(new CharacterControllerGroundInfo(true, hitResult.Normal, hitResult.Collider, slopeAngle));
    }

    /// <summary>
    /// C4: ground exclusively from <see cref="World.CollisionField"/>, replacing the sweep-based
    /// snap. The probe origin covers the tick's own vertical displacement (so a floor crossed
    /// entirely within one step is still found), and the ground found is accepted when it falls
    /// between (foot after step - GroundSnapDistance) and the probe origin.
    /// </summary>
    private bool TryUpdateGroundFromField(
        SceneComponent rootComponent,
        ref Vector3 velocity,
        Vector3 up,
        Vector3 h1,
        Vector3 h2,
        ICollisionField field,
        CollisionComponent collisionComponent,
        ColliderFixture fixture,
        bool isCapsule,
        float footUpBeforeStep)
    {
        var footprint = ResolveFootprint(rootComponent, collisionComponent, fixture, isCapsule, up, h1, h2, out var footUpOffset);
        var footUpAfterStep = Vector3.Dot(rootComponent.Position, up) + footUpOffset;

        var verticalDelta = Math.Abs(footUpAfterStep - footUpBeforeStep);
        var origin = footUpBeforeStep + Math.Max(_settings.StepHeight, verticalDelta);
        var maxDropDistance = (origin - footUpAfterStep) + _settings.GroundSnapDistance;

        if (!TryProbeFieldMaxGroundHeight(field, footprint, origin, up, h1, h2, maxDropDistance, out var groundHeight, out var groundSurfaceTag))
        {
            return false;
        }

        if (groundHeight < footUpAfterStep - _settings.GroundSnapDistance || groundHeight > origin)
        {
            return false;
        }

        var correction = groundHeight - footUpAfterStep;
        if (correction != 0f)
        {
            rootComponent.Position += up * correction;
        }

        if (Vector3.Dot(velocity, up) < 0f)
        {
            velocity -= up * Vector3.Dot(velocity, up);
        }

        SetGroundInfo(new CharacterControllerGroundInfo(true, up, null, 0f), groundSurfaceTag);
        return true;
    }

    /// <summary>
    /// Probes the 4-corner footprint for the maximum ground height, as before M2.
    /// <paramref name="maxGroundSurfaceTag"/> (M2) is the <see cref="GroundSample.SurfaceTag"/> of
    /// whichever corner produced <paramref name="maxGroundHeight"/> - the same explicit
    /// max-height-corner selection rule used for the height itself, so the two are always
    /// consistent with each other. The 4 corners may carry different tags.
    /// </summary>
    private bool TryProbeFieldMaxGroundHeight(
        ICollisionField field,
        in CharacterFootprint footprint,
        float probeUpCoordinate,
        Vector3 up,
        Vector3 h1,
        Vector3 h2,
        float maxDropDistance,
        out float maxGroundHeight,
        out string maxGroundSurfaceTag)
    {
        Span<(float H1, float H2)> corners = stackalloc (float, float)[4];
        footprint.GetCorners(footprint.FootHorizontalCenter, h1, h2, corners);

        maxGroundHeight = 0f;
        maxGroundSurfaceTag = null;
        var found = false;

        for (var i = 0; i < corners.Length; i++)
        {
            var cornerPosition = h1 * corners[i].H1 + h2 * corners[i].H2 + up * probeUpCoordinate;

            if (!field.TrySampleGround(cornerPosition, maxDropDistance, _settings.WalkabilityMask, out var sample))
            {
                continue;
            }

            if (!found || sample.GroundHeight > maxGroundHeight)
            {
                maxGroundHeight = sample.GroundHeight;
                maxGroundSurfaceTag = sample.SurfaceTag;
            }

            found = true;
        }

        return found;
    }

    private bool TryResolveCollisionDependencies(
        out IPhysicsWorld physicsWorldContext,
        out CollisionComponent collisionComponent,
        out ColliderFixture fixture,
        out bool isCapsule)
    {
        physicsWorldContext = null;
        collisionComponent = null;
        fixture = null;
        isCapsule = false;

        var owner = Owner;
        if (owner?.World?.PhysicsWorld == null)
        {
            return false;
        }

        collisionComponent = owner.GetComponent<CollisionComponent>();
        if (collisionComponent == null || !TryFindCharacterFixture(collisionComponent, out fixture, out isCapsule))
        {
            collisionComponent = null;
            return false;
        }

        physicsWorldContext = owner.World.PhysicsWorld;
        return true;
    }

    /// <summary>Capsule fixture takes priority over a box fixture when the owner's
    /// <see cref="CollisionComponent"/> carries both.</summary>
    private static bool TryFindCharacterFixture(CollisionComponent collisionComponent, out ColliderFixture fixture, out bool isCapsule)
    {
        var fixtures = collisionComponent.Fixtures;
        ColliderFixture boxFixture = null;

        for (int i = 0; i < fixtures.Count; i++)
        {
            if (fixtures[i].Shape is Capsule)
            {
                fixture = fixtures[i];
                isCapsule = true;
                return true;
            }

            if (boxFixture == null && fixtures[i].Shape is Box)
            {
                boxFixture = fixtures[i];
            }
        }

        if (boxFixture != null)
        {
            fixture = boxFixture;
            isCapsule = false;
            return true;
        }

        fixture = null;
        isCapsule = false;
        return false;
    }

    /// <summary>
    /// Capsule fixture: unchanged, <see cref="CharacterControllerSettings.Radius"/>/<see cref="CharacterControllerSettings.Height"/>
    /// shrunk by skin width. Box fixture: the fixture's own size, shrunk by skin width on each face.
    /// </summary>
    private PhysicsQueryShape GetSweepShape(IPhysicsWorld physicsWorldContext, ColliderFixture fixture, bool isCapsule)
    {
        if (isCapsule)
        {
            var radius = Math.Max(MinSweepShapeSize, _settings.Radius - _settings.SkinWidth);
            var cylinderHeight = Math.Max(MinSweepShapeSize, _settings.Height - _settings.Radius * 2f);

            if (_sweepShape == null || _sweepShapeIsBox || radius != _sweepShapeRadius || cylinderHeight != _sweepShapeCylinderHeight)
            {
                _sweepShape?.Dispose();
                _sweepShape = physicsWorldContext.CreateQueryShape(new Capsule { Radius = radius, Length = cylinderHeight }, Vector3.One);
                _sweepShapeIsBox = false;
                _sweepShapeRadius = radius;
                _sweepShapeCylinderHeight = cylinderHeight;
            }

            return _sweepShape;
        }

        var box = (Box)fixture.Shape;
        var skin = _settings.SkinWidth * 2f;
        var size = new Vector3(
            Math.Max(MinSweepShapeSize, box.Size.X - skin),
            Math.Max(MinSweepShapeSize, box.Size.Y - skin),
            Math.Max(MinSweepShapeSize, box.Size.Z - skin));

        if (_sweepShape == null || !_sweepShapeIsBox || size != _sweepShapeBoxSize)
        {
            _sweepShape?.Dispose();
            _sweepShape = physicsWorldContext.CreateQueryShape(new Box { Size = size }, Vector3.One);
            _sweepShapeIsBox = true;
            _sweepShapeBoxSize = size;
        }

        return _sweepShape;
    }

    private bool Sweep(
        IPhysicsWorld physicsWorldContext,
        PhysicsQueryShape sweepShape,
        Vector3 startPosition,
        Vector3 targetPosition,
        CollisionComponent collisionComponent,
        out HitResult hitResult)
    {
        var from = Matrix.CreateTranslation(startPosition);
        var to = Matrix.CreateTranslation(targetPosition);
        return physicsWorldContext.ShapeSweep(
            sweepShape,
            from,
            to,
            out hitResult,
            _settings.GetSweepChannelMask(),
            _settings.HitTriggers,
            collisionComponent);
    }

    private bool TryGetWalkableGround(Vector3 normal, Vector3 up, out float slopeAngle)
    {
        slopeAngle = 90f;

        if (normal == Vector3.Zero)
        {
            return false;
        }

        normal.Normalize();
        var upDot = MathHelper.Clamp(Vector3.Dot(normal, up), -1f, 1f);
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

    private Vector3 GetDesiredHorizontalVelocity(Vector3 h1, Vector3 h2)
    {
        return (h1 * _moveIntent.X - h2 * _moveIntent.Y) * _settings.MaxHorizontalSpeed;
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