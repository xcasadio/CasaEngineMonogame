namespace CasaEngine.Framework.Assets.Animations;

public sealed class Animation2dCompositionSampler
{
    private readonly Animation2dCompositionData _composition;

    public Animation2dCompositionRuntimeState RuntimeState { get; } = new();

    public event Action<AnimationEventAsset> AnimationEventTriggered;

    public float CurrentTime { get; private set; }

    public bool IsFinished { get; private set; }

    /// <summary>
    /// Index in <see cref="CollisionKeyframes"/> of the fixture set active at <see cref="CurrentTime"/>,
    /// or -1 before the first keyframe. Step semantics: the last keyframe whose time is at or before
    /// the current time.
    /// </summary>
    public int CurrentCollisionKeyframeIndex { get; private set; } = -1;

    public IReadOnlyList<Animation2dCollisionKeyframeData> CollisionKeyframes => _composition.CollisionKeyframes;

    public Animation2dCompositionSampler(Animation2dCompositionData composition)
    {
        _composition = composition ?? throw new ArgumentNullException(nameof(composition));
        Reset();
    }

    public void Reset()
    {
        CurrentTime = 0f;
        IsFinished = false;
        RuntimeState.Reset(_composition);
        ApplyTracks(0f);
    }

    public void Seek(float timeSeconds)
    {
        float actualTimeSeconds = MathF.Max(0f, timeSeconds);
        CurrentTime = NormalizeTime(actualTimeSeconds);
        IsFinished = _composition.AnimationType != AnimationType.Loop
            && _composition.DurationSeconds > 0f
            && actualTimeSeconds >= _composition.DurationSeconds;
        ApplyTracks(CurrentTime);
    }

    public bool Update(float elapsedTime)
    {
        if (_composition.AnimationType == AnimationType.Loop)
        {
            UpdateLooping(MathF.Max(0f, elapsedTime));
            return false;
        }

        if (_composition.DurationSeconds <= 0f)
        {
            IsFinished = true;
            ApplyTracks(0f);
            return IsFinished;
        }

        var previousTime = CurrentTime;
        CurrentTime += MathF.Max(0f, elapsedTime);
        if (CurrentTime > _composition.DurationSeconds)
        {
            CurrentTime = _composition.DurationSeconds;
            IsFinished = true;
        }

        DispatchEventRange(AnimationEventTriggered, previousTime, CurrentTime);
        ApplyTracks(CurrentTime);
        return IsFinished;
    }

    private void UpdateLooping(float elapsedTime)
    {
        var handler = AnimationEventTriggered;
        float restartTimeSeconds = _composition.DurationSeconds;
        if (restartTimeSeconds <= 0f)
        {
            CurrentTime = 0f;
            IsFinished = false;
            ApplyTracks(0f);
            return;
        }

        float previousTime = CurrentTime;
        float remainingTime = elapsedTime;
        while (remainingTime > 0f)
        {
            float timeUntilRestart = restartTimeSeconds - previousTime;
            if (timeUntilRestart <= 0f)
            {
                timeUntilRestart = restartTimeSeconds;
            }

            if (remainingTime < timeUntilRestart)
            {
                float currentTime = previousTime + remainingTime;
                DispatchEventRange(handler, previousTime, currentTime);
                previousTime = currentTime;
                remainingTime = 0f;
                break;
            }

            DispatchEventRange(handler, previousTime, restartTimeSeconds);
            remainingTime -= timeUntilRestart;
            previousTime = 0f;
        }

        CurrentTime = previousTime;
        IsFinished = false;
        ApplyTracks(CurrentTime);
    }

    private void DispatchEventRange(Action<AnimationEventAsset>? handler, float startExclusive, float endInclusive)
    {
        if (handler == null || _composition.Events.Count == 0)
        {
            return;
        }

        foreach (var animationEvent in _composition.Events)
        {
            if (animationEvent.TimeSeconds > startExclusive && animationEvent.TimeSeconds <= endInclusive)
            {
                handler(animationEvent);
            }
        }
    }

    private float NormalizeTime(float timeSeconds)
    {
        if (_composition.AnimationType == AnimationType.Loop)
        {
            return WrapLoopTime(timeSeconds, _composition.DurationSeconds);
        }

        if (_composition.DurationSeconds <= 0f)
        {
            return 0f;
        }

        return MathF.Min(timeSeconds, _composition.DurationSeconds);
    }

    private static float WrapLoopTime(float timeSeconds, float restartTimeSeconds)
    {
        if (restartTimeSeconds <= 0f || timeSeconds <= restartTimeSeconds)
        {
            return timeSeconds;
        }

        return timeSeconds % restartTimeSeconds;
    }

