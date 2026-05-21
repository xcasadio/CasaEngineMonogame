using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;


namespace CasaEngine.Framework.Assets.TileMap;

public class TileMapLayerData
{
    public const int EmptyTileId = -1;
    public const int DefaultTileSourceIndex = 0;

    public string? Name { get; set; }
    public List<int> tiles = new();
    public List<int> tileSources = new();
    public List<TileCellFlags> tileFlags = new();
    public Dictionary<string, string> CustomProperties { get; } = new(StringComparer.Ordinal);
    public float zOffset;

    public void Load(JObject element)
    {
        Name = element.ContainsKey("name") ? element["name"]?.GetString() : null;
        zOffset = element["z_offset"].GetSingle();

        tiles.Clear();
        tileSources.Clear();
        tileFlags.Clear();
        TileMapData.LoadCustomProperties(element["custom_properties"], CustomProperties);

        foreach (var tileToken in element["tiles"]!)
        {
            tiles.Add(tileToken.Value<int>());
        }

        var tileSourcesToken = element["tile_sources"];
        if (tileSourcesToken != null)
        {
            foreach (var tileSourceToken in tileSourcesToken)
            {
                tileSources.Add(tileSourceToken.Value<int>());
            }
        }

        var tileFlagsToken = element["tile_flags"];
        if (tileFlagsToken != null)
        {
            foreach (var tileFlagToken in tileFlagsToken)
            {
                tileFlags.Add((TileCellFlags)tileFlagToken.Value<int>());
            }
        }
    }

    public TileCellFlags GetTileFlags(int tileIndex)
    {
        return tileIndex >= 0 && tileIndex < tileFlags.Count
            ? tileFlags[tileIndex]
            : TileCellFlags.None;
    }

    public int GetTileSourceIndex(int tileIndex)
    {
        return tileIndex >= 0 && tileIndex < tileSources.Count
            ? tileSources[tileIndex]
            : DefaultTileSourceIndex;
    }

    public void SetTileFlags(int tileIndex, TileCellFlags flags)
    {
        EnsureTileFlagCount();
        tileFlags[tileIndex] = flags;
    }

    public void SetTileSourceIndex(int tileIndex, int tileSourceIndex)
    {
        EnsureTileSourceCount();
        tileSources[tileIndex] = tileSourceIndex;
    }

    public bool HasTileFlags()
    {
        for (var index = 0; index < tileFlags.Count; index++)
        {
            if (tileFlags[index] != TileCellFlags.None)
            {
                return true;
            }
        }

        return false;
    }

    public bool HasNonDefaultTileSources()
    {
        for (var index = 0; index < tileSources.Count; index++)
        {
            if (tileSources[index] != DefaultTileSourceIndex)
            {
                return true;
            }
        }

        return false;
    }

    public void EnsureTileSourceCount()
    {
        while (tileSources.Count < tiles.Count)
        {
            tileSources.Add(DefaultTileSourceIndex);
        }

        if (tileSources.Count > tiles.Count)
        {
            tileSources.RemoveRange(tiles.Count, tileSources.Count - tiles.Count);
        }
    }

    public void EnsureTileFlagCount()
    {
        while (tileFlags.Count < tiles.Count)
        {
            tileFlags.Add(TileCellFlags.None);
        }

        if (tileFlags.Count > tiles.Count)
        {
            tileFlags.RemoveRange(tiles.Count, tileFlags.Count - tiles.Count);
        }
    }

    public bool IsInside(int x, int y, int mapWidth, int mapHeight)
    {
        return x >= 0 && x < mapWidth && y >= 0 && y < mapHeight;
    }

    public int GetTileIndex(int x, int y, int mapWidth, int mapHeight)
    {
        if (!IsInside(x, y, mapWidth, mapHeight))
        {
            throw new ArgumentOutOfRangeException($"Tile coordinates ({x}, {y}) are outside layer bounds {mapWidth}x{mapHeight}.");
        }

        return x + y * mapWidth;
    }

    public void ValidateTileCount(int mapWidth, int mapHeight, int layerIndex)
    {
        var expectedTileCount = mapWidth * mapHeight;
        if (tiles.Count != expectedTileCount)
        {
            throw new InvalidOperationException(
                $"TileMap layer {layerIndex} has {tiles.Count} tiles but expected {expectedTileCount} for map size {mapWidth}x{mapHeight}.");
        }

        if (tileFlags.Count != 0 && tileFlags.Count != expectedTileCount)
        {
            throw new InvalidOperationException(
                $"TileMap layer {layerIndex} has {tileFlags.Count} tile flags but expected {expectedTileCount} for map size {mapWidth}x{mapHeight}.");
        }

        if (tileSources.Count != 0 && tileSources.Count != expectedTileCount)
        {
            throw new InvalidOperationException(
                $"TileMap layer {layerIndex} has {tileSources.Count} tile sources but expected {expectedTileCount} for map size {mapWidth}x{mapHeight}.");
        }
    }
}