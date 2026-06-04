using CasaEngine.Framework.Common;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Animations;

public class Animation2dData : ObjectBase
{
    public List<Animation2dPartData> Parts { get; } = new();
    public List<Animation2dTrackData> Tracks { get; } = new();
    public List<AnimationEventAsset> Events { get; } = new();

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

        bool shouldAppendLegacyRestartEvent = false;
        if (element["animation_type"] is JToken animationTypeNode
            && Enum.TryParse(animationTypeNode.Value<string>(), true, out AnimationType animationType)
            && animationType == AnimationType.Loop)
        {
            shouldAppendLegacyRestartEvent = true;
        }

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
            TryAppendLegacyRestartEvent(shouldAppendLegacyRestartEvent);
            return;
        }

        foreach (var eventNode in eventsNode)
        {
            if (eventNode is JObject eventObject)
            {
                Events.Add(AnimationEventAssetJsonSerializer.Load(eventObject));
            }
        }

        TryAppendLegacyRestartEvent(shouldAppendLegacyRestartEvent);
    }

    private void TryAppendLegacyRestartEvent(bool shouldAppendLegacyRestartEvent)
    {
        if (!shouldAppendLegacyRestartEvent || HasRestartEvent())
        {
            return;
        }

        float durationSeconds = GetDurationSeconds();
        if (durationSeconds <= 0f)
        {
            return;
        }

        Events.Add(new AnimationEventAsset(durationSeconds, Animation2dEventNames.Restart));
    }

    private bool HasRestartEvent()
    {
        for (var index = 0; index < Events.Count; index++)
        {
            if (string.Equals(Events[index].EventName, Animation2dEventNames.Restart, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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