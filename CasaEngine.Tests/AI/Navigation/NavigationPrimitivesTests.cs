using CasaEngine.Framework.AI.Navigation;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.AI.Navigation;

public sealed class NavigationPrimitivesTests
{
    [Fact]
    public void NavigationGridCell_CanEnterRequiresWalkableMatchingLayer()
    {
        var groundCell = new NavigationGridCell(true, 2f, NavigationLayerMask.Ground);
        var blockedCell = new NavigationGridCell(false, 1f, NavigationLayerMask.Ground);

        Assert.True(groundCell.CanEnter(NavigationLayerMask.Ground));
        Assert.False(groundCell.CanEnter(NavigationLayerMask.Water));
        Assert.False(blockedCell.CanEnter(NavigationLayerMask.Ground));
    }

    [Fact]
    public void NavigationGridCell_RejectsInvalidCost()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavigationGridCell(true, 0f, NavigationLayerMask.Ground));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavigationGridCell(true, float.NaN, NavigationLayerMask.Ground));
    }

    [Fact]
    public void NavigationPath_ClearResetsPointsAndIndex()
    {
        var path = new NavigationPath();
        path.AddPoint(new Vector3(1f, 0f, 2f));
        path.CurrentPointIndex = 1;

        path.Clear();

        Assert.Empty(path.Points);
        Assert.Equal(0, path.CurrentPointIndex);
        Assert.True(path.IsFinished);
    }
}