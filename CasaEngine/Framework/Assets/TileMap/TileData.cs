
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
    public Dictionary<string, string> CustomProperties { get; } = new(StringComparer.Ordinal);

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
        TileMapData.LoadCustomProperties(element["custom_properties"], CustomProperties);

        var collisionNode = element["collision"];
        if (collisionNode.ToString() != "null")
        {
            CollisionShape = new Collision2d();
            CollisionShape.Load((JObject)collisionNode);
        }
    }
}