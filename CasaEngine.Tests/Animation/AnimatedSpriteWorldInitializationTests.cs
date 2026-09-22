using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.Textures;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;

namespace CasaEngine.Tests.Animation;

/// <summary>
/// Covers how <see cref="AnimatedSpriteComponent.InitializeWithWorld"/> rebuilds
/// <see cref="AnimatedSpriteComponent.Animations"/> for the world the component enters. It used to
/// rebuild them from the serialized animation asset ids alone, which silently dropped every animation
/// handed over by <see cref="AnimatedSpriteComponent.AddAnimation"/> - the way the demos, the editor
/// preview and any sprite built in code populate the component. Such a component entered the world
/// with no animation at all and a null <c>CurrentAnimation</c>, and <c>TileMapDemo</c>'s
/// <c>PlayerComponent</c> threw a <see cref="NullReferenceException"/> on its very first frame.
/// </summary>
public class AnimatedSpriteWorldInitializationTests
{
    [Fact]
    public void AnimationAddedInCode_SurvivesInitializeWithWorld()
    {
        var component = CreateComponentInWorld(out var world);
        component.AddAnimation(new Animation2d(CreateAnimationData("swordman_stand_right")));

        component.InitializeWithWorld(world);

        var animation = Assert.Single(component.Animations);
        Assert.Equal("swordman_stand_right", animation.Animation2dData.Name);
        Assert.Same(animation, component.CurrentAnimation);

        // Without a composition sampler nothing advances and nothing draws, so a non-null
        // CurrentAnimation on its own would not prove the animation is usable.
        Assert.NotNull(GetCurrentSampler(component));
    }

    [Fact]
    public void SecondInitializeWithWorld_KeepsTheAnimationOnce_AndRebindsItsSampler()
    {
        var component = CreateComponentInWorld(out var world);
        component.AddAnimation(new Animation2d(CreateAnimationData("swordman_stand_right")));

        component.InitializeWithWorld(world);
        var firstSampler = GetCurrentSampler(component);
        Assert.NotNull(firstSampler);

        component.InitializeWithWorld(world);

        // Re-entering a world registers the same animation once more, not twice.
        Assert.Single(component.Animations);
        Assert.NotNull(component.CurrentAnimation);

        // The samplers of the world just left are dropped, so the rebuilt animation gets a fresh one:
        // keeping CurrentAnimation across the rebuild would send SetCurrentAnimation down its
        // same-name shortcut and leave the component with no sampler at all.
        var secondSampler = GetCurrentSampler(component);
        Assert.NotNull(secondSampler);
        Assert.NotSame(firstSampler, secondSampler);
    }

    // ---- ADR-0037: animation data is acquired through a counted handle, not Load<T> ----

    private static readonly Guid AnimationAssetId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private sealed class Animation2dDataLoader : CasaEngine.Framework.Assets.IAssetLoader
    {
        public int Loads;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            Loads++;
            return CreateAnimationData("loaded_from_asset_id");
        }

