
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets.Sprites;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.TileMap;

public class TileData
{
    public int Id { get; set; }
    public TileType Type { get; set; }
    public TileCollisionType CollisionType { get; set; }
    public Collision2d? CollisionShape { get; set; }
    public bool IsBreakable { get; set; }

    protected TileData(TileType type)
    {
        Id = 0;
        Type = type;
        CollisionType = TileCollisionType.None;
    }

    public virtual void Load(JObject element)
    {
        Type = element["type"].GetEnum<TileType>();
        Id = element["id"].GetInt32();
        CollisionType = element["collision_type"].GetEnum<TileCollisionType>();
        IsBreakable = element["is_breakable"].GetBoolean();

        var collisionNode = element["collision"];
        if (collisionNode.ToString() != "null")
        {
            CollisionShape = new Collision2d();
            CollisionShape.Load((JObject)collisionNode);
        }
    }

#if EDITOR

    public virtual void Save(JObject jObject)
    {
        jObject.Add("type", Type.ConvertToString());
        jObject.Add("id", Id);
        jObject.Add("collision_type", CollisionType.ConvertToString());
        jObject.Add("is_breakable", IsBreakable);

        if (CollisionShape == null)
        {
            jObject.Add("collision", "null");
        }
        else
        {
            var newNode = new JObject();
            CollisionShape.Save(newNode);
            jObject.Add("collision", newNode);
        }
    }

#endif
}