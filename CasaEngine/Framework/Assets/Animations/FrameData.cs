using System.Text.Json;
using CasaEngine.Core.Helpers;

namespace CasaEngine.Framework.Assets.Animations;

public class FrameData
{
    public float Duration { get; set; }

    public string SpriteId { get; set; }
    //flip
    //blending

    public void Load(JsonElement element)
    {
        Duration = element.GetJsonPropertyByName("duration").Value.GetSingle();
        SpriteId = element.GetJsonPropertyByName("sprite_id").Value.GetString();
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);
    }
#endif
}