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
            ? GetDefaultTrackName(Tracks.Count)
            : EventTrackName;
    }

    public void EnsureTrackNames()
    {
        for (var index = 0; index < Tracks.Count; index++)
        {
            if (!string.IsNullOrWhiteSpace(Tracks[index].Name))
            {
                continue;
            }

            Tracks[index].Name = GetDefaultTrackName(index);
        }

        if (string.IsNullOrWhiteSpace(EventTrackName))
        {
            EventTrackName = GetDefaultTrackName(Tracks.Count);
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

        return durationSeconds;
    }

    public bool AreEventsSortedByTime()
    {
        for (var index = 1; index < Events.Count; index++)
        {
            if (Events[index - 1].TimeSeconds > Events[index].TimeSeconds)
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

    private static string GetDefaultTrackName(int trackIndex)
    {
        return $"track {trackIndex + 1:00}";
    }
}