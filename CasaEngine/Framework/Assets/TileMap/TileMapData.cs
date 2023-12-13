using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;
using Size = CasaEngine.Core.Maths.Size;

namespace CasaEngine.Framework.Assets.TileMap;

public class TileMapData : AssetInfo
{
    public Size MapSize { get; set; }
    public long TileSetDataAssetId { get; private set; } = IdManager.InvalidId;
    public List<TileMapLayerData> Layers { get; } = new();

    public override void Load(JsonElement element, SaveOption option)
    {
        base.Load(element.GetProperty("asset"), option);

        MapSize = element.GetProperty("map_size").GetSize();
        TileSetDataAssetId = element.GetProperty("tile_set_asset_id").GetInt64();

        Layers.AddRange(element.GetElements("layers", o =>
        {
            var tileMapLayerData = new TileMapLayerData();
            tileMapLayerData.Load(o);
            return tileMapLayerData;
        }));
    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);

        var newObject = new JObject();
        MapSize.Save(newObject);
        jObject.Add("map_size", newObject);
        jObject.Add("tile_set_asset_id", TileSetDataAssetId);

        var jArray = new JArray();
        jObject.Add("layers", jArray);

        foreach (var layer in Layers)
        {
            newObject = new JObject();
            layer.Save(newObject);
            jArray.Add(newObject);
        }
    }

#endif
}