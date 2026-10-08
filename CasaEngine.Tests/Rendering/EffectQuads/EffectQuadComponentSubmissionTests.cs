using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.EffectQuads;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;
using CasaEngineTexture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Tests.Rendering.EffectQuads;

/// <summary>
/// Covers <see cref="EffectQuadComponent"/> (E19.g G2e, ADR-0071) headlessly, like <c>CellularLayerComponentSubmissionTests</c>: an
/// uninitialised <see cref="CasaEngineGame"/>, device-less <see cref="Texture2D"/>s, a <see cref="SpriteRendererComponent"/> built
/// without a device and the queued <c>_spriteDatas</c> read by reflection. The texture resolution runs against a real
/// <see cref="AssetContentManager"/> whose catalog holds two sheet ids; the pixels are proved by the engine demo.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class EffectQuadComponentSubmissionTests
{
    private const int TextureSize = 64;

    private static readonly Guid SheetA = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid SheetB = Guid.Parse("a1000000-0000-0000-0000-000000000002");
    private static readonly Guid SheetNotInCatalog = Guid.Parse("a1000000-0000-0000-0000-0000000000ff");

    private static readonly Rectangle Window = new(10, 20, 8, 6);

    // World corners (y up), in the order of the PS1 data: TL, TR, BL, BR.
    private static readonly Vector2 Tl = new(10f, -20f);
    private static readonly Vector2 Tr = new(30f, -22f);
    private static readonly Vector2 Bl = new(12f, -42f);
    private static readonly Vector2 Br = new(34f, -40f);
    private static readonly Vector2 Center = new(21.5f, -31f);

    // ---- submission -----------------------------------------------------------------------------------------------

    [Fact]
    public void ANoneQuad_IsOneEntry_WithEveryFieldOfTheQuad_WhiteAtZeroDepth()
    {
        var wired = CreateWired();
        wired.Component.Service.SetSheets(new[] { SheetA });
        var texture = CreateTexture();
        wired.Component.ResolveTextures(_ => texture);
        var key = Key(5);
        wired.Component.Service.SetFrame(new[] { Quad(0, SpritePsxSemiTransparency.None, key) });
        var scissor = new Rectangle(10, 20, 320, 240);

        wired.Component.Submit(wired.Renderer, scissor);

        var entry = Assert.Single(GetSpriteDatas(wired.Renderer))!;
        AssertEntry(entry, texture, scissor, key, SpriteBlendMode.Opaque);
    }

    [Theory]
    [InlineData(SpritePsxSemiTransparency.Mode0, SpriteBlendMode.AlphaBlend)]
    [InlineData(SpritePsxSemiTransparency.Mode1, SpriteBlendMode.Additive)]
    [InlineData(SpritePsxSemiTransparency.Mode2, SpriteBlendMode.Subtractive)]
    public void AModeQuad_IsTwoEntries_SameKeyZAndCorners_OpaqueThenTheModeBlend(
        SpritePsxSemiTransparency mode, SpriteBlendMode expectedStpBlend)
    {
        var wired = CreateWired();
        wired.Component.Service.SetSheets(new[] { SheetA });
        var texture = CreateTexture();
        wired.Component.ResolveTextures(_ => texture);
        var key = Key(5);
        wired.Component.Service.SetFrame(new[] { Quad(0, mode, key) });
        var scissor = new Rectangle(10, 20, 320, 240);

        wired.Component.Submit(wired.Renderer, scissor);

        var entries = GetSpriteDatas(wired.Renderer);
        Assert.Equal(2, entries.Count);
        AssertEntry(entries[0]!, texture, scissor, key, SpriteBlendMode.Opaque);
        AssertEntry(entries[1]!, texture, scissor, key, expectedStpBlend);
    }

    [Fact]
    public void TheQuadsAreSubmittedInTheOrderOfTheFrame_EveryKeyKept_TheComponentDoesNotSort()
    {
        var wired = CreateWired();
        wired.Component.Service.SetSheets(new[] { SheetA });
        wired.Component.ResolveTextures(_ => CreateTexture());

        // The producer gives image k of N the local offset N - 1 - k, so that image 0 sorts last (drawn on top) in the sprite pass.
        var keys = new[] { LocalOffsetKey(0, 1), LocalOffsetKey(2, 2), LocalOffsetKey(1, 3), LocalOffsetKey(3, 4) };
        wired.Component.Service.SetFrame(new[]
        {
            Quad(0, SpritePsxSemiTransparency.None, keys[0]),
            Quad(0, SpritePsxSemiTransparency.None, keys[1]),
            Quad(0, SpritePsxSemiTransparency.None, keys[2]),
            Quad(0, SpritePsxSemiTransparency.None, keys[3]),
        });

        wired.Component.Submit(wired.Renderer, Rectangle.Empty);

        var entries = GetSpriteDatas(wired.Renderer);
        Assert.Equal(4, entries.Count);
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(keys[i], (RenderSortKey2D)GetField(entries[i]!, "SortKey"));
        }

        // Sorted by the key, the image with the largest local offset is the last one drawn.
        var sorted = (RenderSortKey2D[])keys.Clone();
        Array.Sort(sorted);
        Assert.Equal(keys[3], sorted[^1]);
    }

    [Fact]
    public void ASheetIndexOutOfRange_OrAnAbsentSheet_SkipsTheQuad_TheOthersAreDrawn()
    {
        var wired = CreateWired();
        wired.Component.Service.SetSheets(new[] { SheetA, Guid.Empty });
        wired.Component.ResolveTextures(id => id == SheetA ? CreateTexture() : null);
        var good = Key(1);
        wired.Component.Service.SetFrame(new[]
        {
            Quad(-1, SpritePsxSemiTransparency.None, Key(10)),
            Quad(2, SpritePsxSemiTransparency.None, Key(11)),
            Quad(1, SpritePsxSemiTransparency.None, Key(12)),
            Quad(0, SpritePsxSemiTransparency.None, good),
        });

        wired.Component.Submit(wired.Renderer, Rectangle.Empty);

        var entry = Assert.Single(GetSpriteDatas(wired.Renderer))!;
        Assert.Equal(good, (RenderSortKey2D)GetField(entry, "SortKey"));
    }

    [Fact]
    public void AnEmptyFrame_SubmitsNothing()
    {
        var wired = CreateWired();
        wired.Component.Service.SetSheets(new[] { SheetA });
        wired.Component.ResolveTextures(_ => CreateTexture());

        wired.Component.Submit(wired.Renderer, Rectangle.Empty);

        Assert.Empty(GetSpriteDatas(wired.Renderer));
    }

    // ---- the frame persists, the update submits -------------------------------------------------------------------

    [Fact]
    public void Update_SubmitsTheFrameEveryTime_ItPersistsUntilTheNextSetFrameOrClear()
    {
        var wired = CreateWired();
        wired.Manager.RegisterSheets(SheetA);
        wired.Component.Service.SetSheets(new[] { SheetA });
        wired.Component.Service.SetFrame(new[] { Quad(0, SpritePsxSemiTransparency.None, Key(1)) });

        wired.Component.Update(new GameTime());
        Assert.Single(GetSpriteDatas(wired.Renderer));

        wired.Component.Update(new GameTime());
        Assert.Equal(2, GetSpriteDatas(wired.Renderer).Count);

        wired.Component.Service.SetFrame(new[]
        {
            Quad(0, SpritePsxSemiTransparency.None, Key(2)),
            Quad(0, SpritePsxSemiTransparency.None, Key(3)),
        });
        wired.Component.Update(new GameTime());
        Assert.Equal(4, GetSpriteDatas(wired.Renderer).Count);

        wired.Component.Service.Clear();
        wired.Component.Update(new GameTime());
        Assert.Equal(4, GetSpriteDatas(wired.Renderer).Count);
    }

    [Fact]
    public void Update_UsesTheDeviceScissor_NotAnythingElse()
    {
        var wired = CreateWired();
        wired.Manager.RegisterSheets(SheetA);
        wired.Component.Service.SetSheets(new[] { SheetA });
        wired.Component.Service.SetFrame(new[] { Quad(0, SpritePsxSemiTransparency.None, Key(1)) });

        wired.Component.Update(new GameTime());

        // The device of the headless game is uninitialised: its scissor rectangle is empty (see CreateWired).
        var entry = Assert.Single(GetSpriteDatas(wired.Renderer))!;
        Assert.Equal(Rectangle.Empty, (Rectangle)GetField(entry, "ScissorRectangle"));
    }

    // ---- texture resolution ---------------------------------------------------------------------------------------

    [Fact]
    public void ResolveTextures_RecordsTheVersionItResolves_TheNextUpdateDoesNotGoBackToTheCatalog()
    {
        var wired = CreateWired();
        wired.Component.Service.SetSheets(new[] { SheetNotInCatalog });
        var calls = 0;
        var texture = CreateTexture();
        wired.Component.ResolveTextures(_ =>
        {
            calls++;
            return texture;
        });
        wired.Component.Service.SetFrame(new[] { Quad(0, SpritePsxSemiTransparency.None, Key(1)) });

        wired.Component.Update(new GameTime());
        wired.Component.Update(new GameTime());

        // Had the seam not recorded the version, the update would have resolved the sheet through the catalog, where it is absent
        // (a warning, the synthetic texture of the seam lost, no quad drawn).
        Assert.Equal(1, calls);
        var entries = GetSpriteDatas(wired.Renderer);
        Assert.Equal(2, entries.Count);
        Assert.Same(texture, GetField(entries[0]!, "Texture"));
    }

    [Fact]
    public void ANewVersion_IsResolvedOnTheNextUpdate_ThroughTheCatalog()
    {
        var wired = CreateWired();
        var textureA = wired.Manager.RegisterSheets(SheetA, SheetB);
        wired.Component.Service.SetSheets(new[] { SheetA });
        wired.Component.Service.SetFrame(new[] { Quad(0, SpritePsxSemiTransparency.None, Key(1)) });

        wired.Component.Update(new GameTime());
        Assert.Same(textureA[SheetA], GetField(GetSpriteDatas(wired.Renderer)[0]!, "Texture"));

        wired.Component.Service.SetSheets(new[] { SheetB });
        wired.Component.Update(new GameTime());

        Assert.Same(textureA[SheetB], GetField(GetSpriteDatas(wired.Renderer)[1]!, "Texture"));
    }

    [Fact]
    public void TheHandles_AreHeldWhileResolved_ReleasedOnTheNextResolution_AndOnDispose()
    {
        var wired = CreateWired();
        wired.Manager.RegisterSheets(SheetA, SheetB);
        wired.Component.Service.SetSheets(new[] { SheetA });
        wired.Component.Update(new GameTime());
        Assert.Equal(0, wired.Manager.Assets.CollectUnreferenced());

        // Several updates of the same version: still one resolution, still held.
        wired.Component.Update(new GameTime());
        Assert.Equal(0, wired.Manager.Assets.CollectUnreferenced());

        wired.Component.Service.SetSheets(new[] { SheetB });
        wired.Component.Update(new GameTime());
        Assert.Equal(1, wired.Manager.Assets.CollectUnreferenced()); // sheet A is freed, sheet B is held

        wired.Component.Dispose();
        Assert.Equal(1, wired.Manager.Assets.CollectUnreferenced()); // sheet B is freed

        wired.Component.Dispose();
        Assert.Equal(0, wired.Manager.Assets.CollectUnreferenced());
    }

    [Fact]
    public void ASheetThatFailsToLoad_WarnsOnce_ItsQuadsAreSkipped_TheOtherSheetsAreDrawn()
    {
        var wired = CreateWired();
        wired.Manager.RegisterSheets(SheetA);
        wired.Component.Service.SetSheets(new[] { SheetNotInCatalog, SheetA });
        var good = Key(2);
        wired.Component.Service.SetFrame(new[]
        {
            Quad(0, SpritePsxSemiTransparency.None, Key(1)),
            Quad(1, SpritePsxSemiTransparency.None, good),
        });

        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            wired.Component.Update(new GameTime());
            wired.Component.Update(new GameTime());
            wired.Component.Update(new GameTime());
        }
        finally
        {
            Logs.Close();
        }

        var warning = Assert.Single(logger.Warnings);
        Assert.Contains(SheetNotInCatalog.ToString(), warning);
        var entries = GetSpriteDatas(wired.Renderer);
        Assert.Equal(3, entries.Count);
        foreach (var entry in entries)
        {
            Assert.Equal(good, (RenderSortKey2D)GetField(entry!, "SortKey"));
        }
    }

    [Fact]
    public void AFailingSeamLoader_IsOneWarning_AndTheQuadIsSkipped()
    {
        var wired = CreateWired();
        wired.Component.Service.SetSheets(new[] { SheetA });
        wired.Component.Service.SetFrame(new[] { Quad(0, SpritePsxSemiTransparency.None, Key(1)) });

        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            wired.Component.ResolveTextures(_ => null);
            wired.Component.Update(new GameTime());
        }
        finally
        {
            Logs.Close();
        }

        Assert.Single(logger.Warnings);
        Assert.Empty(GetSpriteDatas(wired.Renderer));
    }

    [Fact]
    public void AnAbsentSheet_NeverReachesTheLoader_AndIsNoWarning()
    {
        // E19.m8 M8-c (G2e verification, note P3): Guid.Empty is an absent sheet, skipped before the loader - not a failed load.
        var wired = CreateWired();
        wired.Component.Service.SetSheets(new[] { SheetA, Guid.Empty });
        var asked = new List<Guid>();

        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            wired.Component.ResolveTextures(id =>
            {
                asked.Add(id);
                return id == SheetA ? CreateTexture() : null;
            });
        }
        finally
        {
            Logs.Close();
        }

        Assert.Equal(new[] { SheetA }, asked);
        Assert.Empty(logger.Warnings);
    }

    // ---- helpers --------------------------------------------------------------------------------------------------

    private static EffectQuad Quad(int sheetIndex, SpritePsxSemiTransparency mode, RenderSortKey2D key)
    {
        return new EffectQuad(sheetIndex, Window, Tl, Tr, Bl, Br, mode, key);
    }

    private static RenderSortKey2D Key(int stableId)
    {
        return new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 300, 0, 214, 11100, 0, stableId);
    }

    private static RenderSortKey2D LocalOffsetKey(int localSortOffset, int stableId)
    {
        return new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 300, 0, 214, 11100, localSortOffset, stableId);
    }

    private static void AssertEntry(object entry, Texture2D texture, Rectangle scissor, RenderSortKey2D key, SpriteBlendMode blend)
    {
        Assert.Same(texture, GetField(entry, "Texture"));
        Assert.Equal(scissor, (Rectangle)GetField(entry, "ScissorRectangle"));
        Assert.Equal(key, (RenderSortKey2D)GetField(entry, "SortKey"));
        Assert.True((bool)GetField(entry, "HasSortKey"));
        Assert.Equal(blend, (SpriteBlendMode)GetField(entry, "BlendMode"));
        Assert.Equal(Color.White, (Color)GetField(entry, "Color"));
        Assert.True((bool)GetField(entry, "PsxQuad"));

        // z = 0, the centre of the four corners; the four vertices are the corners as offsets from it, in the slots TR, BR, BL, TL.
        var world = (Matrix)GetField(entry, "WorldMatrix");
        Assert.Equal(Matrix.CreateTranslation(Center.X, Center.Y, 0f), world);
        Assert.Equal(new Vector3(Tr - Center, 0f), ((VertexPositionTexture)GetField(entry, "TopLeft")).Position);
        Assert.Equal(new Vector3(Br - Center, 0f), ((VertexPositionTexture)GetField(entry, "TopRight")).Position);
        Assert.Equal(new Vector3(Bl - Center, 0f), ((VertexPositionTexture)GetField(entry, "BottomRight")).Position);
        Assert.Equal(new Vector3(Tl - Center, 0f), ((VertexPositionTexture)GetField(entry, "BottomLeft")).Position);
    }

    private sealed record Wired(EffectQuadComponent Component, SpriteRendererComponent Renderer, SheetCatalog Manager);

    private static Wired CreateWired()
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        SetNonPublicField(game, typeof(Game), "_components", new GameComponentCollection());
        var renderer = new SpriteRendererComponent(game);

        // The scissor of the device: a service whose device is an uninitialised object answers an empty rectangle.
        var service = new FakeGraphicsDeviceService((GraphicsDevice)RuntimeHelpers.GetUninitializedObject(typeof(GraphicsDevice)));
        foreach (var owner in new object[] { game, renderer })
        {
            for (var type = owner.GetType(); type != null; type = type.BaseType)
            {
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType == typeof(IGraphicsDeviceService))
                    {
                        field.SetValue(owner, service);
                    }
                }
            }
        }

        var catalog = new SheetCatalog();
        SetAutoProperty(game, "SpriteRendererComponent", renderer);
        SetAutoProperty(game, "AssetContentManager", catalog.Assets);

        var component = new EffectQuadComponent(game);
        return new Wired(component, renderer, catalog);
    }

    /// <summary>A real asset manager with a texture loader and a catalog of the sheets registered by <see cref="RegisterSheets"/>.</summary>
    private sealed class SheetCatalog
    {
        private readonly Dictionary<Guid, AssetInfo> _infos = new();
        private readonly Dictionary<Guid, Texture2D> _images = new();

        public SheetCatalog()
        {
            Assets = new AssetContentManager
            {
                RuntimeContext = new EngineRuntimeContext(null, Path.GetTempPath(), id => _infos.GetValueOrDefault(id)),
            };
            Assets.RegisterAssetLoader(typeof(CasaEngineTexture), new TextureLoader(this));
        }

        public AssetContentManager Assets { get; }

        public Dictionary<Guid, Texture2D> RegisterSheets(params Guid[] ids)
        {
            foreach (var id in ids)
            {
                _infos[id] = new AssetInfo(id) { Name = id.ToString(), FileName = id + ".texture" };
                _images[id] = CreateTexture();
            }

            return _images;
        }

        private sealed class TextureLoader : IAssetLoader
        {
            private readonly SheetCatalog _owner;

            public TextureLoader(SheetCatalog owner)
            {
                _owner = owner;
            }

            public object LoadAsset(string fileName, AssetContentManager assetContentManager)
            {
                var id = Guid.Parse(Path.GetFileNameWithoutExtension(fileName));
                return new ResolvedTexture(_owner._images[id]);
            }

            public bool IsFileSupported(string fileName) => true;
        }
    }

    /// <summary>A texture asset that already holds its image, as if <c>Load</c> had run (no device is available here).</summary>
    private sealed class ResolvedTexture : CasaEngineTexture
    {
        private readonly Texture2D _image;

        public ResolvedTexture(Texture2D image)
        {
            _image = image;
        }

        public override Texture2D Resource => _image;
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

    private static Texture2D CreateTexture()
    {
        var texture = (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
        SetNonPublicField(texture, typeof(Texture2D), "width", TextureSize);
        SetNonPublicField(texture, typeof(Texture2D), "height", TextureSize);
        return texture;
    }

    private static System.Collections.IList GetSpriteDatas(SpriteRendererComponent component)
    {
        var field = typeof(SpriteRendererComponent).GetField("_spriteDatas", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (System.Collections.IList)field!.GetValue(component)!;
    }

    private static object GetField(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return field!.GetValue(instance)!;
    }

    private static void SetNonPublicField(object instance, Type declaringType, string fieldName, object value)
    {
        var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static void SetAutoProperty(object instance, string propertyName, object value)
    {
        var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private sealed class FakeGraphicsDeviceService : IGraphicsDeviceService
    {
        public FakeGraphicsDeviceService(GraphicsDevice graphicsDevice)
        {
            GraphicsDevice = graphicsDevice;
        }

        public GraphicsDevice GraphicsDevice { get; }

#pragma warning disable CS0067
        public event EventHandler<EventArgs>? DeviceCreated;
        public event EventHandler<EventArgs>? DeviceDisposing;
        public event EventHandler<EventArgs>? DeviceReset;
        public event EventHandler<EventArgs>? DeviceResetting;
#pragma warning restore CS0067
    }
}
