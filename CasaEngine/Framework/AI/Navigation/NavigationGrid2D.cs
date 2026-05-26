using System.Globalization;
using CasaEngine.Framework.Assets.TileMap;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class NavigationGrid2D
{
    public const string NavigationRoleProperty = "navigation.role";
    public const string NavigationRoleGrid = "grid";
    public const string DefaultWalkableProperty = "navigation.defaultWalkable";
    public const string DefaultCostProperty = "navigation.defaultCost";
    public const string WalkableProperty = "navigation.walkable";
    public const string CostProperty = "navigation.cost";
    public const string LayersProperty = "navigation.layers";

    private readonly NavigationGridCell[] _cells;

    public NavigationGrid2D(int width, int height, float cellSize)
        : this(width, height, cellSize, Vector3.Zero)
    { }

    public NavigationGrid2D(int width, int height, float cellSize, Vector3 origin)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (float.IsNaN(cellSize) || float.IsInfinity(cellSize) || cellSize <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize));
        }

        Width = width;
        Height = height;
        CellSize = cellSize;
        Origin = origin;
        _cells = new NavigationGridCell[width * height];

        for (int cellIndex = 0; cellIndex < _cells.Length; cellIndex++)
        {
            _cells[cellIndex] = NavigationGridCell.Blocked;
        }
    }

    public int Width { get; }

    public int Height { get; }

    public float CellSize { get; }

    public Vector3 Origin { get; }

    public static bool TryCreateFromTileMap(TileMapData tileMapData, IReadOnlyList<TileSetData> tileSets, float cellSize, out NavigationGrid2D grid)
    {
        ArgumentNullException.ThrowIfNull(tileMapData);
        ArgumentNullException.ThrowIfNull(tileSets);

        grid = null;
        int navigationLayerIndex = FindNavigationLayerIndex(tileMapData);
        if (navigationLayerIndex < 0)
        {
            return false;
        }

        TileMapLayerData navigationLayer = tileMapData.Layers[navigationLayerIndex];
        bool defaultWalkable = ReadBool(navigationLayer.CustomProperties, DefaultWalkableProperty, false);
        float defaultCost = ReadFloat(navigationLayer.CustomProperties, DefaultCostProperty, 1f);

        grid = new NavigationGrid2D(tileMapData.MapSize.Width, tileMapData.MapSize.Height, cellSize);

        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                int tileIndex = tileMapData.GetTileIndex(x, y);
                int tileId = navigationLayer.tiles[tileIndex];
                int tileSourceIndex = navigationLayer.GetTileSourceIndex(tileIndex);
                grid.SetCell(x, y, CreateCell(tileSets, tileSourceIndex, tileId, defaultWalkable, defaultCost));
            }
        }

        return true;
    }

    public static bool TryCreateFromTileMap(TileMapData tileMapData, TileSetData tileSet, float cellSize, out NavigationGrid2D grid)
    {
        ArgumentNullException.ThrowIfNull(tileSet);
        return TryCreateFromTileMap(tileMapData, [tileSet], cellSize, out grid);
    }

    public bool IsInside(int x, int y)
    {
        return x >= 0 && x < Width && y >= 0 && y < Height;
    }

    public int GetCellIndex(int x, int y)
    {
        if (!IsInside(x, y))
        {
            throw new ArgumentOutOfRangeException($"Navigation cell ({x}, {y}) is outside grid bounds {Width}x{Height}.");
        }

        return x + y * Width;
    }

    public NavigationGridCell GetCell(int x, int y)
    {
        return _cells[GetCellIndex(x, y)];
    }

    public void SetCell(int x, int y, NavigationGridCell cell)
    {
        _cells[GetCellIndex(x, y)] = cell;
    }

    public bool IsCellWalkable(int x, int y, NavigationQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return IsInside(x, y) && query.CanEnter(GetCell(x, y));
    }

    public Vector3 GetWorldPosition(int x, int y)
    {
        GetCellIndex(x, y);
        return Origin + new Vector3((x + 0.5f) * CellSize, 0f, (y + 0.5f) * CellSize);
    }

    public bool TryGetCellFromWorld(Vector3 position, out Point cell)
    {
        int x = (int)MathF.Floor((position.X - Origin.X) / CellSize);
        int y = (int)MathF.Floor((position.Z - Origin.Z) / CellSize);

        cell = new Point(x, y);
        return IsInside(x, y);
    }

    public Vector3 ProjectToNavigation(Vector3 position)
    {
        int x = Math.Clamp((int)MathF.Floor((position.X - Origin.X) / CellSize), 0, Width - 1);
        int y = Math.Clamp((int)MathF.Floor((position.Z - Origin.Z) / CellSize), 0, Height - 1);
        return GetWorldPosition(x, y);
    }

    public bool TryFindPath(Vector3 start, Vector3 end, NavigationQuery query, out NavigationPath path)
    {
        return GridPathfinder2D.Shared.TryFindPath(this, start, end, query, out path);
    }

    private static int FindNavigationLayerIndex(TileMapData tileMapData)
    {
        for (int layerIndex = 0; layerIndex < tileMapData.Layers.Count; layerIndex++)
        {
            TileMapLayerData layer = tileMapData.Layers[layerIndex];
            if (layer.CustomProperties.TryGetValue(NavigationRoleProperty, out string role)
                && string.Equals(role, NavigationRoleGrid, StringComparison.OrdinalIgnoreCase))
            {
                return layerIndex;
            }
        }

        return -1;
    }

    private static NavigationGridCell CreateCell(IReadOnlyList<TileSetData> tileSets, int tileSourceIndex, int tileId, bool defaultWalkable, float defaultCost)
    {
        if (tileId == TileMapData.EmptyTileId)
        {
            return new NavigationGridCell(defaultWalkable, defaultCost, NavigationLayerMask.All);
        }

        if (tileSourceIndex < 0 || tileSourceIndex >= tileSets.Count)
        {
            throw new InvalidOperationException($"Navigation tile references tileset source {tileSourceIndex}, but only {tileSets.Count} tilesets were provided.");
        }

        TileSetData tileSet = tileSets[tileSourceIndex];
        if (!tileSet.TryGetTileData(tileId, out TileData tileData) || tileData == null)
        {
            throw new InvalidOperationException($"Navigation tile id {tileId} was not found in tileset source {tileSourceIndex}.");
        }

        bool walkable = ReadBool(tileData.CustomProperties, WalkableProperty, tileData.CollisionType != TileCollisionType.Blocked);
        float cost = ReadFloat(tileData.CustomProperties, CostProperty, defaultCost);
        NavigationLayerMask layers = ReadLayers(tileData.CustomProperties, LayersProperty, NavigationLayerMask.All);
        return new NavigationGridCell(walkable, cost, layers);
    }

    private static bool ReadBool(Dictionary<string, string> properties, string key, bool fallback)
    {
        if (!properties.TryGetValue(key, out string rawValue))
        {
            return fallback;
        }

        if (bool.TryParse(rawValue, out bool value))
        {
            return value;
        }

        throw new InvalidOperationException($"Navigation property '{key}' must be a boolean value.");
    }

    private static float ReadFloat(Dictionary<string, string> properties, string key, float fallback)
    {
        if (!properties.TryGetValue(key, out string rawValue))
        {
            return fallback;
        }

        if (float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            return value;
        }

        throw new InvalidOperationException($"Navigation property '{key}' must be a floating point value.");
    }

    private static NavigationLayerMask ReadLayers(Dictionary<string, string> properties, string key, NavigationLayerMask fallback)
    {
        if (!properties.TryGetValue(key, out string rawValue) || string.IsNullOrWhiteSpace(rawValue))
        {
            return fallback;
        }

        NavigationLayerMask result = NavigationLayerMask.None;
        string[] tokens = rawValue.Split(',', ';', '|');
        for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
        {
            string token = tokens[tokenIndex].Trim();
            if (token.Length == 0)
            {
                continue;
            }

            if (!Enum.TryParse(token, ignoreCase: true, out NavigationLayerMask layer))
            {
                throw new InvalidOperationException($"Navigation property '{key}' contains unknown layer '{token}'.");
            }

            result |= layer;
        }

        return result == NavigationLayerMask.None ? fallback : result;
    }
}