
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Objects;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Animations;

public class AnimationData : ObjectBase
{
    public AnimationType AnimationType { get; set; }

    public override void Load(JObject element)
    {
        AnimationType = element["animation_type"].GetEnum<AnimationType>();
        base.Load(element);
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        jObject.Add("animation_type", AnimationType.ConvertToString());
        base.Save(jObject);
    }
#endif
}