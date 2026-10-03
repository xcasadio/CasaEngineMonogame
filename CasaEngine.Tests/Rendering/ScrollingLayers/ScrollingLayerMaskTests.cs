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
/// <see cref="ScrollingLayerService.SetLayerActive"/> (plan E19.k2, rule K2-R1): a layer is addressed by
/// its <see cref="ScrollingLayerDefinition.StableId"/>, never by its position; an inactive layer is frozen
/// (no per-tick state moves) and is not submitted by <see cref="ScrollingLayerComponent"/>.
/// </summary>
public class ScrollingLayerMaskTests
{
    private static readonly ScrollingLayerConfiguration Configuration = new(640, 480, 320, 240);
    private static readonly Rectangle Scissor = new(0, 0, 320, 240);

    // 4 frames, AnimTimer 1 (counter rolls every second tick), auto-scroll (2, 1) per tick, no parallax.
    private static ScrollingLayerDefinition MakeLayer(int stableId)
    {
        var frames = new Guid[4];
        for (var i = 0; i < frames.Length; i++)
        {
            frames[i] = Guid.NewGuid();
        }

        return new ScrollingLayerDefinition
        {
            StableId = stableId,
            FrameTextureAssetIds = frames,
            AnimTimer = 1,
            FactorXNum = 0,
            FactorXDenom = 1,
            FactorYNum = 0,
            FactorYDenom = 1,
            ScrollXSpeed = 2,
            ScrollYSpeed = 1,
            Pass = RenderPass2D.Background,
            Blend = SpriteBlendMode.Opaque,
            Tint = Color.White,
        };
    }

    private static ScrollingLayerService CreateService(params ScrollingLayerDefinition[] layers)
    {
        var service = new ScrollingLayerService();
        service.SetConfiguration(Configuration);
        service.SetLayers(layers);
        return service;
    }

    private static void Tick(ScrollingLayerService service, int ticks)
    {
        service.SetFrame(0, 0, ticks, Vector3.Zero);
        service.Advance();
    }

    private static ScrollingLayerState StateOf(ScrollingLayerService service, int index)
    {
        Assert.True(service.TryGetLayerState(index, out var state));
        return state;
    }

    private static void AssertState(ScrollingLayerState state, int timer, int counter, int autoX, int autoY, int timerX, int timerY)
    {
        Assert.Equal(timer, state.AnimFrameTimer);
        Assert.Equal(counter, state.AnimFrameCounter);
        Assert.Equal(autoX, state.AutoScrollOffsetX);
        Assert.Equal(autoY, state.AutoScrollOffsetY);
        Assert.Equal(timerX, state.TimerX);
        Assert.Equal(timerY, state.TimerY);
    }

    [Fact]
    public void SetLayerActive_False_FreezesOnlyTheLayerOfThatId_ThenTrueResumesIt()
    {
        var service = CreateService(MakeLayer(0), MakeLayer(1));

        service.SetLayerActive(1, false);
        Assert.True(service.IsLayerActive(0));
        Assert.False(service.IsLayerActive(1));

        Tick(service, 3);

        // Layer 0 ran three ticks: timer 1/counter 1 (rolled once at tick 2), auto-scroll 6/3, timers 3/3.
        AssertState(StateOf(service, 0), timer: 1, counter: 1, autoX: 6, autoY: 3, timerX: 3, timerY: 3);
        // Layer 1 kept everything it had (a fresh layer: all zero), offsets included.
        var frozen = StateOf(service, 1);
        AssertState(frozen, timer: 0, counter: 0, autoX: 0, autoY: 0, timerX: 0, timerY: 0);
        Assert.Equal(0, frozen.LayerOffsetX);
        Assert.Equal(0, frozen.LayerOffsetY);

        service.SetLayerActive(1, true);
        Assert.True(service.IsLayerActive(1));

        Tick(service, 3);

        // Both advance again: layer 0 has run six ticks, layer 1 three.
        AssertState(StateOf(service, 0), timer: 0, counter: 3, autoX: 12, autoY: 6, timerX: 6, timerY: 6);
        AssertState(StateOf(service, 1), timer: 1, counter: 1, autoX: 6, autoY: 3, timerX: 3, timerY: 3);
    }

