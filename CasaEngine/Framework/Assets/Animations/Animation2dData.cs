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

}