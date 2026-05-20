using CasaEngine.Framework.Assets.TileMap;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.TileMap;

public class TileMapChunkTests
{
    [Fact]
    public void IntersectsTileRange_DetectsOverlappingRanges()
    {
        var chunk = new TileMapChunk(0, new Point(1, 1), new Rectangle(16, 16, 16, 16));

        Assert.True(chunk.IntersectsTileRange(0, 16, 0, 16));
        Assert.True(chunk.IntersectsTileRange(31, 40, 31, 40));
        Assert.False(chunk.IntersectsTileRange(0, 15, 0, 31));
        Assert.False(chunk.IntersectsTileRange(0, 31, 32, 48));
    }

    [Fact]
    public void UpdateWorldBounds_HandlesNegativeTileMapYAxis()
    {
        var chunk = new TileMapChunk(2, new Point(1, 0), new Rectangle(2, 0, 2, 3));

        chunk.UpdateWorldBounds(10f, 20f, 16f, 8f, 0.5f, 0.5f);

        Assert.Equal(new Vector3(42f, -4f, 0.5f), chunk.WorldBounds.Min);
        Assert.Equal(new Vector3(74f, 20f, 0.5f), chunk.WorldBounds.Max);
    }

    [Fact]
    public void MarkDirtyAndClean_TrackVisualAndCollisionStateSeparately()
    {
        var chunk = new TileMapChunk(0, Point.Zero, new Rectangle(0, 0, 16, 16));

        Assert.True(chunk.DirtyVisual);
        Assert.True(chunk.DirtyCollision);

        chunk.MarkVisualClean();
        chunk.MarkCollisionClean();

        Assert.False(chunk.DirtyVisual);
        Assert.False(chunk.DirtyCollision);

        chunk.MarkDirty(visual: true, collision: false);

        Assert.True(chunk.DirtyVisual);
        Assert.False(chunk.DirtyCollision);
    }
}