    [Fact]
    public void SetLayerActive_FreezesAStateThatIsNotTheInitialOne()
    {
        var service = CreateService(MakeLayer(0), MakeLayer(1));

        Tick(service, 3);
        service.SetLayerActive(1, false);
        Tick(service, 3);

        AssertState(StateOf(service, 0), timer: 0, counter: 3, autoX: 12, autoY: 6, timerX: 6, timerY: 6);
        var frozen = StateOf(service, 1);
        AssertState(frozen, timer: 1, counter: 1, autoX: 6, autoY: 3, timerX: 3, timerY: 3);
        Assert.Equal(6, frozen.LayerOffsetX);
        Assert.Equal(3, frozen.LayerOffsetY);
    }

    [Fact]
    public void SetLayerActive_AddressesTheStableId_NotThePosition()
    {
        // One layer, identifier 1, at index 0 (the export may drop a layer 0).
        var service = CreateService(MakeLayer(1));

        service.SetLayerActive(0, false); // absent identifier: no effect.
        Assert.True(service.IsLayerActive(0));
        Tick(service, 1);
        AssertState(StateOf(service, 0), timer: 1, counter: 0, autoX: 2, autoY: 1, timerX: 1, timerY: 1);

        service.SetLayerActive(1, false);
        Assert.False(service.IsLayerActive(0));
        Tick(service, 3);
        AssertState(StateOf(service, 0), timer: 1, counter: 0, autoX: 2, autoY: 1, timerX: 1, timerY: 1);
    }

    [Fact]
    public void SetLayers_AfterAMask_MakesEveryLayerActive()
    {
        var service = CreateService(MakeLayer(0), MakeLayer(1));
        service.SetLayerActive(0, false);
        service.SetLayerActive(1, false);

        service.SetLayers(new[] { MakeLayer(0), MakeLayer(1) });

        Assert.True(service.IsLayerActive(0));
        Assert.True(service.IsLayerActive(1));
        Tick(service, 1);
        AssertState(StateOf(service, 1), timer: 1, counter: 0, autoX: 2, autoY: 1, timerX: 1, timerY: 1);
    }

    [Fact]
    public void Clear_AfterAMask_MakesTheNextLayersActive()
    {
        var service = CreateService(MakeLayer(0));
        service.SetLayerActive(0, false);

        service.Clear();
        service.SetLayers(new[] { MakeLayer(0) });

        Assert.True(service.IsLayerActive(0));
    }

    [Fact]
    public void Submit_InactiveLayer_QueuesNothing_ActiveLayerQueuesItsQuads()
    {
        var (component, renderer) = CreateWired();
        component.Service.SetConfiguration(Configuration);
        component.Service.SetLayers(new[] { MakeLayer(0), MakeLayer(1) });
        component.ResolveTextures(_ => CreateTexture());

        component.Service.SetFrame(0, 0, 1, Vector3.Zero);
        component.Service.Advance();
        component.Service.SetLayerActive(1, false);
        component.Submit(renderer, component.Service.CameraTarget, Scissor);
        var withLayerOneOff = GetSpriteDatas(renderer).Count;

        Assert.True(withLayerOneOff > 0);

        component.Service.SetLayerActive(0, false);
        ClearSpriteDatas(renderer);
        component.Submit(renderer, component.Service.CameraTarget, Scissor);
        Assert.Empty(GetSpriteDatas(renderer));

        component.Service.SetLayerActive(0, true);
        component.Service.SetLayerActive(1, true);
        ClearSpriteDatas(renderer);
        component.Submit(renderer, component.Service.CameraTarget, Scissor);
        Assert.Equal(2 * withLayerOneOff, GetSpriteDatas(renderer).Count);
    }

    // ---- Helpers ------------------------------------------------------------------------------------

    private static (ScrollingLayerComponent component, SpriteRendererComponent renderer) CreateWired()
    {
        var game = CreateHeadlessGame();
        var renderer = new SpriteRendererComponent(game);
        var component = new ScrollingLayerComponent(game);
        return (component, renderer);
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

    private static void ClearSpriteDatas(SpriteRendererComponent component)
    {
        GetSpriteDatas(component).Clear();
    }
}
