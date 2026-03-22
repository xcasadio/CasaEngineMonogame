
using CasaEngine.Core.Serialization;
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

    public override void Save(JObject jObject)
    {
        throw new NotSupportedException("TileMapData authoring serialization lives in CasaEngine.EditorServices.");
    }
}