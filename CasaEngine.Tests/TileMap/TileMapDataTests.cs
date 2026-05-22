using CasaEngine.Core.Math;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Rendering.Depth;
using Microsoft.Xna.Framework;
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
    public void Load_PreservesMultipleTileSetsAndTileSources()
    {
        var firstTileSetId = Guid.NewGuid();
        var secondTileSetId = Guid.NewGuid();
        var tileMapData = new TileMapData();
        var document = CreateTileMapJson(firstTileSetId, "Ground", 1, 2, 3, 4);
        document["tile_set_asset_ids"] = new JArray(firstTileSetId.ToString(), secondTileSetId.ToString());
        var layer = (JObject)((JArray)document["layers"]!)[0]!;
        layer["tile_sources"] = new JArray(0, 1, 1, 0);

        tileMapData.Load(document);

        Assert.Equal(new[] { firstTileSetId, secondTileSetId }, tileMapData.TileSetDataAssetIds);
        Assert.Equal(firstTileSetId, tileMapData.TileSetDataAssetId);
        Assert.Equal(1, tileMapData.GetTileSourceIndex(0, 1, 0));
        Assert.Equal(new TileMapTileReference(1, 3).TileSetIndex, tileMapData.GetTileReference(0, 0, 1).TileSetIndex);
        Assert.Equal(3, tileMapData.GetTileReference(0, 0, 1).TileId);
    }

    [Fact]
    public void Load_PreservesCustomProperties()
    {
        var tileSetId = Guid.NewGuid();
        var tileMapData = new TileMapData();
        var document = CreateTileMapJson(tileSetId, "Ground", 1, 2, 3, 4);
        document["custom_properties"] = new JObject
        {
            ["weather"] = "rain",
        };
        var layer = (JObject)((JArray)document["layers"]!)[0]!;
        layer["custom_properties"] = new JObject
        {
            ["walkable"] = "false",
        };

        tileMapData.Load(document);

        Assert.Equal("rain", tileMapData.CustomProperties["weather"]);
        Assert.Equal("false", tileMapData.Layers[0].CustomProperties["walkable"]);
    }

    [Fact]
    public void Load_ParsesLayerDepthProperties()
    {
        var tileSetId = Guid.NewGuid();
        var tileMapData = new TileMapData();
        var document = CreateTileMapJson(tileSetId, "Props", 1, 2, 3, 4);
        var layer = (JObject)((JArray)document["layers"]!)[0]!;
        layer["custom_properties"] = new JObject
        {
            ["depth.role"] = "YSortedSource",
            ["depth.renderPass"] = "YSortedWorld",
            ["depth.sortingLayer"] = "Props",
            ["depth.orderInLayer"] = "12",
            ["depth.elevation"] = "2",
            ["depth.sortAnchor"] = "16,48",
            ["depth.localSortOffset"] = "-3",
            ["depth.sortMode"] = "TopDownYUp",
        };

        tileMapData.Load(document);

        var depth = tileMapData.Layers[0].Depth;
        Assert.Equal(TileMapDepthRole.YSortedSource, depth.Role);
        Assert.Equal(RenderPass2D.YSortedWorld, depth.RenderPass);
        Assert.Equal(TileMapDepthSettings.GetStableSortingLayerId("Props"), depth.SortingLayer);
        Assert.Equal(12, depth.OrderInLayer);
        Assert.Equal(2, depth.Elevation);
        Assert.Equal(new Vector2(16f, 48f), depth.SortAnchor);
        Assert.Equal(-3, depth.LocalSortOffset);
        Assert.Equal(DepthSortMode2D.TopDownYUp, depth.SortMode);
    }

    [Fact]
    public void Load_PreservesObjectLayers()
    {
        var tileSetId = Guid.NewGuid();
        var tileMapData = new TileMapData();
        var document = CreateTileMapJson(tileSetId, "Ground", 1, 2, 3, 4);
        document["object_layers"] = new JArray
        {
            new JObject
            {
                ["name"] = "Objects",
                ["z_offset"] = 0f,
                ["objects"] = new JArray
                {
                    new JObject
                    {
                        ["id"] = 1,
                        ["name"] = "PlayerStart",
                        ["type"] = "spawn",
                        ["x"] = 16f,
                        ["y"] = 32f,
                        ["width"] = 4f,
                        ["height"] = 5f,
                        ["custom_properties"] = new JObject
                        {
                            ["team"] = "blue",
                            ["depth.sortAnchorX"] = "2",
                            ["depth.sortAnchorY"] = "5",
                            ["depth.spawnAsEntity"] = "true",
                        },
                    },
                },
                ["custom_properties"] = new JObject
                {
                    ["depth.role"] = "ObjectSource",
                    ["depth.sortingLayer"] = "25",
                },
            },
        };

        tileMapData.Load(document);

        var objectLayer = Assert.Single(tileMapData.ObjectLayers);
        Assert.Equal("Objects", objectLayer.Name);
        var objectData = Assert.Single(objectLayer.Objects);
        Assert.Equal("PlayerStart", objectData.Name);
        Assert.Equal("spawn", objectData.Type);
        Assert.Equal(16f, objectData.X);
        Assert.Equal(32f, objectData.Y);
        Assert.Equal("blue", objectData.CustomProperties["team"]);
        Assert.Equal(TileMapDepthRole.ObjectSource, objectLayer.Depth.Role);
        Assert.Equal(25, objectLayer.Depth.SortingLayer);
        Assert.Equal(new Vector2(2f, 5f), objectData.Depth.SortAnchor);
        Assert.True(objectData.Depth.SpawnAsEntity);
    }

    [Fact]
    public void Load_UsesCompatibleDepthDefaults()
    {
        var tileMapData = CreateLoadedTileMap();

        Assert.Equal(TileMapDepthRole.Ground, tileMapData.Layers[0].Depth.Role);
        Assert.Equal(RenderPass2D.Ground, tileMapData.Layers[0].Depth.RenderPass);
        Assert.Equal(DepthSortMode2D.None, tileMapData.Layers[0].Depth.SortMode);
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
    public void Load_RejectsLayerWithWrongTileSourceCount()
    {
        var tileSetId = Guid.NewGuid();
        var tileMapData = new TileMapData();
        var document = CreateTileMapJson(tileSetId, "Broken", 1, 2, 3, 4);
        var layer = (JObject)((JArray)document["layers"]!)[0]!;
        layer["tile_sources"] = new JArray(0, 1, 0);

        Assert.Throws<InvalidOperationException>(() => tileMapData.Load(document));
    }

    [Fact]
    public void Load_RejectsTileSourceOutsideTileSetList()
    {
        var tileSetId = Guid.NewGuid();
        var tileMapData = new TileMapData();
        var document = CreateTileMapJson(tileSetId, "Broken", 1, 2, 3, 4);
        var layer = (JObject)((JArray)document["layers"]!)[0]!;
        layer["tile_sources"] = new JArray(0, 1, 0, 0);

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
    public void SetTileReference_UpdatesLayerCellTileAndSource()
    {
        var tileMapData = CreateLoadedTileMap();

        tileMapData.SetTileReference(0, 1, 1, new TileMapTileReference(1, 42));

        Assert.Equal(42, tileMapData.GetTileId(0, 1, 1));
        Assert.Equal(1, tileMapData.GetTileSourceIndex(0, 1, 1));
        Assert.Equal(42, tileMapData.GetTileReference(0, 1, 1).TileId);
        Assert.Equal(1, tileMapData.GetTileReference(0, 1, 1).TileSetIndex);
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