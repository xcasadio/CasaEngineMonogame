using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScrollingLayers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering.ScrollingLayers;

/// <summary>
/// Covers <see cref="ScrollingLayerComponent.Submit"/> and <see cref="ScrollingLayerComponent.ResolveTextures"/>
/// headlessly, exactly like <c>ScreenEffectComponentSubmissionTests</c>: an uninitialized
/// <see cref="CasaEngineGame"/>, a device-less <see cref="Texture2D"/>, <see cref="SpriteRendererComponent"/>
/// built without a device, and the queued <c>_spriteDatas</c> read by reflection (plan
/// plan-e9b-backdrops-moteur.md, S0 item 6).
/// </summary>
public class ScrollingLayerComponentSubmissionTests
{
    private static readonly ScrollingLayerConfiguration Configuration = new(640, 480, 320, 240);
    private static readonly Rectangle Scissor = new(10, 20, 320, 240);

    [Fact]
    public void Submit_BeforeAnySetFrame_QueuesNothing_EvenWithLayersAndTint()
    {
        var (component, renderer) = CreateWired();
        component.Service.SetConfiguration(Configuration);
        component.Service.SetLayers(new[] { MakeOneQuadLayer() });
        component.Service.SetTint(MakeTint());
        InjectWhiteTexture(component);
        component.ResolveTextures(_ => CreateTexture());

        component.Submit(renderer, Vector3.Zero, Scissor);

        Assert.Empty(GetSpriteDatas(renderer));
    }

    [Fact]
    public void Submit_ScreenFixedLayer_AtTarget1087Minus839_SubmitsOneQuadAtTheOriginalScreenCorner()
    {
        var (component, renderer) = CreateWired();
        component.Service.SetConfiguration(Configuration);
        component.Service.SetLayers(new[] { MakeOneQuadLayer() }); // factor 0/1 both axes.
        var texture = CreateTexture();
        component.ResolveTextures(_ => texture);

        var target = new Vector3(1087f, -839f, 0f);
        component.Service.SetFrame(scrollX: 927, scrollY: 719, ticks: 0, target);
        component.Service.Advance();
        component.Submit(renderer, component.Service.CameraTarget, Scissor);

        var spriteDatas = GetSpriteDatas(renderer);
        Assert.Single(spriteDatas);

        var worldMatrix = (Matrix)GetField(spriteDatas[0], "WorldMatrix");
        // Uninitialized Texture2D => zero-size source rectangle, so the DrawSprite placement formula
        // reduces to the raw quad corner with no half-texture-size term (see BackdropRenderer's own
        // Draw doc for the general formula with a real texture).
        Assert.Equal(927f, worldMatrix.Translation.X, 3);
        Assert.Equal(-719f, worldMatrix.Translation.Y, 3);
        // The helper layer is Pass = Background (D-E9c-5): it recedes by the configured depth from the
        // camera target's own Z (here 0), rather than sitting at it.
        Assert.Equal(-Configuration.BackgroundDepth, worldMatrix.Translation.Z, 3);

        Assert.True(component.Service.TryGetLayerState(0, out var state));
        Assert.Equal(0, state.LayerOffsetX);
        Assert.Equal(0, state.LayerOffsetY);
    }

    [Fact]
    public void TryGetLayerState_FactorOneOverOneLayer_ReportsOffsets287And239()
    {
        var (component, _) = CreateWired();
        component.Service.SetConfiguration(Configuration);
        component.Service.SetLayers(new[] { MakeOneQuadLayer(factorNum: 1) });

        component.Service.SetFrame(scrollX: 927, scrollY: 719, ticks: 0, new Vector3(1087f, -839f, 0f));
        component.Service.Advance();

        Assert.True(component.Service.TryGetLayerState(0, out var state));
        Assert.Equal(287, state.LayerOffsetX);
        Assert.Equal(239, state.LayerOffsetY);
    }

