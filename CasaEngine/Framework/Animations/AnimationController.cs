using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public sealed class AnimationController
{
    private readonly AnimationClipSampler _sampler = new();
    private readonly SkeletonPoseLocal _sourcePose;
    private readonly SkeletonPoseLocal _targetPose;
    private readonly SkeletonPoseLocal _layerPose;
    private readonly SkeletonPoseLocal _referencePose;
    private readonly List<AnimationLayer> _layers = new();
    private BoneTransform _previousSampledRootTransform = BoneTransform.Identity;
    private bool _hasPreviousSampledRootTransform;

    private AnimationState? _currentState;
    private AnimationState? _targetState;
    private float _crossFadeDurationSeconds;
    private float _crossFadeElapsedSeconds;
    private IAnimationGraphNode? _graphRoot;
    private bool _graphPlaying;
    private float _graphTimeSeconds;

    public AnimationController(SkeletonDefinition skeleton)
    {
        Skeleton = skeleton ?? throw new ArgumentNullException(nameof(skeleton));
        RootJointIndex = skeleton.RootIndex;
        OutputPose = skeleton.CreateLocalBindPose();
        _sourcePose = skeleton.CreateLocalBindPose();
        _targetPose = skeleton.CreateLocalBindPose();
        _layerPose = skeleton.CreateLocalBindPose();
        _referencePose = skeleton.CreateLocalBindPose();
    }

    public SkeletonDefinition Skeleton { get; }

    public SkeletonPoseLocal OutputPose { get; }

    public int RootJointIndex { get; set; }

    public RootMotionMode RootMotionMode { get; set; } = RootMotionMode.Observe;

    public RootMotionDelta CurrentRootMotionDelta { get; private set; } = RootMotionDelta.Identity;

    public AnimationState? CurrentState => _currentState;

    public IAnimationGraphNode? GraphRoot => _graphRoot;

    public float CurrentTimeSeconds => _currentState?.TimeSeconds ?? _graphTimeSeconds;

    public bool HasAnimationGraph => _graphRoot != null;

    public bool IsPlaying => _graphRoot != null
        ? _graphPlaying
        : _currentState?.IsPlaying == true || _targetState != null;

    public bool IsCrossFading => _targetState != null;

    public IReadOnlyList<AnimationLayer> Layers => _layers;

    public event Action<AnimationEventKeyframe>? AnimationEventTriggered;

    public void Play(AnimationClip clip, bool loop = true, float speed = 1f)
    {
        ValidateClip(clip);
        ClearGraphPlayback();

        _currentState = new AnimationState(clip, loop, speed);
        _targetState = null;
        _crossFadeDurationSeconds = 0f;
        _crossFadeElapsedSeconds = 0f;
        _sampler.Sample(clip, 0f, OutputPose, loop);
        ResetRootMotionTrackingFromOutputPose();
        if (RootMotionMode == RootMotionMode.Apply)
        {
            RemoveRootMotionFromOutputPose();
        }

        CurrentRootMotionDelta = RootMotionDelta.Identity;
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
        graphRoot.Evaluate(OutputPose);
        ResetRootMotionTrackingFromOutputPose();
        if (RootMotionMode == RootMotionMode.Apply)
        {
            RemoveRootMotionFromOutputPose();
        }

        CurrentRootMotionDelta = RootMotionDelta.Identity;
    }

    public void CrossFade(AnimationClip clip, float durationSeconds, bool loop = true, float speed = 1f)
    {
        ValidateClip(clip);

        if (_currentState == null || durationSeconds <= 0f)
        {
            Play(clip, loop, speed);
            return;
        }

        _targetState = new AnimationState(clip, loop, speed);
        _crossFadeDurationSeconds = durationSeconds;
        _crossFadeElapsedSeconds = 0f;
    }

    public void Stop()
    {
        ClearGraphPlayback();
        _currentState = null;
        _targetState = null;
        _crossFadeDurationSeconds = 0f;
        _crossFadeElapsedSeconds = 0f;
        for (var layerIndex = 0; layerIndex < _layers.Count; layerIndex++)
        {
            _layers[layerIndex].Clear();
        }
        OutputPose.ResetToBindPose();
        ResetRootMotionTrackingFromOutputPose();
        CurrentRootMotionDelta = RootMotionDelta.Identity;
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
    }

    public void SetLayerAnimation(
        int layerIndex,
        AnimationClip clip,
        BoneMask? mask = null,
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
            return;
        }

        if (_currentState == null)
        {
            OutputPose.ResetToBindPose();
            ResetRootMotionTrackingFromOutputPose();
            CurrentRootMotionDelta = RootMotionDelta.Identity;
            return;
        }

        var previousCurrentTime = _currentState.TimeSeconds;
        _currentState.Update(elapsedSeconds);
        for (var layerIndex = 0; layerIndex < _layers.Count; layerIndex++)
        {
            _layers[layerIndex].Update(elapsedSeconds);
        }

        if (_targetState == null)
        {
            _sampler.Sample(_currentState.Clip, _currentState.TimeSeconds, OutputPose, _currentState.Loop);
            ApplyLayers();
            DispatchAnimationEvents(_currentState, previousCurrentTime, _currentState.TimeSeconds);
            UpdateRootMotionDelta();
            return;
        }

        _targetState.Update(elapsedSeconds);
        _crossFadeElapsedSeconds += elapsedSeconds;

        _sampler.Sample(_currentState.Clip, _currentState.TimeSeconds, _sourcePose, _currentState.Loop);
        _sampler.Sample(_targetState.Clip, _targetState.TimeSeconds, _targetPose, _targetState.Loop);

        var blendWeight = _crossFadeDurationSeconds <= 0f
            ? 1f
            : Math.Clamp(_crossFadeElapsedSeconds / _crossFadeDurationSeconds, 0f, 1f);

        AnimationPoseBlender.Blend(_sourcePose, _targetPose, blendWeight, OutputPose);
        ApplyLayers();
        DispatchAnimationEvents(_currentState, previousCurrentTime, _currentState.TimeSeconds);
        UpdateRootMotionDelta();

        if (blendWeight >= 1f)
        {
            _currentState = _targetState;
            _targetState = null;
            _crossFadeDurationSeconds = 0f;
            _crossFadeElapsedSeconds = 0f;
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
        if (OutputPose.Count == 0 || RootJointIndex < 0 || RootJointIndex >= OutputPose.Count)
        {
            return BoneTransform.Identity;
        }

        return OutputPose.GetTransform(RootJointIndex);
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