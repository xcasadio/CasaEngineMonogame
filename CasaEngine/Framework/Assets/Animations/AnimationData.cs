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
        //base.Load(element);
        AnimationType = element.GetJsonPropertyByName("animation_type").Value.GetEnum<AnimationType>();
    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        jObject.Add("animation_type", AnimationType.ConvertToString());
        //base.Save(jObject);
    }
#endif
}