    [Fact]
    public void Submit_FactorOneLayer_StaysWorldGlued_WhenCameraMovesVerticallyWithinOnePeriod()
    {
        var (component, renderer) = CreateWired();
        component.Service.SetConfiguration(Configuration);
        component.Service.SetLayers(new[] { MakeOneQuadLayer(factorNum: 1) });
        component.ResolveTextures(_ => CreateTexture());

        // Mirrors AlundraCameraMath.ToOriginalScrollSpace's own Y conversion (-(int)Y - 120) inline -
        // the engine mechanism does not know about that DLL formula, only about the scroll it is given.
        static int ScrollY(float cameraY) => -(int)cameraY - 120;

        var cameraA = new Vector3(0f, -100f, 0f);
        var cameraB = new Vector3(0f, -110f, 0f);

        component.Service.SetFrame(0, ScrollY(cameraA.Y), 0, cameraA);
        component.Service.Advance();
        component.Submit(renderer, component.Service.CameraTarget, Scissor);
        var quadA = (Matrix)GetField(GetSpriteDatas(renderer)[0], "WorldMatrix");
        GetSpriteDatas(renderer).Clear();

        component.Service.SetFrame(0, ScrollY(cameraB.Y), 0, cameraB);
        component.Service.Advance();
        component.Submit(renderer, component.Service.CameraTarget, Scissor);
        var quadB = (Matrix)GetField(GetSpriteDatas(renderer)[0], "WorldMatrix");

        static float Mod(float v, float m) => ((v % m) + m) % m;
        Assert.Equal(Mod(quadA.Translation.Y, 480f), Mod(quadB.Translation.Y, 480f), 3);
        Assert.Equal(Mod(quadA.Translation.X, 640f), Mod(quadB.Translation.X, 640f), 3);
    }

    [Fact]
    public void Submit_TranslationZ_BackgroundRecedesByConfiguredDepth_EffectsStaysAtCameraDepth()
    {
        var (component, renderer) = CreateWired();
        component.Service.SetConfiguration(Configuration);
        var backgroundLayer = MakeOneQuadLayer();
        backgroundLayer.Pass = RenderPass2D.Background;
        var effectsLayer = MakeOneQuadLayer();
        effectsLayer.Pass = RenderPass2D.Effects;
        component.Service.SetLayers(new[] { backgroundLayer, effectsLayer });
        component.ResolveTextures(_ => CreateTexture());

        component.Service.SetFrame(0, 0, 0, Vector3.Zero);
        component.Service.Advance();
        component.Submit(renderer, component.Service.CameraTarget, Scissor);

        var spriteDatas = GetSpriteDatas(renderer);
        Assert.Equal(2, spriteDatas.Count);
        var backgroundZ = (Matrix)GetField(spriteDatas[0], "WorldMatrix");
        var effectsZ = (Matrix)GetField(spriteDatas[1], "WorldMatrix");
        Assert.Equal(-Configuration.BackgroundDepth, backgroundZ.Translation.Z, 3);
        Assert.Equal(0f, effectsZ.Translation.Z, 3);
    }

    [Fact]
    public void Submit_TranslationZ_BackgroundRecedesFromTheNonZeroCameraTarget()
    {
        var (component, renderer) = CreateWired();
        component.Service.SetConfiguration(Configuration);
        var backgroundLayer = MakeOneQuadLayer();
        backgroundLayer.Pass = RenderPass2D.Background;
        component.Service.SetLayers(new[] { backgroundLayer });
        component.ResolveTextures(_ => CreateTexture());

        var target = new Vector3(0f, 0f, 42f);
        component.Service.SetFrame(0, 0, 0, target);
        component.Service.Advance();
        component.Submit(renderer, component.Service.CameraTarget, Scissor);

        var worldMatrix = (Matrix)GetField(GetSpriteDatas(renderer)[0], "WorldMatrix");
        Assert.Equal(42f - Configuration.BackgroundDepth, worldMatrix.Translation.Z, 3);
    }

    [Fact]
    public void Submit_TintTranslationZ_StaysAtCameraDepth_NeverReceded()
    {
        var (component, renderer) = CreateWired();
        component.Service.SetConfiguration(Configuration);
        component.Service.SetTint(MakeTint());
        InjectWhiteTexture(component);

        var target = new Vector3(0f, 0f, 42f);
        component.Service.SetFrame(0, 0, 0, target);
        component.Service.Advance();
        component.Submit(renderer, component.Service.CameraTarget, Scissor);

        var worldMatrix = (Matrix)GetField(GetSpriteDatas(renderer)[0], "WorldMatrix");
        Assert.Equal(42f, worldMatrix.Translation.Z, 3);
    }

