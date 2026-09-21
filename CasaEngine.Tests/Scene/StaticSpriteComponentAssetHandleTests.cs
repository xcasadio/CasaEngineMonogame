using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.Textures;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// ADR-0037: StaticSpriteComponent holds its sprite data and its sprite's sheet texture through
/// counted handles, and gives them back in Detach.
/// </summary>
/// <remarks>
/// Building the sprite the production way (Sprite.Create) resolves a Texture2D, which needs a
/// GraphicsDevice unavailable in this headless test project (same limit already documented for
/// Sprite.Create by T2.1). The component's private fields are set directly instead - the same
/// reflection technique SpriteRendererComponentBlendModeTests already uses to build a Sprite without a
/// device - so Detach's release logic is exercised against real AssetContentManager handles.
/// </remarks>
public class StaticSpriteComponentAssetHandleTests
{
    private static readonly Guid SpriteDataId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid TextureId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private sealed class SpriteDataLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager) => new SpriteData();

        public bool IsFileSupported(string fileName) => true;
    }

    private sealed class TextureLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager) => new Texture(TextureId, null);

        public bool IsFileSupported(string fileName) => true;
    }

    private static AssetContentManager NewManager()
    {
        var infos = new Dictionary<Guid, AssetInfo>
        {
            [SpriteDataId] = new AssetInfo(SpriteDataId) { Name = "sprite", FileName = "sprite.sprite" },
            [TextureId] = new AssetInfo(TextureId) { Name = "sheet", FileName = "sheet.texture" },
        };

        var manager = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, Path.GetTempPath(), id => infos.GetValueOrDefault(id)),
        };

        manager.RegisterAssetLoader(typeof(SpriteData), new SpriteDataLoader());
        manager.RegisterAssetLoader(typeof(Texture), new TextureLoader());
        return manager;
    }

    /// <summary>A Sprite holding a real texture handle, built without Sprite.Create (no GraphicsDevice needed).</summary>
    private static Sprite CreateSpriteHoldingTexture(AssetContentManager manager)
    {
        var textureHandle = manager.Acquire<Texture>(TextureId);
        var sprite = (Sprite)RuntimeHelpers.GetUninitializedObject(typeof(Sprite));
        SetField(sprite, "_textureHold", textureHandle);
        return sprite;
    }

    private static void SetField(object instance, string fieldName, object value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    [Fact]
    public void Detach_ReleasesTheSpriteDataHandle_AndDisposesTheSprite()
    {
        var manager = NewManager();
        var spriteDataHandle = manager.Acquire<SpriteData>(SpriteDataId);
        var sprite = CreateSpriteHoldingTexture(manager);

        var component = new StaticSpriteComponent();
        SetField(component, "_spriteData", spriteDataHandle.Asset);
        SetField(component, "_spriteDataHandle", spriteDataHandle);
        SetField(component, "_sprite", sprite);

        Assert.Equal(0, manager.CollectUnreferenced());

        component.Detach();

        // Both the sprite data hold and the sprite's own texture hold (given back by Sprite.Dispose)
        // are released: nobody holds either asset any more.
        Assert.Equal(2, manager.CollectUnreferenced());
    }
}
