using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

/// <summary>
/// Reusable foot-lock: while a foot is reported in contact with the ground, pins its ankle
/// (via the existing <see cref="IkSolverTwoBone"/> two-bone IK) to the world position it had
/// when the contact started, with blend-in/out so there is no pop. Meant for in-place-authored
/// locomotion clips (e.g. PSX-era mocap) played on a moving entity, where the stance foot would
/// otherwise slide.
/// <para/>
/// The simplest way to honour the contract below is
/// <c>SkinnedMeshComponent.AttachFootLock(controller, contactsProvider)</c>: the component then
/// updates this controller from inside the runtime's pose post-processing (pure animated pose,
/// before any constraint runs) and solves its constraints in the same frame. Manual call order
/// contract otherwise, once per frame, for each <see cref="SkinnedMeshComponent"/> this
/// controller drives:
/// <list type="number">
/// <item><description>
/// Sample/advance the animation so <c>component.CurrentModelPose</c> holds this frame's
/// <b>animated</b> pose (i.e. before this controller's own IK constraints are solved in
/// <c>PosePostProcessing</c>). This is naturally true right after the underlying
/// <see cref="SkinnedMeshAnimationRuntime"/> samples the clip and before its
/// <c>PosePostProcessing</c> event runs the constraints.
/// </description></item>
/// <item><description>Call <see cref="Update"/> with that pose, the entity's world matrix and the per-foot contact flags.</description></item>
/// <item><description>Call <see cref="SkinnedMeshComponent.ApplyFootLock"/> (or <see cref="FillConstraints"/> / <see cref="GetConstraint"/> directly) to push the resulting two-bone IK constraints onto the component.</description></item>
/// </list>
/// Because the constraints are only solved later (inside <c>PosePostProcessing</c>), the pose read
/// in step 1 is always the pre-IK animated pose, which is what <see cref="Update"/> needs to sample
/// the true (unlocked) animated ankle position for sliding/drift detection.
/// </summary>
public sealed class FootLockController
{
    private struct FootRuntimeState
    {
        public bool PreviousContact;
        public bool IsLocked;
        public bool Unlocking;
        // Released for drift while the contact stayed true: re-pin once the animated ankle has
        // come to rest in world space (see FootLockSettings.RelockMaxSpeed).
        public bool AwaitingRest;
        public float Weight;
        public Vector3 LockedWorldPosition;
        public Vector3 AnimatedWorldPosition;
        public Vector3 AnimatedHipModelPosition;
        public Vector3 AnimatedKneeModelPosition;
        public Vector3 AnimatedAnkleModelPosition;
        public float SlideDistance;
    }

    private readonly SkeletonDefinition _skeleton;
    private readonly FootLockSettings _settings;
    private readonly FootLockFoot[] _feet;
    private readonly FootRuntimeState[] _states;
    private Matrix _cachedEntityWorld;
    private Matrix _cachedInverseEntityWorld;
    private bool _hasCachedInverse;

    public FootLockController(SkeletonDefinition skeleton, FootLockSettings settings, params FootLockFoot[] feet)
    {
        ArgumentNullException.ThrowIfNull(skeleton);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(feet);

        if (feet.Length == 0)
        {
            throw new ArgumentException("At least one foot is required.", nameof(feet));
        }

        settings.Validate();
        for (var footIndex = 0; footIndex < feet.Length; footIndex++)
        {
            feet[footIndex].Validate(skeleton);
        }

        _skeleton = skeleton;
        _settings = settings;
        _feet = (FootLockFoot[])feet.Clone();
        _states = new FootRuntimeState[_feet.Length];
    }

    public IReadOnlyList<FootLockFoot> Feet => _feet;

    public int FeetCount => _feet.Length;

    /// <summary>Returns a snapshot of one foot's current lock state.</summary>
    public FootLockFootState GetFootState(int footIndex)
    {
        ValidateFootIndex(footIndex);
        ref var state = ref _states[footIndex];
        return new FootLockFootState(state.IsLocked, state.Weight, state.LockedWorldPosition, state.AnimatedWorldPosition, state.SlideDistance)
        {
            IsReleasing = state.IsLocked && state.Unlocking,
        };
    }