    [Fact]
    public void Submit_TranslationZ_ZeroConfiguredDepth_ReproducesTheOldEqualDepthBehaviour()
    {
        var zeroDepthConfiguration = new ScrollingLayerConfiguration(640, 480, 320, 240, backgroundDepth: 0f);
        var (component, renderer) = CreateWired();
        component.Service.SetConfiguration(zeroDepthConfiguration);
        var backgroundLayer = MakeOneQuadLayer();
        backgroundLayer.Pass = RenderPass2D.Background;
        var effectsLayer = MakeOneQuadLayer();
        effectsLayer.Pass = RenderPass2D.Effects;
        component.Service.SetLayers(new[] { backgroundLayer, effectsLayer });
        component.ResolveTextures(_ => CreateTexture());

        component.Service.SetFrame(0, 0, 0, Vector3.Zero);
        component.Service.Advance();
        component.Submit(renderer, component.Service.CameraTarget, Scissor);

        var spriteDatas = GetSpriteDatas(renderer);
        Assert.Equal(2, spriteDatas.Count);
        foreach (var entry in spriteDatas)
        {
            var worldMatrix = (Matrix)GetField(entry, "WorldMatrix");
            Assert.Equal(0f, worldMatrix.Translation.Z, 3);
        }
    }

    [Fact]
    public void Submit_PassesTheGivenScissorRectangleThrough()
    {
        var (component, renderer) = CreateWired();
        component.Service.SetConfiguration(Configuration);
        component.Service.SetLayers(new[] { MakeOneQuadLayer() });
        component.ResolveTextures(_ => CreateTexture());

        component.Service.SetFrame(0, 0, 0, Vector3.Zero);
        component.Service.Advance();
        component.Submit(renderer, component.Service.CameraTarget, Scissor);

        var entry = GetSpriteDatas(renderer)[0];
        Assert.Equal(Scissor, (Rectangle)GetField(entry, "ScissorRectangle"));
    }

    [Fact]
    public void Submit_TintAlone_SubmitsOneAlphaBlendQuadAtTheTintSortKey()
    {
        var (component, renderer) = CreateWired();
        component.Service.SetConfiguration(Configuration);
        var tint = MakeTint();
        component.Service.SetTint(tint);
        InjectWhiteTexture(component);

        component.Service.SetFrame(0, 0, 0, Vector3.Zero);
        component.Service.Advance();
        component.Submit(renderer, component.Service.CameraTarget, Scissor);

        var spriteDatas = GetSpriteDatas(renderer);
        Assert.Single(spriteDatas);
        var entry = spriteDatas[0];
        Assert.Equal(tint.Color, (Color)GetField(entry, "Color"));
        Assert.Equal(tint.SortKey, (RenderSortKey2D)GetField(entry, "SortKey"));
        Assert.Equal(SpriteBlendMode.AlphaBlend, (SpriteBlendMode)GetField(entry, "BlendMode"));
    }

    [Fact]
    public void Submit_NoLayersAndNoTint_QueuesNothing()
    {
        var (component, renderer) = CreateWired();
        component.Service.SetConfiguration(Configuration);
        component.Service.SetLayers(Array.Empty<ScrollingLayerDefinition>());

        component.Service.SetFrame(0, 0, 0, Vector3.Zero);
        component.Service.Advance();
        component.Submit(renderer, component.Service.CameraTarget, Scissor);

        Assert.Empty(GetSpriteDatas(renderer));
    }

