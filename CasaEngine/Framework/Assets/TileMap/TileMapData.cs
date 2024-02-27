
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Objects;
using Newtonsoft.Json.Linq;
using Size = CasaEngine.Core.Maths.Size;

namespace CasaEngine.Framework.Assets.TileMap;

public class TileMapData : ObjectBase
{
    public Size MapSize { get; set; }
    public Guid TileSetDataAssetId { get; private set; } = Guid.Empty;
    public List<TileMapLayerData> Layers { get; } = new();

    public override void Load(JObject element)
    {
        base.Load(element);

        MapSize = element["map_size"].GetSize();
        TileSetDataAssetId = element["tile_set_asset_id"].GetGuid();

        Layers.AddRange(element.GetElements("layers", jToken =>
        {
            var tileMapLayerData = new TileMapLayerData();
            tileMapLayerData.Load((JObject)jToken);
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