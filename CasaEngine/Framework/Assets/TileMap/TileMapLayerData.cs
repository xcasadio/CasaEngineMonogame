using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;


namespace CasaEngine.Framework.Assets.TileMap;

public class TileMapLayerData
{
    public const int EmptyTileId = -1;

    public string? Name { get; set; }
    public List<int> tiles = new();
    public float zOffset;

    public void Load(JObject element)
    {
        Name = element.ContainsKey("name") ? element["name"]?.GetString() : null;
        zOffset = element["z_offset"].GetSingle();

        tiles.Clear();

        foreach (var tileToken in element["tiles"]!)
        {
            tiles.Add(tileToken.Value<int>());
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
    }
}