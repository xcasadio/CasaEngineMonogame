
using CasaEngine.Core.Serialization;
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
}