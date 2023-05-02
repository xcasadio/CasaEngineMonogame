using System.Text.Json;
using CasaEngine.Core.Helpers;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Map2d;

public class SpriteData : Asset
{
    public string SpriteSheetFileName { get; set; }
    public Rectangle PositionInTexture { get; set; }
    public Point Origin { get; set; }
    //List<Vector2I> m_Sockets;
    //List<Collision> _collisionShapes;

    public override void Load(JsonElement element)
    {
        //base.Load(element);
        Name = element.GetJsonPropertyByName("asset_name").Value.GetString(); //TODO : in base.Load()

        SpriteSheetFileName = element.GetJsonPropertyByName("sprite_sheet").Value.GetString();
        PositionInTexture = element.GetJsonPropertyByName("location").Value.GetRectangle();
        Origin = element.GetJsonPropertyByName("hotspot").Value.GetPoint();
        //from_json(j.at("collisions"), t._collisionShapes);
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);
    }
#endif
};