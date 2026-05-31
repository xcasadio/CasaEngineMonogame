namespace CasaEngine.Framework.Assets.Animations;

public static class Animation2dCompositionAdapter
{
    public const string LegacyPartId = "legacy";

    public static Animation2dCompositionData Create(Animation2dData animationData)
    {
        ArgumentNullException.ThrowIfNull(animationData);

        return animationData.Parts.Count > 0 || animationData.Tracks.Count > 0
            ? CreateFromComposedData(animationData)
            : CreateFromLegacyFrames(animationData);
    }

    private static Animation2dCompositionData CreateFromComposedData(Animation2dData animationData)
    {
        var parts = new List<Animation2dPartData>(animationData.Parts.Count);
        foreach (var part in animationData.Parts)
        {
            parts.Add(ClonePart(part));
        }

        var tracks = new List<Animation2dTrackData>(animationData.Tracks.Count);
        foreach (var track in animationData.Tracks)
        {
            tracks.Add(CloneTrack(track));
        }

        return new Animation2dCompositionData(
            animationData.AnimationType,
            CalculateDurationSeconds(animationData.Tracks, animationData.Events),
            parts,
            tracks,
            CopyEvents(animationData.Events));
    }

    private static Animation2dCompositionData CreateFromLegacyFrames(Animation2dData animationData)
    {
        var parts = new List<Animation2dPartData>(animationData.Frames.Count > 0 ? 1 : 0);
        var tracks = new List<Animation2dTrackData>(animationData.Frames.Count > 0 ? 1 : 0);
        var durationSeconds = 0f;

        if (animationData.Frames.Count > 0)
        {
            parts.Add(new Animation2dPartData
            {
                Id = LegacyPartId,
                Name = "Legacy",
                DefaultSpriteId = animationData.Frames[0].SpriteId,
                DefaultVisible = true,
            });

            var spriteTrack = new Animation2dTrackData
            {
                TargetPartId = LegacyPartId,
                Property = Animation2dTrackProperty.Sprite,
            };

            foreach (var frame in animationData.Frames)
            {
                spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(durationSeconds, frame.SpriteId));
                durationSeconds += frame.Duration;
            }

            tracks.Add(spriteTrack);
        }

        return new Animation2dCompositionData(
            animationData.AnimationType,
            durationSeconds,
            parts,
            tracks,
            CopyEvents(animationData.Events));
    }

    private static Animation2dPartData ClonePart(Animation2dPartData part)
    {
        return new Animation2dPartData
        {
            Id = part.Id,
            Name = part.Name,
            DefaultSpriteId = part.DefaultSpriteId,
            DefaultPosition = part.DefaultPosition,
            DefaultDrawOrder = part.DefaultDrawOrder,
            DefaultVisible = part.DefaultVisible,
            DefaultFlipX = part.DefaultFlipX,
            DefaultFlipY = part.DefaultFlipY,
        };
    }

    private static Animation2dTrackData CloneTrack(Animation2dTrackData track)
    {
        var clone = new Animation2dTrackData
        {
            TargetPartId = track.TargetPartId,
            Property = track.Property,
            Interpolation = track.Interpolation,
        };

        foreach (var keyframe in track.SpriteKeyframes)
        {
            clone.SpriteKeyframes.Add(keyframe);
        }

        foreach (var keyframe in track.PositionKeyframes)
        {
            clone.PositionKeyframes.Add(keyframe);
        }

        foreach (var keyframe in track.VisibleKeyframes)
        {
            clone.VisibleKeyframes.Add(keyframe);
        }

        foreach (var keyframe in track.DrawOrderKeyframes)
        {
            clone.DrawOrderKeyframes.Add(keyframe);
        }

        foreach (var keyframe in track.FlipKeyframes)
        {
            clone.FlipKeyframes.Add(keyframe);
        }

        return clone;
    }

    private static List<AnimationEventAsset> CopyEvents(List<AnimationEventAsset> events)
    {
        var copy = new List<AnimationEventAsset>(events.Count);
        foreach (var animationEvent in events)
        {
            copy.Add(animationEvent);
        }

        return copy;
    }

    private static float CalculateDurationSeconds(List<Animation2dTrackData> tracks, List<AnimationEventAsset> events)
    {
        var durationSeconds = 0f;

        foreach (var track in tracks)
        {
            durationSeconds = MathF.Max(durationSeconds, GetLastGuidKeyframeTime(track.SpriteKeyframes));
            durationSeconds = MathF.Max(durationSeconds, GetLastVector2KeyframeTime(track.PositionKeyframes));
            durationSeconds = MathF.Max(durationSeconds, GetLastBoolKeyframeTime(track.VisibleKeyframes));
            durationSeconds = MathF.Max(durationSeconds, GetLastIntKeyframeTime(track.DrawOrderKeyframes));
            durationSeconds = MathF.Max(durationSeconds, GetLastBoolKeyframeTime(track.FlipKeyframes));
        }

        foreach (var animationEvent in events)
        {
            durationSeconds = MathF.Max(durationSeconds, animationEvent.TimeSeconds);
        }

        return durationSeconds;
    }

    private static float GetLastGuidKeyframeTime(List<Animation2dGuidKeyframeData> keyframes)
    {
        return keyframes.Count == 0 ? 0f : keyframes[^1].TimeSeconds;
    }

    private static float GetLastVector2KeyframeTime(List<Animation2dVector2KeyframeData> keyframes)
    {
        return keyframes.Count == 0 ? 0f : keyframes[^1].TimeSeconds;
    }

    private static float GetLastBoolKeyframeTime(List<Animation2dBoolKeyframeData> keyframes)
    {
        return keyframes.Count == 0 ? 0f : keyframes[^1].TimeSeconds;
    }

    private static float GetLastIntKeyframeTime(List<Animation2dIntKeyframeData> keyframes)
    {
        return keyframes.Count == 0 ? 0f : keyframes[^1].TimeSeconds;
    }
}