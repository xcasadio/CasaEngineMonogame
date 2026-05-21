using CasaEngine.Core.Math;
using CasaEngine.Framework.Assets.TileMap;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.TileMap;

public class TileSetDataTests
{
    [Fact]
    public void Load_PreservesAnimatedTileFrames()
    {
        var tileSetData = new TileSetData();

        tileSetData.Load(CreateTileSetJson(new JObject
        {
            ["type"] = TileType.Animated.ToString(),
            ["id"] = 0,
            ["collision_type"] = TileCollisionType.None.ToString(),
            ["is_breakable"] = false,
            ["custom_properties"] = new JObject(),
            ["collision"] = "null",
            ["animation_frames"] = new JArray
            {
                new JObject
                {
                    ["tile_id"] = 0,
                    ["duration_ms"] = 120,
                },
                new JObject
                {
                    ["tile_id"] = 1,
                    ["duration_ms"] = 80,
                },
            },
        }));

        var animatedTileData = Assert.IsType<AnimatedTileData>(tileSetData.GetTileData(0));
        Assert.Equal(2, animatedTileData.Frames.Count);
        Assert.Equal(0, animatedTileData.Frames[0].TileId);
        Assert.Equal(120, animatedTileData.Frames[0].DurationMilliseconds);
        Assert.Equal(1, animatedTileData.Frames[1].TileId);
        Assert.Equal(80, animatedTileData.Frames[1].DurationMilliseconds);
    }

    [Fact]
    public void Load_PreservesLegacyAnimation2dId()
    {
        var tileSetData = new TileSetData();

        tileSetData.Load(CreateTileSetJson(new JObject
        {
            ["type"] = TileType.Animated.ToString(),
            ["id"] = 0,
            ["collision_type"] = TileCollisionType.None.ToString(),
            ["is_breakable"] = false,
            ["custom_properties"] = new JObject(),
            ["collision"] = "null",
            ["animation_2d_id"] = "legacy_animation",
        }));

        var animatedTileData = Assert.IsType<AnimatedTileData>(tileSetData.GetTileData(0));
        Assert.Equal("legacy_animation", animatedTileData.Animation2dId);
        Assert.Empty(animatedTileData.Frames);
    }

    private static JObject CreateTileSetJson(JObject tileNode)
    {
        var tileSize = new Size(16, 16);

        return new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "test_tileset",
            ["sprite_sheet_asset_id"] = Guid.NewGuid().ToString(),
            ["tile_size"] = new JObject
            {
                ["w"] = tileSize.Width,
                ["h"] = tileSize.Height,
            },
            ["tiles"] = new JArray(tileNode),
        };
    }
}