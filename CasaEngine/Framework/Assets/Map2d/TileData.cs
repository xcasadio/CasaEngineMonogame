using CasaEngine.Core.Helpers;
using System.Text.Json;

namespace CasaEngine.Framework.Assets.Map2d;

public class TileData
{
    public int Id { get; set; }
    public TileType Type { get; set; }
    public TileCollisionType CollisionType { get; set; }
    public bool IsBreakable { get; set; }

    protected TileData(TileType type)
    {
        Id = 0;
        Type = type;
        CollisionType = TileCollisionType.None;
    }

    public virtual void Load(JsonElement jObject)
    {
        Type = jObject.GetJsonPropertyByName("type").Value.GetEnum<TileType>();
        Id = jObject.GetJsonPropertyByName("id").Value.GetInt32();
        CollisionType = jObject.GetJsonPropertyByName("collision_type").Value.GetEnum<TileCollisionType>();
        IsBreakable = jObject.GetJsonPropertyByName("is_breakable").Value.GetBoolean();
    }
}