        public bool IsFileSupported(string fileName) => true;
    }

    private static AssetContentManager NewAssetContentManager(out Animation2dDataLoader loader)
    {
        var infos = new Dictionary<Guid, CasaEngine.Framework.Assets.AssetInfo>
        {
            [AnimationAssetId] = new CasaEngine.Framework.Assets.AssetInfo(AnimationAssetId) { Name = "walk", FileName = "walk.anim2d" },
        };

        var manager = new AssetContentManager
        {
            RuntimeContext = new CasaEngine.Framework.Application.EngineRuntimeContext(null, Path.GetTempPath(), id => infos.GetValueOrDefault(id)),
        };

        loader = new Animation2dDataLoader();
        manager.RegisterAssetLoader(typeof(Animation2dData), loader);
        return manager;
    }

    private static void AddAnimationAssetId(AnimatedSpriteComponent component, Guid assetId)
    {
        var field = typeof(AnimatedSpriteComponent).GetField("_animationAssetIds", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        ((List<Guid>)field!.GetValue(component)!).Add(assetId);
    }

    [Fact]
    public void InitializeWithWorld_AcquiresAnimationDataById_HeldUntilDetach()
    {
        var manager = NewAssetContentManager(out var loader);
        var component = CreateComponentInWorld(manager, out var world);
        AddAnimationAssetId(component, AnimationAssetId);

        component.InitializeWithWorld(world);

        Assert.Equal(1, loader.Loads);
        Assert.Equal(0, manager.CollectUnreferenced());

        component.Detach();

        Assert.Equal(1, manager.CollectUnreferenced());
    }

    [Fact]
    public void SecondInitializeWithWorld_ReleasesThePreviousAnimationDataHandle()
    {
        var manager = NewAssetContentManager(out var loader);
        var component = CreateComponentInWorld(manager, out var world);
        AddAnimationAssetId(component, AnimationAssetId);

        component.InitializeWithWorld(world);
        component.InitializeWithWorld(world);

        // The first handle was given back before the second acquire, and nothing collected in between:
        // the asset is still cached, so the second acquire reuses it instead of reloading it.
        Assert.Equal(1, loader.Loads);
        // Only the current (second) acquire is still held.
        Assert.Equal(0, manager.CollectUnreferenced());

        component.Detach();
        Assert.Equal(1, manager.CollectUnreferenced());
    }

    [Fact]
    public void Detach_ReleasesEveryHeldSprite_AndItsSpriteDataHandle()
    {
        var spriteDataId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var textureId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var infos = new Dictionary<Guid, AssetInfo>
        {
            [spriteDataId] = new AssetInfo(spriteDataId) { Name = "sprite", FileName = "sprite.sprite" },
            [textureId] = new AssetInfo(textureId) { Name = "sheet", FileName = "sheet.texture" },
        };
        var manager = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, Path.GetTempPath(), id => infos.GetValueOrDefault(id)),
        };
        manager.RegisterAssetLoader(typeof(SpriteData), new SpriteDataLoader());
        manager.RegisterAssetLoader(typeof(Texture), new TextureLoader());

        var component = CreateComponentInWorld(out _);
        var spriteDataHandle = manager.Acquire<SpriteData>(spriteDataId);
        var sprite = CreateSpriteHoldingTexture(manager, textureId);
        PutSpriteAndHandle(component, spriteDataId, sprite, spriteDataHandle);

        Assert.Equal(0, manager.CollectUnreferenced());

        component.Detach();

        // Both the sprite data hold and the sprite's own texture hold (given back by Sprite.Dispose)
        // are released.
        Assert.Equal(2, manager.CollectUnreferenced());
    }

    private sealed class SpriteDataLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager) => new SpriteData();

        public bool IsFileSupported(string fileName) => true;
    }

    private sealed class TextureLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager) => new Texture(Guid.NewGuid(), null);

        public bool IsFileSupported(string fileName) => true;
    }

    /// <summary>A Sprite holding a real texture handle, built without Sprite.Create (no GraphicsDevice
    /// needed - same technique SpriteRendererComponentBlendModeTests uses).</summary>
    private static Sprite CreateSpriteHoldingTexture(AssetContentManager manager, Guid textureId)
    {
        var textureHandle = manager.Acquire<Texture>(textureId);
        var sprite = (Sprite)RuntimeHelpers.GetUninitializedObject(typeof(Sprite));
        var field = typeof(Sprite).GetField("_textureHold", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(sprite, textureHandle);
        return sprite;
    }

    private static void PutSpriteAndHandle(AnimatedSpriteComponent component, Guid spriteId, Sprite sprite, AssetHandle<SpriteData> handle)
    {
        var spriteByIdField = typeof(AnimatedSpriteComponent).GetField("_spriteById", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(spriteByIdField);
        ((Dictionary<Guid, Sprite>)spriteByIdField!.GetValue(component)!)[spriteId] = sprite;

        var handleByIdField = typeof(AnimatedSpriteComponent).GetField("_spriteDataHandleById", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(handleByIdField);
        ((Dictionary<Guid, AssetHandle<SpriteData>>)handleByIdField!.GetValue(component)!)[spriteId] = handle;
    }

    private static AnimatedSpriteComponent CreateComponentInWorld(AssetContentManager assetContentManager, out CasaEngine.Framework.Scene.World.World world)
    {
        var component = CreateComponentInWorld(out world);
        SetBackingField(world.Game, nameof(CasaEngineGame.AssetContentManager), assetContentManager);
        return component;
    }

    private static void SetBackingField(object instance, string propertyName, object value)
    {
        var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static Animation2dData CreateAnimationData(string name)
    {
        var animationData = new Animation2dData { Name = name };
        animationData.Parts.Add(new Animation2dPartData { Id = "sprite" });
        return animationData;
    }

    private static AnimatedSpriteComponent CreateComponentInWorld(out CasaEngine.Framework.Scene.World.World world)
    {
        world = new CasaEngine.Framework.Scene.World.World();

        // RuntimeHelpers.GetUninitializedObject skips Game's constructor, so Components (backed by
        // _components) is otherwise null: AnimatedSpriteComponent.InitializeWithWorld looks up a
        // SpriteRendererComponent through it. AssetContentManager stays null, which is exactly the
        // state of a component that names no animation asset id.
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var componentsField = typeof(Microsoft.Xna.Framework.Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!;
        componentsField.SetValue(game, new Microsoft.Xna.Framework.GameComponentCollection());
        SetProperty(world, nameof(CasaEngine.Framework.Scene.World.World.Game), game);

        var entityRoot = new TestSceneComponent();
        var entity = new Entity { RootComponent = entityRoot };
        SetProperty(entity, nameof(Entity.World), world);

        var component = new AnimatedSpriteComponent();
        entityRoot.AddChildComponent(component);
        return component;
    }

    private static Animation2dCompositionSampler? GetCurrentSampler(AnimatedSpriteComponent component)
    {
        var field = typeof(AnimatedSpriteComponent).GetField("_currentCompositionSampler", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (Animation2dCompositionSampler?)field!.GetValue(component);
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var property = typeof(TTarget).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    private sealed class TestSceneComponent : SceneComponent
    {
        public TestSceneComponent()
        {
        }

        private TestSceneComponent(TestSceneComponent other) : base(other)
        {
        }

        public override TestSceneComponent Clone() => new(this);
    }
}
