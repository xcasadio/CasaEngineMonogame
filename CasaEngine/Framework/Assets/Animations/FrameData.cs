
using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Animations;

public class FrameData
{
    public float Duration { get; set; }

    public Guid SpriteId { get; set; }
    //flip
    //blending

    public void Load(JObject element)
    {
        Duration = element["duration"].GetSingle();
        SpriteId = element["sprite_id"].GetGuid();
    }

#if EDITOR

    public void Save(JObject jObject)
    {
        jObject.Add("duration", Duration);
        jObject.Add("sprite_id", SpriteId);

    }
#endif
}