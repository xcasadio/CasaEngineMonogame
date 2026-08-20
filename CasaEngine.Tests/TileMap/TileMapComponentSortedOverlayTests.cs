using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.EditorServices;
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
/// Covers the runtime-only sorted tile overlay added to <see cref="TileMapComponent"/>:
/// <see cref="TileMapComponent.AddSortedOverlayTile"/> submits a keyed sprite for each queued tile,
/// ordering follows the same <see cref="RenderSortKey2D"/> contract as every other keyed sprite, and
/// overlay entries never reach the saved asset.
/// </summary>
public class TileMapComponentSortedOverlayTests
{
    private const int MapSize = 8;
    private const int TilePixelSize = 16;
    private const int TileId = 1;
    private const int AnimatedTileId = 2;
    private static readonly Rectangle AnimatedFrame0Location = new(0, 0, TilePixelSize, TilePixelSize);
    private static readonly Rectangle AnimatedFrame1Location = new(TilePixelSize, 0, TilePixelSize, TilePixelSize);

    [Fact]
    public void AddSortedOverlayTile_Draw_ProducesSpriteSubmissionWithExactKey()
    {
        var component = CreateComponentWithOverlaySupport(out var spriteRendererComponent);
        var key = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 2, 3, 5, 7, 11, 13);

        component.AddSortedOverlayTile(new TileMapTileReference(0, TileId), 2, 3, in key);
        component.Draw(0f);

        var spriteDatas = GetSpriteDatas(spriteRendererComponent);
        Assert.Single(spriteDatas);

