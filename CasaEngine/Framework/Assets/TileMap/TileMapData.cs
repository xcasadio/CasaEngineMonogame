
using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;
using Size = CasaEngine.Core.Math.Size;

namespace CasaEngine.Framework.Assets.TileMap;

public class TileMapData : ObjectBase
{
    public const int EmptyTileId = TileMapLayerData.EmptyTileId;

    public Size MapSize { get; set; }
    public Guid TileSetDataAssetId
    {
        get => TileSetDataAssetIds.Count > 0 ? TileSetDataAssetIds[0] : Guid.Empty;
        set
        {
            TileSetDataAssetIds.Clear();
            if (value != Guid.Empty)
            {
                TileSetDataAssetIds.Add(value);
            }
        }
    }

    public List<Guid> TileSetDataAssetIds { get; } = new();
    public List<TileMapLayerData> Layers { get; } = new();
    public List<TileMapObjectLayerData> ObjectLayers { get; } = new();
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

    public int GetTileSourceIndex(int layerIndex, int x, int y)
    {
        return GetLayer(layerIndex).GetTileSourceIndex(GetTileIndex(x, y));
    }

    public TileMapTileReference GetTileReference(int layerIndex, int x, int y)
    {
        var tileIndex = GetTileIndex(x, y);
        var layer = GetLayer(layerIndex);
        return new TileMapTileReference(layer.GetTileSourceIndex(tileIndex), layer.tiles[tileIndex]);
    }

    public TileCellFlags GetTileFlags(int layerIndex, int x, int y)
    {
        return GetLayer(layerIndex).GetTileFlags(GetTileIndex(x, y));
    }

    public void SetTileId(int layerIndex, int x, int y, int tileId)
    {
        GetLayer(layerIndex).tiles[GetTileIndex(x, y)] = tileId;
    }

    public void SetTileSourceIndex(int layerIndex, int x, int y, int tileSourceIndex)
    {
        GetLayer(layerIndex).SetTileSourceIndex(GetTileIndex(x, y), tileSourceIndex);
    }

    public void SetTileReference(int layerIndex, int x, int y, TileMapTileReference tileReference)
    {
        var tileIndex = GetTileIndex(x, y);
        var layer = GetLayer(layerIndex);
        layer.tiles[tileIndex] = tileReference.TileId;
        layer.SetTileSourceIndex(tileIndex, tileReference.TileSetIndex);
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

        if (TileSetDataAssetIds.Count == 0)
        {
            throw new InvalidOperationException("TileMapData requires at least one valid TileSetData asset reference.");
        }

        for (var tileSetIndex = 0; tileSetIndex < TileSetDataAssetIds.Count; tileSetIndex++)
        {
            if (TileSetDataAssetIds[tileSetIndex] == Guid.Empty)
            {
                throw new InvalidOperationException($"TileMapData tileset reference {tileSetIndex} is invalid.");
            }
        }

        for (var layerIndex = 0; layerIndex < Layers.Count; layerIndex++)
        {
            var layer = Layers[layerIndex];
            layer.ValidateTileCount(MapSize.Width, MapSize.Height, layerIndex);

            if (layer.tileSources.Count == 0)
            {
                continue;
            }

            for (var tileIndex = 0; tileIndex < layer.tiles.Count; tileIndex++)
            {
                var tileSourceIndex = layer.tileSources[tileIndex];
                if (tileSourceIndex < 0 || tileSourceIndex >= TileSetDataAssetIds.Count)
                {
                    throw new InvalidOperationException(
                        $"TileMap layer {layerIndex} references tileset source {tileSourceIndex} at tile index {tileIndex}, but only {TileSetDataAssetIds.Count} tilesets are available.");
                }
            }
        }
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        TileSetDataAssetIds.Clear();
        Layers.Clear();
        ObjectLayers.Clear();
        MapSize = element["map_size"].GetSize();

        var tileSetAssetIdsToken = element["tile_set_asset_ids"];
        if (tileSetAssetIdsToken != null)
        {
            foreach (var tileSetAssetIdToken in tileSetAssetIdsToken)
            {
                TileSetDataAssetIds.Add(tileSetAssetIdToken.GetGuid());
            }
        }
        else
        {
            TileSetDataAssetId = element["tile_set_asset_id"].GetGuid();
        }

        LoadCustomProperties(element["custom_properties"], CustomProperties);

        Layers.AddRange(element.GetElements("layers", jToken =>
        {
            var tileMapLayerData = new TileMapLayerData();
            tileMapLayerData.Load((JObject)jToken);
            return tileMapLayerData;
        }));

        if (element["object_layers"] != null)
        {
            ObjectLayers.AddRange(element.GetElements("object_layers", jToken =>
            {
                var objectLayerData = new TileMapObjectLayerData();
                objectLayerData.Load((JObject)jToken);
                return objectLayerData;
            }));
        }

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