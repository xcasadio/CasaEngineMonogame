using System.Text.Json;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Animations;

public class FrameData
{
    public float Duration { get; set; }

    public long SpriteId { get; set; }
    //flip
    //blending

    public void Load(JsonElement element)
    {
        Duration = element.GetJsonPropertyByName("duration").Value.GetSingle();
        SpriteId = element.GetJsonPropertyByName("sprite_id").Value.GetInt64();
    }

#if EDITOR

    public void Save(JObject jObject)
    {
        jObject.Add("duration", Duration);
        jObject.Add("sprite_id", SpriteId);

    }
#endif
}