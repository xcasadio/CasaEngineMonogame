using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Animations;

public class AnimationData : Asset
{
    public AnimationType AnimationType { get; set; }

    public override void Load(JsonElement element, SaveOption option)
    {
        AnimationType = element.GetJsonPropertyByName("animation_type").Value.GetEnum<AnimationType>();
        base.Load(element.GetProperty("asset"), option);
    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        jObject.Add("animation_type", AnimationType.ConvertToString());
        base.Save(jObject, option);
    }
#endif
}