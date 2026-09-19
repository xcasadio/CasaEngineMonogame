using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScreenEffects;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering.ScreenEffects;

/// <summary>
/// Covers <see cref="ScreenEffectComponent"/>'s <see cref="ScreenEffectLayer.AboveUI"/> registration
/// lifecycle (D2/D3, ai-agent/tasks/screen-effect-above-ui-tasks.md, T1.2):
/// <see cref="ScreenEffectComponent.Update"/> submits directly only while
/// <see cref="ScreenEffectLayer.BelowUI"/> (the pre-existing, byte-for-byte unchanged path); while
/// <see cref="ScreenEffectLayer.AboveUI"/> it submits nothing and instead registers/unregisters the
/// component as an <see cref="IPostUIOverlay"/> on the active view. The overlay's own
/// <see cref="ScreenEffectComponent.Draw"/> (submit-then-flush against a real
/// <see cref="GraphicsDevice"/>) is out of reach of this headless suite - see
/// <c>ScreenEffectComponentSubmissionTests</c>'s own doc for the same constraint - and is covered by
/// the T2.2 smoke instead.
/// </summary>
public class ScreenEffectComponentAboveUIRegistrationTests
{
    [Fact]
    public void Update_WhenLayerIsBelowUI_SubmitsOverlayAndRegistersNoOverlay()
    {
        var game = CreateHeadlessGame();
        var renderer = new SpriteRendererComponent(game);
        SetSpriteRendererComponent(game, renderer);
        var view = CreateView();
        SetGameManagerWithActiveView(game, view);

        var component = new ScreenEffectComponent(game);
        SetPixelTexture(component, CreateTexture());
        component.Service.SetOverlay(1, 2, 3, SpriteBlendMode.Additive);
        // Layer defaults to BelowUI (D1): nothing here changes it.

        component.Update(new GameTime());

        Assert.Single(GetSpriteDatas(renderer));
        Assert.Empty(view.PostUIOverlays);
    }

    [Fact]
    public void Update_WhenLayerIsAboveUI_DoesNotSubmitAndRegistersOnTheActiveView()
    {
        var game = CreateHeadlessGame();
        var renderer = new SpriteRendererComponent(game);
        SetSpriteRendererComponent(game, renderer);
        var view = CreateView();
        SetGameManagerWithActiveView(game, view);

        var component = new ScreenEffectComponent(game);
        component.Service.SetOverlay(1, 2, 3, SpriteBlendMode.Additive);
        component.Service.Layer = ScreenEffectLayer.AboveUI;

        component.Update(new GameTime());

        Assert.Empty(GetSpriteDatas(renderer));
        Assert.Single(view.PostUIOverlays);
        Assert.Same(component, view.PostUIOverlays[0]);
    }

    [Fact]
    public void Update_WhenLayerTogglesBackToBelowUI_UnregistersFromTheView()
    {
        var game = CreateHeadlessGame();
        var renderer = new SpriteRendererComponent(game);
        SetSpriteRendererComponent(game, renderer);
        var view = CreateView();
        SetGameManagerWithActiveView(game, view);

        var component = new ScreenEffectComponent(game);
        SetPixelTexture(component, CreateTexture());
        component.Service.SetOverlay(1, 2, 3, SpriteBlendMode.Additive);
        component.Service.Layer = ScreenEffectLayer.AboveUI;
        component.Update(new GameTime());
        Assert.Single(view.PostUIOverlays);

        component.Service.Layer = ScreenEffectLayer.BelowUI;
        component.Update(new GameTime());

        Assert.Empty(view.PostUIOverlays);
        // BelowUI resumes submitting again once the layer flips back.
        Assert.Single(GetSpriteDatas(renderer));
    }

