using System.Runtime.CompilerServices;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.UI.Backend.MonoGame.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Shared.Assets;
using MGUI.Shared.Text;
using Xunit;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Tests.UI;

/// <summary>
/// T3.2 (decision D11; ADR-0038 "Images are named by asset" and "UI animations run on the UI clock"; MGUI
/// ADR-0016 "Animated image sources"): an MGUI <c>Image</c> whose <c>SourceName</c> names a catalogued 2D
/// animation asset (by id or by name) gets a new, independent <see cref="CasaUIAssetProvider.CasaUIAnimatedImage"/>
/// player from <see cref="CasaUIAssetProvider.TryCreateAnimatedImage"/>. The animation's composition data and
/// the sprites its frames use are resolved and held only once per name / per sprite id, in the provider; each
/// per-image instance owns only its own sampler.
/// <para/>
/// The asset catalog (<see cref="AssetCatalog"/>) is global state, so this class runs in the
/// <see cref="ProjectEnvironmentCollection"/> and clears it on both sides, like <c>CasaUIAssetProviderTests</c>.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class CasaUIAssetProviderAnimatedImageTests : IDisposable
{
    private static readonly Guid AnimationId = Guid.Parse("eeeeeeee-5555-5555-5555-555555555555");
    private static readonly Guid SheetTextureId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
    private static readonly Guid RawTexture2DId = Guid.Parse("dddddddd-4444-4444-4444-444444444444");

    private static readonly Guid[] SpriteIds =
    {
        Guid.Parse("11111111-0000-0000-0000-000000000000"),
        Guid.Parse("22222222-0000-0000-0000-000000000000"),
        Guid.Parse("33333333-0000-0000-0000-000000000000"),
        Guid.Parse("44444444-0000-0000-0000-000000000000"),
    };

    private static readonly Rectangle[] SpriteRects =
    {
        new(0, 0, 16, 16),
        new(16, 0, 16, 16),
        new(32, 0, 16, 16),
        new(48, 0, 16, 16),
    };

    /// <summary>Step keyframes at 0, 0.2, 0.4 and 0.6s cycling through the four frames, plus a padding
    /// keyframe at 0.8s repeating the last frame (docs/engine/animation2d-composed-format-v1.md), looped. A
    /// position track moves the single part from (0,0) to (3.4, 7.6) at 0.2s, to exercise <c>CurrentDrawOffset</c>.</summary>
    private const string PartId = "root";

    public CasaUIAssetProviderAnimatedImageTests()
    {
        AssetCatalog.ClearInternal();
    }

    public void Dispose()
    {
        AssetCatalog.ClearInternal();
    }

    private sealed class AnimationDataLoader : IAssetLoader
    {
        public int Loads;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            Loads++;
            return BuildWalkAnimation();
        }

        public bool IsFileSupported(string fileName) => true;
    }

    private sealed class SpriteDataLoader : IAssetLoader
    {
        public int Loads;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            Loads++;
            var index = Array.FindIndex(SpriteIds, id => fileName.Contains(id.ToString()));
            return new SpriteData
            {
                SpriteSheetAssetId = SheetTextureId,
                PositionInTexture = index >= 0 ? SpriteRects[index] : SpriteRects[0],
            };
        }

        public bool IsFileSupported(string fileName) => true;
    }

    private sealed class TextureLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
            => new Texture(RawTexture2DId, null);

        public bool IsFileSupported(string fileName) => true;
    }

    private sealed class Texture2DLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
            => RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));

        public bool IsFileSupported(string fileName) => true;
    }

    private static Animation2dData BuildWalkAnimation()
    {
        var data = new Animation2dData { AnimationType = AnimationType.Loop };
        data.Parts.Add(new Animation2dPartData { Id = PartId, DefaultSpriteId = SpriteIds[0] });

        var spriteTrack = new Animation2dTrackData { TargetPartId = PartId, Property = Animation2dTrackProperty.Sprite };
        spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0f, SpriteIds[0]));
        spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0.2f, SpriteIds[1]));
        spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0.4f, SpriteIds[2]));
        spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0.6f, SpriteIds[3]));
        spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0.8f, SpriteIds[3])); // padding keyframe
        data.Tracks.Add(spriteTrack);

        var positionTrack = new Animation2dTrackData { TargetPartId = PartId, Property = Animation2dTrackProperty.Position };
        positionTrack.PositionKeyframes.Add(new Animation2dVector2KeyframeData(0f, Vector2.Zero));
        positionTrack.PositionKeyframes.Add(new Animation2dVector2KeyframeData(0.2f, new Vector2(3.4f, 7.6f)));
        data.Tracks.Add(positionTrack);

        return data;
    }

    private static void RegisterCatalog()
    {
        AssetCatalog.AddInternal(new AssetInfo(AnimationId) { Name = "walk", FileName = "walk.anim2d" });
        for (var i = 0; i < SpriteIds.Length; i++)
        {
            AssetCatalog.AddInternal(new AssetInfo(SpriteIds[i]) { Name = $"frame{i}", FileName = $"frame-{SpriteIds[i]}.sprite" });
        }
        AssetCatalog.AddInternal(new AssetInfo(SheetTextureId) { Name = "walk-sheet", FileName = "walk-sheet.texture" });
        AssetCatalog.AddInternal(new AssetInfo(RawTexture2DId) { Name = "walk-sheet-image", FileName = "walk-sheet-image.png" });
    }

    private static AssetContentManager NewManager(out AnimationDataLoader animationLoader, out SpriteDataLoader spriteDataLoader)
    {
        var manager = new AssetContentManager();
        animationLoader = new AnimationDataLoader();
        manager.RegisterAssetLoader(typeof(Animation2dData), animationLoader);
        spriteDataLoader = new SpriteDataLoader();
        manager.RegisterAssetLoader(typeof(SpriteData), spriteDataLoader);
        manager.RegisterAssetLoader(typeof(Texture), new TextureLoader());
        manager.RegisterAssetLoader(typeof(Texture2D), new Texture2DLoader());
        return manager;
    }

    private sealed class NullServiceProvider : IServiceProvider
    {
        public static readonly NullServiceProvider Instance = new();
        public object GetService(Type serviceType) => null;
    }

    private static CasaUIAssetProvider NewProvider(AssetContentManager assetContentManager)
        => new(new ContentManager(NullServiceProvider.Instance), new FontManager(new ContentManager(NullServiceProvider.Instance), "Arial"), assetContentManager);

    private static int IndexOfRect(Rectangle? rect)
    {
        Assert.NotNull(rect);
        return Array.IndexOf(SpriteRects, rect.Value);
    }

    [Fact]
    public void TryCreateAnimatedImage_ByName_ResolvesTheFirstFrame()
    {
        RegisterCatalog();
        var manager = NewManager(out _, out _);
        using var provider = NewProvider(manager);

        var resolved = provider.TryCreateAnimatedImage("walk", out var animatedImage);

        Assert.True(resolved);
        Assert.NotNull(animatedImage);
        Assert.Equal(SpriteRects[0], animatedImage.CurrentSourceRect);
    }

    [Fact]
    public void Advance_StepsThroughFrames_OnFrameBoundaries()
    {
        RegisterCatalog();
        var manager = NewManager(out _, out _);
        using var provider = NewProvider(manager);
        provider.TryCreateAnimatedImage("walk", out var animatedImage);

        const double dt = 1.0 / 60.0;
        const int cycles = 25;
        const double cycleDuration = 0.8;
        var steps = (int)(cycles * cycleDuration / dt) + 1;

        double time = 0;
        var previousRect = animatedImage.CurrentSourceRect;
        for (var step = 0; step < steps; step++)
        {
            animatedImage.Advance(TimeSpan.FromSeconds(dt));
            time += dt;
            var rect = animatedImage.CurrentSourceRect;
            if (rect != previousRect)
            {
                var nearestBoundary = Math.Round(time / 0.2) * 0.2;
                Assert.True(Math.Abs(time - nearestBoundary) <= dt + 1e-6,
                    $"Frame changed at t={time:F4}s, not within one step of a 200ms boundary ({nearestBoundary:F4}).");
                previousRect = rect;
            }
        }
    }

    [Fact]
    public void TwoInstances_RestartedOneFrameApart_StayOneFrameApart()
    {
        RegisterCatalog();
        var manager = NewManager(out _, out _);
        using var provider = NewProvider(manager);
        provider.TryCreateAnimatedImage("walk", out var image1);
        provider.TryCreateAnimatedImage("walk", out var image2);

        image1.Restart(TimeSpan.Zero);
        image2.Restart(TimeSpan.FromMilliseconds(200));

        var dt = TimeSpan.FromSeconds(1.0 / 60.0);
        for (var step = 0; step < 150; step++)
        {
            image1.Advance(dt);
            image2.Advance(dt);

            var index1 = IndexOfRect(image1.CurrentSourceRect);
            var index2 = IndexOfRect(image2.CurrentSourceRect);
            Assert.Equal((index1 + 1) % 4, index2);
        }
    }

    [Fact]
    public void CurrentDrawOffset_FollowsThePositionTrack_RoundedToPixels()
    {
        RegisterCatalog();
        var manager = NewManager(out _, out _);
        using var provider = NewProvider(manager);
        provider.TryCreateAnimatedImage("walk", out var animatedImage);

        animatedImage.Restart(TimeSpan.Zero);
        Assert.Equal(Point.Zero, animatedImage.CurrentDrawOffset);

        animatedImage.Restart(TimeSpan.FromMilliseconds(250));
        Assert.Equal(new Point(3, 8), animatedImage.CurrentDrawOffset); // (3.4, 7.6) rounded to pixels
    }

    [Fact]
    public void Advance_AllocatesNothing_AfterWarmup()
    {
        RegisterCatalog();
        var manager = NewManager(out _, out _);
        using var provider = NewProvider(manager);
        provider.TryCreateAnimatedImage("walk", out var animatedImage);

        var dt = TimeSpan.FromSeconds(1.0 / 60.0);
        for (var i = 0; i < 10; i++)
        {
            animatedImage.Advance(dt);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            animatedImage.Advance(dt);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void CompositionSamplerUpdate_AllocatesNothing_AfterWarmup()
    {
        var composition = Animation2dCompositionAdapter.Create(BuildWalkAnimation());
        var sampler = new Animation2dCompositionSampler(composition);

        for (var i = 0; i < 10; i++)
        {
            sampler.Update(1f / 60f);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            sampler.Update(1f / 60f);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void Provider_CreatesANewInstancePerCall_ButAcquiresTheAnimationAndItsSpritesOnce()
    {
        RegisterCatalog();
        var manager = NewManager(out var animationLoader, out var spriteDataLoader);
        var provider = NewProvider(manager);

        Assert.True(provider.TryCreateAnimatedImage("walk", out var image1));
        Assert.True(provider.TryCreateAnimatedImage(AnimationId.ToString(), out var image2));

        Assert.NotSame(image1, image2);
        Assert.Equal(1, animationLoader.Loads);
        Assert.Equal(4, spriteDataLoader.Loads); // once per distinct frame sprite, never per image instance.
        Assert.Equal(0, manager.CollectUnreferenced()); // still held by the provider.

        provider.Dispose();

        // Freed: the animation data, its 4 frame sprite data assets, the shared sheet Texture, and its
        // underlying Texture2D (ADR-0037) -- 7 assets, none of it kept alive by either per-image instance.
        Assert.Equal(7, manager.CollectUnreferenced());

        Assert.True(((CasaUIAssetProvider.CasaUIAnimatedImage)image1).IsDisposed);
        Assert.True(((CasaUIAssetProvider.CasaUIAnimatedImage)image2).IsDisposed);
    }

    /// <summary>O4 of the bound screens program: an instance MGUI disposes (its image changed source) leaves the
    /// provider's list, so an image switching between a sprite and an animation all world long does not grow it.</summary>
    [Fact]
    public void Provider_ForgetsAnInstance_OnceItIsDisposed()
    {
        RegisterCatalog();
        var manager = NewManager(out _, out _);
        var provider = NewProvider(manager);

        for (var i = 0; i < 50; i++)
        {
            Assert.True(provider.TryCreateAnimatedImage("walk", out var image));
            image.Dispose();
            image.Dispose(); // idempotent
        }

        Assert.True(provider.TryCreateAnimatedImage("walk", out var kept));
        Assert.Equal(1, provider.LiveAnimatedImageCount);

        provider.Dispose();

        Assert.Equal(0, provider.LiveAnimatedImageCount);
        Assert.True(((CasaUIAssetProvider.CasaUIAnimatedImage)kept).IsDisposed);
    }

    [Fact]
    public void Provider_Dispose_IsIdempotent()
    {
        RegisterCatalog();
        var manager = NewManager(out _, out _);
        var provider = NewProvider(manager);
        Assert.True(provider.TryCreateAnimatedImage("walk", out _));

        provider.Dispose();
        provider.Dispose();

        Assert.Equal(7, manager.CollectUnreferenced());
    }

    [Fact]
    public void TryResolveImage_ReturnsFalse_ForAnAnimationName_WithoutWarning()
    {
        RegisterCatalog();
        var manager = NewManager(out _, out _);
        using var provider = NewProvider(manager);

        var resolved = true;
        IUIImageResource image = null;
        Rectangle? sourceRect = null;
        var warnings = CaptureWarnings(() => resolved = provider.TryResolveImage("walk", out image, out sourceRect));

        // MGUI asks for a static image before an animated one: a valid animation name is not an error there.
        Assert.False(resolved);
        Assert.Null(image);
        Assert.Null(sourceRect);
        Assert.Empty(warnings);
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
}
