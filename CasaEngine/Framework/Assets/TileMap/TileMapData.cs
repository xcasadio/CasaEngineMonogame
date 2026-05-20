
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
    public Dictionary<string, string> CustomProperties { get; } = new(StringComparer.Ordinal);

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

    public TileCellFlags GetTileFlags(int layerIndex, int x, int y)
    {
        return GetLayer(layerIndex).GetTileFlags(GetTileIndex(x, y));
    }

    public void SetTileId(int layerIndex, int x, int y, int tileId)
    {
        GetLayer(layerIndex).tiles[GetTileIndex(x, y)] = tileId;
    }

    public void SetTileFlags(int layerIndex, int x, int y, TileCellFlags flags)
    {
        GetLayer(layerIndex).SetTileFlags(GetTileIndex(x, y), flags);
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
        LoadCustomProperties(element["custom_properties"], CustomProperties);

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

    internal static void LoadCustomProperties(JToken? propertiesToken, Dictionary<string, string> customProperties)
    {
        customProperties.Clear();
        if (propertiesToken is not JObject propertiesObject)
        {
            return;
        }

        foreach (var property in propertiesObject.Properties())
        {
            customProperties[property.Name] = property.Value.Type == JTokenType.Null
                ? string.Empty
                : property.Value.ToString();
        }
    }
}