using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;
using Size = CasaEngine.Core.Math.Size;

namespace CasaEngine.Tests.TileMap;

/// <summary>
/// Dirty tracking of <see cref="TileMapSurfaceComponent"/>: the redraw predicate itself and the
/// <see cref="TileMapComponent.TileRevision"/> counter it relies on. The GPU pass is not testable
/// without a graphics device, so only the pure logic is covered here.
/// </summary>
public class TileMapSurfaceInvalidationTests
{
    private const int MapSize = 4;
    private const int ChunkSize = 2;
    private const int TilePixelSize = 16;

    [Fact]
    public void ShouldRedraw_FirstPass_RendersOnce()
    {
        Assert.True(TileMapSurfaceComponent.ShouldRedraw(
            neverRendered: true,
            invalidated: false,
            hasAnimatedTiles: false,
            hasDirtyAutoTiles: false,
            tileRevision: 0,
            lastRenderedTileRevision: 0));
    }

    [Fact]
    public void ShouldRedraw_StaticMapAlreadyRendered_DoesNotRedraw()
    {
        Assert.False(TileMapSurfaceComponent.ShouldRedraw(
            neverRendered: false,
            invalidated: false,
            hasAnimatedTiles: false,
            hasDirtyAutoTiles: false,
            tileRevision: 7,
            lastRenderedTileRevision: 7));
    }

    [Fact]
    public void ShouldRedraw_TileRevisionChanged_Redraws()
    {
        Assert.True(TileMapSurfaceComponent.ShouldRedraw(
            neverRendered: false,
            invalidated: false,
            hasAnimatedTiles: false,
            hasDirtyAutoTiles: false,
            tileRevision: 8,
            lastRenderedTileRevision: 7));
    }

    [Fact]
    public void ShouldRedraw_AnimatedTiles_RedrawsEveryPass()
    {
        Assert.True(TileMapSurfaceComponent.ShouldRedraw(
            neverRendered: false,
            invalidated: false,
            hasAnimatedTiles: true,
            hasDirtyAutoTiles: false,
            tileRevision: 7,
            lastRenderedTileRevision: 7));
    }

    [Fact]
    public void ShouldRedraw_DirtyAutoTiles_Redraws()
    {
        Assert.True(TileMapSurfaceComponent.ShouldRedraw(
            neverRendered: false,
            invalidated: false,
            hasAnimatedTiles: false,
            hasDirtyAutoTiles: true,
            tileRevision: 7,
            lastRenderedTileRevision: 7));
    }

    [Fact]
    public void ShouldRedraw_ExplicitInvalidate_Redraws()
    {
        Assert.True(TileMapSurfaceComponent.ShouldRedraw(
            neverRendered: false,
            invalidated: true,
            hasAnimatedTiles: false,
            hasDirtyAutoTiles: false,
            tileRevision: 7,
            lastRenderedTileRevision: 7));
    }

    [Fact]
    public void TileRevision_TileMutation_IsBumped()
    {
        var component = CreateTileMapComponent();
        var revisionBeforeMutation = component.TileRevision;

        component.SetTileReference(0, 1, 1, TileMapTileReference.Empty);

        Assert.NotEqual(revisionBeforeMutation, component.TileRevision);
    }

    [Fact]
    public void TileRevision_UnchangedTile_IsNotBumped()
    {
        var component = CreateTileMapComponent();
        component.SetTileReference(0, 1, 1, TileMapTileReference.Empty);
        var revisionAfterMutation = component.TileRevision;

        component.SetTileReference(0, 1, 1, TileMapTileReference.Empty);

        Assert.Equal(revisionAfterMutation, component.TileRevision);
    }

    [Fact]
    public void TileRevision_DrawingTheMap_IsNotBumped()
    {
        var component = CreateTileMapComponent();
        var revisionBeforeDraw = component.TileRevision;

        component.Draw(0f);
        component.Draw(0f);

        Assert.Equal(revisionBeforeDraw, component.TileRevision);
    }

    [Fact]
    public void SkipMainPassDraw_SkipsTheWorldPassButNotTheSurfacePass()
    {
        var component = CreateTileMapComponent();
        component.SkipMainPassDraw = true;

        component.Draw(0f);
        Assert.Equal(0, component.LastDrawnTileCount);

        component.DrawTileMap();
        Assert.Equal(MapSize * MapSize, component.LastDrawnTileCount);
    }

    [Fact]
    public void SkipMainPassDraw_IsPreservedByTheCopyConstructor()
    {
        var component = CreateTileMapComponent();
        component.SkipMainPassDraw = true;

        var clone = component.Clone();

        Assert.True(clone.SkipMainPassDraw);
    }

    [Fact]
    public void TryGetSurfaceSize_DefaultsToTheMapSizeInPixels()
    {
        var component = CreateTileMapComponent();

        Assert.True(TileMapSurfaceComponent.TryGetSurfaceSize(component, 1, out var width, out var height));
        Assert.Equal(MapSize * TilePixelSize, width);
        Assert.Equal(MapSize * TilePixelSize, height);
    }

    [Fact]
    public void TryGetSurfaceSize_ZoomMultipliesTheResolution()
    {
        var component = CreateTileMapComponent();

        Assert.True(TileMapSurfaceComponent.TryGetSurfaceSize(component, 3, out var width, out var height));
        Assert.Equal(MapSize * TilePixelSize * 3, width);
        Assert.Equal(MapSize * TilePixelSize * 3, height);
    }

    [Fact]
    public void TryGetSurfaceSize_IsClampedToTheMaximumSurfaceSize()
    {
        var component = CreateTileMapComponent();

        Assert.True(TileMapSurfaceComponent.TryGetSurfaceSize(component, 1000, out var width, out var height));
        Assert.True(width <= TileMapSurfaceComponent.MaximumSurfaceSize);
        Assert.True(height <= TileMapSurfaceComponent.MaximumSurfaceSize);
        Assert.Equal(width, height);
    }

    /// <summary>
    /// Builds a tile map component without a graphics device: the runtime tiles are stubs and the tile
    /// set lists only hold the single placeholder entry required by the mutation guards.
    /// </summary>
    private static TileMapComponent CreateTileMapComponent()
    {
        var world = new World();
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        // Runtime tiles rebuilt by SetTileReference look up the sprite renderer in the component list.
        var componentsField = typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(componentsField);
        componentsField!.SetValue(game, new GameComponentCollection());
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

        GetPrivateList<TileSetData>(component, "_tileSets").Add(component.TileSetData);
        GetPrivateList<Texture2D>(component, "_tileSetTextures").Add(null);

        var layer = new TileMapLayer(layerData);
        for (var tileIndex = 0; tileIndex < MapSize * MapSize; tileIndex++)
        {
            layer.Tiles.Add(new StubTile());
            layer.CollisionObjects.Add(null);
        }

        GetLayers(component).Add(layer);
        InvokeBuildChunks(component, layer);

        return component;
    }

    private static List<TileMapLayer> GetLayers(TileMapComponent component)
    {
        var property = typeof(TileMapComponent).GetProperty("Layers", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return (List<TileMapLayer>)property!.GetValue(component)!;
    }

    private static List<T> GetPrivateList<T>(TileMapComponent component, string fieldName)
    {
        var field = typeof(TileMapComponent).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (List<T>)field!.GetValue(component)!;
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

    private sealed class StubTile : Tile
    {
        public StubTile() : base(null)
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

        public override Rectangle GetCurrentSourceRectangle()
        {
            return Rectangle.Empty;
        }
    }
}
