using System.Text.Json;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.SceneManagement;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Sprites;

public class SpriteData : UObject
{
    public Guid SpriteSheetAssetId { get; set; }
    public Rectangle PositionInTexture { get; set; }
    public Point Origin { get; set; }
    public List<Socket> Sockets { get; } = new();
    public List<Collision2d> CollisionShapes { get; } = new();

    public override void Load(JsonElement element)
    {
        base.Load(element.GetProperty("asset"));

        //TODO : remove
        SpriteSheetAssetId = AssetInfo.GuidsById[element.GetJsonPropertyByName("sprite_sheet_asset_id").Value.GetInt32()];
        //SpriteSheetAssetId = element.GetJsonPropertyByName("sprite_sheet_asset_id").Value.GetGuid();
        PositionInTexture = element.GetJsonPropertyByName("location").Value.GetRectangle();
        Origin = element.GetJsonPropertyByName("hotspot").Value.GetPoint();

        if (element.TryGetProperty("collisions", out var collisionsElement))
        {
            foreach (var collisionElement in collisionsElement.EnumerateArray())
            {
                var collision2d = new Collision2d();
                collision2d.Load(collisionElement);
                CollisionShapes.Add(collision2d);
            }
        }

        if (element.TryGetProperty("sockets", out var socketsElement))
        {
            foreach (var socketElement in socketsElement.EnumerateArray())
            {
                var socket = new Socket();
                socket.Load(socketElement);
                Sockets.Add(socket);
            }
        }
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);
        //jObject.Add("asset_name", Name);
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