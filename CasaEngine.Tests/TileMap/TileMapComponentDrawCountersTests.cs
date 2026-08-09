using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Xunit;
using Size = CasaEngine.Core.Math.Size;

namespace CasaEngine.Tests.TileMap;

/// <summary>
/// Verifies that the axis-aligned tile map draw path is unchanged by the world matrix / frustum work:
/// same visited chunks, same drawn tiles, same plane based visible tile range.
/// The component is built without a GraphicsDevice: tile sets are never loaded, so tiles are drawn
/// through <see cref="Tile"/> stubs and no static batch can be issued (LastStaticBatchCount stays 0).
/// </summary>
public class TileMapComponentDrawCountersTests
{
    private const int MapSize = 8;
    private const int ChunkSize = 2;
    private const int TilePixelSize = 16;

    [Fact]
    public void Draw_WithoutRenderFrame_VisitsEveryChunkAndTile()
    {
        var component = CreateTileMapComponent(out _);

        component.Draw(0f);

        Assert.Equal((MapSize / ChunkSize) * (MapSize / ChunkSize), component.LastVisitedChunkCount);
        Assert.Equal(MapSize * MapSize, component.LastVisitedTileCount);
        Assert.Equal(MapSize * MapSize, component.LastDrawnTileCount);
        Assert.Equal(0, component.LastStaticBatchCount);
    }

    [Fact]
    public void Draw_WithRenderFrame_KeepsPlaneBasedVisibleTileRange()
    {
        var component = CreateTileMapComponent(out var world);

        // Orthographic view centred on the top left tile, two tiles wide and high. The plane based
        // range adds a one tile margin on each side, so tiles 0..2 on X and 0..2 on Y are visible.
        SetVisibleWorldWindow(world, TilePixelSize, TilePixelSize);

        component.Draw(0f);

        Assert.Equal(4, component.LastVisitedChunkCount);
        Assert.Equal(3 * 3, component.LastVisitedTileCount);
        Assert.Equal(3 * 3, component.LastDrawnTileCount);
    }

    [Fact]
    public void Draw_WithRotation_DrawsTheSameTilesWhenNothingIsCulled()
    {
        var component = CreateTileMapComponent(out _);
        component.LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathHelper.PiOver2);

        component.Draw(0f);

        Assert.Equal((MapSize / ChunkSize) * (MapSize / ChunkSize), component.LastVisitedChunkCount);
        Assert.Equal(MapSize * MapSize, component.LastVisitedTileCount);
        Assert.Equal(MapSize * MapSize, component.LastDrawnTileCount);
    }

    [Fact]
    public void Draw_WithRotation_CullsChunksOutsideTheViewFrustum()
    {
        var component = CreateTileMapComponent(out var world);
        component.LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathHelper.PiOver2);
        SetVisibleWorldWindow(world, TilePixelSize, TilePixelSize);

        component.Draw(0f);

        // The map now lies flat in the XZ plane: it is edge-on for a camera looking down -Z,
        // so only the chunks crossing the view window survive the frustum test.
        Assert.True(component.LastVisitedChunkCount < (MapSize / ChunkSize) * (MapSize / ChunkSize));
        Assert.True(component.LastVisitedChunkCount > 0);
    }

    private static TileMapComponent CreateTileMapComponent(out World world)
    {
        world = new World();
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        SetProperty(world, nameof(World.Game), game);

        var entity = new Entity();
        SetProperty(entity, nameof(Entity.World), world);

        var component = new TileMapComponent { ChunkTileSize = ChunkSize };
        entity.RootComponent = component;

        var tileMapData = new TileMapData { MapSize = new Size(MapSize, MapSize) };
        var layerData = new TileMapLayerData();
        for (var tileIndex = 0; tileIndex < MapSize * MapSize; tileIndex++)
        {
            layerData.tiles.Add(1);
        }

        tileMapData.Layers.Add(layerData);
        component.TileMapData = tileMapData;
        component.TileSetData = new TileSetData { TileSize = new Size(TilePixelSize, TilePixelSize) };

        var layer = new TileMapLayer(layerData);
        for (var tileIndex = 0; tileIndex < MapSize * MapSize; tileIndex++)
        {
            layer.Tiles.Add(new CountingTile());
        }

        GetLayers(component).Add(layer);
        InvokeBuildChunks(component, layer);

        return component;
    }

    private static void SetVisibleWorldWindow(World world, float width, float height)
    {
        var center = new Vector3(width / 2f, -height / 2f, 0f);
        var view = Matrix.CreateLookAt(center + Vector3.Backward * 10f, center, Vector3.Up);
        var projection = Matrix.CreateOrthographic(width, height, 0.1f, 100f);
        var frame = new RenderFrame(view, projection, center + Vector3.Backward * 10f, new Rectangle(0, 0, (int)width, (int)height));

        SetProperty(world, nameof(World.CurrentRenderFrame), (RenderFrame?)frame);
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

    private sealed class CountingTile : Tile
    {
        public CountingTile() : base(null)
        {
        }

        public override void Update(float elapsedTime)
        {
        }

        public override void Draw(float x, float y, float z, Vector2 scale)
        {
        }

        public override void Draw(float x, float y, float z, Rectangle uvOffset, Vector2 scale)
        {
        }
    }
}
