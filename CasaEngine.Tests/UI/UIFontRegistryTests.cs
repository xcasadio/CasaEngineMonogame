using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Fonts;
using CasaEngine.Framework.UI;
using FontStashSharp;
using MGUI.FontStashSharp;
using MGUI.Shared.Text;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>
/// ADR-0036: the game-level registry that gives the bitmap fonts the game holds to every UI text engine,
/// by reference. A real BMFont needs a graphics device, so the test loader builds each
/// <see cref="BitmapFont"/> on a fixed-size font rasterized on the CPU from a TTF, the substitute
/// <c>FontStashSharpBitmapFontRegistrationTests</c> documents.
/// </summary>
public class UIFontRegistryTests
{
    private const string Family = "font3";
    private static readonly Guid FontId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private sealed class CpuBitmapFontLoader : IAssetLoader
    {
        private readonly SpriteFontBase _font;
        public int Loads;

        public CpuBitmapFontLoader()
        {
            var fontSystem = new FontSystem();
            fontSystem.AddFont(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Content", "Fonts", "ttf", "arial.ttf")));
            _font = fontSystem.GetFont(16);
        }

        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            Loads++;
            return new BitmapFont(Family, _font);
        }

        public bool IsFileSupported(string fileName) => true;
    }

    private static UIFontRegistry NewRegistry(out AssetContentManager assets, out CpuBitmapFontLoader loader)
    {
        var fontInfo = new AssetInfo(FontId) { Name = Family, FileName = @"UI\font3.fnt" };
        assets = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, Path.GetTempPath(), id => id == FontId ? fontInfo : null),
        };

        loader = new CpuBitmapFontLoader();
        assets.RegisterAssetLoader(typeof(BitmapFont), loader);
        return new UIFontRegistry(assets);
    }

    private static bool Resolves(FontStashSharpTextEngine textEngine)
        => !textEngine.ResolveFont(new FontSpec(Family, 12, CustomFontStyles.Normal)).IsFallback;

    [Fact]
    public void AFontHeldBeforeAttaching_ReachesTheTextEngine()
    {
        var registry = NewRegistry(out _, out _);
        using var hold = registry.Acquire(FontId);
        var textEngine = new FontStashSharpTextEngine();

        registry.Attach(textEngine);

        Assert.True(Resolves(textEngine));
    }

    [Fact]
    public void AFontHeldAfterAttaching_ReachesTheTextEngine()
    {
        var registry = NewRegistry(out _, out _);
        var textEngine = new FontStashSharpTextEngine();
        registry.Attach(textEngine);

        using var hold = registry.Acquire(FontId);

        Assert.True(Resolves(textEngine));
    }

    [Fact]
    public void AFontGivenBack_StaysResolvable_UntilItIsCollected()
    {
        var registry = NewRegistry(out var assets, out _);
        var textEngine = new FontStashSharpTextEngine();
        registry.Attach(textEngine);

        registry.Acquire(FontId).Dispose();
        Assert.True(Resolves(textEngine));

        assets.CollectUnreferenced();
        Assert.False(Resolves(textEngine));
    }

    /// <summary>The map change: the old world's holder gives the font back, the old text engine goes, the new
    /// world's text engine attaches, and the new world's holder takes the font again — the same instance,
    /// never reloaded, and resolvable on the new text engine.</summary>
    [Fact]
    public void AcrossAMapChange_TheFontReachesTheNewTextEngine_WithoutBeingReloaded()
    {
        var registry = NewRegistry(out var assets, out var loader);
        var oldTextEngine = new FontStashSharpTextEngine();
        registry.Attach(oldTextEngine);
        var oldHolder = registry.Acquire(FontId);
        Assert.True(Resolves(oldTextEngine));

        assets.CollectUnreferenced(); // start of the world change: the old holder still holds
        oldHolder.Dispose();          // the old world clears
        registry.Detach(oldTextEngine);
        var newTextEngine = new FontStashSharpTextEngine();
        registry.Attach(newTextEngine);
        Assert.False(Resolves(newTextEngine)); // pending fonts are not given to a new text engine

        using var newHolder = registry.Acquire(FontId);

        Assert.True(Resolves(newTextEngine));
        Assert.Equal(1, loader.Loads);
        Assert.Equal(0, assets.CollectUnreferenced()); // the next world change frees nothing
        Assert.True(Resolves(newTextEngine));
    }

    [Fact]
    public void ADetachedTextEngine_ReceivesNothingMore()
    {
        var registry = NewRegistry(out _, out _);
        var textEngine = new FontStashSharpTextEngine();
        registry.Attach(textEngine);
        registry.Detach(textEngine);

        using var hold = registry.Acquire(FontId);

        Assert.False(Resolves(textEngine));
    }

    [Fact]
    public void TwoHolders_TheFontStaysHeldUntilTheLastGivesItBack()
    {
        var registry = NewRegistry(out var assets, out var loader);
        var textEngine = new FontStashSharpTextEngine();
        registry.Attach(textEngine);
        var first = registry.Acquire(FontId);
        var second = registry.Acquire(FontId);

        first.Dispose();
        first.Dispose(); // idempotent
        Assert.Equal(0, assets.CollectUnreferenced());
        Assert.True(Resolves(textEngine));

        second.Dispose();
        Assert.Equal(1, assets.CollectUnreferenced());
        Assert.False(Resolves(textEngine));
        Assert.Equal(1, loader.Loads);
    }

    [Fact]
    public void AFontCollectedThenAcquiredAgain_IsLoadedAgain_AndReachesTheTextEngines()
    {
        var registry = NewRegistry(out var assets, out var loader);
        var textEngine = new FontStashSharpTextEngine();
        registry.Attach(textEngine);
        registry.Acquire(FontId).Dispose();
        assets.CollectUnreferenced();

        using var hold = registry.Acquire(FontId);

        Assert.True(Resolves(textEngine));
        Assert.Equal(2, loader.Loads);
    }

    [Fact]
    public void AcquiringAFontMissingFromTheCatalog_Throws_AndLeavesTheRegistryUsable()
    {
        var registry = NewRegistry(out _, out _);

        Assert.Throws<InvalidOperationException>(() => registry.Acquire(Guid.NewGuid()));

        var textEngine = new FontStashSharpTextEngine();
        registry.Attach(textEngine);
        using var hold = registry.Acquire(FontId);
        Assert.True(Resolves(textEngine));
    }
}