        var entry = spriteDatas[0]!;
        Assert.True((bool)GetField(entry, "HasSortKey"));
        Assert.Equal(key, (RenderSortKey2D)GetField(entry, "SortKey"));
    }

    [Fact]
    public void SortedOverlayTiles_OrderByElevation_LikeAnyOtherKeyedSprite()
    {
        var component = CreateComponentWithOverlaySupport(out var spriteRendererComponent);
        var lowElevation = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 0, 0, 1, 0, 0, 0);
        var highElevation = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 0, 0, 2, 0, 0, 0);

        component.AddSortedOverlayTile(new TileMapTileReference(0, TileId), 0, 0, in lowElevation);
        component.AddSortedOverlayTile(new TileMapTileReference(0, TileId), 1, 0, in highElevation);
        component.Draw(0f);

        var spriteDatas = GetSpriteDatas(spriteRendererComponent);
        Assert.Equal(2, spriteDatas.Count);

        var compare = GetCompareSpriteDisplayData();
        Assert.True((int)compare.Invoke(null, new[] { spriteDatas[0], spriteDatas[1] })! < 0);
        Assert.True((int)compare.Invoke(null, new[] { spriteDatas[1], spriteDatas[0] })! > 0);
    }

    [Fact]
    public void SortedOverlayTile_OrdersAgainstADirectlyKeyedSprite_ByTheSameContract()
    {
        var component = CreateComponentWithOverlaySupport(out var spriteRendererComponent);
        var overlayKey = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 0, 0, 0, 100, 0, 0);
        var entityKey = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 0, 0, 0, 200, 0, 0);

        component.AddSortedOverlayTile(new TileMapTileReference(0, TileId), 0, 0, in overlayKey);
        component.Draw(0f);

        // A regular Y-sorted entity sprite submitted directly through the same keyed path, as the
        // renderer sees it (not through the tile map at all).
        spriteRendererComponent.DrawSprite(
            GetTexture(component), new Rectangle(0, 0, TilePixelSize, TilePixelSize), Point.Zero,
            new Vector2(500f, -500f), 0f, Vector2.One, Color.White, 0f, in entityKey, SpriteEffects.None, Rectangle.Empty);

        var spriteDatas = GetSpriteDatas(spriteRendererComponent);
        Assert.Equal(2, spriteDatas.Count);

        var compare = GetCompareSpriteDisplayData();
        // overlayKey.SortCoordinate (100) < entityKey.SortCoordinate (200): the overlay tile must draw
        // first regardless of the very different world positions/Z used for the two submissions.
        Assert.True((int)compare.Invoke(null, new[] { spriteDatas[0], spriteDatas[1] })! < 0);
    }

    [Fact]
    public void KeyOrder_WinsOverZ_WhenSubmittedZIsEqual()
    {
        var component = CreateComponentWithOverlaySupport(out var spriteRendererComponent);
        // Both tiles are overlay tiles of the very same component/frame, so they share the exact same
        // submitted world Z per the overlay's Z policy. Without a sort key, the depth-only comparator
        // would treat an equal Z as a tie (CompareTo returns 0, no deterministic order). With keys, the
        // pair must still resolve to a strict, deterministic order taken purely from the keys.
        var firstKey = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 0, 0, -10, 0, 0, 0);
        var secondKey = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 0, 0, 10, 0, 0, 0);

        component.AddSortedOverlayTile(new TileMapTileReference(0, TileId), 0, 0, in firstKey);
        component.AddSortedOverlayTile(new TileMapTileReference(0, TileId), 1, 0, in secondKey);
        component.Draw(0f);

        var spriteDatas = GetSpriteDatas(spriteRendererComponent);
        Assert.Equal(2, spriteDatas.Count);

        var firstZ = GetField(spriteDatas[0]!, "WorldMatrix") is Matrix firstMatrix ? firstMatrix.Translation.Z : 0f;
        var secondZ = GetField(spriteDatas[1]!, "WorldMatrix") is Matrix secondMatrix ? secondMatrix.Translation.Z : 0f;
        Assert.Equal(firstZ, secondZ);

        var compare = GetCompareSpriteDisplayData();
        Assert.True((int)compare.Invoke(null, new[] { spriteDatas[0], spriteDatas[1] })! < 0);
        Assert.True((int)compare.Invoke(null, new[] { spriteDatas[1], spriteDatas[0] })! > 0);
    }

    [Fact]
    public void AddSortedOverlayTile_AnimatedTile_SourceRectangleAdvancesAsItAnimates()
    {
        var component = CreateComponentWithOverlaySupport(out _);
        component.AddSortedOverlayTile(new TileMapTileReference(0, AnimatedTileId), 2, 3, in RenderSortKey2D.Default);

        var overlayTile = GetOverlayTile(component, 0);
        Assert.Equal(AnimatedFrame0Location, overlayTile.GetCurrentSourceRectangle());

        // Frame 0 lasts 100ms: advance past it so the overlay's own tile instance (resolved once at
        // AddSortedOverlayTile time, then updated every frame through the component's regular
        // animated-tile update loop, exactly like a flat-layer tile) has moved to frame 1. This is the
        // exact rectangle DrawSortedOverlayTiles submits to the sprite renderer every frame.
        component.Update(0.101f);

        Assert.Equal(AnimatedFrame1Location, overlayTile.GetCurrentSourceRectangle());
        Assert.NotEqual(AnimatedFrame0Location, overlayTile.GetCurrentSourceRectangle());
    }

    [Fact]
    public void AddSortedOverlayTile_StaticTile_SourceRectangleStaysFixedAcrossUpdates()
    {
        var component = CreateComponentWithOverlaySupport(out _);
        component.AddSortedOverlayTile(new TileMapTileReference(0, TileId), 2, 3, in RenderSortKey2D.Default);

        var overlayTile = GetOverlayTile(component, 0);
        var beforeRectangle = overlayTile.GetCurrentSourceRectangle();

        component.Update(1f);

        Assert.Equal(beforeRectangle, overlayTile.GetCurrentSourceRectangle());
    }

    [Fact]
    public void AnimatedOverlayTile_StaysInPhaseWithAFlatLayerTileOfTheSameId()
    {
        var component = CreateComponentWithOverlaySupport(out _);
        var flatAnimatedTile = AddFlatAnimatedTileLayer(component);

        component.AddSortedOverlayTile(new TileMapTileReference(0, AnimatedTileId), 4, 4, in RenderSortKey2D.Default);
        var overlayAnimatedTile = (AnimatedTile)GetOverlayTile(component, 0);

        // Both tile instances are driven by the exact same Update calls (the component's flat-layer and
        // overlay animated tiles are updated together, see TileMapComponent.Update), so even though they
        // are separate per-cell instances (not a shared flyweight — see AnimatedTile's per-cell frame
        // state), they must always report the same current frame.
        Assert.Equal(flatAnimatedTile.CurrentFrameIndex, overlayAnimatedTile.CurrentFrameIndex);

        component.Update(0.101f);
        Assert.Equal(flatAnimatedTile.CurrentFrameIndex, overlayAnimatedTile.CurrentFrameIndex);
        Assert.Equal(1, overlayAnimatedTile.CurrentFrameIndex);

        component.Update(0.051f);
        Assert.Equal(flatAnimatedTile.CurrentFrameIndex, overlayAnimatedTile.CurrentFrameIndex);
        Assert.Equal(0, overlayAnimatedTile.CurrentFrameIndex);
    }

    [Fact]
    public void ClearSortedOverlayTiles_UnregistersAnimatedOverlayTilesFromTheUpdateLoop()
    {
        var component = CreateComponentWithOverlaySupport(out _);
        component.AddSortedOverlayTile(new TileMapTileReference(0, AnimatedTileId), 2, 3, in RenderSortKey2D.Default);
        Assert.True(component.HasAnimatedTiles);

        component.ClearSortedOverlayTiles();

        Assert.False(component.HasAnimatedTiles);
        Assert.Empty(GetPrivateField<IList>(component, "_animatedTiles"));
    }

    [Fact]
    public void SavedEntity_NeverContainsSortedOverlayTiles()
    {
        var component = CreateComponentWithOverlaySupport(out _);

        // Same component, saved once without overlay tiles and once with several queued: the saved
        // node must be byte-for-byte identical either way, proving the overlay never reaches the asset.
        var nodeWithoutOverlay = SaveAsEntity(component);

        component.AddSortedOverlayTile(new TileMapTileReference(0, TileId), 0, 0, in RenderSortKey2D.Default);
        component.AddSortedOverlayTile(new TileMapTileReference(0, TileId), 1, 1,
            new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 1, 2, 3, 4, 5, 6));
        Assert.Equal(2, component.SortedOverlayTileCount);

        var nodeWithOverlay = SaveAsEntity(component);

        Assert.True(JToken.DeepEquals(nodeWithOverlay["root_component"], nodeWithoutOverlay["root_component"]));

        var overlayNodeText = nodeWithOverlay["root_component"]!.ToString();
        Assert.DoesNotContain("overlay", overlayNodeText, StringComparison.OrdinalIgnoreCase);
    }

    private static JObject SaveAsEntity(TileMapComponent component)
    {
        var entity = new Entity { RootComponent = component };
        var node = new JObject();
        EditorEntityJsonSerializer.SaveEntity(entity, node);
        return node;
    }

    private static TileMapComponent CreateComponentWithOverlaySupport(out SpriteRendererComponent spriteRendererComponent)
    {
        var world = new World();
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        SetBaseField<Game>(game, "_components", new GameComponentCollection());
        SetProperty(world, nameof(World.Game), game);

        var entity = new Entity();
        SetProperty(entity, nameof(Entity.World), world);

        var component = new TileMapComponent();
        entity.RootComponent = component;

        var tileSetData = new TileSetData { TileSize = new Size(TilePixelSize, TilePixelSize) };
        tileSetData.AddTile(new StaticTileData { Id = TileId, Location = new Rectangle(0, 0, TilePixelSize, TilePixelSize) });

        // A synthetic two-frame animated tile, mirroring AnimatedTileTests: frame 0 is the tile's own
        // location, frame 1 references a second static tile id purely to give it a distinct rectangle.
        const int frameReferenceTileId = 3;
        var animatedTileData = new AnimatedTileData { Id = AnimatedTileId, Location = AnimatedFrame0Location };
        animatedTileData.Frames.Add(new AnimatedTileFrameData { TileId = AnimatedTileId, DurationMilliseconds = 100 });
        animatedTileData.Frames.Add(new AnimatedTileFrameData { TileId = frameReferenceTileId, DurationMilliseconds = 50 });
        tileSetData.AddTile(animatedTileData);
        tileSetData.AddTile(new StaticTileData { Id = frameReferenceTileId, Location = AnimatedFrame1Location });

        component.TileMapData = new TileMapData { MapSize = new Size(MapSize, MapSize) };
        component.TileSetData = tileSetData;

        GetPrivateField<IList>(component, "_tileSets").Add(tileSetData);
        GetPrivateField<IList>(component, "_tileSetTextures").Add(RuntimeHelpers.GetUninitializedObject(typeof(Texture2D)));

        // A real SpriteRendererComponent: its constructor only needs game.Components (queuing sprites
        // in DrawSprite never touches the GraphicsDevice, only Flush()/Draw() does).
        spriteRendererComponent = new SpriteRendererComponent(game);
        SetPrivateField(component, "_spriteRendererComponent", spriteRendererComponent);

        return component;
    }

    private static Texture2D GetTexture(TileMapComponent component)
    {
        var textures = GetPrivateField<IList>(component, "_tileSetTextures");
        return (Texture2D)textures[0]!;
    }

    /// <summary>Reads back the <c>Tile</c> field of one queued <c>SortedOverlayTile</c> entry.</summary>
    private static Tile GetOverlayTile(TileMapComponent component, int index)
    {
        var overlayTiles = GetPrivateField<IList>(component, "_sortedOverlayTiles");
        var entry = overlayTiles[index]!;
        var tileField = entry.GetType().GetField("Tile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(tileField);
        return (Tile)tileField!.GetValue(entry)!;
    }

    /// <summary>
    /// Adds a flat layer whose single tile source is a real <see cref="AnimatedTile"/> instance for
    /// <see cref="AnimatedTileId"/>, registered exactly like <c>TileMapComponent.CreateRuntimeTile</c>
    /// would (added to the private <c>_animatedTiles</c> update list and <c>_hasAnimatedTiles</c> set),
    /// without going through the full asset-loading <c>InitializeWithWorld</c> path.
    /// </summary>
    private static AnimatedTile AddFlatAnimatedTileLayer(TileMapComponent component)
    {
        var tileSetData = (TileSetData)GetPrivateField<IList>(component, "_tileSets")[0]!;
        var texture = GetTexture(component);
        var animatedTileData = (AnimatedTileData)tileSetData.GetTileData(AnimatedTileId);
        var flatAnimatedTile = new AnimatedTile(texture, tileSetData, animatedTileData);

        var layerData = new TileMapLayerData();
        for (var index = 0; index < MapSize * MapSize; index++)
        {
            layerData.tiles.Add(AnimatedTileId);
        }

        component.TileMapData.Layers.Add(layerData);

        var layer = new TileMapLayer(layerData);
        for (var index = 0; index < MapSize * MapSize; index++)
        {
            layer.Tiles.Add(flatAnimatedTile);
        }

        GetLayers(component).Add(layer);
        InvokeBuildChunks(component, layer);

        GetPrivateField<IList>(component, "_animatedTiles").Add(flatAnimatedTile);
        SetPrivateField(component, "_hasAnimatedTiles", true);

        return flatAnimatedTile;
    }

    private static IList GetLayers(TileMapComponent component)
    {
        var property = typeof(TileMapComponent).GetProperty("Layers", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return (IList)property!.GetValue(component)!;
    }

    private static void InvokeBuildChunks(TileMapComponent component, TileMapLayer layer)
    {
        var method = typeof(TileMapComponent).GetMethod("BuildChunks", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(component, new object[] { layer, 0 });
    }

    private static IList GetSpriteDatas(SpriteRendererComponent spriteRendererComponent)
    {
        var field = typeof(SpriteRendererComponent).GetField("_spriteDatas", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (IList)field!.GetValue(spriteRendererComponent)!;
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

    private static TField GetPrivateField<TField>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (TField)field!.GetValue(instance)!;
    }

    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    /// <summary>Sets a private field declared on <typeparamref name="TBase"/> rather than on the instance's runtime type.</summary>
    private static void SetBaseField<TBase>(object instance, string fieldName, object value)
    {
        var field = typeof(TBase).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var property = typeof(TTarget).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }
}
