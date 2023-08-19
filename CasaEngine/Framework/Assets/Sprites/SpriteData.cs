using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Sprites;

public class SpriteData : Asset
{
    public string SpriteSheetFileName { get; set; }
    public Rectangle PositionInTexture { get; set; }
    public Point Origin { get; set; }
    public List<Socket> Sockets { get; } = new();
    public List<Collision2d> CollisionShapes { get; } = new();

    public override void Load(JsonElement element, SaveOption option)
    {
        Name = element.GetJsonPropertyByName("asset_name").Value.GetString(); //TODO : in base.Load()

        SpriteSheetFileName = element.GetJsonPropertyByName("sprite_sheet").Value.GetString();
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

    public override void Save(JObject jObject, SaveOption option)
    {
        jObject.Add("asset_name", Name);
        jObject.Add("sprite_sheet", SpriteSheetFileName);

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