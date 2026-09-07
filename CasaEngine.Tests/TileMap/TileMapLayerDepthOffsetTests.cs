using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;
using Size = CasaEngine.Core.Math.Size;

namespace CasaEngine.Tests.TileMap;

/// <summary>
/// T1.1 (D1, D6, D7): a chunked tile map layer that carries depth metadata folds its render pass into
/// its world Z; every other layer, and every draw with no <c>depth.*</c> key anywhere on the map,
/// is unchanged character for character. Covers both draw paths, both culling sites
/// (<c>GetRenderedLayerWorldZRange</c> and <see cref="TileMapComponent.GetBoundingBox"/>), and that
/// layer-index addressing survives the Z change.
///
/// Component setup follows <see cref="TileMapComponentDrawCountersTests"/>: no GraphicsDevice, so tiles
/// are drawn through <see cref="Tile"/> stubs and never batched, and private wiring (the runtime
/// <c>Layers</c> list, <c>BuildChunks</c>) is reached by reflection instead of a full asset load.
/// </summary>
public class TileMapLayerDepthOffsetTests
{
    private const int TilePixelSize = 16;
    private const int MapSize = 2;

    [Fact]
    public void DrawTileMap_LayerWithDepthMetadata_FoldsItsRenderPassIntoWorldZ()
    {
        var (component, _, tiles) = CreateComponent(translationZ: 5f);

        component.Draw(0f);

        // No depth.* key: unchanged, translation.Z + zOffset.
        Assert.Equal(5f + 0f, tiles[0].LastZ);
        // depth.role=Ground only, zOffset 0: translation.Z + DeriveDepthOffset(Ground) + 0.
        Assert.Equal(5f + RenderPassDepthOffset.DeriveDepthOffset(RenderPass2D.Ground) + 0f, tiles[1].LastZ);
        // depth.role=Foreground, zOffset 0.1: translation.Z + DeriveDepthOffset(Foreground) + 0.1.
        Assert.Equal(5f + RenderPassDepthOffset.DeriveDepthOffset(RenderPass2D.Foreground) + 0.1f, tiles[2].LastZ, 5);
    }

    [Fact]
    public void DrawWithWorldMatrix_LayerWithDepthMetadata_FoldsItsRenderPassIntoWorldZ()
    {
        // A rotation forces the DrawWithWorldMatrix path; the world matrix itself carries the
        // translation there, so the z argument passed to each tile is the layer offset alone.
        var (component, _, tiles) = CreateComponent(translationZ: 5f);
        component.LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.PiOver4);

        component.Draw(0f);

