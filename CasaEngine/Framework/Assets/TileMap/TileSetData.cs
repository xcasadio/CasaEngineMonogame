using System.Text.Json;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Entities;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.TileMap;

public class TileSetData : ObjectBase
{
    private readonly Dictionary<int, TileData> _tileById = new();

    public Guid SpriteSheetAssetId { get; set; }
    public Core.Maths.Size TileSize { get; set; }
    public List<TileData> Tiles { get; } = new();

    public TileData GetTileData(int tileId)
    {
        return _tileById[tileId];
    }

    public override void Load(JsonElement element)
    {
        base.Load(element);

        SpriteSheetAssetId = element.GetProperty("sprite_sheet_asset_id").GetGuid();
        TileSize = element.GetProperty("tile_size").GetSize();

        foreach (var tileNode in element.GetProperty("tiles").EnumerateArray())
        {
            var type = tileNode.GetJsonPropertyByName("type").Value.GetEnum<TileType>();
            TileData tileData;
            switch (type)
            {
                case TileType.Static:
                    tileData = new StaticTileData();
                    break;
                case TileType.Animated:
                    tileData = new AnimatedTileData();
                    break;
                case TileType.Auto:
                    tileData = new AutoTileData();
                    break;
                default: throw new Exception($"TileSetData.Load() : TileSetData, tile type {type} not supported");
            }

            tileData.Load(tileNode);

            AddTile(tileData);
        }
    }

    private void AddTile(TileData tileData)
    {
        Tiles.Add(tileData);
        _tileById.Add(tileData.Id, tileData);
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        jObject.Add("sprite_sheet_asset_id", SpriteSheetAssetId);

        var newNode = new JObject();
        TileSize.Save(newNode);
        jObject.Add("tile_size", newNode);

        var jArray = new JArray();

        foreach (var tileData in Tiles)
        {
            newNode = new JObject();
            tileData.Save(newNode);
            jArray.Add(newNode);
        }

        jObject.Add("tiles", jArray);
    }

#endif
}