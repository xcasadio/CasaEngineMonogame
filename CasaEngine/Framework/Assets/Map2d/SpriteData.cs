using System.Text.Json;
using System.Xml.Linq;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Shapes;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Map2d;

public class SpriteData : Asset
{
    public string SpriteSheetFileName { get; set; }
    public Rectangle PositionInTexture { get; set; }
    public Point Origin { get; set; }
    private List<Socket> Sockets { get; } = new();
    List<Collision2d> CollisionShapes { get; } = new();

    public override void Load(JsonElement element)
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

    public override void Save(JObject jObject)
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
};

public class Socket
{
    public string Name;
    public Point Position;

    public void Load(JsonElement jsonElement)
    {
        Name = jsonElement.GetJsonPropertyByName("name").Value.GetString();
        Position = jsonElement.GetJsonPropertyByName("position").Value.GetPoint();
    }

    public void Save(JObject jObject)
    {
        jObject.Add("name", Name);
        var newjObject = new JObject();
        Position.Save(newjObject);
        jObject.Add("position", newjObject);
    }
}

public class Collision2d
{
    public CollisionHitType CollisionHitType;
    public Shape2d Shape;

    public void Load(JsonElement jsonElement)
    {
        CollisionHitType = jsonElement.GetJsonPropertyByName("collision_type").Value.GetEnum<CollisionHitType>();
        Shape = ShapeLoader.LoadShape2d(jsonElement);
    }

    public void Save(JObject jObject)
    {
        jObject.Add("collision_type", CollisionHitType.ConvertToString());
        Shape.Save(jObject);
    }
}

public enum CollisionHitType
{
    Unknown = 0,
    Attack = 1,
    Defense
}