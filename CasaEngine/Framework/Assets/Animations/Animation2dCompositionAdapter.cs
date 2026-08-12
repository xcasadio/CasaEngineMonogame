using CasaEngine.Engine.Physics;

namespace CasaEngine.Framework.Assets.Animations;

public static class Animation2dCompositionAdapter
{
    public static Animation2dCompositionData Create(Animation2dData animationData)
    {
        ArgumentNullException.ThrowIfNull(animationData);

        var copiedEvents = CopyEvents(animationData.Events);

        return new Animation2dCompositionData(
            animationData.GetDurationSeconds(),
            animationData.AnimationType,
            CopyParts(animationData.Parts),
            CopyTracks(animationData.Tracks),
            copiedEvents,
            CopyCollisionKeyframes(animationData.CollisionKeyframes));
    }

    private static List<Animation2dCollisionKeyframeData> CopyCollisionKeyframes(List<Animation2dCollisionKeyframeData> collisionKeyframes)
    {
        var copy = new List<Animation2dCollisionKeyframeData>(collisionKeyframes.Count);
        foreach (var collisionKeyframe in collisionKeyframes)
        {
            copy.Add(CloneCollisionKeyframe(collisionKeyframe));
        }

        copy.Sort(static (left, right) => left.TimeSeconds.CompareTo(right.TimeSeconds));
        return copy;
    }

    private static Animation2dCollisionKeyframeData CloneCollisionKeyframe(Animation2dCollisionKeyframeData collisionKeyframe)
    {
        var clone = new Animation2dCollisionKeyframeData
        {
            TimeSeconds = collisionKeyframe.TimeSeconds,
        };

        foreach (var fixture in collisionKeyframe.Fixtures)
        {
            clone.Fixtures.Add(new ColliderFixture(fixture));
        }

        return clone;
    }

    private static List<Animation2dPartData> CopyParts(List<Animation2dPartData> parts)
    {
        var copy = new List<Animation2dPartData>(parts.Count);
        foreach (var part in parts)
        {
            copy.Add(ClonePart(part));
        }

        return copy;
    }

    private static List<Animation2dTrackData> CopyTracks(List<Animation2dTrackData> tracks)
    {
        var copy = new List<Animation2dTrackData>(tracks.Count);
        foreach (var track in tracks)
        {
            copy.Add(CloneTrack(track));
        }

        return copy;
    }

    private static Animation2dPartData ClonePart(Animation2dPartData part)
    {
        return new Animation2dPartData
        {
            Id = part.Id,
            Name = part.Name,
            DefaultSpriteId = part.DefaultSpriteId,
            DefaultPosition = part.DefaultPosition,
            DefaultRotation = part.DefaultRotation,
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
            LaneId = track.LaneId,
            Name = track.Name,
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

        foreach (var keyframe in track.RotationKeyframes)
        {
            clone.RotationKeyframes.Add(keyframe);
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
}