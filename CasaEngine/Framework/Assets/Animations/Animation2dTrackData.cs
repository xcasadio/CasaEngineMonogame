using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using CasaEngine.Core.Serialization;

namespace CasaEngine.Framework.Assets.Animations;

public enum Animation2dTrackProperty
{
    Sprite,
    Position,
    Visible,
    DrawOrder,
    FlipX,
    FlipY,
    Rotation
}

public enum Animation2dInterpolationMode
{
    Step
}

public sealed class Animation2dTrackData
{
    public string LaneId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string TargetPartId { get; set; } = string.Empty;

    public Animation2dTrackProperty Property { get; set; }

    public Animation2dInterpolationMode Interpolation { get; set; } = Animation2dInterpolationMode.Step;

    public List<Animation2dGuidKeyframeData> SpriteKeyframes { get; } = new();

    public List<Animation2dVector2KeyframeData> PositionKeyframes { get; } = new();

    public List<Animation2dBoolKeyframeData> VisibleKeyframes { get; } = new();

    public List<Animation2dIntKeyframeData> DrawOrderKeyframes { get; } = new();

    public List<Animation2dBoolKeyframeData> FlipKeyframes { get; } = new();

    public List<Animation2dFloatKeyframeData> RotationKeyframes { get; } = new();

    public static Animation2dTrackData Load(JObject node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var track = new Animation2dTrackData
        {
            LaneId = node["lane_id"]?.Value<string>() ?? string.Empty,
            Name = node["name"]?.Value<string>() ?? string.Empty,
            TargetPartId = node["target_part_id"]?.Value<string>() ?? string.Empty,
            Property = node["property"]?.GetEnum<Animation2dTrackProperty>() ?? Animation2dTrackProperty.Sprite,
            Interpolation = node["interpolation"]?.GetEnum<Animation2dInterpolationMode>() ?? Animation2dInterpolationMode.Step,
        };

        LoadGuidKeyframes(node["sprite_keyframes"] as JArray, track.SpriteKeyframes);
        LoadVector2Keyframes(node["position_keyframes"] as JArray, track.PositionKeyframes);
        LoadBoolKeyframes(node["visible_keyframes"] as JArray, track.VisibleKeyframes);
        LoadIntKeyframes(node["draw_order_keyframes"] as JArray, track.DrawOrderKeyframes);
        LoadBoolKeyframes(node["flip_keyframes"] as JArray, track.FlipKeyframes);
        LoadFloatKeyframes(node["rotation_keyframes"] as JArray, track.RotationKeyframes);
        return track;
    }

    private static void LoadGuidKeyframes(JArray keyframesNode, List<Animation2dGuidKeyframeData> keyframes)
    {
        if (keyframesNode == null)
        {
            return;
        }

        foreach (var keyframeNode in keyframesNode)
        {
            if (keyframeNode is JObject keyframeObject)
            {
                keyframes.Add(new Animation2dGuidKeyframeData(
                    keyframeObject["time_seconds"]?.Value<float>() ?? 0f,
                    keyframeObject["value"]?.GetGuid() ?? Guid.Empty));
            }
        }
    }

    private static void LoadVector2Keyframes(JArray keyframesNode, List<Animation2dVector2KeyframeData> keyframes)
    {
        if (keyframesNode == null)
        {
            return;
        }

        foreach (var keyframeNode in keyframesNode)
        {
            if (keyframeNode is JObject keyframeObject)
            {
                keyframes.Add(new Animation2dVector2KeyframeData(
                    keyframeObject["time_seconds"]?.Value<float>() ?? 0f,
                    keyframeObject["value"]?.GetVector2() ?? Vector2.Zero));
            }
        }
    }

    private static void LoadBoolKeyframes(JArray keyframesNode, List<Animation2dBoolKeyframeData> keyframes)
    {
        if (keyframesNode == null)
        {
            return;
        }

        foreach (var keyframeNode in keyframesNode)
        {
            if (keyframeNode is JObject keyframeObject)
            {
                keyframes.Add(new Animation2dBoolKeyframeData(
                    keyframeObject["time_seconds"]?.Value<float>() ?? 0f,
                    keyframeObject["value"]?.Value<bool>() ?? false));
            }
        }
    }

    private static void LoadIntKeyframes(JArray keyframesNode, List<Animation2dIntKeyframeData> keyframes)
    {
        if (keyframesNode == null)
        {
            return;
        }

        foreach (var keyframeNode in keyframesNode)
        {
            if (keyframeNode is JObject keyframeObject)
            {
                keyframes.Add(new Animation2dIntKeyframeData(
                    keyframeObject["time_seconds"]?.Value<float>() ?? 0f,
                    keyframeObject["value"]?.Value<int>() ?? 0));
            }
        }
    }

    private static void LoadFloatKeyframes(JArray keyframesNode, List<Animation2dFloatKeyframeData> keyframes)
    {
        if (keyframesNode == null)
        {
            return;
        }

        foreach (var keyframeNode in keyframesNode)
        {
            if (keyframeNode is JObject keyframeObject)
            {
                keyframes.Add(new Animation2dFloatKeyframeData(
                    keyframeObject["time_seconds"]?.Value<float>() ?? 0f,
                    keyframeObject["value"]?.Value<float>() ?? 0f));
            }
        }
    }
}

public readonly record struct Animation2dGuidKeyframeData(float TimeSeconds, Guid Value);

public readonly record struct Animation2dVector2KeyframeData(float TimeSeconds, Vector2 Value);

public readonly record struct Animation2dBoolKeyframeData(float TimeSeconds, bool Value);

public readonly record struct Animation2dIntKeyframeData(float TimeSeconds, int Value);

public readonly record struct Animation2dFloatKeyframeData(float TimeSeconds, float Value);