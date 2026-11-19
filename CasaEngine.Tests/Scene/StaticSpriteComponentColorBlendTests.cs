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
/// The colour, the blend mode and the hidden start of a sprite entity: JSON in both directions, malformed fields,
/// the load of a named sprite, the copy, and what the renderer receives. No GPU: the renderer queue is inspected,
/// and the sheet image is a texture object that was never created on a device.
/// The asset catalogue is global state, so this class runs in <see cref="ProjectEnvironmentCollection"/>.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class StaticSpriteComponentColorBlendTests : IDisposable
{
    private static readonly Guid SpriteId = Guid.Parse("c1000000-0000-4000-8000-000000000001");
    private static readonly Guid SheetId = Guid.Parse("c1000000-0000-4000-8000-000000000002");
    private static readonly Guid ImageId = Guid.Parse("c1000000-0000-4000-8000-000000000003");
    private const string SpriteName = "unit-sprite";

    private static readonly Rectangle Source = new(0, 0, 16, 16);

    public StaticSpriteComponentColorBlendTests()
    {
        AssetCatalog.ClearInternal();
        AssetCatalog.AddInternal(new AssetInfo(SpriteId) { Name = SpriteName, FileName = "unit-sprite.sprite" });
        AssetCatalog.AddInternal(new AssetInfo(SheetId) { Name = "unit-sheet", FileName = "unit-sheet.texture" });
        AssetCatalog.AddInternal(new AssetInfo(ImageId) { Name = "unit-image", FileName = "unit-image.png" });
    }

    public void Dispose()
    {
        AssetCatalog.ClearInternal();
    }

    // ---- JSON: defaults and round trip ----

    [Fact]
    public void Load_OfAFileWithoutTheNewFields_KeepsTheCurrentState()
    {
        var node = SaveEntity(new Entity { RootComponent = new StaticSpriteComponent() });

        var entity = new Entity();
        entity.Load(node);

        var sprite = Assert.IsType<StaticSpriteComponent>(entity.RootComponent);
        Assert.Equal(Color.White, sprite.Color);
        Assert.Equal(SpriteBlendMode.Opaque, sprite.BlendMode);
        Assert.True(entity.IsVisible);
        Assert.Null(sprite.PendingSpriteDataName);
    }

    [Fact]
    public void Save_OfADefaultSprite_WritesNoneOfTheNewKeys()
    {
        var node = SaveEntity(new Entity { RootComponent = new StaticSpriteComponent() });

        Assert.Null(node["is_visible"]);
        Assert.Null(node["root_component"]!["color"]);
        Assert.Null(node["root_component"]!["blend_mode"]);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsTheColourTheBlendModeAndTheHiddenStart()
    {
        var entity = new Entity
        {
            IsVisible = false,
            RootComponent = new StaticSpriteComponent { Color = new Color(10, 20, 30, 40), BlendMode = SpriteBlendMode.Subtractive },
        };

        var node = SaveEntity(entity);
        var loaded = new Entity();
        loaded.Load(node);

        var sprite = Assert.IsType<StaticSpriteComponent>(loaded.RootComponent);
        Assert.Equal(new Color(10, 20, 30, 40), sprite.Color);
        Assert.Equal(SpriteBlendMode.Subtractive, sprite.BlendMode);
        Assert.False(loaded.IsVisible);
        Assert.Equal("Subtractive", (string?)node["root_component"]!["blend_mode"]);
        Assert.Equal(10, (int?)node["root_component"]!["color"]!["r"]);
    }

    [Fact]
    public void Load_OfAPendingSpriteName_IsWrittenBackBySave()
    {
        var node = SaveEntity(new Entity { RootComponent = new StaticSpriteComponent() });
        node["root_component"]!["spriteDataName"] = SpriteName;
        var loaded = new Entity();
        loaded.Load(node);

        var again = SaveEntity(loaded);

        Assert.Equal(SpriteName, (string?)again["root_component"]!["spriteDataName"]);
    }

    // ---- JSON: malformed fields ----

    [Theory]
    [InlineData("{\"r\":1,\"g\":2,\"b\":3}")]
    [InlineData("{\"r\":1,\"g\":2,\"b\":3,\"a\":256}")]
    [InlineData("{\"r\":-1,\"g\":2,\"b\":3,\"a\":255}")]
    [InlineData("{\"r\":1.5,\"g\":2,\"b\":3,\"a\":255}")]
    [InlineData("{\"r\":\"1\",\"g\":2,\"b\":3,\"a\":255}")]
    [InlineData("[1,2,3,4]")]
    [InlineData("\"white\"")]
    public void Load_OfAMalformedColour_NamesTheComponentTheFieldAndTheValue(string colour)
    {
        var node = SaveEntity(new Entity { RootComponent = new StaticSpriteComponent() });
        node["root_component"]!["color"] = JToken.Parse(colour);

        var error = Assert.Throws<InvalidDataException>(() => new Entity().Load(node));

        Assert.Contains("StaticSpriteComponent", error.Message);
        Assert.Contains("'color'", error.Message);
        Assert.Contains(JToken.Parse(colour).ToString(Newtonsoft.Json.Formatting.None), error.Message);
    }

    [Theory]
    [InlineData("\"Glow\"")]
    [InlineData("\"additive\"")]
    [InlineData("\"1\"")]
    [InlineData("1")]
    [InlineData("null")]
    public void Load_OfAnUnknownBlendMode_NamesTheComponentTheFieldAndTheValue(string blendMode)
    {
        var node = SaveEntity(new Entity { RootComponent = new StaticSpriteComponent() });
        node["root_component"]!["blend_mode"] = JToken.Parse(blendMode);

        var error = Assert.Throws<InvalidDataException>(() => new Entity().Load(node));

        Assert.Contains("StaticSpriteComponent", error.Message);
        Assert.Contains("'blend_mode'", error.Message);
        Assert.Contains(JToken.Parse(blendMode).ToString(Newtonsoft.Json.Formatting.None), error.Message);
    }

    [Theory]
    [InlineData("\"false\"")]
    [InlineData("0")]
    [InlineData("null")]
    public void Load_OfAnIsVisibleThatIsNotABoolean_NamesTheFieldAndTheValue(string isVisible)
    {
        var node = SaveEntity(new Entity { Name = "Hero", RootComponent = new StaticSpriteComponent() });
        node["is_visible"] = JToken.Parse(isVisible);

        var error = Assert.Throws<InvalidDataException>(() => new Entity().Load(node));

        Assert.Contains("'is_visible'", error.Message);
        Assert.Contains("Hero", error.Message);
        Assert.Contains(JToken.Parse(isVisible).ToString(Newtonsoft.Json.Formatting.None), error.Message);
    }

    // ---- Named sprite load, copy, renderer ----

    [Fact]
    public void InlineEntity_WithANamedSprite_LoadsInTheWorldAndReachesTheRenderer()
    {
        var harness = new Harness();
        var entity = harness.LoadEntity(NamedSpriteNode(withDepthSort: false));

        harness.AddToWorld(entity);
        harness.Submit(entity);

        var entry = Assert.Single(harness.Queue);
        Assert.Equal(new Color(1, 2, 3, 4), (Color)Field(entry, "Color"));
        Assert.Equal(SpriteBlendMode.Additive, (SpriteBlendMode)Field(entry, "BlendMode"));
        Assert.False((bool)Field(entry, "HasSortKey"));
    }

    [Fact]
    public void ClonedEntity_WithANamedSprite_LoadsInTheWorldAndReachesTheRenderer()
    {
        var harness = new Harness();
        var original = harness.LoadEntity(NamedSpriteNode(withDepthSort: false));
        var clone = original.Clone();

        harness.AddToWorld(clone);
        harness.Submit(clone);

        var clonedSprite = Assert.IsType<StaticSpriteComponent>(clone.RootComponent);
        Assert.Equal(new Color(1, 2, 3, 4), clonedSprite.Color);
        Assert.Equal(SpriteBlendMode.Additive, clonedSprite.BlendMode);
        var entry = Assert.Single(harness.Queue);
        Assert.Equal(new Color(1, 2, 3, 4), (Color)Field(entry, "Color"));
        Assert.Equal(SpriteBlendMode.Additive, (SpriteBlendMode)Field(entry, "BlendMode"));
    }

    [Fact]
    public void Entity_WithAnUnknownSpriteName_FailsInTheWorldAndNamesTheSprite()
    {
        var harness = new Harness();
        var node = NamedSpriteNode(withDepthSort: false);
        node["root_component"]!["spriteDataName"] = "missing-sprite";
        var entity = harness.LoadEntity(node);

        var error = Assert.Throws<InvalidDataException>(() => harness.AddToWorld(entity));

        Assert.Contains("missing-sprite", error.Message);
    }

    [Fact]
    public void DefaultSprite_ReachesTheRendererWhiteAndOpaque()
    {
        var harness = new Harness();
        var node = NamedSpriteNode(withDepthSort: false);
        ((JObject)node["root_component"]!).Remove("color");
        ((JObject)node["root_component"]!).Remove("blend_mode");
        var entity = harness.LoadEntity(node);

        harness.AddToWorld(entity);
        harness.Submit(entity);

        var entry = Assert.Single(harness.Queue);
        Assert.Equal(Color.White, (Color)Field(entry, "Color"));
        Assert.Equal(SpriteBlendMode.Opaque, (SpriteBlendMode)Field(entry, "BlendMode"));
    }

    [Fact]
    public void BlendedSprite_WithoutDepthSort_HasNoKeyAndIsOrderedByZAgainstAnOpaqueSprite()
    {
        var harness = new Harness();
        var opaque = harness.NewSprite(NamedSpriteNode(withDepthSort: false, blendMode: null), z: 5f);
        var low = harness.NewSprite(NamedSpriteNode(withDepthSort: false, blendMode: "Subtractive"), z: 1f);
        var high = harness.NewSprite(NamedSpriteNode(withDepthSort: false, blendMode: "Additive"), z: 9f);

        harness.Submit(opaque);
        harness.Submit(low);
        harness.Submit(high);
        harness.Sort();

        Assert.Equal(3, harness.Queue.Count);
        var modes = new List<SpriteBlendMode>();
        foreach (var entry in harness.Queue)
        {
            modes.Add((SpriteBlendMode)Field(entry!, "BlendMode"));
        }

        // Larger Z is drawn first, exactly as for opaque sprites.
        Assert.Equal(new[] { SpriteBlendMode.Additive, SpriteBlendMode.Opaque, SpriteBlendMode.Subtractive }, modes);
        Assert.False((bool)Field(harness.Queue[0]!, "HasSortKey"));
        Assert.False((bool)Field(harness.Queue[2]!, "HasSortKey"));
    }

    [Fact]
    public void BlendedSprite_WithDepthSort_CarriesTheModeAndTheKey()
    {
        var harness = new Harness();
        var entity = harness.NewSprite(NamedSpriteNode(withDepthSort: true), z: 3f);

        harness.Submit(entity);

        var entry = Assert.Single(harness.Queue);
        Assert.Equal(SpriteBlendMode.Additive, (SpriteBlendMode)Field(entry, "BlendMode"));
        Assert.True((bool)Field(entry, "HasSortKey"));
        var key = (RenderSortKey2D)Field(entry, "SortKey");
        var expected = entity.GetComponent<DepthSortable2DComponent>()!.BuildSortKey(new Vector3(0f, 0f, 3f));
        Assert.Equal(expected, key);
    }

    // ---- Helpers ----

    private static JObject SaveEntity(Entity entity)
    {
        var node = new JObject();
        EditorEntityJsonSerializer.SaveEntity(entity, node);
        return node;
    }

    private static JObject NamedSpriteNode(bool withDepthSort, string? blendMode = "Additive")
    {
        var sprite = new StaticSpriteComponent { Color = new Color(1, 2, 3, 4), BlendMode = SpriteBlendMode.Additive };
        var entity = new Entity { Name = "Unit", RootComponent = sprite };
        if (withDepthSort)
        {
            entity.AddComponent(new DepthSortable2DComponent());
        }

        var node = SaveEntity(entity);
        var root = (JObject)node["root_component"]!;
        root["spriteDataName"] = SpriteName;
        if (blendMode == null)
        {
            root.Remove("blend_mode");
            root.Remove("color");
        }
        else
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
            => new SpriteData { SpriteSheetAssetId = SheetId, PositionInTexture = Source };

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
            var sprite = (StaticSpriteComponent)entity.RootComponent!;
            sprite.Submit(Rectangle.Empty);
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
