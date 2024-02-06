using System.Text.Json;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Entities;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Animations;

public class AnimationData : ObjectBase
{
    public AnimationType AnimationType { get; set; }

    public override void Load(JsonElement element)
    {
        AnimationType = element.GetJsonPropertyByName("animation_type").Value.GetEnum<AnimationType>();
        //TODO remove
        base.Load(element.TryGetProperty("asset", out _) ? element.GetProperty("asset") : element);
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        jObject.Add("animation_type", AnimationType.ConvertToString());
        base.Save(jObject);
    }
#endif
}