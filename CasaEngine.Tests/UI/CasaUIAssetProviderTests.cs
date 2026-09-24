using System.Runtime.CompilerServices;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.UI.Backend.MonoGame.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Core.UI;
using MGUI.Shared.Text;
using Xunit;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Tests.UI;

/// <summary>
/// T3.1 (ADR-0038 P3, MGUI ADR-0016 "Images are named by asset"): an MGUI <c>Image</c> whose
/// <c>SourceName</c> names a catalogued sprite asset (by id or by name) is resolved by
/// <see cref="CasaUIAssetProvider.TryResolveImage"/> to that sprite's sheet texture and
/// <see cref="SpriteData.PositionInTexture"/>, held through counted <see cref="AssetContentManager"/>
/// handles until the provider is disposed.
/// <para/>
/// The asset catalog (<see cref="AssetCatalog"/>) is global state, so this class runs in the
/// <see cref="ProjectEnvironmentCollection"/> and clears it on both sides, like
/// <c>EnvironmentAssetLookupHandleTests</c>.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class CasaUIAssetProviderTests : IDisposable
{
    private static readonly Guid SpriteId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");

    /// <summary>The catalogued "Texture" asset that <see cref="SpriteData.SpriteSheetAssetId"/> points to.</summary>
    private static readonly Guid SheetTextureId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");

    /// <summary>The raw <see cref="Texture2D"/> content the sheet <see cref="Texture"/> itself references
    /// (<c>Texture._texture2dAssetId</c>) - a separate catalogued id, same as production (a ".texture" asset
    /// wraps a distinct image file), never the same id as <see cref="SheetTextureId"/>: the asset cache is
    /// keyed by id alone, one instance per id regardless of type.</summary>
    private static readonly Guid RawTexture2DId = Guid.Parse("dddddddd-4444-4444-4444-444444444444");

    private static readonly Rectangle SpriteRect = new(4, 8, 16, 16);

    public CasaUIAssetProviderTests()
    {
        AssetCatalog.ClearInternal();
    }

    public void Dispose()
    {
        AssetCatalog.ClearInternal();
    }

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

    private static List<string> CaptureWarnings(Action action)
    {
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            action();
        }
        finally
        {
            Logs.Close(); // detaches every logger added during this test, including `logger`.
        }

        return logger.Warnings;
    }

    private sealed class SpriteDataLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
            => new SpriteData { SpriteSheetAssetId = SheetTextureId, PositionInTexture = SpriteRect };

        public bool IsFileSupported(string fileName) => true;
    }

    private sealed class TextureLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
            => new Texture(RawTexture2DId, null);

        public bool IsFileSupported(string fileName) => true;
    }

    /// <summary>A fake <see cref="Texture2D"/>, built the same reflection way
    /// <c>SpriteRendererComponentBlendModeTests.CreateTexture</c> does, since a real one needs a
    /// GraphicsDevice unavailable in this headless test project.</summary>
    private sealed class Texture2DLoader : IAssetLoader
    {
        public int Loads;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            Loads++;
            return RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
        }

        public bool IsFileSupported(string fileName) => true;
    }

    /// <summary>Catalogues the sprite asset and the two assets it resolves through
    /// (<see cref="SheetTextureId"/>, <see cref="RawTexture2DId"/>): every test that expects
    /// <see cref="CasaUIAssetProvider.TryResolveImage"/> to succeed needs all three present, since
    /// <see cref="AssetContentManager.Acquire{T}"/> looks each one up in the catalog.</summary>
    private static void RegisterSpriteCatalog()
    {
        AssetCatalog.AddInternal(new AssetInfo(SpriteId) { Name = "hud-heart", FileName = "hud-heart.sprite" });
        AssetCatalog.AddInternal(new AssetInfo(SheetTextureId) { Name = "hud-sheet", FileName = "hud-sheet.texture" });
        AssetCatalog.AddInternal(new AssetInfo(RawTexture2DId) { Name = "hud-sheet-image", FileName = "hud-sheet-image.png" });
    }

    private static AssetContentManager NewManager(out Texture2DLoader textureLoader)
    {
        var manager = new AssetContentManager();
        manager.RegisterAssetLoader(typeof(SpriteData), new SpriteDataLoader());
        manager.RegisterAssetLoader(typeof(Texture), new TextureLoader());
        textureLoader = new Texture2DLoader();
        manager.RegisterAssetLoader(typeof(Texture2D), textureLoader);
        return manager;
    }

    private static CasaUIAssetProvider NewProvider(AssetContentManager assetContentManager)
        => new(new ContentManager(NullServiceProvider.Instance), new FontManager(new ContentManager(NullServiceProvider.Instance), "Arial"), assetContentManager);

    /// <summary>A <see cref="ContentManager"/> needs an <see cref="IServiceProvider"/>; the provider under test
    /// never calls into it for a sprite resolution (only <see cref="AssetContentManager"/> is used), so an
    /// empty one is enough.</summary>
    private sealed class NullServiceProvider : IServiceProvider
    {
        public static readonly NullServiceProvider Instance = new();
        public object GetService(Type serviceType) => null;
    }

    [Fact]
    public void TryResolveImage_ByGuidString_ResolvesTheSheetAndTheSpriteRectangle()
    {
        RegisterSpriteCatalog();
        var manager = NewManager(out _);
        using var provider = NewProvider(manager);

        var resolved = provider.TryResolveImage(SpriteId.ToString(), out var image, out var sourceRect);

        Assert.True(resolved);
        Assert.NotNull(image);
        Assert.Equal(SpriteRect, sourceRect);
    }

    [Fact]
    public void TryResolveImage_ByAssetName_ResolvesTheSameSprite()
    {
        RegisterSpriteCatalog();
        var manager = NewManager(out _);
        using var provider = NewProvider(manager);

        var resolved = provider.TryResolveImage("hud-heart", out var image, out var sourceRect);

        Assert.True(resolved);
        Assert.NotNull(image);
        Assert.Equal(SpriteRect, sourceRect);
    }

    [Fact]
    public void Dispose_GivesBackTheHandles_AndCollectUnreferenced_FreesTheSpriteAndItsTexture()
    {
        RegisterSpriteCatalog();
        var manager = NewManager(out _);
        var provider = NewProvider(manager);

        Assert.True(provider.TryResolveImage("hud-heart", out _, out _));
        Assert.Equal(0, manager.CollectUnreferenced()); // still held by the provider.

        provider.Dispose();

        // The sprite data handle and the sheet texture's own handle (given back by Sprite.Dispose) are both
        // released: nobody holds either asset any more (ADR-0037), plus the underlying Texture2D handle.
        Assert.Equal(3, manager.CollectUnreferenced());
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        RegisterSpriteCatalog();
        var manager = NewManager(out _);
        var provider = NewProvider(manager);
        Assert.True(provider.TryResolveImage("hud-heart", out _, out _));

        provider.Dispose();
        provider.Dispose();

        Assert.Equal(3, manager.CollectUnreferenced());
    }

    [Fact]
    public void TryResolveImage_UnknownName_ReturnsFalse_AndLogsOnce()
    {
        var manager = NewManager(out _);
        using var provider = NewProvider(manager);

        bool first = true, second = true;
        var warnings = CaptureWarnings(() =>
        {
            first = provider.TryResolveImage("no-such-asset", out var image1, out _);
            Assert.Null(image1);
            second = provider.TryResolveImage("no-such-asset", out var image2, out _);
            Assert.Null(image2);
        });

        Assert.False(first);
        Assert.False(second);
        Assert.Single(warnings);
        Assert.Contains("no-such-asset", warnings[0]);
    }

    [Fact]
    public void TryResolveImage_NonSpriteAsset_ReturnsFalse_AndLogsOnce_WithoutThrowing()
    {
        var notASpriteId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");
        AssetCatalog.AddInternal(new AssetInfo(notASpriteId) { Name = "hud-font", FileName = "hud-font.material" });
        var manager = NewManager(out _);
        using var provider = NewProvider(manager);

        bool first = true, second = true;
        var warnings = CaptureWarnings(() =>
        {
            first = provider.TryResolveImage("hud-font", out var image1, out _);
            Assert.Null(image1);
            second = provider.TryResolveImage("hud-font", out var image2, out _);
            Assert.Null(image2);
        });

        Assert.False(first);
        Assert.False(second);
        Assert.Single(warnings);
    }

    /// <summary>T4.2 (ADR-0038 "Design-time data"): the editor builds its UI backend, and the
    /// <see cref="CasaUIAssetProvider"/> it creates, before its own <see cref="AssetContentManager"/> exists --
    /// so the provider starts with none, and resolving anything fails softly until it is attached one.</summary>
    [Fact]
    public void TryResolveImage_BeforeAttach_FailsSoftly_AndSucceedsAfterAttach()
    {
        RegisterSpriteCatalog();
        var manager = NewManager(out _);
        using var provider = NewProvider(null);

        bool beforeAttach = true;
        var warnings = CaptureWarnings(() =>
        {
            beforeAttach = provider.TryResolveImage("hud-heart", out var image, out _);
            Assert.Null(image);
        });
        Assert.False(beforeAttach);
        Assert.Single(warnings);
        Assert.Contains("no AssetContentManager", warnings[0]);

        provider.AttachAssetContentManager(manager);

        var afterAttach = provider.TryResolveImage("hud-heart", out var resolvedImage, out var sourceRect);
        Assert.True(afterAttach);
        Assert.NotNull(resolvedImage);
        Assert.Equal(SpriteRect, sourceRect);
    }

    /// <summary>Attaching clears the provider's "already warned" bookkeeping, so a name that failed before an
    /// asset manager existed gets a fresh attempt -- and a fresh warning if it fails again -- rather than
    /// staying silently unresolvable for this provider's lifetime.</summary>
    [Fact]
    public void AttachAssetContentManager_ClearsPreviouslyWarnedNames()
    {
        var manager = NewManager(out _);
        using var provider = NewProvider(null);

        CaptureWarnings(() => provider.TryResolveImage("hud-heart", out _, out _));

        provider.AttachAssetContentManager(manager);
        RegisterSpriteCatalog();

        var warnings = CaptureWarnings(() =>
        {
            var resolved = provider.TryResolveImage("hud-heart", out var image, out _);
            Assert.True(resolved);
            Assert.NotNull(image);
        });
        Assert.Empty(warnings);
    }

    /// <summary>T4.2: on an editor project change, <see cref="CasaUIAssetProvider.ReleaseHeldAssets"/> gives
    /// back every handle this provider holds -- same as <see cref="CasaUIAssetProvider.Dispose"/> -- without
    /// disposing the provider, so it keeps resolving names (typically against a freshly-attached manager)
    /// afterwards.</summary>
    [Fact]
    public void ReleaseHeldAssets_GivesBackHandles_WithoutDisposing_AndStillResolvesAfterwards()
    {
        RegisterSpriteCatalog();
        var manager = NewManager(out _);
        var provider = NewProvider(manager);

        Assert.True(provider.TryResolveImage("hud-heart", out _, out _));
        Assert.Equal(0, manager.CollectUnreferenced()); // still held.

        provider.ReleaseHeldAssets();
        Assert.Equal(3, manager.CollectUnreferenced()); // same release as Dispose (sprite data + sheet handle + Texture2D).

        // Not disposed: resolving the same name again re-acquires it rather than failing.
        var resolvedAgain = provider.TryResolveImage("hud-heart", out var image, out var sourceRect);
        Assert.True(resolvedAgain);
        Assert.NotNull(image);
        Assert.Equal(SpriteRect, sourceRect);

        provider.Dispose();
    }

    [Fact]
    public void EndToEnd_MGImage_WithSourceNameAsTheSpriteGuid_ResolvesTheSameImageAndRectangle()
    {
        RegisterSpriteCatalog();
        var manager = NewManager(out _);
        using var provider = NewProvider(manager);

        // Not HeadlessUiTestHarness.NewDesktop: it calls MGDesktop.LoadDefaultResources, which loads the
        // desktop's default icons through the *real* CasaUIAssetProvider.LoadImage (a real ContentManager),
        // and there is no Content directory to load them from in this headless test project.
        var runtime = new HeadlessUiTestHarness.HeadlessRuntime(new Rectangle(0, 0, 640, 480), provider);
        var desktop = new MGDesktop(runtime);
        var window = new MGWindow(desktop, 0, 0, 200, 200);
        var image = new MGImage(window, SpriteId.ToString());

        Assert.NotNull(image.ActualSource);
        Assert.Equal(SpriteRect, image.ActualSource.Value.SourceRect);
    }
}
