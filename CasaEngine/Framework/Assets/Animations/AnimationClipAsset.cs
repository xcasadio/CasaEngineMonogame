using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Animations;

public sealed class AnimationClipAsset : ObjectBase
{
    public Guid SkeletonAssetId { get; set; } = Guid.Empty;

    public float DurationSeconds { get; set; }

    public List<AnimationJointTrackAsset> JointTracks { get; } = new();

    public List<AnimationEventAsset> Events { get; } = new();

    public override void Load(JObject element)
    {
        base.Load(element);
        AnimationClipAssetJsonSerializer.Load(this, element);
    }
}

public sealed class AnimationJointTrackAsset
{
    public string JointName { get; set; } = string.Empty;

    public List<Vector3AnimationKeyframeAsset> TranslationKeyframes { get; } = new();

    public List<QuaternionAnimationKeyframeAsset> RotationKeyframes { get; } = new();

    public List<Vector3AnimationKeyframeAsset> ScaleKeyframes { get; } = new();
}

public readonly record struct Vector3AnimationKeyframeAsset(float TimeSeconds, Vector3 Value);

public readonly record struct QuaternionAnimationKeyframeAsset(float TimeSeconds, Quaternion Value);

public readonly record struct AnimationEventAsset(float TimeSeconds, string EventName);

public static class AnimationClipAssetJsonSerializer
{
    public static void Save(AnimationClipAsset animationClipAsset, JObject node)
    {
        ArgumentNullException.ThrowIfNull(animationClipAsset);
        ArgumentNullException.ThrowIfNull(node);

        node["id"] = animationClipAsset.Id.ToString();
        node["name"] = animationClipAsset.Name;
        node["skeleton_asset_id"] = animationClipAsset.SkeletonAssetId.ToString();
        node["duration_seconds"] = animationClipAsset.DurationSeconds;

        var jointTracksNode = new JArray();
        for (var index = 0; index < animationClipAsset.JointTracks.Count; index++)
        {
            jointTracksNode.Add(SaveJointTrack(animationClipAsset.JointTracks[index]));
        }

        node["joint_tracks"] = jointTracksNode;

        var eventsNode = new JArray();
        for (var index = 0; index < animationClipAsset.Events.Count; index++)
        {
            eventsNode.Add(SaveEvent(animationClipAsset.Events[index]));
        }

        node["events"] = eventsNode;
    }

    public static void Load(AnimationClipAsset animationClipAsset, JObject node)
    {
        ArgumentNullException.ThrowIfNull(animationClipAsset);
        ArgumentNullException.ThrowIfNull(node);

        animationClipAsset.SkeletonAssetId = node["skeleton_asset_id"]?.GetGuid() ?? Guid.Empty;
        animationClipAsset.DurationSeconds = node["duration_seconds"]?.Value<float>() ?? 0f;

        animationClipAsset.JointTracks.Clear();
        if (node["joint_tracks"] is JArray jointTracksNode)
        {
            for (var index = 0; index < jointTracksNode.Count; index++)
            {
                if (jointTracksNode[index] is JObject jointTrackNode)
                {
                    animationClipAsset.JointTracks.Add(LoadJointTrack(jointTrackNode));
                }
            }
        }

        animationClipAsset.Events.Clear();
        if (node["events"] is JArray eventsNode)
        {
            for (var index = 0; index < eventsNode.Count; index++)
            {
                if (eventsNode[index] is JObject eventNode)
                {
                    animationClipAsset.Events.Add(LoadEvent(eventNode));
                }
            }
        }
    }

    private static JObject SaveJointTrack(AnimationJointTrackAsset track)
    {
        ArgumentNullException.ThrowIfNull(track);

        return new JObject
        {
            ["joint_name"] = track.JointName,
            ["translation_keyframes"] = SaveVector3Keyframes(track.TranslationKeyframes),
            ["rotation_keyframes"] = SaveQuaternionKeyframes(track.RotationKeyframes),
            ["scale_keyframes"] = SaveVector3Keyframes(track.ScaleKeyframes),
        };
    }

    private static AnimationJointTrackAsset LoadJointTrack(JObject node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var track = new AnimationJointTrackAsset
        {
            JointName = node["joint_name"]?.Value<string>() ?? string.Empty,
        };

        LoadVector3Keyframes(node["translation_keyframes"] as JArray, track.TranslationKeyframes);
        LoadQuaternionKeyframes(node["rotation_keyframes"] as JArray, track.RotationKeyframes);
        LoadVector3Keyframes(node["scale_keyframes"] as JArray, track.ScaleKeyframes);
        return track;
    }

    private static JArray SaveVector3Keyframes(IReadOnlyList<Vector3AnimationKeyframeAsset> keyframes)
    {
        var keyframesNode = new JArray();
        for (var index = 0; index < keyframes.Count; index++)
        {
            var keyframe = keyframes[index];
            keyframesNode.Add(new JObject
            {
                ["time_seconds"] = keyframe.TimeSeconds,
                ["value"] = AnimationAuthoringJsonSerialization.SaveVector3(keyframe.Value),
            });
        }

        return keyframesNode;
    }

    private static void LoadVector3Keyframes(JArray? keyframesNode, List<Vector3AnimationKeyframeAsset> keyframes)
    {
        keyframes.Clear();
        if (keyframesNode is null)
        {
            return;
        }

        for (var index = 0; index < keyframesNode.Count; index++)
        {
            if (keyframesNode[index] is not JObject keyframeNode)
            {
                continue;
            }

            var value = keyframeNode["value"] is { } valueToken
                ? valueToken.GetVector3()
                : Vector3.Zero;
            keyframes.Add(new Vector3AnimationKeyframeAsset(
                keyframeNode["time_seconds"]?.Value<float>() ?? 0f,
                value));
        }
    }

    private static JArray SaveQuaternionKeyframes(IReadOnlyList<QuaternionAnimationKeyframeAsset> keyframes)
    {
        var keyframesNode = new JArray();
        for (var index = 0; index < keyframes.Count; index++)
        {
            var keyframe = keyframes[index];
            keyframesNode.Add(new JObject
            {
                ["time_seconds"] = keyframe.TimeSeconds,
                ["value"] = AnimationAuthoringJsonSerialization.SaveQuaternion(keyframe.Value),
            });
        }

        return keyframesNode;
    }

    private static void LoadQuaternionKeyframes(JArray? keyframesNode, List<QuaternionAnimationKeyframeAsset> keyframes)
    {
        keyframes.Clear();
        if (keyframesNode is null)
        {
            return;
        }

        for (var index = 0; index < keyframesNode.Count; index++)
        {
            if (keyframesNode[index] is not JObject keyframeNode)
            {
                continue;
            }

            var value = keyframeNode["value"] is { } valueToken
                ? valueToken.GetQuaternion()
                : Quaternion.Identity;
            keyframes.Add(new QuaternionAnimationKeyframeAsset(
                keyframeNode["time_seconds"]?.Value<float>() ?? 0f,
                value));
        }
    }

    private static JObject SaveEvent(AnimationEventAsset animationEvent)
    {
        return new JObject
        {
            ["time_seconds"] = animationEvent.TimeSeconds,
            ["event_name"] = animationEvent.EventName,
        };
    }

    private static AnimationEventAsset LoadEvent(JObject node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return new AnimationEventAsset(
            node["time_seconds"]?.Value<float>() ?? 0f,
            node["event_name"]?.Value<string>() ?? string.Empty);
    }
}