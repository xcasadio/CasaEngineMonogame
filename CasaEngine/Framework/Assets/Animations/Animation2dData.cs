using System.Text.Json;
using CasaEngine.Core.Helpers;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Animations;

public class Animation2dData : AnimationData
{
    public List<FrameData> Frames { get; } = new();

    public override void Load(JsonElement element)
    {
        base.Load(element);
        Name = element.GetJsonPropertyByName("asset_name").Value.GetString(); //TODO : in base.Load()

        foreach (var jsonElement in element.GetJsonPropertyByName("frames").Value.EnumerateArray())
        {
            var frameData = new FrameData();
            frameData.Load(jsonElement);
            Frames.Add(frameData);
        }
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);
    }
#endif
}