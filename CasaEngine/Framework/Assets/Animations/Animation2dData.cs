
using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Animations;

public class Animation2dData : AnimationData
{
    public List<FrameData> Frames { get; } = new();

    public override void Load(JObject element)
    {
        base.Load(element);

        foreach (var frameNode in element["frames"])
        {
            var frameData = new FrameData();
            frameData.Load((JObject)frameNode);
            Frames.Add(frameData);
        }
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        var jArray = new JArray();
        foreach (var frame in Frames)
        {
            var newJObject = new JObject();
            frame.Save(newJObject);
            jArray.Add(newJObject);
        }
        jObject.Add("frames", jArray);
    }
#endif
}