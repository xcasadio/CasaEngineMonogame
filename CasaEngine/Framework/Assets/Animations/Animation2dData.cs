using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Animations;

public class Animation2dData : AnimationData
{
    public List<FrameData> Frames { get; } = new();

    public override void Load(JsonElement element, SaveOption option)
    {
        base.Load(element, option);
        Name = element.GetJsonPropertyByName("asset_name").Value.GetString(); //TODO : in base.Load()

        foreach (var jsonElement in element.GetJsonPropertyByName("frames").Value.EnumerateArray())
        {
            var frameData = new FrameData();
            frameData.Load(jsonElement);
            Frames.Add(frameData);
        }
    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        jObject.Add("asset_name", Name);

        var jArray = new JArray();
        foreach (var frame in Frames)
        {
            var newJObject = new JObject();
            frame.Save(newJObject);
            jArray.Add(newJObject);
        }
        jObject.Add("frames", jArray);

        base.Save(jObject, option);
    }
#endif
}