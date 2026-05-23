using CasaEngine.Framework.AI.Navigation;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.AI.Navigation;

public sealed class GridPathfinder2DTests
{
    [Fact]
    public void GridPathfinder2D_PrefersLowerTotalCostOverShortestCellCount()
    {
        NavigationGrid2D grid = CreateGrid(3, 2);
        grid.SetCell(1, 0, new NavigationGridCell(true, 20f, NavigationLayerMask.Ground));
        var query = new NavigationQuery { LayerMask = NavigationLayerMask.Ground };

        bool found = GridPathfinder2D.Shared.TryFindPath(grid, new Point(0, 0), new Point(2, 0), query, out NavigationPath? path);

        Assert.True(found);
        Assert.NotNull(path);
        Assert.Equal(new Vector3(0.5f, 0f, 0.5f), path.Points[0]);
        Assert.Equal(new Vector3(0.5f, 0f, 1.5f), path.Points[1]);
        Assert.Equal(new Vector3(1.5f, 0f, 1.5f), path.Points[2]);
        Assert.Equal(new Vector3(2.5f, 0f, 1.5f), path.Points[3]);
        Assert.Equal(new Vector3(2.5f, 0f, 0.5f), path.Points[4]);
    }

    [Fact]
    public void GridPathfinder2D_BlocksDiagonalCornerCutting()
    {
        NavigationGrid2D grid = CreateGrid(2, 2);
        grid.SetCell(1, 0, NavigationGridCell.Blocked);
        grid.SetCell(0, 1, NavigationGridCell.Blocked);
        var query = new NavigationQuery
        {
            AllowDiagonalMovement = true,
            PreventDiagonalCornerCutting = true,
            LayerMask = NavigationLayerMask.Ground,
        };

        bool found = GridPathfinder2D.Shared.TryFindPath(grid, new Point(0, 0), new Point(1, 1), query, out _);

        Assert.False(found);
    }

    [Fact]
    public void GridPathfinder2D_ReturnsFalseWhenGoalIsUnreachable()
    {
        NavigationGrid2D grid = CreateGrid(3, 1);
        grid.SetCell(1, 0, NavigationGridCell.Blocked);
        var query = new NavigationQuery { LayerMask = NavigationLayerMask.Ground };

        bool found = GridPathfinder2D.Shared.TryFindPath(grid, new Point(0, 0), new Point(2, 0), query, out NavigationPath? path);

        Assert.False(found);
        Assert.Null(path);
    }

    private static NavigationGrid2D CreateGrid(int width, int height)
    {
        var grid = new NavigationGrid2D(width, height, 1f);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                grid.SetCell(x, y, new NavigationGridCell(true, 1f, NavigationLayerMask.Ground));
            }
        }

        return grid;
    }
}