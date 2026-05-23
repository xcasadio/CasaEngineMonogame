using CasaEngine.Framework.AI.Navigation;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.AI.Navigation;

public sealed class NavigationDebugDrawTests
{
    [Fact]
    public void NavigationDebugDraw2D_ClipsVisibleCellsToGridBounds()
    {
        var grid = new NavigationGrid2D(4, 3, 1f);

        Rectangle clippedCells = NavigationDebugDraw2D.ClipVisibleCells(grid, new Rectangle(-1, 1, 3, 5));

        Assert.Equal(new Rectangle(0, 1, 2, 2), clippedCells);
        Assert.Equal(4, NavigationDebugDraw2D.CountVisibleCells(grid, new Rectangle(-1, 1, 3, 5)));
    }

    [Fact]
    public void NavigationDebugDrawBudget_StopsAtPrimitiveLimitAndCanReset()
    {
        var budget = new NavigationDebugDrawBudget(2);

        Assert.True(budget.TryConsume());
        Assert.True(budget.TryConsume());
        Assert.False(budget.TryConsume());
        Assert.Equal(0, budget.RemainingPrimitiveCount);

        budget.Reset();

        Assert.Equal(2, budget.RemainingPrimitiveCount);
        Assert.True(budget.TryConsume());
    }
}