        Assert.Equal(0f, tiles[0].LastZ);
        Assert.Equal(RenderPassDepthOffset.DeriveDepthOffset(RenderPass2D.Ground) + 0f, tiles[1].LastZ);
        Assert.Equal(RenderPassDepthOffset.DeriveDepthOffset(RenderPass2D.Foreground) + 0.1f, tiles[2].LastZ, 5);
    }

    [Fact]
    public void GetRenderedLayerWorldZRange_MatchesTheZValuesActuallyUsedAtDraw()
    {
        var (component, _, tiles) = CreateComponent(translationZ: 5f);
        component.Draw(0f);
        var drawnZs = tiles.Select(t => t.LastZ).ToArray();

        InvokeGetRenderedLayerWorldZRange(component, 5f, out var minZ, out var maxZ);

        Assert.Equal(drawnZs.Min(), minZ);
        Assert.Equal(drawnZs.Max(), maxZ);
    }

    [Fact]
    public void GetRenderedLayerWorldZRange_NoDepthMetadataAnywhere_MatchesTodaysZOffsetOnlyRange()
    {
        var (component, _, _) = CreateComponentWithoutDepthMetadata(translationZ: 5f);

        InvokeGetRenderedLayerWorldZRange(component, 5f, out var minZ, out var maxZ);

        // Today's behaviour: translationZ + zOffset, nothing else.
        Assert.Equal(5f + 0f, minZ);
        Assert.Equal(5f + 0.8f, maxZ, 5);
    }

    [Fact]
    public void GetBoundingBox_MatchesTheZValuesActuallyUsedAtDraw()
    {
        var (component, _, tiles) = CreateComponent(translationZ: 5f);
        component.Draw(0f);
        var drawnZs = tiles.Select(t => t.LastZ).ToArray();

        var boundingBox = component.GetBoundingBox();

        Assert.Equal(drawnZs.Min(), boundingBox.Min.Z, 4);
        Assert.Equal(drawnZs.Max(), boundingBox.Max.Z, 4);
    }

    [Fact]
    public void GetBoundingBox_NoDepthMetadataAnywhere_MatchesTodaysZOffsetOnlyExtent()
    {
        var (component, _, _) = CreateComponentWithoutDepthMetadata(translationZ: 5f);

        var boundingBox = component.GetBoundingBox();

        Assert.Equal(5f + 0f, boundingBox.Min.Z, 4);
        Assert.Equal(5f + 0.8f, boundingBox.Max.Z, 4);
    }

    [Fact]
    public void GetTileReference_ReturnsTheSameTilePerLayerIndex_WhenMetadataChangesZ()
    {
        var (component, _, _) = CreateComponent(translationZ: 5f);

        // Distinct tile ids per layer (see CreateComponent): layer index still addresses the right
        // layer's data even though the three layers now draw at very different world Z.
        Assert.Equal(1, component.TileMapData.GetTileReference(0, 0, 0).TileId);
        Assert.Equal(2, component.TileMapData.GetTileReference(1, 0, 0).TileId);
        Assert.Equal(3, component.TileMapData.GetTileReference(2, 0, 0).TileId);
    }

    /// <summary>
    /// Three layers, all 2x2, tile ids 1/2/3 so <see cref="GetTileReference"/> can prove addressing:
    /// layer 0 carries no <c>depth.*</c> key (unchanged), layer 1 carries only <c>depth.role=Ground</c>
    /// (D7: metadata present, settings otherwise identical to the default), layer 2 carries
    /// <c>depth.role=Foreground</c> with a non-zero zOffset.
    /// </summary>
    private static (TileMapComponent Component, World World, List<RecordingTile> Tiles) CreateComponent(float translationZ)
    {
        var document = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "test_tile_map",
            ["map_size"] = new JObject { ["w"] = MapSize, ["h"] = MapSize },
            ["tile_set_asset_id"] = Guid.NewGuid().ToString(),
            ["layers"] = new JArray
            {
                CreateLayerJson("no_metadata", 0f, tileId: 1, customProperties: null),
                CreateLayerJson("ground_metadata", 0f, tileId: 2, customProperties: new JObject { ["depth.role"] = "Ground" }),
                CreateLayerJson("foreground_metadata", 0.1f, tileId: 3, customProperties: new JObject { ["depth.role"] = "Foreground" }),
            },
        };

        var tileMapData = new TileMapData();
        tileMapData.Load(document);

        return BuildComponent(tileMapData, translationZ);
    }

    /// <summary>D6 shape: same four zOffsets as <c>CasaEngine.Demos/Content/Maps/map_1_1.tileMap</c>
    /// (0 / 0.1 / 0.2 / 0.8), no <c>depth.*</c> key anywhere - today's ordering must be untouched.</summary>
    private static (TileMapComponent Component, World World, List<RecordingTile> Tiles) CreateComponentWithoutDepthMetadata(float translationZ)
    {
        var document = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "test_tile_map",
            ["map_size"] = new JObject { ["w"] = MapSize, ["h"] = MapSize },
            ["tile_set_asset_id"] = Guid.NewGuid().ToString(),
            ["layers"] = new JArray
            {
                CreateLayerJson("l0", 0f, tileId: 1, customProperties: null),
                CreateLayerJson("l1", 0.1f, tileId: 1, customProperties: null),
                CreateLayerJson("l2", 0.2f, tileId: 1, customProperties: null),
                CreateLayerJson("l3", 0.8f, tileId: 1, customProperties: null),
            },
        };

        var tileMapData = new TileMapData();
        tileMapData.Load(document);

        return BuildComponent(tileMapData, translationZ);
    }

    private static JObject CreateLayerJson(string name, float zOffset, int tileId, JObject customProperties)
    {
        var tileIds = new int[MapSize * MapSize];
        for (var index = 0; index < tileIds.Length; index++)
        {
            tileIds[index] = tileId;
        }

        var layer = new JObject
        {
            ["name"] = name,
            ["z_offset"] = zOffset,
            ["tiles"] = new JArray(tileIds),
        };

        if (customProperties != null)
        {
            layer["custom_properties"] = customProperties;
        }

        return layer;
    }

    private static (TileMapComponent Component, World World, List<RecordingTile> Tiles) BuildComponent(TileMapData tileMapData, float translationZ)
    {
        var world = new World();
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        SetProperty(world, nameof(World.Game), game);

        var entity = new Entity();
        SetProperty(entity, nameof(Entity.World), world);

        var component = new TileMapComponent { ChunkTileSize = MapSize };
        entity.RootComponent = component;
        component.Position = new Vector3(0f, 0f, translationZ);

        // Runs through the working copy, exactly what the production loader hands the component: D7
        // exists because HasDepthMetadata must survive CreateWorldWorkingCopy, and this test must
        // exercise that path rather than the freshly loaded template.
        component.TileMapData = tileMapData.CreateWorldWorkingCopy();
        component.TileSetData = new TileSetData { TileSize = new Size(TilePixelSize, TilePixelSize) };

        var tiles = new List<RecordingTile>();
        var layers = GetLayers(component);
        for (var layerIndex = 0; layerIndex < component.TileMapData.Layers.Count; layerIndex++)
        {
            var layerData = component.TileMapData.Layers[layerIndex];
            var layer = new TileMapLayer(layerData);

            RecordingTile firstTile = null;
            for (var tileIndex = 0; tileIndex < layerData.tiles.Count; tileIndex++)
            {
                var tile = new RecordingTile();
                firstTile ??= tile;
                layer.Tiles.Add(tile);
            }

            tiles.Add(firstTile);
            layers.Add(layer);
            InvokeBuildChunks(component, layer);
        }

        return (component, world, tiles);
    }

    private static void InvokeGetRenderedLayerWorldZRange(TileMapComponent component, float translationZ, out float minZ, out float maxZ)
    {
        var method = typeof(TileMapComponent).GetMethod("GetRenderedLayerWorldZRange", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var args = new object[] { translationZ, 0f, 0f };
        method!.Invoke(component, args);
        minZ = (float)args[1];
        maxZ = (float)args[2];
    }

    private static List<TileMapLayer> GetLayers(TileMapComponent component)
    {
        var property = typeof(TileMapComponent).GetProperty("Layers", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return (List<TileMapLayer>)property!.GetValue(component)!;
    }

    private static void InvokeBuildChunks(TileMapComponent component, TileMapLayer layer)
    {
        var method = typeof(TileMapComponent).GetMethod("BuildChunks", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(component, new object[] { layer, 0 });
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var property = typeof(TTarget).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    private sealed class RecordingTile : Tile
    {
        public float LastZ { get; private set; } = float.NaN;

        public RecordingTile() : base(null)
        {
        }

        public override void Update(float elapsedTime)
        {
        }

        public override void Draw(float x, float y, float z, Vector2 scale)
        {
            LastZ = z;
        }

        public override void Draw(float x, float y, float z, Rectangle uvOffset, Vector2 scale)
        {
            LastZ = z;
        }

        public override void Draw(float x, float y, float z, Vector2 scale, TileCellFlags flags)
        {
            LastZ = z;
        }

        public override void Draw(float x, float y, float z, Vector2 scale, TileCellFlags flags, in Matrix worldTransform)
        {
            LastZ = z;
        }

        public override Rectangle GetCurrentSourceRectangle()
        {
            return Rectangle.Empty;
        }
    }
}
