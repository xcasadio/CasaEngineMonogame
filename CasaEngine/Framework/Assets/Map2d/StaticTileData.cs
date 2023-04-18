using System.Text.Json;
using CasaEngine.Core.Helpers;

namespace CasaEngine.Framework.Assets.Map2d;

public class StaticTileData : TileData
{
    public string SpriteId;

    public StaticTileData() : base(TileType.Static)
    { }

    public override void Load(JsonElement jObject)
    {
        base.Load(jObject);
        SpriteId = jObject.GetJsonPropertyByName("sprite_id").Value.GetString();
    }
};