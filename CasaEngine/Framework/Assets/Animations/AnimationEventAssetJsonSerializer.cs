using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Animations;

public static class AnimationEventAssetJsonSerializer
{
    public static JObject Save(AnimationEventAsset animationEvent)
    {
        return new JObject
        {
            ["time_seconds"] = animationEvent.TimeSeconds,
            ["event_name"] = animationEvent.EventName,
        };
    }

    public static AnimationEventAsset Load(JObject node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return new AnimationEventAsset(
            node["time_seconds"]?.Value<float>() ?? 0f,
            node["event_name"]?.Value<string>() ?? string.Empty);
    }
}