    /// <summary>
    /// Advances the per-foot lock state machine. <paramref name="animatedModelPose"/> must hold
    /// this frame's animated (pre-IK) pose. See the type-level call-order contract.
    /// </summary>
    public void Update(float dt, SkeletonPoseModel animatedModelPose, Matrix entityWorld, ReadOnlySpan<bool> contacts)
    {
        ArgumentNullException.ThrowIfNull(animatedModelPose);

        if (!ReferenceEquals(animatedModelPose.Skeleton, _skeleton))
        {
            throw new ArgumentException("The model pose must target the same skeleton the controller was built for.", nameof(animatedModelPose));
        }

        if (contacts.Length != _feet.Length)
        {
            throw new ArgumentException("The contacts span must have exactly one entry per foot.", nameof(contacts));
        }

        _cachedEntityWorld = entityWorld;
        _cachedInverseEntityWorld = Matrix.Invert(entityWorld);
        _hasCachedInverse = true;

        for (var footIndex = 0; footIndex < _feet.Length; footIndex++)
        {
            var foot = _feet[footIndex];
            ref var state = ref _states[footIndex];

            var hipModelPosition = animatedModelPose.GetTransform(foot.RootJointIndex).Translation;
            var kneeModelPosition = animatedModelPose.GetTransform(foot.MidJointIndex).Translation;
            var ankleModelPosition = animatedModelPose.GetTransform(foot.EndJointIndex).Translation;
            var animatedWorldPosition = Vector3.Transform(ankleModelPosition, entityWorld);
            var previousAnimatedWorldPosition = state.AnimatedWorldPosition;

            state.AnimatedHipModelPosition = hipModelPosition;
            state.AnimatedKneeModelPosition = kneeModelPosition;
            state.AnimatedAnkleModelPosition = ankleModelPosition;
            state.AnimatedWorldPosition = animatedWorldPosition;

            var contact = contacts[footIndex];
            var fallingEdge = !contact && state.PreviousContact;

            // A contact starts the lock on its rising edge, or - after a drift release that left the
            // contact flag true (a transition dragging the planted foot to the target clip's
            // stance) - once the animated ankle has come to rest in world space again.
            var risingEdge = contact && !state.PreviousContact;
            if (state.AwaitingRest)
            {
                if (!contact)
                {
                    state.AwaitingRest = false;
                }
                else if (dt > 0f && Vector3.Distance(animatedWorldPosition, previousAnimatedWorldPosition) <= _settings.RelockMaxSpeed * dt)
                {
                    risingEdge = true;
                }
            }

            if (risingEdge && ankleModelPosition.Y > _settings.GroundHeight + _settings.MaxLockHeight)
            {
                // Contact reported while the animated ankle is still in the air (e.g. a target clip's
                // first-frame contact read while the blended pose still follows the source clip's
                // swing): pinning here would freeze the foot mid-air, so keep the contact pending.
                // PreviousContact stays false, which makes the next near-ground frame the rising edge.
                risingEdge = false;
                contact = state.AwaitingRest;
            }

            if (risingEdge)
            {
                // (Re)pin at the current animated position: this also recovers cleanly from a
                // quick off/on contact toggle that happened mid blend-out.
                state.IsLocked = true;
                state.Unlocking = false;
                state.AwaitingRest = false;
                state.LockedWorldPosition = animatedWorldPosition;
            }

            if (state.IsLocked)
            {
                // Measure the drift on the axes the lock actually enforces: with LockVertical off
                // the foot's animated bob (PSX feet rise a few units during a stance) is neither
                // slide nor a reason to release.
                var drift = animatedWorldPosition - state.LockedWorldPosition;
                if (!_settings.LockVertical)
                {
                    drift.Y = 0f;
                }

                state.SlideDistance = drift.Length();

                if (fallingEdge || state.SlideDistance > _settings.MaxLockDistance)
                {
                    state.Unlocking = true;
                }

                if (state.Unlocking)
                {
                    state.Weight = _settings.BlendOutSeconds > 0f
                        ? MathHelper.Clamp(state.Weight - dt / _settings.BlendOutSeconds, 0f, 1f)
                        : 0f;

                    if (state.Weight <= 0f)
                    {
                        state.Weight = 0f;
                        state.IsLocked = false;
                        state.Unlocking = false;
                        state.SlideDistance = 0f;
                        state.AwaitingRest = contact && _settings.RelockMaxSpeed > 0f;
                    }
                }
                else
                {
                    state.Weight = _settings.BlendInSeconds > 0f
                        ? MathHelper.Clamp(state.Weight + dt / _settings.BlendInSeconds, 0f, 1f)
                        : 1f;
                }
            }
            else
            {
                state.Weight = 0f;
                state.SlideDistance = 0f;
            }

            state.PreviousContact = contact;
        }
    }

