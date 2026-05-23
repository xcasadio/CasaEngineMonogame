using CasaEngine.Core.Math;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Assets.TileMap;
using Xunit;

namespace CasaEngine.Tests.AI.Navigation;

public sealed class NavigationGrid2DTests
{
    [Fact]
    public void NavigationGrid2D_BuildsWalkabilityFromNavigationLayer()
    {
        TileMapData tileMapData = CreateTileMapData();
        TileSetData tileSetData = CreateTileSet(
            CreateTile(1, TileCollisionType.None, true, 1f, NavigationLayerMask.Ground),
            CreateTile(2, TileCollisionType.None, false, 1f, NavigationLayerMask.Ground));

        bool created = NavigationGrid2D.TryCreateFromTileMap(tileMapData, tileSetData, 16f, out NavigationGrid2D? grid);

        Assert.True(created);
        Assert.NotNull(grid);
        Assert.True(grid.IsCellWalkable(0, 0, new NavigationQuery { LayerMask = NavigationLayerMask.Ground }));
        Assert.False(grid.IsCellWalkable(1, 0, new NavigationQuery { LayerMask = NavigationLayerMask.Ground }));
        Assert.False(grid.IsCellWalkable(0, 1, new NavigationQuery { LayerMask = NavigationLayerMask.Ground }));
    }

    [Fact]
    public void NavigationGrid2D_UsesTileNavigationPropertiesBeforeCollisionFallback()
    {
        TileMapData tileMapData = CreateTileMapData(1, 2, 3, TileMapData.EmptyTileId);
        TileSetData tileSetData = CreateTileSet(
            CreateTile(1, TileCollisionType.Blocked, true, 3f, NavigationLayerMask.Water),
            CreateTile(2, TileCollisionType.None, null, null, null),
            CreateTile(3, TileCollisionType.Blocked, null, null, null));

        bool created = NavigationGrid2D.TryCreateFromTileMap(tileMapData, tileSetData, 8f, out NavigationGrid2D? grid);

        Assert.True(created);
        Assert.NotNull(grid);

        NavigationGridCell explicitCell = grid.GetCell(0, 0);
        Assert.True(explicitCell.IsWalkable);
        Assert.Equal(3f, explicitCell.Cost);
        Assert.True(explicitCell.CanEnter(NavigationLayerMask.Water));
        Assert.False(explicitCell.CanEnter(NavigationLayerMask.Ground));

        Assert.True(grid.GetCell(1, 0).IsWalkable);
        Assert.False(grid.GetCell(0, 1).IsWalkable);
        Assert.False(grid.GetCell(1, 1).IsWalkable);
    }

    private static TileMapData CreateTileMapData(params int[] navigationTiles)
    {
        if (navigationTiles.Length == 0)
        {
            navigationTiles = [1, 2, TileMapData.EmptyTileId, 1];
        }

        var tileMapData = new TileMapData
        {
            MapSize = new Size(2, 2),
        };
        tileMapData.TileSetDataAssetIds.Add(Guid.NewGuid());
        tileMapData.Layers.Add(new TileMapLayerData
        {
            Name = "Visual",
            tiles = { 1, 1, 1, 1 },
        });

        var navigationLayer = new TileMapLayerData
        {
            Name = "Navigation",
        };
        navigationLayer.tiles.AddRange(navigationTiles);
        navigationLayer.CustomProperties[NavigationGrid2D.NavigationRoleProperty] = NavigationGrid2D.NavigationRoleGrid;
        navigationLayer.CustomProperties[NavigationGrid2D.DefaultWalkableProperty] = "false";
        navigationLayer.CustomProperties[NavigationGrid2D.DefaultCostProperty] = "1.5";
        tileMapData.Layers.Add(navigationLayer);
        return tileMapData;
    }

    private static TileSetData CreateTileSet(params TileData[] tiles)
    {
        var tileSetData = new TileSetData();
        for (int tileIndex = 0; tileIndex < tiles.Length; tileIndex++)
        {
            tileSetData.AddTile(tiles[tileIndex]);
        }

        return tileSetData;
    }

    private static StaticTileData CreateTile(int id, TileCollisionType collisionType, bool? walkable, float? cost, NavigationLayerMask? layers)
    {
        var tileData = new StaticTileData
        {
            Id = id,
            CollisionType = collisionType,
        };

        if (walkable.HasValue)
        {
            tileData.CustomProperties[NavigationGrid2D.WalkableProperty] = walkable.Value ? "true" : "false";
        }

        if (cost.HasValue)
        {
            tileData.CustomProperties[NavigationGrid2D.CostProperty] = cost.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (layers.HasValue)
        {
            tileData.CustomProperties[NavigationGrid2D.LayersProperty] = layers.Value.ToString();
        }

        return tileData;
    }
}