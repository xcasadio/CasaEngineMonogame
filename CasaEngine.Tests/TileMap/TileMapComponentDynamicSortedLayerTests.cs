using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Xunit;
using Size = CasaEngine.Core.Math.Size;

namespace CasaEngine.Tests.TileMap;

/// <summary>
/// T2.1 (D2): a tile map layer whose <see cref="TileMapDepthSettings.UsesDynamicSort"/> is true leaves
/// the static chunked draw path and submits every visible tile through the keyed sprite queue instead,
/// at the tile map entity's coplanar Z, carrying its own <see cref="RenderSortKey2D"/> and
/// <see cref="TileCellFlags"/>. Covers the predicate's precedence over role (D2), the key construction,
/// the coplanar Z policy, flag propagation, the rotated-path fallback with its once-per-layer warning,
/// and ordering against a directly submitted Y-sorted sprite.
///
/// Component setup mirrors <see cref="TileMapComponentSortedOverlayTests"/> (a real
/// <see cref="SpriteRendererComponent"/>, no GraphicsDevice) crossed with
/// <see cref="TileMapLayerDepthOffsetTests"/> (layers built by loading real <c>depth.*</c> custom
/// properties through <see cref="TileMapData.Load"/>, then through
/// <see cref="TileMapData.CreateWorldWorkingCopy"/> exactly like the production loader).
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class TileMapComponentDynamicSortedLayerTests
{
    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = new();

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) => Warnings.Add(msg);
        public void WriteError(string msg) { }
    }


    private const int TilePixelSize = 16;
    private const int MapSize = 2;
    private const int TileId = 1;

    [Fact]
    public void DynamicSortLayer_PrimesOverItsRole_LeavesTheChunkedBatch_TilesSubmittedWithAKey()
    {
        // depth.role = Ground (KeepsStaticChunking) AND depth.ySort = true (UsesDynamicSort): D2 says
        // the explicit sort request wins over the role.
        var (component, _, tiles, spriteRendererComponent) = CreateComponent(
            translationZ: 5f,
            customProperties: new JObject { ["depth.role"] = "Ground", ["depth.ySort"] = "true" });

        component.Draw(0f);

        // Never drawn through the flat/chunked Tile.Draw path.
        Assert.True(float.IsNaN(tiles[0].LastZ));

        // Drawn through the keyed sprite path instead: one submission per non-empty tile (all four here).
        var spriteDatas = GetSpriteDatas(spriteRendererComponent);
        Assert.Equal(MapSize * MapSize, spriteDatas.Count);
        Assert.All(spriteDatas, entry => Assert.True((bool)GetField(entry, "HasSortKey")));
    }

    [Fact]
    public void DynamicSortLayer_KeyFields_ComeFromLayerDepthAndTileIndex()
    {
        var customProperties = new JObject
        {
            ["depth.role"] = "Ground",
            ["depth.renderPass"] = "Foreground",
            ["depth.sortingLayer"] = "7",
            ["depth.orderInLayer"] = "12",
            ["depth.elevation"] = "-2",
            ["depth.localSortOffset"] = "3",
            ["depth.sortMode"] = "TopDownYUp",
        };
        var (component, _, _, spriteRendererComponent) = CreateComponent(translationZ: 0f, customProperties);

        component.Draw(0f);

        var spriteDatas = GetSpriteDatas(spriteRendererComponent);
        Assert.Equal(MapSize * MapSize, spriteDatas.Count);

        foreach (var entry in spriteDatas)
        {
            var key = (RenderSortKey2D)GetField(entry, "SortKey");
            Assert.Equal((int)RenderPass2D.Foreground, key.RenderPass);
            Assert.Equal(7, key.SortingLayer);
            Assert.Equal(12, key.OrderInLayer);
            Assert.Equal(-2, key.Elevation);
            Assert.Equal(3, key.LocalSortOffset);
        }

        // StableId is the tile index (row-major, row 0 then row 1 for a 2x2 map): distinct per tile,
        // and every tile here shares the same reference so nothing else could produce the distinction.
        var stableIds = spriteDatas.Select(entry => ((RenderSortKey2D)GetField(entry, "SortKey")).StableId).OrderBy(id => id).ToArray();
        Assert.Equal(new[] { 0, 1, 2, 3 }, stableIds);

        // TopDownYUp: a tile on a lower grid row (larger gridY, smaller world Y) sorts after one on a
        // higher row - same ordering a Y-sorted entity sprite would get from DepthSortable2DComponent.
        var topRowEntry = spriteDatas.Single(entry => ((RenderSortKey2D)GetField(entry, "SortKey")).StableId == 0);
        var bottomRowEntry = spriteDatas.Single(entry => ((RenderSortKey2D)GetField(entry, "SortKey")).StableId == 2);
        var topRowCoordinate = ((RenderSortKey2D)GetField(topRowEntry, "SortKey")).SortCoordinate;
        var bottomRowCoordinate = ((RenderSortKey2D)GetField(bottomRowEntry, "SortKey")).SortCoordinate;
        Assert.True(bottomRowCoordinate > topRowCoordinate);
    }

    [Fact]
    public void DynamicSortLayer_SubmitsAtTheComponentsCoplanarZ_NeverAtThePassOrZOffsetDerivedZ()
    {
        // A non-zero zOffset and a render pass far from YSortedWorld: if either leaked into the
        // submitted Z, it would move it away from translationZ.
        var customProperties = new JObject
        {
            ["depth.role"] = "Foreground",
            ["depth.ySort"] = "true",
        };
        var (component, _, _, spriteRendererComponent) = CreateComponent(
            translationZ: 5f, customProperties, zOffset: 0.8f);

        component.Draw(0f);

        var spriteDatas = GetSpriteDatas(spriteRendererComponent);
        Assert.NotEmpty(spriteDatas);
        Assert.All(spriteDatas, entry =>
        {
            var worldMatrix = (Matrix)GetField(entry, "WorldMatrix");
            Assert.Equal(5f, worldMatrix.Translation.Z);
        });
    }

    [Fact]
    public void DynamicSortLayer_MirroredTile_IsSubmittedWithFlipHorizontally()
    {
        var customProperties = new JObject { ["depth.role"] = "Ground", ["depth.ySort"] = "true" };
        var (component, _, _, spriteRendererComponent) = CreateComponent(
            translationZ: 0f, customProperties, tileFlags: new[] { TileCellFlags.FlipHorizontal, TileCellFlags.None, TileCellFlags.None, TileCellFlags.None });

        component.Draw(0f);

        var spriteDatas = GetSpriteDatas(spriteRendererComponent);
        var flippedEntry = spriteDatas[0]!;

        // The device-less Texture2D used here reports a zero size, so the queued UVs cannot be read
        // back as meaningful 0..1 coordinates (same limitation CellularLayerComponentSubmissionTests
        // notes) - but source rectangle Left is 0 and Right is 16, so a flip is still observable from
        // WHICH corner divides a non-zero numerator by that zero size (+Infinity) versus a zero
        // numerator (0/0, NaN): flipped swaps the source rectangle's left/right onto TopLeft/TopRight
        // (see SpriteRendererComponent.DrawSprite), so TopLeft reads Right (16) -> +Infinity here.
        var topLeftU = GetVertexTextureCoordinateX(flippedEntry, "TopLeft");
        var topRightU = GetVertexTextureCoordinateX(flippedEntry, "TopRight");
        Assert.True(float.IsPositiveInfinity(topLeftU));
        Assert.True(float.IsNaN(topRightU));
    }

    [Fact]
    public void DynamicSortLayer_UnflaggedTile_IsSubmittedWithNoFlip()
    {
        var customProperties = new JObject { ["depth.role"] = "Ground", ["depth.ySort"] = "true" };
        var (component, _, _, spriteRendererComponent) = CreateComponent(translationZ: 0f, customProperties);

        component.Draw(0f);

        var spriteDatas = GetSpriteDatas(spriteRendererComponent);
        var entry = spriteDatas[0]!;

        // Unflipped: TopLeft reads source rectangle Left (0) -> 0/0, NaN; TopRight reads Right (16) ->
        // +Infinity. The reverse of the flipped case above.
        var topLeftU = GetVertexTextureCoordinateX(entry, "TopLeft");
        var topRightU = GetVertexTextureCoordinateX(entry, "TopRight");
        Assert.True(float.IsNaN(topLeftU));
        Assert.True(float.IsPositiveInfinity(topRightU));
    }

    [Fact]
    public void DrawWithWorldMatrix_DynamicSortLayer_DrawsFlat_AndWarnsExactlyOnceAcrossTenFrames()
    {
        var customProperties = new JObject { ["depth.role"] = "Ground", ["depth.ySort"] = "true" };
        var (component, _, tiles, spriteRendererComponent) = CreateComponent(translationZ: 5f, customProperties);
        component.LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.PiOver4);

        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            for (var frame = 0; frame < 10; frame++)
            {
                component.Draw(0f);
            }
        }
        finally
        {
            Logs.Close(); // detaches every logger added during this test, including `logger`.
        }

        var dynamicSortWarnings = logger.Warnings.Where(w => w.Contains("dynamic depth sort", StringComparison.Ordinal)).ToList();
        Assert.Single(dynamicSortWarnings);

        // Drawn flat, through the unchanged chunked path (RecordingTile.Draw got called), never through
        // the keyed sprite queue: no keyed DrawSprite overload accepts a world transform.
        Assert.False(float.IsNaN(tiles[0].LastZ));
        Assert.Empty(GetSpriteDatas(spriteRendererComponent));

        // ShouldRenderTiles is still honoured for such a layer under rotation.
        Assert.Equal(0f + 0f, tiles[0].LastZ);
    }

    [Fact]
    public void DynamicSortLayer_TileOrdersAgainstADirectlyKeyedSprite_ByTheSameContract()
    {
        // Two tiles of a UsesDynamicSort layer, TopDownYUp: row 0 (top) then row 1 (bottom). A directly
        // submitted Y-sorted sprite between them, at a world Y that must sort between the two rows.
        var customProperties = new JObject
        {
            ["depth.role"] = "Ground",
            ["depth.ySort"] = "true",
        };
        var (component, _, _, spriteRendererComponent) = CreateComponent(translationZ: 0f, customProperties);

        component.Draw(0f);
        var tileSpriteDatas = GetSpriteDatas(spriteRendererComponent);
        Assert.Equal(MapSize * MapSize, tileSpriteDatas.Count);

        var topRowKey = (RenderSortKey2D)GetField(
            tileSpriteDatas.Single(entry => ((RenderSortKey2D)GetField(entry, "SortKey")).StableId == 0), "SortKey");
        var bottomRowKey = (RenderSortKey2D)GetField(
            tileSpriteDatas.Single(entry => ((RenderSortKey2D)GetField(entry, "SortKey")).StableId == 2), "SortKey");

        // A sprite key whose SortCoordinate sits strictly between the two tile rows' coordinates.
        var betweenCoordinate = (topRowKey.SortCoordinate + bottomRowKey.SortCoordinate) / 2;
        var spriteKey = new RenderSortKey2D(
            topRowKey.RenderPass, topRowKey.SortingLayer, topRowKey.OrderInLayer, topRowKey.Elevation,
            betweenCoordinate, topRowKey.LocalSortOffset, 999);

        var textures = GetPrivateField<System.Collections.IList>(component, "_tileSetTextures");
        var texture = (Texture2D)textures[0]!;
        spriteRendererComponent.DrawSprite(
            texture, new Rectangle(0, 0, TilePixelSize, TilePixelSize), Point.Zero,
            new Vector2(500f, -500f), 0f, Vector2.One, Color.White, 0f, in spriteKey, SpriteEffects.None, Rectangle.Empty);

        var compare = GetCompareSpriteDisplayData();
        var allSpriteDatas = GetSpriteDatas(spriteRendererComponent);
        var topEntry = allSpriteDatas.Single(entry => ((RenderSortKey2D)GetField(entry, "SortKey")).StableId == 0);
        var midEntry = allSpriteDatas.Single(entry => ((RenderSortKey2D)GetField(entry, "SortKey")).StableId == 999);
        var bottomEntry = allSpriteDatas.Single(entry => ((RenderSortKey2D)GetField(entry, "SortKey")).StableId == 2);

        Assert.True((int)compare.Invoke(null, new[] { topEntry, midEntry })! < 0);
        Assert.True((int)compare.Invoke(null, new[] { midEntry, bottomEntry })! < 0);
    }

    private static float GetVertexTextureCoordinateX(object spriteDisplayData, string vertexFieldName)
    {
        var vertex = GetField(spriteDisplayData, vertexFieldName);
        var textureCoordinateField = vertex.GetType().GetField("TextureCoordinate", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(textureCoordinateField);
        var textureCoordinate = (Vector2)textureCoordinateField!.GetValue(vertex)!;
        return textureCoordinate.X;
    }

    /// <summary>
    /// A 2x2 tile map with a single layer, loaded from real JSON custom properties through
    /// <see cref="TileMapData.Load"/> and <see cref="TileMapData.CreateWorldWorkingCopy"/>, exactly like
    /// the production loader hands the component. A real <see cref="SpriteRendererComponent"/> is wired
    /// so the keyed submissions the new draw path makes can be inspected.
    /// </summary>
    private static (TileMapComponent Component, World World, List<RecordingTile> Tiles, SpriteRendererComponent SpriteRendererComponent) CreateComponent(
        float translationZ,
        JObject customProperties,
        float zOffset = 0f,
        TileCellFlags[] tileFlags = null)
    {
        var tileIds = new int[MapSize * MapSize];
        for (var index = 0; index < tileIds.Length; index++)
        {
            tileIds[index] = TileId;
        }

        var layerJson = new JObject
        {
            ["name"] = "dynamic_sort_layer",
            ["z_offset"] = zOffset,
            ["tiles"] = new JArray(tileIds),
        };

        if (tileFlags != null)
        {
            layerJson["tile_flags"] = new JArray(tileFlags.Select(f => (int)f));
        }

        if (customProperties != null)
        {
            layerJson["custom_properties"] = customProperties;
        }

        var document = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "test_tile_map",
            ["map_size"] = new JObject { ["w"] = MapSize, ["h"] = MapSize },
            ["tile_set_asset_id"] = Guid.NewGuid().ToString(),
            ["layers"] = new JArray { layerJson },
        };

        var tileMapData = new TileMapData();
        tileMapData.Load(document);

        var world = new World();
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        SetBaseField<Game>(game, "_components", new GameComponentCollection());
        SetProperty(world, nameof(World.Game), game);

        var entity = new Entity();
        SetProperty(entity, nameof(Entity.World), world);

        var component = new TileMapComponent { ChunkTileSize = MapSize };
        entity.RootComponent = component;
        component.Position = new Vector3(0f, 0f, translationZ);

        // Runs through the working copy, exactly what the production loader hands the component (D7:
        // HasDepthMetadata must survive it).
        component.TileMapData = tileMapData.CreateWorldWorkingCopy();
        component.TileSetData = new TileSetData { TileSize = new Size(TilePixelSize, TilePixelSize) };

        GetPrivateField<System.Collections.IList>(component, "_tileSets").Add(component.TileSetData);
        GetPrivateField<System.Collections.IList>(component, "_tileSetTextures").Add(RuntimeHelpers.GetUninitializedObject(typeof(Texture2D)));

        var spriteRendererComponent = new SpriteRendererComponent(game);
        SetPrivateField(component, "_spriteRendererComponent", spriteRendererComponent);

        var tiles = new List<RecordingTile>();
        var layers = GetLayers(component);
        var layerData = component.TileMapData.Layers[0];
        var layer = new TileMapLayer(layerData);

        for (var tileIndex = 0; tileIndex < layerData.tiles.Count; tileIndex++)
        {
            var tile = new RecordingTile();
            tiles.Add(tile);
            layer.Tiles.Add(tile);
        }

        layers.Add(layer);
        InvokeBuildChunks(component, layer);

        return (component, world, tiles, spriteRendererComponent);
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

    private static void SetBaseField<TBase>(object instance, string fieldName, object value)
    {
        var field = typeof(TBase).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static TField GetPrivateField<TField>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (TField)field!.GetValue(instance)!;
    }

    private static List<object> GetSpriteDatas(SpriteRendererComponent spriteRendererComponent)
    {
        var field = typeof(SpriteRendererComponent).GetField("_spriteDatas", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var list = (System.Collections.IList)field!.GetValue(spriteRendererComponent)!;
        var result = new List<object>(list.Count);
        foreach (var entry in list)
        {
            result.Add(entry!);
        }

        return result;
    }

    private static MethodInfo GetCompareSpriteDisplayData()
    {
        var method = typeof(SpriteRendererComponent).GetMethod("CompareSpriteDisplayData", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method!;
    }

    private static object GetField(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return field!.GetValue(instance)!;
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

        // A non-empty rectangle: DynamicSortLayer_MirroredTile_IsSubmittedWithFlipHorizontally needs
        // distinct left/right texture coordinates to prove a flip actually swapped them.
        public override Rectangle GetCurrentSourceRectangle()
        {
            return new Rectangle(0, 0, TilePixelSize, TilePixelSize);
        }
    }
}