    private void ApplyTracks(float sampleTime)
    {
        CurrentCollisionKeyframeIndex = EvaluateCollisionKeyframeIndex(sampleTime);
        RuntimeState.ApplyDefaults(_composition);

        foreach (var track in _composition.Tracks)
        {
            if (!RuntimeState.TryGetPart(track.TargetPartId, out var part))
            {
                continue;
            }

            ApplyTrack(track, part, sampleTime);
        }

        RuntimeState.UpdateDrawOrder();
    }

    private int EvaluateCollisionKeyframeIndex(float sampleTime)
    {
        var collisionKeyframes = _composition.CollisionKeyframes;
        int activeIndex = -1;

        for (var index = 0; index < collisionKeyframes.Count; index++)
        {
            if (collisionKeyframes[index].TimeSeconds > sampleTime)
            {
                break;
            }

            activeIndex = index;
        }

        return activeIndex;
    }

    private static void ApplyTrack(Animation2dTrackData track, Animation2dPartRuntimeState part, float sampleTime)
    {
        if (track.Interpolation != Animation2dInterpolationMode.Step)
        {
            throw new NotSupportedException($"Animation2d interpolation '{track.Interpolation}' is not supported.");
        }

        switch (track.Property)
        {
            case Animation2dTrackProperty.Sprite:
                if (TryEvaluateGuid(track.SpriteKeyframes, sampleTime, out var spriteId))
                {
                    part.SpriteId = spriteId;
                }
                break;
            case Animation2dTrackProperty.Position:
                if (TryEvaluateVector2(track.PositionKeyframes, sampleTime, out var position))
                {
                    part.Position = position;
                }
                break;
            case Animation2dTrackProperty.Visible:
                if (TryEvaluateBool(track.VisibleKeyframes, sampleTime, out var visible))
                {
                    part.Visible = visible;
                }
                break;
            case Animation2dTrackProperty.DrawOrder:
                if (TryEvaluateInt(track.DrawOrderKeyframes, sampleTime, out var drawOrder))
                {
                    part.DrawOrder = drawOrder;
                }
                break;
            case Animation2dTrackProperty.FlipX:
            case Animation2dTrackProperty.FlipY:
                if (TryEvaluateBool(track.FlipKeyframes, sampleTime, out var flip))
                {
                    if (track.Property == Animation2dTrackProperty.FlipX)
                    {
                        part.FlipX = flip;
                    }
                    else
                    {
                        part.FlipY = flip;
                    }
                }
                break;
            case Animation2dTrackProperty.Rotation:
                if (TryEvaluateFloat(track.RotationKeyframes, sampleTime, out var rotation))
                {
                    part.Rotation = rotation;
                }
                break;
            default:
                throw new NotSupportedException($"Animation2d track property '{track.Property}' is not supported.");
        }
    }

    private static bool TryEvaluateGuid(List<Animation2dGuidKeyframeData> keyframes, float sampleTime, out Guid value)
    {
        value = Guid.Empty;
        var hasValue = false;
        for (var index = 0; index < keyframes.Count; index++)
        {
            var keyframe = keyframes[index];
            if (keyframe.TimeSeconds > sampleTime)
            {
                break;
            }

            value = keyframe.Value;
            hasValue = true;
        }

        return hasValue;
    }

    private static bool TryEvaluateVector2(List<Animation2dVector2KeyframeData> keyframes, float sampleTime, out Microsoft.Xna.Framework.Vector2 value)
    {
        value = Microsoft.Xna.Framework.Vector2.Zero;
        var hasValue = false;
        for (var index = 0; index < keyframes.Count; index++)
        {
            var keyframe = keyframes[index];
            if (keyframe.TimeSeconds > sampleTime)
            {
                break;
            }

            value = keyframe.Value;
            hasValue = true;
        }

        return hasValue;
    }

    private static bool TryEvaluateBool(List<Animation2dBoolKeyframeData> keyframes, float sampleTime, out bool value)
    {
        value = false;
        var hasValue = false;
        for (var index = 0; index < keyframes.Count; index++)
        {
            var keyframe = keyframes[index];
            if (keyframe.TimeSeconds > sampleTime)
            {
                break;
            }

            value = keyframe.Value;
            hasValue = true;
        }

        return hasValue;
    }

    private static bool TryEvaluateFloat(List<Animation2dFloatKeyframeData> keyframes, float sampleTime, out float value)
    {
        value = 0f;
        var hasValue = false;
        for (var index = 0; index < keyframes.Count; index++)
        {
            var keyframe = keyframes[index];
            if (keyframe.TimeSeconds > sampleTime)
            {
                break;
            }

            value = keyframe.Value;
            hasValue = true;
        }

        return hasValue;
    }

    private static bool TryEvaluateInt(List<Animation2dIntKeyframeData> keyframes, float sampleTime, out int value)
    {
        value = 0;
        var hasValue = false;
        for (var index = 0; index < keyframes.Count; index++)
        {
            var keyframe = keyframes[index];
            if (keyframe.TimeSeconds > sampleTime)
            {
                break;
            }

            value = keyframe.Value;
            hasValue = true;
        }

        return hasValue;
    }
}
