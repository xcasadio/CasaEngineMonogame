using CasaEngine.Framework.Common;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Animations;

public class Animation2dData : ObjectBase
{
    public AnimationType AnimationType { get; set; } = AnimationType.Once;

    public string EventTrackName { get; set; } = string.Empty;

    public List<Animation2dPartData> Parts { get; } = new();
    public List<Animation2dTrackData> Tracks { get; } = new();
    public List<AnimationEventAsset> Events { get; } = new();

    /// <summary>
    /// Timeline of collision fixture sets, sorted by time. Step sampled: a keyframe stays active until
    /// the next one, and nothing is active before the first keyframe.
    /// </summary>
    public List<Animation2dCollisionKeyframeData> CollisionKeyframes { get; } = new();

    public string GetTrackName(int trackIndex)
    {
        if (trackIndex < 0 || trackIndex >= Tracks.Count)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(Tracks[trackIndex].Name)
            ? GetDefaultTrackName(trackIndex)
            : Tracks[trackIndex].Name;
    }

    public string GetEventTrackName()
    {
        return string.IsNullOrWhiteSpace(EventTrackName)
            ? GetDefaultLegacyEventTrackName()
            : EventTrackName;
    }

    public string GetDefaultLegacyEventTrackName()
    {
        return GetDefaultTrackName(Math.Max(Parts.Count, GetLaneIds().Count));
    }

    public void EnsureTrackNames()
    {
        for (var index = 0; index < Tracks.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(Tracks[index].LaneId))
            {
                Tracks[index].LaneId = Tracks[index].TargetPartId;
            }

            if (!string.IsNullOrWhiteSpace(Tracks[index].Name))
            {
                continue;
            }

            Tracks[index].Name = GetDefaultTrackName(index);
        }

