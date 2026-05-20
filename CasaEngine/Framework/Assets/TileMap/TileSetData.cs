
using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.TileMap;

public class TileSetData : ObjectBase
{
    private readonly Dictionary<int, TileData> _tileById = new();

    public Guid SpriteSheetAssetId { get; set; }
    public CasaEngine.Core.Math.Size TileSize { get; set; }
    public List<TileData> Tiles { get; } = new();

    public TileData GetTileData(int tileId)
    {
        return _tileById[tileId];
    }

    public bool IsKnownTileId(int tileId)
    {
        return _tileById.ContainsKey(tileId);
    }

    public bool TryGetTileData(int tileId, out TileData? tileData)
    {
        return _tileById.TryGetValue(tileId, out tileData);
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        SpriteSheetAssetId = element["sprite_sheet_asset_id"].GetGuid();
        TileSize = element["tile_size"].GetSize();

        foreach (var tileNode in element["tiles"])
        {
            var type = tileNode["type"].GetEnum<TileType>();
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

            tileData.Load((JObject)tileNode);

            AddTile(tileData);
        }
    }

    private void AddTile(TileData tileData)
    {
        Tiles.Add(tileData);
        _tileById.Add(tileData.Id, tileData);
    }
}