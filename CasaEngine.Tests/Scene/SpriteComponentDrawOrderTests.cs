using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Xunit;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// The <c>draw_order</c> of the sprite and of the video components (ADR-0092): JSON in both directions, malformed values, the
/// copy, and what the static sprite queues on the renderer. No GPU: the renderer queue is inspected. The queue entry of the
/// video component is checked in the GPU tests, which have a player. The asset catalogue is global state, so this class runs
/// in <see cref="ProjectEnvironmentCollection"/>.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class SpriteComponentDrawOrderTests : IDisposable
{
    private static readonly Guid SpriteId = Guid.Parse("c2000000-0000-4000-8000-000000000001");
    private static readonly Guid SheetId = Guid.Parse("c2000000-0000-4000-8000-000000000002");
    private static readonly Guid ImageId = Guid.Parse("c2000000-0000-4000-8000-000000000003");
    private const string SpriteName = "order-sprite";

    public SpriteComponentDrawOrderTests()
    {
        AssetCatalog.ClearInternal();
        AssetCatalog.AddInternal(new AssetInfo(SpriteId) { Name = SpriteName, FileName = "order-sprite.sprite" });
        AssetCatalog.AddInternal(new AssetInfo(SheetId) { Name = "order-sheet", FileName = "order-sheet.texture" });
        AssetCatalog.AddInternal(new AssetInfo(ImageId) { Name = "order-image", FileName = "order-image.png" });
    }

    public void Dispose()
    {
        AssetCatalog.ClearInternal();
    }

    // ---- JSON: defaults, round trip, malformed values ----

    [Fact]
    public void Static_Load_WithoutTheField_GivesZero_AndSaveWritesNothing()
    {
        var node = SaveEntity(new Entity { RootComponent = new StaticSpriteComponent() });

        var loaded = new Entity();
        loaded.Load(node);

        Assert.Equal(0, Assert.IsType<StaticSpriteComponent>(loaded.RootComponent).DrawOrder);
        Assert.Null(node["root_component"]!["draw_order"]);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(-3)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Static_SaveThenLoad_RoundTripsTheDrawOrder(int drawOrder)
    {
        var node = SaveEntity(new Entity { RootComponent = new StaticSpriteComponent { DrawOrder = drawOrder } });

        var loaded = new Entity();
        loaded.Load(node);

        Assert.Equal(drawOrder, (int?)node["root_component"]!["draw_order"]);
        Assert.Equal(drawOrder, Assert.IsType<StaticSpriteComponent>(loaded.RootComponent).DrawOrder);
    }

    [Fact]
    public void Static_Load_OfAZeroWrittenByHand_GivesZero()
    {
        var node = SaveEntity(new Entity { RootComponent = new StaticSpriteComponent() });
        node["root_component"]!["draw_order"] = 0;

        var loaded = new Entity();
        loaded.Load(node);

        Assert.Equal(0, Assert.IsType<StaticSpriteComponent>(loaded.RootComponent).DrawOrder);
    }

    [Fact]
    public void Video_Load_WithoutTheField_GivesZero_AndSaveWritesNothing()
    {
        var node = SaveEntity(VideoEntity());

        var loaded = new Entity();
        loaded.Load(node);

        Assert.Equal(0, Assert.IsType<VideoSpriteComponent>(loaded.RootComponent).DrawOrder);
        Assert.Null(node["root_component"]!["draw_order"]);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(-3)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Video_SaveThenLoad_RoundTripsTheDrawOrder(int drawOrder)
    {
        var entity = VideoEntity();
        ((VideoSpriteComponent)entity.RootComponent!).DrawOrder = drawOrder;
        var node = SaveEntity(entity);

        var loaded = new Entity();
        loaded.Load(node);

        Assert.Equal(drawOrder, (int?)node["root_component"]!["draw_order"]);
        Assert.Equal(drawOrder, Assert.IsType<VideoSpriteComponent>(loaded.RootComponent).DrawOrder);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("2.0")]
    [InlineData("\"3\"")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("[1]")]
    [InlineData("{\"v\":1}")]
    [InlineData("3000000000")]
    [InlineData("-3000000000")]
    public void Static_Load_OfAMalformedDrawOrder_NamesTheComponentTheFieldAndTheValue(string value)
    {
        var node = SaveEntity(new Entity { RootComponent = new StaticSpriteComponent() });
        node["root_component"]!["draw_order"] = JToken.Parse(value);

        var error = Assert.Throws<InvalidDataException>(() => new Entity().Load(node));

        Assert.Contains("StaticSpriteComponent", error.Message);
        Assert.Contains("'draw_order'", error.Message);
        Assert.Contains(JToken.Parse(value).ToString(Newtonsoft.Json.Formatting.None), error.Message);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("2.0")]
    [InlineData("\"3\"")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("[1]")]
    [InlineData("{\"v\":1}")]
    [InlineData("3000000000")]
    [InlineData("-3000000000")]
    public void Video_Load_OfAMalformedDrawOrder_NamesTheComponentTheFieldAndTheValue(string value)
    {
        var node = SaveEntity(VideoEntity());
        node["root_component"]!["draw_order"] = JToken.Parse(value);

        var error = Assert.Throws<InvalidDataException>(() => new Entity().Load(node));

        Assert.Contains("VideoSpriteComponent", error.Message);
        Assert.Contains("'draw_order'", error.Message);
        Assert.Contains(JToken.Parse(value).ToString(Newtonsoft.Json.Formatting.None), error.Message);
    }

    // ---- Copy ----

    [Fact]
    public void Copies_KeepTheDrawOrder()
    {
        var sprite = new StaticSpriteComponent { DrawOrder = 4 };
        var video = new VideoSpriteComponent { VideoName = "film", DrawOrder = -2 };

        Assert.Equal(4, sprite.Clone().DrawOrder);
        Assert.Equal(-2, video.Clone().DrawOrder);
    }

    [Fact]
    public void ClonedEntity_KeepsTheDrawOrder_AndQueuesIt()
    {
        var harness = new Harness();
        var original = harness.LoadEntity(SpriteNode(drawOrder: 6));
        var clone = original.Clone();

        harness.AddToWorld(clone);
        harness.Submit(clone);

        Assert.Equal(6, Assert.IsType<StaticSpriteComponent>(clone.RootComponent).DrawOrder);
        Assert.Equal(6, (int)Field(Assert.Single(harness.Queue), "DrawOrder"));
    }

    // ---- Static sprite: what the renderer receives ----

    [Fact]
    public void Static_WithDrawOrderZero_QueuesAnEntryOfOrderZero_OpaqueAndBlended()
    {
        var harness = new Harness();
        var opaque = harness.NewSprite(SpriteNode(drawOrder: 0, blendMode: null), z: 1f);
        var blended = harness.NewSprite(SpriteNode(drawOrder: 0, blendMode: "Additive"), z: 1f);

        harness.Submit(opaque);
        harness.Submit(blended);

        Assert.Equal(2, harness.Queue.Count);
        foreach (var entry in harness.Queue)
        {
            Assert.Equal(0, (int)Field(entry!, "DrawOrder"));
        }

        Assert.Equal(SpriteBlendMode.Opaque, (SpriteBlendMode)Field(harness.Queue[0]!, "BlendMode"));
        Assert.Equal(SpriteBlendMode.Additive, (SpriteBlendMode)Field(harness.Queue[1]!, "BlendMode"));
    }

    [Fact]
    public void Static_WithADrawOrder_QueuesAnEntryThatCarriesIt_OpaqueAndBlended()
    {
        var harness = new Harness();
        var opaque = harness.NewSprite(SpriteNode(drawOrder: 5, blendMode: null), z: 1f);
        var blended = harness.NewSprite(SpriteNode(drawOrder: -4, blendMode: "Subtractive"), z: 1f);

        harness.Submit(opaque);
        harness.Submit(blended);

        var first = harness.Queue[0]!;
        var second = harness.Queue[1]!;
        Assert.Equal(5, (int)Field(first, "DrawOrder"));
        Assert.Equal(SpriteBlendMode.Opaque, (SpriteBlendMode)Field(first, "BlendMode"));
        Assert.False((bool)Field(first, "HasSortKey"));
        Assert.Equal(-4, (int)Field(second, "DrawOrder"));
        Assert.Equal(SpriteBlendMode.Subtractive, (SpriteBlendMode)Field(second, "BlendMode"));
        Assert.False((bool)Field(second, "HasSortKey"));
    }

    [Fact]
    public void Static_WithADrawOrder_AtEqualZ_IsOrderedByTheDrawOrder_WhateverTheSubmissionOrder()
    {
        var harness = new Harness();
        foreach (var order in new[] { 2, 0, 3, 1 })
        {
            harness.Submit(harness.NewSprite(SpriteNode(drawOrder: order, blendMode: order % 2 == 0 ? null : "Additive"), z: 4f));
        }

        harness.Sort();

        var sorted = new List<int>();
        foreach (var entry in harness.Queue)
        {
            sorted.Add((int)Field(entry!, "DrawOrder"));
        }

        Assert.Equal(new[] { 0, 1, 2, 3 }, sorted);
    }

    [Fact]
    public void Static_WithADepthSort_KeepsItsKey_AndTheDrawOrderHasNoEffect()
    {
        var harness = new Harness();
        var entity = harness.NewSprite(SpriteNode(drawOrder: 9, blendMode: null, withDepthSort: true), z: 3f);

        harness.Submit(entity);

        var entry = Assert.Single(harness.Queue);
        Assert.True((bool)Field(entry, "HasSortKey"));
        Assert.Equal(0, (int)Field(entry, "DrawOrder"));
    }

    // ---- Helpers ----

    private static JObject SaveEntity(Entity entity)
    {
        var node = new JObject();
        EditorEntityJsonSerializer.SaveEntity(entity, node);
        return node;
    }

    private static Entity VideoEntity() => new() { Name = "Film", RootComponent = new VideoSpriteComponent { VideoName = "order-film" } };

    private static JObject SpriteNode(int drawOrder, string? blendMode = "Additive", bool withDepthSort = false)
    {
        var entity = new Entity { Name = "Unit", RootComponent = new StaticSpriteComponent { DrawOrder = drawOrder } };
        if (withDepthSort)
        {
            entity.AddComponent(new DepthSortable2DComponent());
        }

        var node = SaveEntity(entity);
        var root = (JObject)node["root_component"]!;
        root["spriteDataName"] = SpriteName;
        if (blendMode != null)
        {
            root["blend_mode"] = blendMode;
        }

        return node;
    }

    private static object Field(object instance, string name)
    {
        var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return field!.GetValue(instance)!;
    }

    private sealed class SpriteDataLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
            => new SpriteData { SpriteSheetAssetId = SheetId, PositionInTexture = new Rectangle(0, 0, 16, 16) };

        public bool IsFileSupported(string fileName) => true;
    }

    private sealed class SheetLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager) => new Texture(ImageId, null);

        public bool IsFileSupported(string fileName) => true;
    }

    private sealed class ImageLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
            => RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));

        public bool IsFileSupported(string fileName) => true;
    }

    private sealed class Harness
    {
        private readonly World _world;
        private readonly SpriteRendererComponent _renderer;

        public Harness()
        {
            var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
            var componentsField = typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(componentsField);
            componentsField!.SetValue(game, new GameComponentCollection());

            var manager = new AssetContentManager();
            manager.RegisterAssetLoader(typeof(SpriteData), new SpriteDataLoader());
            manager.RegisterAssetLoader(typeof(Texture), new SheetLoader());
            manager.RegisterAssetLoader(typeof(Texture2D), new ImageLoader());
            SetBackingField(game, nameof(CasaEngineGame.AssetContentManager), manager);

            _renderer = new SpriteRendererComponent(game);
            _world = new World();
            SetBackingField(_world, nameof(World.Game), game);
        }

        public System.Collections.IList Queue
        {
            get
            {
                var field = typeof(SpriteRendererComponent).GetField("_spriteDatas", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(field);
                return (System.Collections.IList)field!.GetValue(_renderer)!;
            }
        }

        public Entity LoadEntity(JObject node)
        {
            var entity = new Entity();
            entity.Load(node);
            return entity;
        }

        public void AddToWorld(Entity entity)
        {
            entity.Initialize();
            entity.InitializeWithWorld(_world);
        }

        public Entity NewSprite(JObject node, float z)
        {
            var entity = LoadEntity(node);
            entity.RootComponent!.LocalPosition = new Vector3(0f, 0f, z);
            AddToWorld(entity);
            return entity;
        }

        public void Submit(Entity entity)
        {
            ((StaticSpriteComponent)entity.RootComponent!).Submit(Rectangle.Empty);
        }

        public void Sort()
        {
            var comparisonField = typeof(SpriteRendererComponent).GetField("SpriteDisplayDataComparison", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(comparisonField);
            var comparison = comparisonField!.GetValue(null)!;
            var sortMethod = Queue.GetType().GetMethod("Sort", new[] { comparison.GetType() });
            Assert.NotNull(sortMethod);
            sortMethod!.Invoke(Queue, new[] { comparison });
        }

        private static void SetBackingField(object instance, string propertyName, object value)
        {
            var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field!.SetValue(instance, value);
        }
    }
}
