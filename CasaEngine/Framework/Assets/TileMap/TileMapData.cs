
using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;
using Size = CasaEngine.Core.Math.Size;

namespace CasaEngine.Framework.Assets.TileMap;

public class TileMapData : ObjectBase
{
    public const int EmptyTileId = TileMapLayerData.EmptyTileId;

    public Size MapSize { get; set; }
    public Guid TileSetDataAssetId { get; set; } = Guid.Empty;
    public List<TileMapLayerData> Layers { get; } = new();

    public bool IsInside(int x, int y)
    {
        return x >= 0 && x < MapSize.Width && y >= 0 && y < MapSize.Height;
    }

    public int GetTileIndex(int x, int y)
    {
        if (!IsInside(x, y))
        {
            throw new ArgumentOutOfRangeException($"Tile coordinates ({x}, {y}) are outside map bounds {MapSize.Width}x{MapSize.Height}.");
        }

        return x + y * MapSize.Width;
    }

    public int GetTileId(int layerIndex, int x, int y)
    {
        return GetLayer(layerIndex).tiles[GetTileIndex(x, y)];
    }

    public void SetTileId(int layerIndex, int x, int y, int tileId)
    {
        GetLayer(layerIndex).tiles[GetTileIndex(x, y)] = tileId;
    }

    public void Validate()
    {
        if (MapSize.Width <= 0 || MapSize.Height <= 0)
        {
            throw new InvalidOperationException($"TileMap size must be positive but was {MapSize.Width}x{MapSize.Height}.");
        }

        if (TileSetDataAssetId == Guid.Empty)
        {
            throw new InvalidOperationException("TileMapData requires a valid TileSetDataAssetId.");
        }

        for (var layerIndex = 0; layerIndex < Layers.Count; layerIndex++)
        {
            Layers[layerIndex].ValidateTileCount(MapSize.Width, MapSize.Height, layerIndex);
        }
    }

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

        Validate();
    }

    private TileMapLayerData GetLayer(int layerIndex)
    {
        if (layerIndex < 0 || layerIndex >= Layers.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(layerIndex), $"Layer index {layerIndex} is outside the valid range 0..{Layers.Count - 1}.");
        }

        return Layers[layerIndex];
    }
}