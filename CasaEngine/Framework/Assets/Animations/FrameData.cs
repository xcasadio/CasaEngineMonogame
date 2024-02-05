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
        if (element.GetProperty("sprite_id").ValueKind == JsonValueKind.Number)
        {
            SpriteId = AssetInfo.GuidsById[element.GetProperty("sprite_id").GetInt32()];
        }
        else
        {
            SpriteId = element.GetProperty("sprite_id").GetGuid();
        }
    }

#if EDITOR

    public void Save(JObject jObject)
    {
        jObject.Add("duration", Duration);
        jObject.Add("sprite_id", SpriteId);

    }
#endif
}