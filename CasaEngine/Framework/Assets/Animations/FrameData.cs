using System.Text.Json;
using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Animations;

public class FrameData
{
    public float Duration { get; set; }

    public Guid SpriteId { get; set; }
    //flip
    //blending

    public void Load(JsonElement element)
    {
        Duration = element.GetJsonPropertyByName("duration").Value.GetSingle();
        //TODO : remove
        SpriteId = AssetInfo.GuidsById[element.GetJsonPropertyByName("sprite_id").Value.GetInt32()];
        //SpriteId = element.GetJsonPropertyByName("sprite_id").Value.GetGuid();
    }

#if EDITOR

    public void Save(JObject jObject)
    {
        jObject.Add("duration", Duration);
        jObject.Add("sprite_id", SpriteId);

    }
#endif
}