    [Fact]
    public void Submit_AfterAdvancingTicks_SubmitsTheExpectedFrameTexture_AtTicks7And28()
    {
        var (component, renderer) = CreateWired();
        component.Service.SetConfiguration(Configuration);
        component.Service.SetLayers(new[] { MakeOneQuadLayer(frameCount: 4, animTimer: 6) });
        var frames = new[] { CreateTexture(), CreateTexture(), CreateTexture(), CreateTexture() };
        var frameIds = component.Service.GetLayerDefinition(0).FrameTextureAssetIds;
        component.ResolveTextures(id =>
        {
            var index = Array.IndexOf(frameIds, id);
            return frames[index];
        });

        component.Service.SetFrame(0, 0, 7, Vector3.Zero);
        component.Service.Advance();
        component.Submit(renderer, component.Service.CameraTarget, Scissor);
        var textureAtTick7 = (Texture2D)GetField(GetSpriteDatas(renderer)[0], "Texture");
        Assert.Same(frames[1], textureAtTick7);

        GetSpriteDatas(renderer).Clear();
        component.Service.SetFrame(0, 0, 21, Vector3.Zero); // tick 7 + 21 = tick 28.
        component.Service.Advance();
        component.Submit(renderer, component.Service.CameraTarget, Scissor);
        var textureAtTick28 = (Texture2D)GetField(GetSpriteDatas(renderer)[0], "Texture");
        Assert.Same(frames[0], textureAtTick28);
    }

    // ---- D-E9-9 fallback (ResolveLayerFrames) ------------------------------------------------------------

    [Fact]
    public void ResolveLayerFrames_FrameZeroFails_SkipsTheWholeLayer()
    {
        var id0 = Guid.NewGuid();
        var result = ScrollingLayerComponent.ResolveLayerFrames(new[] { id0 }, _ => null);

        Assert.Empty(result);
    }

    [Fact]
    public void ResolveLayerFrames_FrameOneFails_FallsBackToFrameZeroOnly()
    {
        var id0 = Guid.NewGuid();
        var id1 = Guid.NewGuid();
        var frame0 = CreateTexture();

        var result = ScrollingLayerComponent.ResolveLayerFrames(new[] { id0, id1 }, id => id == id0 ? frame0 : null);

        Assert.Single(result);
        Assert.Same(frame0, result[0]);
    }

    [Fact]
    public void ResolveLayerFrames_FrameThreeFails_FallsBackToFrameZeroOnly_NotToThePartialPrefix()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var frame0 = CreateTexture();
        var frame1 = CreateTexture();
        var frame2 = CreateTexture();

        var result = ScrollingLayerComponent.ResolveLayerFrames(ids, id =>
        {
            if (id == ids[0]) return frame0;
            if (id == ids[1]) return frame1;
            if (id == ids[2]) return frame2;
            return null; // frame 3 fails.
        });