        if (string.IsNullOrWhiteSpace(EventTrackName))
        {
            EventTrackName = GetDefaultLegacyEventTrackName();
        }
    }

    public float GetDurationSeconds()
    {
        var durationSeconds = 0f;

        foreach (var track in Tracks)
        {
            durationSeconds = MathF.Max(durationSeconds, GetLastGuidKeyframeTime(track.SpriteKeyframes));
            durationSeconds = MathF.Max(durationSeconds, GetLastVector2KeyframeTime(track.PositionKeyframes));
            durationSeconds = MathF.Max(durationSeconds, GetLastBoolKeyframeTime(track.VisibleKeyframes));
            durationSeconds = MathF.Max(durationSeconds, GetLastIntKeyframeTime(track.DrawOrderKeyframes));
            durationSeconds = MathF.Max(durationSeconds, GetLastBoolKeyframeTime(track.FlipKeyframes));
            durationSeconds = MathF.Max(durationSeconds, GetLastFloatKeyframeTime(track.RotationKeyframes));
        }

        foreach (var animationEvent in Events)
        {
            durationSeconds = MathF.Max(durationSeconds, animationEvent.TimeSeconds);
        }

        foreach (var collisionKeyframe in CollisionKeyframes)
        {
            durationSeconds = MathF.Max(durationSeconds, collisionKeyframe.TimeSeconds);
        }

        return durationSeconds;
    }

    public bool AreEventsSortedByTime()
    {
        for (var index = 1; index < Events.Count; index++)
        {
            AnimationEventAsset previous = Events[index - 1];
            AnimationEventAsset current = Events[index];
            int laneOrder = string.Compare(previous.TargetPartId, current.TargetPartId, StringComparison.Ordinal);
            if (laneOrder > 0)
            {
                return false;
            }

            if (laneOrder == 0 && previous.TimeSeconds > current.TimeSeconds)
            {
                return false;
            }
        }

        return true;
    }

    public List<string> GetInvalidTrackTargetPartIds()
    {
        var partIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in Parts)
        {
            partIds.Add(part.Id);
        }

        var invalidPartIds = new List<string>();
        foreach (var track in Tracks)
        {
            if (partIds.Contains(track.TargetPartId) || invalidPartIds.Contains(track.TargetPartId))
            {
                continue;
            }

            invalidPartIds.Add(track.TargetPartId);
        }

        return invalidPartIds;
    }

    public List<string> GetInvalidEventTargetPartIds()
    {
        var partIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in Parts)
        {
            partIds.Add(part.Id);
        }

        var invalidPartIds = new List<string>();
        foreach (var animationEvent in Events)
        {
            if (string.IsNullOrWhiteSpace(animationEvent.TargetPartId)
                || partIds.Contains(animationEvent.TargetPartId)
                || invalidPartIds.Contains(animationEvent.TargetPartId))
            {
                continue;
            }

            invalidPartIds.Add(animationEvent.TargetPartId);
        }

        return invalidPartIds;
    }

    public List<string> GetLaneIds()
    {
        var laneIds = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < Tracks.Count; index++)
        {
            string laneId = string.IsNullOrWhiteSpace(Tracks[index].LaneId)
                ? Tracks[index].TargetPartId
                : Tracks[index].LaneId;
            if (string.IsNullOrWhiteSpace(laneId) || !seen.Add(laneId))
            {
                continue;
            }

            laneIds.Add(laneId);
        }

        for (var index = 0; index < Events.Count; index++)
        {
            string laneId = Events[index].TargetPartId;
            if (string.IsNullOrWhiteSpace(laneId) || !seen.Add(laneId))
            {
                continue;
            }

            laneIds.Add(laneId);
        }

        return laneIds;
    }

    public string GetLaneDisplayName(string laneId, int laneOrdinal)
    {
        if (!string.IsNullOrWhiteSpace(laneId))
        {
            for (var index = 0; index < Parts.Count; index++)
            {
                Animation2dPartData part = Parts[index];
                if (string.Equals(part.Id, laneId, StringComparison.Ordinal))
                {
                    if (!string.IsNullOrWhiteSpace(part.Name))
                    {
                        return part.Name;
                    }

                    return $"Track {laneOrdinal + 1}";
                }
            }

            for (var index = 0; index < Tracks.Count; index++)
            {
                Animation2dTrackData track = Tracks[index];
                string trackLaneId = string.IsNullOrWhiteSpace(track.LaneId) ? track.TargetPartId : track.LaneId;
                if (string.Equals(trackLaneId, laneId, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(track.Name))
                {
                    return track.Name;
                }
            }
        }

        return $"Track {laneOrdinal + 1}";
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        AnimationType = element["animation_type"] is JToken animationTypeNode
            && Enum.TryParse(animationTypeNode.Value<string>(), true, out AnimationType animationType)
                ? animationType
                : AnimationType.Once;
        EventTrackName = element["event_track_name"]?.Value<string>() ?? string.Empty;

        Parts.Clear();
        Tracks.Clear();
        Events.Clear();
        CollisionKeyframes.Clear();

        if (element["parts"] is JArray partsNode)
        {
            foreach (var partNode in partsNode)
            {
                if (partNode is JObject partObject)
                {
                    Parts.Add(Animation2dPartData.Load(partObject));
                }
            }
        }

        if (element["tracks"] is JArray tracksNode)
        {
            foreach (var trackNode in tracksNode)
            {
                if (trackNode is JObject trackObject)
                {
                    Tracks.Add(Animation2dTrackData.Load(trackObject));
                }
            }
        }

        if (element["collision_keyframes"] is JArray collisionKeyframesNode)
        {
            foreach (var collisionKeyframeNode in collisionKeyframesNode)
            {
                if (collisionKeyframeNode is JObject collisionKeyframeObject)
                {
                    CollisionKeyframes.Add(Animation2dCollisionKeyframeData.Load(collisionKeyframeObject));
                }
            }

            SortCollisionKeyframesByTime();
        }

        if (element["events"] is not JArray eventsNode)
        {
            EnsureTrackNames();
            return;
        }

        bool inferredLoopFromLegacyRestartEvent = false;
        foreach (var eventNode in eventsNode)
        {
            if (eventNode is JObject eventObject)
            {
                AnimationEventAsset animationEvent = AnimationEventAssetJsonSerializer.Load(eventObject);
                if (string.Equals(animationEvent.EventName, Animation2dEventNames.Restart, StringComparison.OrdinalIgnoreCase))
                {
                    inferredLoopFromLegacyRestartEvent = true;
                    continue;
                }

                Events.Add(animationEvent);
            }
        }

        SortEventsByLaneAndTime();

        if (element["animation_type"] == null && inferredLoopFromLegacyRestartEvent)
        {
            AnimationType = AnimationType.Loop;
        }

        EnsureTrackNames();
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

    private static float GetLastFloatKeyframeTime(List<Animation2dFloatKeyframeData> keyframes)
    {
        return keyframes.Count == 0 ? 0f : keyframes[^1].TimeSeconds;
    }

    public void SortCollisionKeyframesByTime()
    {
        CollisionKeyframes.Sort(static (left, right) => left.TimeSeconds.CompareTo(right.TimeSeconds));
    }

    public void SortEventsByLaneAndTime()
    {
        Events.Sort(static (left, right) =>
        {
            int laneOrder = string.Compare(left.TargetPartId, right.TargetPartId, StringComparison.Ordinal);
            if (laneOrder != 0)
            {
                return laneOrder;
            }

            int timeOrder = left.TimeSeconds.CompareTo(right.TimeSeconds);
            if (timeOrder != 0)
            {
                return timeOrder;
            }

            return string.Compare(left.EventName, right.EventName, StringComparison.Ordinal);
        });
    }

    private static string GetDefaultTrackName(int trackIndex)
    {
        return $"Track {trackIndex + 1}";
    }
}