    /// <summary>
    /// Forgets every foot's contact history and starts blending out any active lock, without a
    /// weight discontinuity. Call it when the contact flags fed to <see cref="Update"/> change
    /// source (clip change, transition start): the next <see cref="Update"/> re-evaluates them from
    /// scratch, so a foot still reported in contact (and near the ground, see
    /// <see cref="FootLockSettings.MaxLockHeight"/>) re-pins at its current animated position
    /// instead of being dragged toward the pin inherited from the previous clip.
    /// </summary>
    public void Release()
    {
        for (var footIndex = 0; footIndex < _states.Length; footIndex++)
        {
            ref var state = ref _states[footIndex];
            state.PreviousContact = false;
            state.AwaitingRest = false;
            if (state.IsLocked)
            {
                state.Unlocking = true;
            }
        }
    }

    /// <summary>Clears every foot's state immediately (no blend-out): nothing is locked and no contact history remains.</summary>
    public void Reset()
    {
        Array.Clear(_states);
    }

    /// <summary>
    /// Moves every foot's locked (and last sampled) world position by <paramref name="worldDelta"/>.
    /// Call it when the entity is teleported rather than moved (wrap-around of a treadmill preview,
    /// respawn, streaming origin shift...) so the pins travel with it instead of staying behind and
    /// releasing through <see cref="FootLockSettings.MaxLockDistance"/>.
    /// </summary>
    public void TranslateLockedPositions(Vector3 worldDelta)
    {
        for (var footIndex = 0; footIndex < _states.Length; footIndex++)
        {
            ref var state = ref _states[footIndex];
            state.LockedWorldPosition += worldDelta;
            state.AnimatedWorldPosition += worldDelta;
        }
    }

    /// <summary>Fills <paramref name="constraints"/> (one entry per foot, in <see cref="Feet"/> order) for the given entity world matrix.</summary>
    public void FillConstraints(Matrix entityWorld, Span<TwoBoneIkConstraint> constraints)
    {
        if (constraints.Length < _feet.Length)
        {
            throw new ArgumentException("The destination span is smaller than the number of feet.", nameof(constraints));
        }

        for (var footIndex = 0; footIndex < _feet.Length; footIndex++)
        {
            constraints[footIndex] = GetConstraint(footIndex, entityWorld);
        }
    }

    /// <summary>Computes the two-bone IK constraint for one foot, in the skeleton's model space, for the given entity world matrix.</summary>
    public TwoBoneIkConstraint GetConstraint(int footIndex, Matrix entityWorld)
    {
        ValidateFootIndex(footIndex);

        var foot = _feet[footIndex];
        ref var state = ref _states[footIndex];

        var inverseEntityWorld = _hasCachedInverse && entityWorld == _cachedEntityWorld
            ? _cachedInverseEntityWorld
            : Matrix.Invert(entityWorld);

        var targetModelPosition = Vector3.Transform(state.LockedWorldPosition, inverseEntityWorld);
        if (!_settings.LockVertical)
        {
            // PSX feet bob slightly on contact: keep following the animated vertical motion.
            targetModelPosition.Y = state.AnimatedAnkleModelPosition.Y;
        }

        var polePosition = ComputePole(in state);

        return new TwoBoneIkConstraint(
            foot.RootJointIndex,
            foot.MidJointIndex,
            foot.EndJointIndex,
            targetModelPosition,
            polePosition,
            state.Weight,
            state.Weight > 0f);
    }

    private Vector3 ComputePole(in FootRuntimeState state)
    {
        var hipToAnkleMidpoint = (state.AnimatedHipModelPosition + state.AnimatedAnkleModelPosition) * 0.5f;
        var bendVector = state.AnimatedKneeModelPosition - hipToAnkleMidpoint;

        var bendDirection = bendVector.LengthSquared() > 1e-8f
            ? Vector3.Normalize(bendVector)
            : Vector3.Backward; // fixed forward offset (skeleton faces +Z) when the leg is straight

        return state.AnimatedKneeModelPosition + bendDirection * _settings.PoleOffset;
    }

    private void ValidateFootIndex(int footIndex)
    {
        if ((uint)footIndex >= (uint)_feet.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(footIndex));
        }
    }
}
