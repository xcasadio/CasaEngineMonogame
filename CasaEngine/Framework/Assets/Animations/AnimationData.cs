using System.Text.Json;
using CasaEngine.Core.Helpers;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Animations;

public class AnimationData : Asset
{
    public AnimationType AnimationType { get; set; }

    public override void Load(JsonElement element)
    {
        //base.Load(element);
        AnimationType = element.GetJsonPropertyByName("animation_type").Value.GetEnum<AnimationType>();
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);
    }
#endif
}