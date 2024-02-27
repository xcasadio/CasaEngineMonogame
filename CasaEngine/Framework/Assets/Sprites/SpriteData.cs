
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Objects;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Sprites;

public class SpriteData : ObjectBase
{
    public Guid SpriteSheetAssetId { get; set; }
    public Rectangle PositionInTexture { get; set; }
    public Point Origin { get; set; }
    public List<Socket> Sockets { get; } = new();
    public List<Collision2d> CollisionShapes { get; } = new();

    public override void Load(JObject element)
    {
        base.Load(element);

        SpriteSheetAssetId = element["sprite_sheet_asset_id"].GetGuid();
        PositionInTexture = element["location"].GetRectangle();
        Origin = element["hotspot"].GetPoint();

        if (element.TryGetValue("collisions", out var collisionsElement))
        {
            foreach (var collisionElement in collisionsElement)
            {
                var collision2d = new Collision2d();
                collision2d.Load((JObject)collisionElement);
                CollisionShapes.Add(collision2d);
            }
        }

        if (element.TryGetValue("sockets", out var socketsElement))
        {
            foreach (var socketElement in socketsElement)
            {
                var socket = new Socket();
                socket.Load((JObject)socketElement);
                Sockets.Add(socket);
            }
        }
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);
        jObject.Add("sprite_sheet_asset_id", SpriteSheetAssetId);

        JObject newJObject = new();
        PositionInTexture.Save(newJObject);
        jObject.Add("location", newJObject);

        newJObject = new();
        Origin.Save(newJObject);
        jObject.Add("hotspot", newJObject);

        var jArray = new JArray();
        foreach (var collisionShape in CollisionShapes)
        {
            newJObject = new();
            collisionShape.Save(newJObject);
            jArray.Add(newJObject);
        }
        jObject.Add("collisions", jArray);

        jArray = new JArray();
        foreach (var socket in Sockets)
        {
            newJObject = new();
            socket.Save(newJObject);
            jArray.Add(newJObject);
        }
        jObject.Add("sockets", jArray);
    }
#endif
}