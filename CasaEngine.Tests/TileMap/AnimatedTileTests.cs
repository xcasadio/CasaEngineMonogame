using CasaEngine.Framework.Assets.TileMap;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.TileMap;

public class AnimatedTileTests
{
    [Fact]
    public void Update_AdvancesFramesAndLoops()
    {
        var tileSetData = CreateTileSetData(out var animatedTileData);
        var animatedTile = new AnimatedTile(null!, tileSetData, animatedTileData);

        animatedTile.Update(0.119f);
        Assert.Equal(0, animatedTile.CurrentFrameIndex);

        animatedTile.Update(0.001f);
        Assert.Equal(1, animatedTile.CurrentFrameIndex);

        animatedTile.Update(0.080f);
        Assert.Equal(0, animatedTile.CurrentFrameIndex);
    }

    [Fact]
    public void Update_AdvancesMultipleFramesForLargeElapsedTime()
    {
        var tileSetData = CreateTileSetData(out var animatedTileData);
        var animatedTile = new AnimatedTile(null!, tileSetData, animatedTileData);

        animatedTile.Update(0.200f);

        Assert.Equal(0, animatedTile.CurrentFrameIndex);
    }

    private static TileSetData CreateTileSetData(out AnimatedTileData animatedTileData)
    {
        var tileSetData = new TileSetData
        {
            TileSize = new CasaEngine.Core.Math.Size(16, 16),
        };

        animatedTileData = new AnimatedTileData
        {
            Id = 0,
            Location = new Rectangle(0, 0, 16, 16),
        };
        animatedTileData.Frames.Add(new AnimatedTileFrameData
        {
            TileId = 0,
            DurationMilliseconds = 120,
        });
        animatedTileData.Frames.Add(new AnimatedTileFrameData
        {
            TileId = 1,
            DurationMilliseconds = 80,
        });

        tileSetData.AddTile(animatedTileData);
        tileSetData.AddTile(new StaticTileData
        {
            Id = 1,
            Location = new Rectangle(16, 0, 16, 16),
        });

        return tileSetData;
    }
}