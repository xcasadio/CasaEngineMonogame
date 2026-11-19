using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scripting.Coroutines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json.Linq;
using Xunit;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Cutscenes;

/// <summary>
/// The cutscene actions that read the asset catalogue of a running game (ShowSprite and ExitOnInput), through the
/// real path: a catalogued asset of another type, a sprite file that fails to load, an unknown mapping, an input
/// missing from the mapping and an unknown next cutscene. No GPU: the game is an uninitialised object that only
/// carries an asset content manager and an input component, and the sheet image is a texture object that was never
/// created on a device. The catalogue is global state, so this class runs in <see cref="ProjectEnvironmentCollection"/>.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class CutsceneCataloguedAssetsTests : IDisposable
{
    private static readonly Guid SpriteId = Guid.Parse("c2000000-0000-4000-8000-000000000001");
    private static readonly Guid SheetId = Guid.Parse("c2000000-0000-4000-8000-000000000002");
    private static readonly Guid ImageId = Guid.Parse("c2000000-0000-4000-8000-000000000003");
    private static readonly Guid BrokenSpriteId = Guid.Parse("c2000000-0000-4000-8000-000000000004");
    private static readonly Guid MappingId = Guid.Parse("c2000000-0000-4000-8000-000000000005");
    private static readonly Guid OtherSpriteId = Guid.Parse("c2000000-0000-4000-8000-000000000006");

    private const string SpriteName = "unit-sprite";
    private const string OtherSpriteName = "unit-other-sprite";
    private const string BrokenSpriteName = "unit-broken-sprite";
    private const string ImageName = "unit-image";
    private const string MappingName = "unit-mapping";

    public CutsceneCataloguedAssetsTests()
    {
        AssetCatalog.ClearInternal();
        AssetCatalog.AddInternal(new AssetInfo(SpriteId) { Name = SpriteName, FileName = "unit-sprite.sprite" });
        AssetCatalog.AddInternal(new AssetInfo(OtherSpriteId) { Name = OtherSpriteName, FileName = "unit-other.sprite" });
        AssetCatalog.AddInternal(new AssetInfo(BrokenSpriteId) { Name = BrokenSpriteName, FileName = "unit-broken.sprite" });
        AssetCatalog.AddInternal(new AssetInfo(SheetId) { Name = "unit-sheet", FileName = "unit-sheet.texture" });
        AssetCatalog.AddInternal(new AssetInfo(ImageId) { Name = ImageName, FileName = "unit-image.png" });
        AssetCatalog.AddInternal(new AssetInfo(MappingId) { Name = MappingName, FileName = "unit-mapping.buttonsMapping" });
    }

    public void Dispose()
    {
        AssetCatalog.ClearInternal();
    }

    // ---- ShowSprite ----

    [Fact]
    public void ShowSprite_OfACataloguedAssetThatIsNotASprite_FailsAndKeepsTheCurrentSprite()
    {
        var rig = new Rig();

        rig.PlayShowSprite(ImageName);

        rig.AssertFailedWith($"ShowSprite: sprite '{ImageName}' is not a sprite asset");
        rig.AssertNothingChanged();
    }

    [Fact]
    public void ShowSprite_OfASpriteFileThatFailsToLoad_FailsAndKeepsTheCurrentSprite()
    {
        var rig = new Rig();

        rig.PlayShowSprite(BrokenSpriteName);

        rig.AssertFailedWith($"ShowSprite: sprite '{BrokenSpriteName}' cannot be loaded.");
        rig.AssertNothingChanged();
    }

    [Fact]
    public void ShowSprite_OfAnotherSprite_ReplacesTheCurrentSprite()
    {
        var rig = new Rig();

        rig.PlayShowSprite(OtherSpriteName);

        Assert.Equal(CutsceneRuntimeState.Completed, rig.World.CutsceneDirector.GetDebugSnapshot().State);
        Assert.NotNull(rig.CurrentSprite);
        Assert.NotSame(rig.InitialSprite, rig.CurrentSprite);
        Assert.True(rig.Entity.IsVisible);
    }

    // ---- ExitOnInput through the real path ----

    [Fact]
    public void ExitOnInput_OfAnUnknownMapping_FailsNamingTheField()
    {
        var rig = new Rig();

        rig.PlayExitOnInput("unknown-mapping", "exit", "next");

        rig.AssertFailedWith("ExitOnInput: mapping 'unknown-mapping' cannot be used.");
    }

    [Fact]
    public void ExitOnInput_OfACataloguedAssetThatIsNotAMapping_FailsNamingTheField()
    {
        var rig = new Rig();

        rig.PlayExitOnInput(ImageName, "exit", "next");

        rig.AssertFailedWith($"ExitOnInput: mapping '{ImageName}' cannot be used.");
    }

    [Fact]
    public void ExitOnInput_OfAnInputMissingFromTheMapping_FailsNamingTheInput()
    {
        var rig = new Rig();

        rig.PlayExitOnInput(MappingName, "missing", "next");

        rig.AssertFailedWith($"ExitOnInput: input 'missing' is not in mapping '{MappingName}'.");
    }

    [Fact]
    public void ExitOnInput_OfAnUnknownNextCutscene_FailsNamingTheField()
    {
        var rig = new Rig();

        rig.PlayExitOnInput(MappingName, "exit", "unknown-cutscene");

        rig.AssertFailedWith("ExitOnInput: next_cutscene 'unknown-cutscene' cannot be used.");
    }

    // ---- Rig ----

    private sealed class SpriteDataLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            if (fileName.Contains("unit-broken", StringComparison.Ordinal))
            {
                throw new InvalidDataException("The sprite file is damaged.");
            }

            return new SpriteData { SpriteSheetAssetId = SheetId, PositionInTexture = new Rectangle(0, 0, 16, 16) };
        }

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

    private sealed class MappingLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            var mapping = new ButtonsMapping();
            mapping.Buttons.Add(new InputMapping
            {
                Name = "exit",
                ButtonBehavior = ButtonBehaviors.DigitalInput,
                KeyButton = new KeyButton(Keys.Space),
                AlternativeKeyButton = new KeyButton(),
            });
            return mapping;
        }

        public bool IsFileSupported(string fileName) => true;
    }

    private sealed class Rig
    {
        private readonly object _initialTextureHold;

        public Rig()
        {
            var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
            var componentsField = typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(componentsField);
            componentsField!.SetValue(game, new GameComponentCollection());

            var manager = new AssetContentManager();
            manager.RegisterAssetLoader(typeof(SpriteData), new SpriteDataLoader());
            manager.RegisterAssetLoader(typeof(Texture), new SheetLoader());
            manager.RegisterAssetLoader(typeof(Texture2D), new ImageLoader());
            manager.RegisterAssetLoader(typeof(ButtonsMapping), new MappingLoader());
            SetBackingField(game, nameof(CasaEngineGame.AssetContentManager), manager);
            SetBackingField(game, nameof(CasaEngineGame.InputComponent), RuntimeHelpers.GetUninitializedObject(typeof(InputComponent)));

            _ = new SpriteRendererComponent(game);
            World = new World();
            SetBackingField(World, nameof(World.Game), game);

            var template = new Entity { Name = "Star", RootComponent = new StaticSpriteComponent() };
            var node = new JObject();
            EditorEntityJsonSerializer.SaveEntity(template, node);
            ((JObject)node["root_component"]!)["spriteDataName"] = SpriteName;

            Entity = new Entity();
            Entity.Load(node);
            Entity.Initialize();
            Entity.InitializeWithWorld(World);
            World.Entities.Add(Entity);

            Sprite = (StaticSpriteComponent)Entity.RootComponent!;
            InitialSprite = CurrentSprite;
            Assert.NotNull(InitialSprite);
            _initialTextureHold = TextureHold(InitialSprite!)!;
            Assert.NotNull(_initialTextureHold);
            Entity.IsVisible = false;
        }

        public World World { get; }

        public Entity Entity { get; }

        public StaticSpriteComponent Sprite { get; }

        public Sprite? InitialSprite { get; }

        public Sprite? CurrentSprite
            => (Sprite?)typeof(StaticSpriteComponent).GetField("_sprite", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Sprite);

        public void PlayShowSprite(string sprite)
            => Play(new ShowSpriteCutsceneActionData { EntityName = "Star", Sprite = sprite });

        public void PlayExitOnInput(string mapping, string input, string next)
        {
            var action = new ExitOnInputCutsceneActionData { Mapping = mapping, NextCutscene = next };
            action.Inputs.Add(input);
            Play(action);
        }

        public void AssertFailedWith(string messageStart)
        {
            var snapshot = World.CutsceneDirector.GetDebugSnapshot();
            Assert.Equal(CutsceneRuntimeState.Failed, snapshot.State);
            Assert.StartsWith(messageStart, snapshot.ActiveActionStopReason);
        }

        /// <summary>The entity is still hidden and its sprite is the one it had, with its sheet hold not given back.</summary>
        public void AssertNothingChanged()
        {
            Assert.False(Entity.IsVisible);
            Assert.Same(InitialSprite, CurrentSprite);
            Assert.Same(_initialTextureHold, TextureHold(CurrentSprite!));
        }

        private void Play(CutsceneActionData root)
        {
            World.CutsceneDirector.Play(new CutsceneAsset { Name = "Catalogued", RootAction = root });
            World.CoroutineManager.Update(new CoroutineUpdateContext(0.1f, 0.1f, 1f, 1));
        }

        private static object? TextureHold(Sprite sprite)
            => typeof(Sprite).GetField("_textureHold", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(sprite);

        private static void SetBackingField(object instance, string propertyName, object value)
        {
            var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field!.SetValue(instance, value);
        }
    }
}
