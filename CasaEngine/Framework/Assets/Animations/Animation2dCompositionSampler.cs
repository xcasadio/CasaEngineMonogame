namespace CasaEngine.Framework.Assets.Animations;

public sealed class Animation2dCompositionSampler
{
    private readonly Animation2dCompositionData _composition;

    public Animation2dCompositionRuntimeState RuntimeState { get; } = new();

    public event Action<AnimationEventAsset> AnimationEventTriggered;

    public float CurrentTime { get; private set; }

    public bool IsFinished { get; private set; }

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
        CurrentTime = MathF.Max(0f, timeSeconds);
        IsFinished = _composition.AnimationType == AnimationType.Once
            && _composition.DurationSeconds > 0f
            && CurrentTime >= _composition.DurationSeconds;
        ApplyTracks(GetSampleTime(CurrentTime));
    }

    public bool Update(float elapsedTime)
    {
        if (_composition.DurationSeconds <= 0f)
        {
            IsFinished = _composition.AnimationType == AnimationType.Once;
            ApplyTracks(0f);
            return IsFinished;
        }

        var previousTime = CurrentTime;
        CurrentTime += MathF.Max(0f, elapsedTime);
        if (_composition.AnimationType == AnimationType.Once && CurrentTime > _composition.DurationSeconds)
        {
            CurrentTime = _composition.DurationSeconds;
            IsFinished = true;
        }

        DispatchEvents(previousTime, CurrentTime);
        ApplyTracks(GetSampleTime(CurrentTime));
        return IsFinished;
    }

    private void DispatchEvents(float previousTime, float currentTime)
    {
        var handler = AnimationEventTriggered;
        if (handler == null || _composition.Events.Count == 0)
        {
            return;
        }

        if (_composition.AnimationType == AnimationType.Loop && currentTime > _composition.DurationSeconds)
        {
            var previousSampleTime = GetSampleTime(previousTime);
            var currentSampleTime = GetSampleTime(currentTime);
            if (currentSampleTime < previousSampleTime || currentTime - previousTime > _composition.DurationSeconds)
            {
                DispatchEventRange(handler, previousSampleTime, _composition.DurationSeconds);
                DispatchEventRange(handler, 0f, currentSampleTime);
                return;
            }
        }

        DispatchEventRange(handler, GetSampleTime(previousTime), GetSampleTime(currentTime));
    }

    private void DispatchEventRange(Action<AnimationEventAsset> handler, float startExclusive, float endInclusive)
    {
        foreach (var animationEvent in _composition.Events)
        {
            if (animationEvent.TimeSeconds > startExclusive && animationEvent.TimeSeconds <= endInclusive)
            {
                handler(animationEvent);
            }
        }
    }

    private float GetSampleTime(float timeSeconds)
    {
        var durationSeconds = _composition.DurationSeconds;
        if (durationSeconds <= 0f || timeSeconds <= durationSeconds)
        {
            return timeSeconds;
        }

        if (_composition.AnimationType == AnimationType.Loop)
        {
            var wrappedTime = timeSeconds % durationSeconds;
            return wrappedTime == 0f ? durationSeconds : wrappedTime;
        }

        if (_composition.AnimationType == AnimationType.PingPong)
        {
            var pingPongTime = timeSeconds;
            var pingPongState = 0;
            while (pingPongTime > durationSeconds)
            {
                pingPongTime -= durationSeconds;
                pingPongState = 1 - pingPongState;
            }

            return pingPongState == 1 ? durationSeconds - pingPongTime : pingPongTime;
        }

        return durationSeconds;
    }

    private void ApplyTracks(float sampleTime)
    {
        RuntimeState.ApplyDefaults(_composition);

        foreach (var track in _composition.Tracks)
        {
            if (!RuntimeState.TryGetPart(track.TargetPartId, out var part))
            {
                continue;
            }

            ApplyTrack(track, part, sampleTime);
        }
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
