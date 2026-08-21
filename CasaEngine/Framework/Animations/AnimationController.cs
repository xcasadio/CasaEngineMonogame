using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public sealed class AnimationController
{
    private readonly AnimationClipSampler _sampler = new();
    private readonly SkeletonPoseLocal _sourcePose;
    private readonly SkeletonPoseLocal _targetPose;
    private readonly SkeletonPoseLocal _transitionPose;
    private readonly SkeletonPoseLocal _layerPose;
    private readonly SkeletonPoseLocal _referencePose;
    private readonly SkeletonPoseLocal _previousOutputPose;
    private readonly SkeletonPoseLocal _prePreviousOutputPose;
    private readonly JointInertializationState[] _inertializationStates;
    private readonly List<AnimationLayer> _layers = new();
    private BoneTransform _previousSampledRootTransform = BoneTransform.Identity;
    private bool _hasPreviousSampledRootTransform;
    private float _lastFrameDeltaSeconds;

    private AnimationState _currentState;
    private AnimationState _targetState;
    private float _crossFadeDurationSeconds;
    private float _crossFadeElapsedSeconds;
    private AnimationCrossFadeSettings _crossFadeSettings = AnimationCrossFadeSettings.Default;
    private IAnimationGraphNode _graphRoot;
    private bool _graphPlaying;
    private float _graphTimeSeconds;

    public AnimationController(SkeletonDefinition skeleton)
    {
        Skeleton = skeleton ?? throw new ArgumentNullException(nameof(skeleton));
        RootJointIndex = skeleton.RootIndex;
        OutputPose = skeleton.CreateLocalBindPose();
        _sourcePose = skeleton.CreateLocalBindPose();
        _targetPose = skeleton.CreateLocalBindPose();
        _transitionPose = skeleton.CreateLocalBindPose();
        _layerPose = skeleton.CreateLocalBindPose();
        _referencePose = skeleton.CreateLocalBindPose();
        _previousOutputPose = skeleton.CreateLocalBindPose();
        _prePreviousOutputPose = skeleton.CreateLocalBindPose();
        _inertializationStates = new JointInertializationState[skeleton.Count];
    }

    /// <summary>
    /// Per-joint decay state captured at the start of an <see cref="AnimationTransitionMode.Inertialize"/>
    /// transition: the translation offset/velocity are tracked per axis, the rotation offset/velocity as a
    /// single scalar (signed angle) around a fixed axis captured at transition start. All six quintic
    /// coefficients are precomputed once so evaluating the curve every frame is a handful of multiplies.
    /// </summary>
    private struct JointInertializationState
    {
        public Vector3 TranslationX0;
        public Vector3 TranslationV0;
        public Vector3 TranslationDuration;
        public Vector3 TranslationCoeffA;
        public Vector3 TranslationCoeffB;
        public Vector3 TranslationCoeffC;

        public Vector3 RotationAxis;
        public float RotationX0;
        public float RotationV0;
        public float RotationDuration;
        public float RotationCoeffA;
        public float RotationCoeffB;
        public float RotationCoeffC;
    }

    public SkeletonDefinition Skeleton { get; }

    public SkeletonPoseLocal OutputPose { get; }

    public int RootJointIndex { get; set; }

    public RootMotionMode RootMotionMode { get; set; } = RootMotionMode.Observe;

    public RootMotionDelta CurrentRootMotionDelta { get; private set; } = RootMotionDelta.Identity;

    public AnimationState CurrentState => _currentState;

    public AnimationState TargetState => _targetState;

    public IAnimationGraphNode GraphRoot => _graphRoot;

    public float CurrentTimeSeconds => _currentState?.TimeSeconds ?? _graphTimeSeconds;

    public bool HasAnimationGraph => _graphRoot != null;

    public bool IsPlaying => _graphRoot != null
        ? _graphPlaying
        : _currentState?.IsPlaying == true || _targetState != null;

    public bool IsCrossFading => _targetState != null;

    public float CrossFadeBlendWeight
    {
        get
        {
            if (_targetState == null)
            {
                return 1f;
            }

            var linearBlendWeight = _crossFadeDurationSeconds <= 0f
                ? 1f
                : Math.Clamp(_crossFadeElapsedSeconds / _crossFadeDurationSeconds, 0f, 1f);
            return AnimationTransitionEasing.Evaluate(_crossFadeSettings.EasingMode, linearBlendWeight);
        }
    }

    public IReadOnlyList<AnimationLayer> Layers => _layers;

    /// <summary>
    /// Reads or writes the playback speed of the current (and, mid-transition, the target)
    /// animation state. Returns 1 when no state-based animation is playing (e.g. graph
    /// playback). Setting it while a transition is in progress applies to both states.
    /// </summary>
    public float PlaybackSpeed
    {
        get => _currentState?.Speed ?? 1f;
        set
        {
            if (_currentState != null)
            {
                _currentState.Speed = value;
            }

            if (_targetState != null)
            {
                _targetState.Speed = value;
            }
        }
    }

    public event Action<AnimationEventKeyframe> AnimationEventTriggered;

    public void Play(AnimationClip clip, bool loop = true, float speed = 1f)
    {
        ValidateClip(clip);
        ClearGraphPlayback();

        _currentState = new AnimationState(clip, loop, speed);
        _targetState = null;
        _crossFadeDurationSeconds = 0f;
        _crossFadeElapsedSeconds = 0f;
        _crossFadeSettings = AnimationCrossFadeSettings.Default;
        _sampler.Sample(clip, 0f, OutputPose, loop);
        ResetRootMotionTrackingFromOutputPose();
        if (RootMotionMode == RootMotionMode.Apply)
        {
            RemoveRootMotionFromOutputPose();
        }

        CurrentRootMotionDelta = RootMotionDelta.Identity;
        CaptureOutputPoseHistory(0f);
    }

    public void PlayGraph(IAnimationGraphNode graphRoot)
    {
        ValidateGraph(graphRoot);

        _graphRoot = graphRoot;
        _graphPlaying = true;
        _graphTimeSeconds = 0f;
        _currentState = null;
        _targetState = null;
        _crossFadeDurationSeconds = 0f;
        _crossFadeElapsedSeconds = 0f;
        _crossFadeSettings = AnimationCrossFadeSettings.Default;
        graphRoot.Evaluate(OutputPose);
        ResetRootMotionTrackingFromOutputPose();
        if (RootMotionMode == RootMotionMode.Apply)
        {
            RemoveRootMotionFromOutputPose();
        }

        CurrentRootMotionDelta = RootMotionDelta.Identity;
        CaptureOutputPoseHistory(0f);
    }

    public void CrossFade(AnimationClip clip, float durationSeconds, bool loop = true, float speed = 1f)
    {
        CrossFade(clip, durationSeconds, AnimationCrossFadeSettings.Default, loop, speed);
    }

    public void CrossFade(AnimationClip clip, float durationSeconds, AnimationCrossFadeSettings settings, bool loop = true, float speed = 1f)
    {
        ValidateClip(clip);
        settings ??= AnimationCrossFadeSettings.Default;
        settings.Validate();

        if (_currentState == null || durationSeconds <= 0f)
        {
            Play(clip, loop, speed);
            return;
        }

        _targetState = new AnimationState(clip, loop, speed);
        _crossFadeDurationSeconds = durationSeconds;
        _crossFadeElapsedSeconds = 0f;
        _crossFadeSettings = settings;

        if (settings.TransitionMode == AnimationTransitionMode.Inertialize)
        {
            BeginInertializedTransition(clip, durationSeconds, settings, loop);
        }
    }

    public void Stop()
    {
        ClearGraphPlayback();
        _currentState = null;
        _targetState = null;
        _crossFadeDurationSeconds = 0f;
        _crossFadeElapsedSeconds = 0f;
        _crossFadeSettings = AnimationCrossFadeSettings.Default;
        for (var layerIndex = 0; layerIndex < _layers.Count; layerIndex++)
        {
            _layers[layerIndex].Clear();
        }
        OutputPose.ResetToBindPose();
        ResetRootMotionTrackingFromOutputPose();
        CurrentRootMotionDelta = RootMotionDelta.Identity;
        CaptureOutputPoseHistory(0f);
    }

    public RootMotionDelta ConsumeRootMotionDelta()
    {
        var rootMotionDelta = CurrentRootMotionDelta;
        CurrentRootMotionDelta = RootMotionDelta.Identity;
        return rootMotionDelta;
    }

    public void Pause()
    {
        _graphPlaying = false;
        _currentState?.Pause();
        _targetState?.Pause();
        for (var layerIndex = 0; layerIndex < _layers.Count; layerIndex++)
        {
            _layers[layerIndex].Pause();
        }
    }

    public void Resume()
    {
        if (_graphRoot != null)
        {
            _graphPlaying = true;
        }

        _currentState?.Resume();
        _targetState?.Resume();
        for (var layerIndex = 0; layerIndex < _layers.Count; layerIndex++)
        {
            _layers[layerIndex].Resume();
        }
    }

    public void Seek(float timeSeconds)
    {
        if (_currentState == null)
        {
            return;
        }

        _currentState.Seek(timeSeconds);
        _sampler.Sample(_currentState.Clip, _currentState.TimeSeconds, OutputPose, _currentState.Loop);
        ResetRootMotionTrackingFromOutputPose();
        if (RootMotionMode == RootMotionMode.Apply)
        {
            RemoveRootMotionFromOutputPose();
        }

        CurrentRootMotionDelta = RootMotionDelta.Identity;
        CaptureOutputPoseHistory(0f);
    }

    public void SetLayerAnimation(
        int layerIndex,
        AnimationClip clip,
        BoneMask mask = null,
        float weight = 1f,
        AnimationLayerBlendMode blendMode = AnimationLayerBlendMode.Override,
        bool loop = true,
        float speed = 1f)
    {
        ValidateClip(clip);
        var layer = GetOrCreateLayer(layerIndex);
        layer.Configure(clip, mask, weight, blendMode, loop, speed);
    }

    public void ClearLayer(int layerIndex)
    {
        if (layerIndex < 0 || layerIndex >= _layers.Count)
        {
            return;
        }

        _layers[layerIndex].Clear();
    }

    public void SetLayerWeight(int layerIndex, float weight)
    {
        GetOrCreateLayer(layerIndex).SetWeight(weight);
    }

    public void Update(float elapsedSeconds)
    {
        if (_graphRoot != null)
        {
            if (_graphPlaying && _graphRoot is IAnimationGraphRuntimeNode runtimeNode)
            {
                runtimeNode.Advance(elapsedSeconds);
                _graphTimeSeconds += elapsedSeconds;
            }

            _graphRoot.Evaluate(OutputPose);
            ApplyLayers();
            UpdateRootMotionDelta();
            CaptureOutputPoseHistory(elapsedSeconds);
            return;
        }

        if (_currentState == null)
        {
            OutputPose.ResetToBindPose();
            ResetRootMotionTrackingFromOutputPose();
            CurrentRootMotionDelta = RootMotionDelta.Identity;
            CaptureOutputPoseHistory(elapsedSeconds);
            return;
        }

        for (var layerIndex = 0; layerIndex < _layers.Count; layerIndex++)
        {
            _layers[layerIndex].Update(elapsedSeconds);
        }

        EvaluateStateTransition(elapsedSeconds, forced: false);
        CaptureOutputPoseHistory(elapsedSeconds);
    }

    /// <summary>
    /// Advances playback by <paramref name="elapsedSeconds"/> regardless of the paused
    /// state, then re-evaluates the output pose. Mirrors <see cref="Update"/> but bypasses
    /// the paused gate, enabling explicit single-step advancing.
    /// </summary>
    public void Advance(float elapsedSeconds)
    {
        if (elapsedSeconds == 0f)
        {
            return;
        }

        if (_graphRoot != null)
        {
            if (_graphRoot is IAnimationGraphRuntimeNode runtimeNode)
            {
                runtimeNode.Advance(elapsedSeconds);
                _graphTimeSeconds += elapsedSeconds;
            }

            _graphRoot.Evaluate(OutputPose);
            ApplyLayers();
            UpdateRootMotionDelta();
            CaptureOutputPoseHistory(elapsedSeconds);
            return;
        }

        if (_currentState == null)
        {
            return;
        }

        EvaluateStateTransition(elapsedSeconds, forced: true);
        CaptureOutputPoseHistory(elapsedSeconds);
    }

    /// <summary>
    /// Advances the current (and, mid-transition, target) state by <paramref name="elapsedSeconds"/>
    /// and re-evaluates <see cref="OutputPose"/>. Shared by <see cref="Update"/> and
    /// <see cref="Advance"/>, which only differ in whether playback respects the paused state
    /// (<paramref name="forced"/> = false) or always advances (<paramref name="forced"/> = true).
    /// </summary>
    private void EvaluateStateTransition(float elapsedSeconds, bool forced)
    {
        var previousCurrentTime = _currentState.TimeSeconds;
        if (forced)
        {
            _currentState.AdvanceForced(elapsedSeconds);
        }
        else
        {
            _currentState.Update(elapsedSeconds);
        }

        if (_targetState == null)
        {
            _sampler.Sample(_currentState.Clip, _currentState.TimeSeconds, OutputPose, _currentState.Loop);
            ApplyLayers();
            DispatchAnimationEvents(_currentState, previousCurrentTime, _currentState.TimeSeconds);
            UpdateRootMotionDelta();
            return;
        }

        if (forced)
        {
            _targetState.AdvanceForced(elapsedSeconds);
        }
        else
        {
            _targetState.Update(elapsedSeconds);
        }

        _crossFadeElapsedSeconds += elapsedSeconds;

        var linearBlendWeight = _crossFadeDurationSeconds <= 0f
            ? 1f
            : Math.Clamp(_crossFadeElapsedSeconds / _crossFadeDurationSeconds, 0f, 1f);

        bool transitionComplete;
        if (_crossFadeSettings.TransitionMode == AnimationTransitionMode.Inertialize)
        {
            EvaluateInertializedTransition();
            transitionComplete = linearBlendWeight >= 1f;
        }
        else
        {
            _sampler.Sample(_currentState.Clip, _currentState.TimeSeconds, _sourcePose, _currentState.Loop);
            _sampler.Sample(_targetState.Clip, _targetState.TimeSeconds, _targetPose, _targetState.Loop);

            PreserveRootTranslationVelocity(previousCurrentTime, elapsedSeconds, linearBlendWeight);
            var blendWeight = AnimationTransitionEasing.Evaluate(_crossFadeSettings.EasingMode, linearBlendWeight);

            AnimationPoseBlender.Blend(_sourcePose, _targetPose, blendWeight, OutputPose);
            transitionComplete = blendWeight >= 1f;
        }

        ApplyLayers();
        DispatchAnimationEvents(_currentState, previousCurrentTime, _currentState.TimeSeconds);
        UpdateRootMotionDelta();

        if (transitionComplete)
        {
            _currentState = _targetState;
            _targetState = null;
            _crossFadeDurationSeconds = 0f;
            _crossFadeElapsedSeconds = 0f;
            _crossFadeSettings = AnimationCrossFadeSettings.Default;
        }
    }

    private void ValidateClip(AnimationClip clip)
    {
        if (!ReferenceEquals(clip.Skeleton, Skeleton))
        {
            throw new ArgumentException("The animation clip targets a different skeleton.", nameof(clip));
        }
    }

    private void ValidateGraph(IAnimationGraphNode graphRoot)
    {
        ArgumentNullException.ThrowIfNull(graphRoot);

        if (!ReferenceEquals(graphRoot.Skeleton, Skeleton))
        {
            throw new ArgumentException("The animation graph targets a different skeleton.", nameof(graphRoot));
        }
    }

    private void ClearGraphPlayback()
    {
        _graphRoot = null;
        _graphPlaying = false;
        _graphTimeSeconds = 0f;
    }

    private BoneTransform GetCurrentRootTransform()
    {
        return GetRootTransform(OutputPose);
    }

    private BoneTransform GetRootTransform(SkeletonPoseLocal pose)
    {
        if (pose.Count == 0 || RootJointIndex < 0 || RootJointIndex >= pose.Count)
        {
            return BoneTransform.Identity;
        }

        return pose.GetTransform(RootJointIndex);
    }

    private void ResetRootMotionTrackingFromOutputPose()
    {
        _previousSampledRootTransform = GetCurrentRootTransform();
        _hasPreviousSampledRootTransform = true;
    }

    private void UpdateRootMotionDelta()
    {
        var currentRootTransform = GetCurrentRootTransform();
        if (!_hasPreviousSampledRootTransform)
        {
            _previousSampledRootTransform = currentRootTransform;
            _hasPreviousSampledRootTransform = true;

            if (RootMotionMode == RootMotionMode.Apply)
            {
                RemoveRootMotionFromOutputPose();
            }

            CurrentRootMotionDelta = RootMotionDelta.Identity;
            return;
        }

        var previousRootTransform = _previousSampledRootTransform;
        var previousRotation = previousRootTransform.Rotation.LengthSquared() <= float.Epsilon
            ? Quaternion.Identity
            : previousRootTransform.Rotation;
        var currentRotation = currentRootTransform.Rotation.LengthSquared() <= float.Epsilon
            ? Quaternion.Identity
            : currentRootTransform.Rotation;
        var rotationDelta = Quaternion.Normalize(Quaternion.Inverse(previousRotation) * currentRotation);

        _previousSampledRootTransform = currentRootTransform;
        CurrentRootMotionDelta = new RootMotionDelta(
            currentRootTransform.Translation - previousRootTransform.Translation,
            rotationDelta);

        if (RootMotionMode == RootMotionMode.Apply)
        {
            RemoveRootMotionFromOutputPose();
        }
    }

    private void RemoveRootMotionFromOutputPose()
    {
        if (OutputPose.Count == 0 || RootJointIndex < 0 || RootJointIndex >= OutputPose.Count)
        {
            return;
        }

        OutputPose.SetTransformDirect(RootJointIndex, Skeleton.GetBindLocalTransform(RootJointIndex));
        OutputPose.MarkDirtyFrom(RootJointIndex);
    }

    private AnimationLayer GetOrCreateLayer(int layerIndex)
    {
        if (layerIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(layerIndex));
        }

        while (_layers.Count <= layerIndex)
        {
            _layers.Add(new AnimationLayer(_layers.Count, Skeleton));
        }

        return _layers[layerIndex];
    }

    private void ApplyLayers()
    {
        for (var layerIndex = 0; layerIndex < _layers.Count; layerIndex++)
        {
            var layer = _layers[layerIndex];
            if (!layer.Enabled || layer.State == null || layer.Weight <= 0f)
            {
                continue;
            }

            _sampler.Sample(layer.State.Clip, layer.State.TimeSeconds, _layerPose, layer.State.Loop);
            ApplyLayerPose(layer, _layerPose, OutputPose, _referencePose);
        }
    }

    private void PreserveRootTranslationVelocity(float previousCurrentTime, float elapsedSeconds, float linearBlendWeight)
    {
        if (!_crossFadeSettings.PreserveRootTranslationVelocity
            || _currentState == null
            || elapsedSeconds <= float.Epsilon
            || RootJointIndex < 0
            || RootJointIndex >= Skeleton.Count)
        {
            return;
        }

        _sampler.Sample(_currentState.Clip, previousCurrentTime, _transitionPose, _currentState.Loop);

        var previousSourceRoot = GetRootTransform(_transitionPose);
        var currentSourceRoot = GetRootTransform(_sourcePose);
        var remainingTimeSeconds = Math.Max(_crossFadeDurationSeconds - _crossFadeElapsedSeconds, 0f);
        if (remainingTimeSeconds <= 0f)
        {
            return;
        }

        var sourceVelocity = (currentSourceRoot.Translation - previousSourceRoot.Translation) / elapsedSeconds;
        if (sourceVelocity.LengthSquared() <= float.Epsilon)
        {
            return;
        }

        var targetRoot = GetRootTransform(_targetPose);
        var preservedTranslation = targetRoot.Translation + sourceVelocity * remainingTimeSeconds * _crossFadeSettings.RootTranslationVelocityWeight;
        _targetPose.SetTransformDirect(
            RootJointIndex,
            new BoneTransform(
                preservedTranslation,
                targetRoot.Rotation,
                targetRoot.Scale));
        _targetPose.MarkDirtyFrom(RootJointIndex);
    }

    /// <summary>
    /// Captures the pose discontinuity at the start of an inertialization transition and
    /// precomputes the quintic decay coefficients for every joint. The root joint is treated
    /// like any other joint: its local-space offset is decayed the same way, and the existing
    /// root-motion extraction (<see cref="UpdateRootMotionDelta"/>) keeps reading whatever
    /// ends up in <see cref="OutputPose"/> afterwards, exactly as it does for a cross-fade.
    /// </summary>
    private void BeginInertializedTransition(AnimationClip targetClip, float durationSeconds, AnimationCrossFadeSettings settings, bool loop)
    {
        _sampler.Sample(targetClip, 0f, _targetPose, loop);

        var dt = _lastFrameDeltaSeconds;
        var maxTranslationOffset = settings.InertializeMaxTranslationOffset;
        var maxTranslationOffsetVector = new Vector3(maxTranslationOffset, maxTranslationOffset, maxTranslationOffset);

        for (var jointIndex = 0; jointIndex < Skeleton.Count; jointIndex++)
        {
            var previousTransform = _previousOutputPose.GetTransform(jointIndex);
            var prePreviousTransform = _prePreviousOutputPose.GetTransform(jointIndex);
            var targetTransform = _targetPose.GetTransform(jointIndex);

            var state = new JointInertializationState();

            var translationX0 = Vector3.Clamp(
                previousTransform.Translation - targetTransform.Translation,
                -maxTranslationOffsetVector,
                maxTranslationOffsetVector);
            var translationV0 = dt > float.Epsilon
                ? (previousTransform.Translation - prePreviousTransform.Translation) / dt
                : Vector3.Zero;

            state.TranslationX0 = translationX0;
            state.TranslationV0 = translationV0;
            state.TranslationDuration = new Vector3(
                InertializationMath.ComputeEffectiveDuration(translationX0.X, translationV0.X, durationSeconds),
                InertializationMath.ComputeEffectiveDuration(translationX0.Y, translationV0.Y, durationSeconds),
                InertializationMath.ComputeEffectiveDuration(translationX0.Z, translationV0.Z, durationSeconds));

            InertializationMath.ComputeCoefficients(translationX0.X, translationV0.X, state.TranslationDuration.X, out var translationAx, out var translationBx, out var translationCx);
            InertializationMath.ComputeCoefficients(translationX0.Y, translationV0.Y, state.TranslationDuration.Y, out var translationAy, out var translationBy, out var translationCy);
            InertializationMath.ComputeCoefficients(translationX0.Z, translationV0.Z, state.TranslationDuration.Z, out var translationAz, out var translationBz, out var translationCz);
            state.TranslationCoeffA = new Vector3(translationAx, translationAy, translationAz);
            state.TranslationCoeffB = new Vector3(translationBx, translationBy, translationBz);
            state.TranslationCoeffC = new Vector3(translationCx, translationCy, translationCz);

            var previousRotation = NormalizeOrIdentity(previousTransform.Rotation);
            var prePreviousRotation = NormalizeOrIdentity(prePreviousTransform.Rotation);
            var targetRotation = NormalizeOrIdentity(targetTransform.Rotation);

            var offsetRotation = Quaternion.Normalize(previousRotation * Quaternion.Inverse(targetRotation));
            ExtractShortestAxisAngle(offsetRotation, out var rotationAxis, out var rotationAngle0);

            var deltaRotation = Quaternion.Normalize(previousRotation * Quaternion.Inverse(prePreviousRotation));
            ExtractShortestAxisAngle(deltaRotation, out var deltaAxis, out var deltaAngle);
            var rotationV0 = dt > float.Epsilon
                ? Vector3.Dot(deltaAxis * deltaAngle, rotationAxis) / dt
                : 0f;

            state.RotationAxis = rotationAxis;
            state.RotationX0 = rotationAngle0;
            state.RotationV0 = rotationV0;
            state.RotationDuration = InertializationMath.ComputeEffectiveDuration(rotationAngle0, rotationV0, durationSeconds);
            InertializationMath.ComputeCoefficients(rotationAngle0, rotationV0, state.RotationDuration, out state.RotationCoeffA, out state.RotationCoeffB, out state.RotationCoeffC);

            _inertializationStates[jointIndex] = state;
        }
    }

    /// <summary>
    /// Evaluates the inertialization decay for every joint and writes the result directly into
    /// <see cref="OutputPose"/>: the target clip plays back unmodified, and the captured
    /// per-joint translation/rotation offset is decayed towards zero and added on top. Scale
    /// channels are not inertialized; the target scale is used as-is.
    /// </summary>
    private void EvaluateInertializedTransition()
    {
        _sampler.Sample(_targetState.Clip, _targetState.TimeSeconds, _targetPose, _targetState.Loop);

        var t = _crossFadeElapsedSeconds;

        for (var jointIndex = 0; jointIndex < Skeleton.Count; jointIndex++)
        {
            var state = _inertializationStates[jointIndex];
            var targetTransform = _targetPose.GetTransform(jointIndex);

            var translationOffset = new Vector3(
                InertializationMath.Evaluate(t, state.TranslationX0.X, state.TranslationV0.X, state.TranslationDuration.X, state.TranslationCoeffA.X, state.TranslationCoeffB.X, state.TranslationCoeffC.X),
                InertializationMath.Evaluate(t, state.TranslationX0.Y, state.TranslationV0.Y, state.TranslationDuration.Y, state.TranslationCoeffA.Y, state.TranslationCoeffB.Y, state.TranslationCoeffC.Y),
                InertializationMath.Evaluate(t, state.TranslationX0.Z, state.TranslationV0.Z, state.TranslationDuration.Z, state.TranslationCoeffA.Z, state.TranslationCoeffB.Z, state.TranslationCoeffC.Z));

            var rotationAngle = InertializationMath.Evaluate(t, state.RotationX0, state.RotationV0, state.RotationDuration, state.RotationCoeffA, state.RotationCoeffB, state.RotationCoeffC);

            var outputRotation = targetTransform.Rotation;
            if (MathF.Abs(rotationAngle) > 1e-6f && state.RotationAxis.LengthSquared() > float.Epsilon)
            {
                var rotationOffset = Quaternion.CreateFromAxisAngle(state.RotationAxis, rotationAngle);
                outputRotation = Quaternion.Normalize(rotationOffset * targetTransform.Rotation);
            }

            OutputPose.SetTransformDirect(
                jointIndex,
                new BoneTransform(targetTransform.Translation + translationOffset, outputRotation, targetTransform.Scale));
        }

        OutputPose.MarkDirtyFrom(0);
    }

    /// <summary>
    /// Snapshots <see cref="OutputPose"/> into the P-1/P-2 history buffers used to estimate
    /// velocity when starting an inertialization transition, and records the frame duration
    /// (P-2 to P-1) used for that estimate. Called once per frame at the end of every
    /// <see cref="Update"/>/<see cref="Advance"/> call (and, with a zero duration, whenever
    /// <see cref="OutputPose"/> is set discontinuously by <see cref="Play"/>, <see cref="PlayGraph"/>,
    /// <see cref="Stop"/> or <see cref="Seek"/>) so it is never allocated per-frame.
    /// </summary>
    private void CaptureOutputPoseHistory(float elapsedSeconds)
    {
        _prePreviousOutputPose.CopyFrom(_previousOutputPose);
        _previousOutputPose.CopyFrom(OutputPose);
        _lastFrameDeltaSeconds = elapsedSeconds;
    }

    private static Quaternion NormalizeOrIdentity(Quaternion rotation)
    {
        return rotation.LengthSquared() <= float.Epsilon ? Quaternion.Identity : Quaternion.Normalize(rotation);
    }

    /// <summary>
    /// Decomposes a rotation into an axis and a shortest-path angle in [0, pi]. Because a
    /// quaternion double-covers SO(3), negating it when W is negative picks the equivalent
    /// rotation with the smaller angle (e.g. a 350-degree offset becomes -10 degrees around the
    /// opposite axis instead of decaying the long way around).
    /// </summary>
    private static void ExtractShortestAxisAngle(Quaternion rotation, out Vector3 axis, out float angle)
    {
        var normalized = NormalizeOrIdentity(rotation);
        if (normalized.W < 0f)
        {
            normalized = new Quaternion(-normalized.X, -normalized.Y, -normalized.Z, -normalized.W);
        }

        var clampedW = Math.Clamp(normalized.W, -1f, 1f);
        angle = 2f * MathF.Acos(clampedW);

        var sinHalfAngle = MathF.Sqrt(Math.Max(1f - clampedW * clampedW, 0f));
        axis = sinHalfAngle > 1e-6f
            ? new Vector3(normalized.X, normalized.Y, normalized.Z) / sinHalfAngle
            : Vector3.UnitX;
    }

    private static void ApplyLayerPose(AnimationLayer layer, SkeletonPoseLocal layerPose, SkeletonPoseLocal basePose, SkeletonPoseLocal referencePose)
    {
        for (var jointIndex = 0; jointIndex < basePose.Count; jointIndex++)
        {
            var maskWeight = layer.Mask.GetWeight(jointIndex) * layer.Weight;
            if (maskWeight <= 0f)
            {
                continue;
            }

            var baseTransform = basePose.GetTransform(jointIndex);
            var layerTransform = layerPose.GetTransform(jointIndex);
            BoneTransform resultTransform;

            if (layer.BlendMode == AnimationLayerBlendMode.Additive)
            {
                var referenceTransform = referencePose.GetTransform(jointIndex);
                var translationDelta = layerTransform.Translation - referenceTransform.Translation;
                var scaleDelta = layerTransform.Scale - referenceTransform.Scale;
                var referenceRotation = referenceTransform.Rotation.LengthSquared() <= float.Epsilon
                    ? Quaternion.Identity
                    : referenceTransform.Rotation;
                var layerRotation = layerTransform.Rotation.LengthSquared() <= float.Epsilon
                    ? Quaternion.Identity
                    : layerTransform.Rotation;
                var deltaRotation = Quaternion.Normalize(Quaternion.Inverse(referenceRotation) * layerRotation);
                var weightedDeltaRotation = Quaternion.Slerp(Quaternion.Identity, deltaRotation, maskWeight);

                resultTransform = new BoneTransform(
                    baseTransform.Translation + translationDelta * maskWeight,
                    Quaternion.Normalize(weightedDeltaRotation * baseTransform.Rotation),
                    baseTransform.Scale + scaleDelta * maskWeight);
            }
            else
            {
                resultTransform = new BoneTransform(
                    Vector3.Lerp(baseTransform.Translation, layerTransform.Translation, maskWeight),
                    Quaternion.Slerp(baseTransform.Rotation, layerTransform.Rotation, maskWeight),
                    Vector3.Lerp(baseTransform.Scale, layerTransform.Scale, maskWeight));
            }

            basePose.SetTransformDirect(jointIndex, resultTransform);
        }

        basePose.MarkDirtyFrom(0);
    }

    private void DispatchAnimationEvents(AnimationState state, float previousTimeSeconds, float currentTimeSeconds)
    {
        var eventTrack = state.Clip.EventTrack;
        if (eventTrack == null || eventTrack.Count == 0)
        {
            return;
        }

        var durationSeconds = state.Clip.DurationSeconds;
        if (!state.Loop || durationSeconds <= 0f)
        {
            DispatchAnimationEventsInRange(eventTrack, previousTimeSeconds, currentTimeSeconds);
            return;
        }

        var previousLoopIndex = (int)MathF.Floor(previousTimeSeconds / durationSeconds);
        var currentLoopIndex = (int)MathF.Floor(currentTimeSeconds / durationSeconds);
        var previousWrappedTime = WrapTime(previousTimeSeconds, durationSeconds);
        var currentWrappedTime = WrapTime(currentTimeSeconds, durationSeconds);

        if (currentLoopIndex == previousLoopIndex)
        {
            DispatchAnimationEventsInRange(eventTrack, previousWrappedTime, currentWrappedTime);
            return;
        }

        DispatchAnimationEventsInRange(eventTrack, previousWrappedTime, durationSeconds);

        for (var loopIndex = previousLoopIndex + 1; loopIndex < currentLoopIndex; loopIndex++)
        {
            DispatchAnimationEventsInRange(eventTrack, 0f, durationSeconds);
        }

        DispatchAnimationEventsInRange(eventTrack, 0f, currentWrappedTime);
    }

    private void DispatchAnimationEventsInRange(AnimationEventTrack eventTrack, float startTimeSeconds, float endTimeSeconds)
    {
        for (var eventIndex = 0; eventIndex < eventTrack.Count; eventIndex++)
        {
            var keyframe = eventTrack.GetKeyframe(eventIndex);
            if (keyframe.TimeSeconds > startTimeSeconds && keyframe.TimeSeconds <= endTimeSeconds)
            {
                AnimationEventTriggered?.Invoke(keyframe);
            }
        }
    }

    private static float WrapTime(float timeSeconds, float durationSeconds)
    {
        if (durationSeconds <= 0f)
        {
            return 0f;
        }

        var wrappedTime = timeSeconds % durationSeconds;
        if (wrappedTime < 0f)
        {
            wrappedTime += durationSeconds;
        }

        return wrappedTime;
    }
}