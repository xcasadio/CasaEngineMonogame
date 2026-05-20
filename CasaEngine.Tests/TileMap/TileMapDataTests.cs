using CasaEngine.Core.Math;
using CasaEngine.Framework.Assets.TileMap;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.TileMap;

public class TileMapDataTests
{
    [Fact]
    public void Load_PreservesLayerName()
    {
        var tileSetId = Guid.NewGuid();
        var tileMapData = new TileMapData();

        tileMapData.Load(CreateTileMapJson(tileSetId, "Ground", 1, 2, 3, 4));

        Assert.Equal("Ground", tileMapData.Layers[0].Name);
    }

    [Fact]
    public void Load_RejectsLayerWithWrongTileCount()
    {
        var tileSetId = Guid.NewGuid();
        var tileMapData = new TileMapData();

        Assert.Throws<InvalidOperationException>(() => tileMapData.Load(CreateTileMapJson(tileSetId, "Broken", 1, 2, 3)));
    }

    [Fact]
    public void Load_PreservesOptionalTileFlags()
    {
        var tileSetId = Guid.NewGuid();
        var tileMapData = new TileMapData();
        var document = CreateTileMapJson(tileSetId, "Ground", 1, 2, 3, 4);
        var layer = (JObject)((JArray)document["layers"]!)[0]!;
        layer["tile_flags"] = new JArray(0, (int)TileCellFlags.FlipHorizontal, (int)TileCellFlags.FlipVertical, 0);

        tileMapData.Load(document);

        Assert.Equal(TileCellFlags.FlipHorizontal, tileMapData.GetTileFlags(0, 1, 0));
        Assert.Equal(TileCellFlags.FlipVertical, tileMapData.GetTileFlags(0, 0, 1));
    }

    [Fact]
    public void Load_RejectsLayerWithWrongTileFlagCount()
    {
        var tileSetId = Guid.NewGuid();
        var tileMapData = new TileMapData();
        var document = CreateTileMapJson(tileSetId, "Broken", 1, 2, 3, 4);
        var layer = (JObject)((JArray)document["layers"]!)[0]!;
        layer["tile_flags"] = new JArray(0, 1, 0);

        Assert.Throws<InvalidOperationException>(() => tileMapData.Load(document));
    }

    [Fact]
    public void GetTileIndex_RejectsCoordinatesOutsideMap()
    {
        var tileMapData = CreateLoadedTileMap();

        Assert.Throws<ArgumentOutOfRangeException>(() => tileMapData.GetTileIndex(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => tileMapData.GetTileIndex(2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => tileMapData.GetTileIndex(0, 2));
    }

    [Fact]
    public void SetTileId_UpdatesLayerCell()
    {
        var tileMapData = CreateLoadedTileMap();

        tileMapData.SetTileId(0, 1, 1, 42);

        Assert.Equal(42, tileMapData.GetTileId(0, 1, 1));
        Assert.Equal(42, tileMapData.Layers[0].tiles[3]);
    }

    [Fact]
    public void SetTileFlags_UpdatesLayerCellFlags()
    {
        var tileMapData = CreateLoadedTileMap();

        tileMapData.SetTileFlags(0, 1, 1, TileCellFlags.FlipHorizontal | TileCellFlags.FlipVertical);

        Assert.Equal(TileCellFlags.FlipHorizontal | TileCellFlags.FlipVertical, tileMapData.GetTileFlags(0, 1, 1));
        Assert.Equal(TileCellFlags.FlipHorizontal | TileCellFlags.FlipVertical, tileMapData.Layers[0].tileFlags[3]);
    }

    [Fact]
    public void Validate_RejectsEmptyTileSetReference()
    {
        var tileMapData = new TileMapData
        {
            MapSize = new Size(1, 1),
        };
        tileMapData.Layers.Add(new TileMapLayerData { tiles = { TileMapData.EmptyTileId } });

        Assert.Throws<InvalidOperationException>(tileMapData.Validate);
    }

    private static TileMapData CreateLoadedTileMap()
    {
        var tileMapData = new TileMapData();
        tileMapData.Load(CreateTileMapJson(Guid.NewGuid(), "Ground", 1, 2, 3, 4));
        return tileMapData;
    }

    private static JObject CreateTileMapJson(Guid tileSetId, string layerName, params int[] tileIds)
    {
        return new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "test_tile_map",
            ["map_size"] = new JObject
            {
                ["w"] = 2,
                ["h"] = 2,
            },
            ["tile_set_asset_id"] = tileSetId.ToString(),
            ["layers"] = new JArray
            {
                new JObject
                {
                    ["name"] = layerName,
                    ["z_offset"] = 0f,
                    ["tiles"] = new JArray(tileIds),
                },
            },
        };
    }
}