        Assert.Single(result); // never [frame0, frame1, frame2] - D-E9-9's "never a partial prefix".
        Assert.Same(frame0, result[0]);
    }

    [Fact]
    public void ResolveLayerFrames_AllFramesSucceed_ReturnsTheFullDenseArray()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var frames = new[] { CreateTexture(), CreateTexture(), CreateTexture() };

        var result = ScrollingLayerComponent.ResolveLayerFrames(ids, id => frames[Array.IndexOf(ids, id)]);

        Assert.Equal(frames, result);
    }

    [Fact]
    public void ResolveLayerFrames_GuidEmpty_NeverCallsTheLoader_ResolvesToANullFrame()
    {
        var called = false;
        Texture2D Loader(Guid id)
        {
            called = true;
            return CreateTexture();
        }

        var result = ScrollingLayerComponent.ResolveLayerFrames(new[] { Guid.Empty }, Loader);

        Assert.False(called);
        Assert.Empty(result); // frame 0 null -> whole layer skipped, same as any other frame-0 failure.
    }

    [Fact]
    public void Submit_AfterAFrameOneLoadFailure_AlwaysSubmitsFrameZero_EvenWhenTheCounterAdvancesPastIt()
    {
        var (component, renderer) = CreateWired();
        component.Service.SetConfiguration(Configuration);
        // AnimTimer 0 with 4 raw ids: cadence still cycles 0,1,2,3,0,... against the RAW definition
        // length even though the resolved frame array is truncated to one element by the load failure
        // below - Submit must clamp the frame index rather than throw or pick the wrong texture.
        component.Service.SetLayers(new[] { MakeOneQuadLayer(frameCount: 4, animTimer: 0) });
        var frameIds = component.Service.GetLayerDefinition(0).FrameTextureAssetIds;
        var frame0 = CreateTexture();
        component.ResolveTextures(id => id == frameIds[0] ? frame0 : null); // frame 1 fails.

        component.Service.SetFrame(0, 0, 2, Vector3.Zero); // counter reaches 2 (raw modulo 4).
        component.Service.Advance();
        Assert.True(component.Service.TryGetLayerState(0, out var state));
        Assert.Equal(2, state.AnimFrameCounter);

        var exception = Record.Exception(() => component.Submit(renderer, component.Service.CameraTarget, Scissor));

        Assert.Null(exception);
        var entry = Assert.Single(GetSpriteDatas(renderer));
        Assert.Same(frame0, (Texture2D)GetField(entry, "Texture"));
    }

    // ---- Explicit counter reset on re-resolve (D-E9b-7, S0 fix item 2) --------------------------------

    [Fact]
    public void ResolveTextures_CalledAgainAfterABareSetTint_ResetsTheLayerCounters()
    {
        // Mirrors Update's own sequence on a LayersVersion change: SetTint alone (no SetLayers) still
        // bumps the version, so Update still calls ResolveTextures again - which must reset the
        // counters explicitly, since SetTint itself does not rebuild the layer array.
        var (component, _) = CreateWired();
        component.Service.SetConfiguration(Configuration);
        component.Service.SetLayers(new[] { MakeOneQuadLayer(frameCount: 1, animTimer: 100) });
        component.ResolveTextures(_ => CreateTexture());

        component.Service.SetFrame(0, 0, 5, Vector3.Zero); // AnimTimer 100 => 5 ticks never rolls the frame.
        component.Service.Advance();
        Assert.True(component.Service.TryGetLayerState(0, out var beforeTint));
        Assert.Equal(5, beforeTint.AnimFrameTimer);

        component.Service.SetTint(MakeTint());
        Assert.True(component.Service.TryGetLayerState(0, out var afterBareTint));
        Assert.Equal(5, afterBareTint.AnimFrameTimer); // SetTint alone must not reset anything.

        component.ResolveTextures(_ => CreateTexture());
        Assert.True(component.Service.TryGetLayerState(0, out var afterResolve));
        Assert.Equal(0, afterResolve.AnimFrameTimer);
    }

    // ---- Helpers ------------------------------------------------------------------------------------------

    private static ScrollingLayerDefinition MakeOneQuadLayer(int factorNum = 0, int frameCount = 1, int animTimer = 100)
    {
        var frames = new Guid[frameCount];
        for (var i = 0; i < frameCount; i++)
        {
            frames[i] = Guid.NewGuid();
        }

        return new ScrollingLayerDefinition
        {
            FrameTextureAssetIds = frames,
            FactorXNum = factorNum,
            FactorXDenom = 1,
            FactorYNum = factorNum,
            FactorYDenom = 1,
            AnimTimer = animTimer,
            Pass = RenderPass2D.Background,
            Blend = SpriteBlendMode.Opaque,
            Tint = Color.White,
        };
    }

    private static ScrollingTintDefinition MakeTint()
    {
        return new ScrollingTintDefinition(new Color(40, 40, 40, 128), new RenderSortKey2D((int)RenderPass2D.Effects, -1, 0, 0, 0, 0, 0));
    }

    private static (ScrollingLayerComponent component, SpriteRendererComponent renderer) CreateWired()
    {
        var game = CreateHeadlessGame();
        var renderer = new SpriteRendererComponent(game);
        var component = new ScrollingLayerComponent(game);
        return (component, renderer);
    }

    private static void InjectWhiteTexture(ScrollingLayerComponent component)
    {
        var field = typeof(ScrollingLayerComponent).GetField("_whiteTexture", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(component, CreateTexture());
    }

    private static CasaEngineGame CreateHeadlessGame()
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var componentsField = typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(componentsField);
        componentsField!.SetValue(game, new GameComponentCollection());
        return game;
    }

    private static Texture2D CreateTexture()
    {
        return (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
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
}
