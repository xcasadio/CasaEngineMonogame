using System.Text.Json;
using CasaEngine.Core.Helpers;

namespace CasaEngine.Framework.Assets.Map2d;

public class AutoTileData : TileData
{
    public int AutoTileSetId;

    public AutoTileData() : base(TileType.Auto)
    { }

    public override void Load(JsonElement jObject)
    {
        base.Load(jObject);
        AutoTileSetId = jObject.GetJsonPropertyByName("autoTileSetId").Value.GetInt32();
    }

}