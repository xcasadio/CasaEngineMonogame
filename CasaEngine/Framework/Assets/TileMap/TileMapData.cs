using System.Text.Json;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.SceneManagement;
using Newtonsoft.Json.Linq;
using Size = CasaEngine.Core.Maths.Size;

namespace CasaEngine.Framework.Assets.TileMap;

public class TileMapData : UObject
{
    public Size MapSize { get; set; }
    public Guid TileSetDataAssetId { get; private set; } = Guid.Empty;
    public List<TileMapLayerData> Layers { get; } = new();

    public override void Load(JsonElement element)
    {
        base.Load(element.GetProperty("asset"));

        MapSize = element.GetProperty("map_size").GetSize();
        //TODO : remove
        TileSetDataAssetId = AssetInfo.GuidsById[element.GetProperty("tile_set_asset_id").GetInt32()];
        //TileSetDataAssetId = element.GetProperty("tile_set_asset_id").GetGuid();

        Layers.AddRange(element.GetElements("layers", o =>
        {
            var tileMapLayerData = new TileMapLayerData();
            tileMapLayerData.Load(o);
            return tileMapLayerData;
        }));
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

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