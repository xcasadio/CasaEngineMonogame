using CasaEngine.Framework.Assets.TileMap;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.TileMap;

public class TileMapCollisionChunkTests
{
    [Fact]
    public void BuildMergedTileRectangles_MergesAdjacentSolidCells()
    {
        var chunk = new TileMapCollisionChunk(0, Point.Zero, new Rectangle(4, 8, 4, 3));
        var solidCells = chunk.EnsureSolidCapacity(12);

        solidCells[0] = true;
        solidCells[1] = true;
        solidCells[4] = true;
        solidCells[5] = true;
        solidCells[10] = true;
        solidCells[11] = true;

        var rectangles = chunk.BuildMergedTileRectangles(4, 3, 4, 8, solidCells);

        Assert.Equal(2, rectangles.Count);
        Assert.Equal(new Rectangle(4, 8, 2, 2), rectangles[0]);
        Assert.Equal(new Rectangle(6, 10, 2, 1), rectangles[1]);
    }

    [Fact]
    public void BuildMergedTileRectangles_DoesNotMergeSeparatedCells()
    {
        var chunk = new TileMapCollisionChunk(0, Point.Zero, new Rectangle(0, 0, 3, 3));
        var solidCells = chunk.EnsureSolidCapacity(9);

        solidCells[0] = true;
        solidCells[2] = true;
        solidCells[8] = true;

        var rectangles = chunk.BuildMergedTileRectangles(3, 3, 0, 0, solidCells);

        Assert.Equal(3, rectangles.Count);
        Assert.Equal(new Rectangle(0, 0, 1, 1), rectangles[0]);
        Assert.Equal(new Rectangle(2, 0, 1, 1), rectangles[1]);
        Assert.Equal(new Rectangle(2, 2, 1, 1), rectangles[2]);
    }
}