    [Fact]
    public void Update_WhenClearedWhileAboveUI_UnregistersFromTheView()
    {
        var game = CreateHeadlessGame();
        var renderer = new SpriteRendererComponent(game);
        SetSpriteRendererComponent(game, renderer);
        var view = CreateView();
        SetGameManagerWithActiveView(game, view);

        var component = new ScreenEffectComponent(game);
        component.Service.SetOverlay(1, 2, 3, SpriteBlendMode.Additive);
        component.Service.Layer = ScreenEffectLayer.AboveUI;
        component.Update(new GameTime());
        Assert.Single(view.PostUIOverlays);

        component.Service.Clear();
        component.Update(new GameTime());

        Assert.Empty(view.PostUIOverlays);
    }

    [Fact]
    public void Draw_WithNoSpriteRendererComponent_DoesNothingAndDoesNotThrow()
    {
        // No SpriteRendererComponent wired on the game (mirrors the headless construction used by
        // ScreenEffectComponentSubmissionTests): the device-facing submit-then-flush cannot run
        // without a GraphicsDevice, which this suite has none of, so Draw must bypass cleanly.
        var game = CreateHeadlessGame();
        var view = CreateView();
        var component = new ScreenEffectComponent(game);
        component.Service.SetOverlay(1, 2, 3, SpriteBlendMode.Additive);

        var exception = Record.Exception(() => component.Draw(null, view, in EmptyFrame));

        Assert.Null(exception);
    }

    private static readonly RenderFrame EmptyFrame =
        new(Matrix.Identity, Matrix.Identity, Vector3.Zero, new Rectangle(0, 0, 1, 1));

    private static RenderView CreateView()
    {
        // A sized Camera2dComponent, not ArcBallCameraComponent (a 3d camera): TryGetCameraViewSize
        // must resolve so Update never falls back to CasaEngineGame.ScreenSizeWidth/Height, which
        // reads the (headless-null) Game.Window and would NRE regardless of this task's changes.
        var camera = new Camera2dComponent();
        camera.OnScreenResized(320, 240);
        return new RenderView(new CasaEngine.Framework.Scene.World.World(), camera, new StubRenderSurface());
    }

    private sealed class StubRenderSurface : IRenderSurface
    {
        public bool IsBackBuffer => false;

        public Rectangle ViewportRect => new(0, 0, 1, 1);

        public RenderTarget2D RenderTarget => null;

        public void Apply(GraphicsDevice graphicsDevice)
        {
        }

        public void Restore(GraphicsDevice graphicsDevice)
        {
        }
    }

    private static CasaEngineGame CreateHeadlessGame()
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var componentsField = typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(componentsField);
        componentsField!.SetValue(game, new GameComponentCollection());
        return game;
    }

    private static void SetSpriteRendererComponent(CasaEngineGame game, SpriteRendererComponent renderer)
    {
        var field = typeof(CasaEngineGame).GetField("<SpriteRendererComponent>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(game, renderer);
    }

    private static void SetGameManagerWithActiveView(CasaEngineGame game, RenderView view)
    {
        var gameManager = new GameManager(null);
        gameManager.ViewManager.Add(view);

        var field = typeof(CasaEngineGame).GetField("<GameManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(game, gameManager);
    }

    private static Texture2D CreateTexture()
    {
        return (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
    }

    private static void SetPixelTexture(ScreenEffectComponent component, Texture2D texture)
    {
        // GetOrCreatePixelTexture's lazy-create path needs a real GraphicsDevice, unavailable here
        // (same constraint as ScreenEffectComponentSubmissionTests' explicit overlayTexture param);
        // priming the cache directly lets Update's own (parameterless) SubmitOverlay call reach it.
        var field = typeof(ScreenEffectComponent).GetField("_pixelTexture", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(component, texture);
    }

    private static System.Collections.IList GetSpriteDatas(SpriteRendererComponent component)
    {
        var field = typeof(SpriteRendererComponent).GetField("_spriteDatas", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (System.Collections.IList)field!.GetValue(component)!;
    }
}
