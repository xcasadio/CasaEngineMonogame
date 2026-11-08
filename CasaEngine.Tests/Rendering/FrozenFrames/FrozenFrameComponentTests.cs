using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.FrozenFrames;
using CasaEngine.Framework.Rendering.ScreenEffects;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering.FrozenFrames;

/// <summary>
/// E19.TR-4a, T1a: registration of <see cref="FrozenFrameComponent"/> on the active view and the pure drawing
/// seam (D-TR4A-4, D-TR4A-5, D-TR4A-6, R-TR5-1). Headless, on the pattern of
/// <c>ScreenEffectComponentAboveUIRegistrationTests</c>. The GPU draw itself is proved by the engine demo.
/// </summary>
public class FrozenFrameComponentTests
{
    [Fact]
    public void Update_WhileBaseShouldBeRegistered_RegistersOnTheActiveViewAtTheFrozenOrder()
    {
        var game = CreateHeadlessGame();
        var view = CreateView();
        SetGameManagerWithViews(game, view);
        var earlier = new DummyOverlay();
        view.RegisterPostUIOverlay(earlier); // order 0, registered before the component
        var component = new FrozenFrameComponent(game);
        HoldAnImage(component);
        component.Service.ShowBase();

        component.Update(new GameTime());

        // The frozen order (-100) puts it before the order-0 overlay registered first.
        Assert.Equal(new IPostUIOverlay[] { component, earlier }, view.PostUIOverlays);
    }

    [Theory]
    [InlineData("hidden with an image")]
    [InlineData("visible without image nor request")]
    [InlineData("visible after Release")]
    public void Update_WhenNotRegistrable_RegistersNothing(string state)
    {
        var game = CreateHeadlessGame();
        var view = CreateView();
        SetGameManagerWithViews(game, view);
        var component = new FrozenFrameComponent(game);
        switch (state)
        {
            case "hidden with an image":
                HoldAnImage(component);
                component.Service.HideBase();
                break;
            case "visible without image nor request":
                component.Service.ShowBase();
                break;
            default:
                HoldAnImage(component);
                component.Service.ShowBase();
                component.Service.Release();
                component.Service.ShowBase();
                break;
        }

        component.Update(new GameTime());

        Assert.Empty(view.PostUIOverlays);
    }

    [Fact]
    public void Release_UnregistersOnTheNextUpdate()
    {
        var game = CreateHeadlessGame();
        var view = CreateView();
        SetGameManagerWithViews(game, view);
        var component = new FrozenFrameComponent(game);
        HoldAnImage(component);
        component.Service.ShowBase();
        component.Update(new GameTime());
        Assert.Single(view.PostUIOverlays);

        component.Service.Release();
        component.Update(new GameTime());

        Assert.Empty(view.PostUIOverlays);
    }

    [Fact]
    public void TheRegistrationFollowsTheActiveView()
    {
        var game = CreateHeadlessGame();
        var first = CreateView();
        var second = CreateView();
        SetGameManagerWithViews(game, first, second);
        var component = new FrozenFrameComponent(game);
        HoldAnImage(component);
        component.Service.ShowBase();
        component.Update(new GameTime());
        Assert.Single(first.PostUIOverlays);
        Assert.Empty(second.PostUIOverlays);

        game.GameManager.ViewManager.SetActive(second);
        component.Update(new GameTime());

        Assert.Empty(first.PostUIOverlays);
        Assert.Single(second.PostUIOverlays);
        Assert.Same(component, second.PostUIOverlays[0]);
    }

    [Fact]
    public void Dispose_UnregistersFromTheView()
    {
        var game = CreateHeadlessGame();
        var view = CreateView();
        SetGameManagerWithViews(game, view);
        var component = new FrozenFrameComponent(game);
        HoldAnImage(component);
        component.Service.ShowBase();
        component.Update(new GameTime());
        Assert.Single(view.PostUIOverlays);

        component.Dispose();

        Assert.Empty(view.PostUIOverlays);
    }

    [Theory]
    [InlineData(true)]  // the veil registers first
    [InlineData(false)] // the frozen frame registers first
    public void TheFrozenFrameSitsBelowTheScreenEffect_WhicheverRegisteredFirst(bool veilFirst)
    {
        var game = CreateHeadlessGame();
        var view = CreateView();
        SetGameManagerWithViews(game, view);
        var frozen = new FrozenFrameComponent(game);
        var veil = new ScreenEffectComponent(game);
        veil.Service.SetOverlay(1, 2, 3, SpriteBlendMode.Additive);
        veil.Service.Layer = ScreenEffectLayer.AboveUI;
        HoldAnImage(frozen);
        frozen.Service.ShowBase();

        if (veilFirst)
        {
            veil.Update(new GameTime());
            frozen.Update(new GameTime());
        }
        else
        {
            frozen.Update(new GameTime());
            veil.Update(new GameTime());
        }

        Assert.Equal(new IPostUIOverlay[] { frozen, veil }, view.PostUIOverlays);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RetentionTargetsTheActiveView_AndFollowsTheFlag(bool retain)
    {
        var game = CreateHeadlessGame();
        var view = CreateView();
        SetGameManagerWithViews(game, view);
        var component = new FrozenFrameComponent(game);
        component.Service.RetainLastFrame = retain;

        component.Update(new GameTime());

        Assert.Same(view, component.Retention.TargetView);
        Assert.Equal(retain, component.Retention.Enabled);
    }

    [Theory]
    [InlineData(true, 160, 0, 960, 960, true, 0, 0, 960, 960)]  // source: RenderPipeline.cs:254, the destination is (0, 0, w, h), not offset by the view
    [InlineData(false, 160, 0, 960, 960, false, 0, 0, 0, 0)]    // an image that is not drawable draws nothing, even registered
    [InlineData(true, 0, 0, 0, 0, false, 0, 0, 0, 0)]          // an empty view draws nothing
    public void TheBaseIsDrawnOverTheWholeViewport_AtTheOrigin(
        bool isBaseDrawable, int x, int y, int width, int height,
        bool expectedDrawn, int expectedX, int expectedY, int expectedWidth, int expectedHeight)
    {
        var drawn = FrozenFrameComponent.TryGetBaseDestination(isBaseDrawable, new Rectangle(x, y, width, height), out var destination);

        Assert.Equal(expectedDrawn, drawn);
        if (expectedDrawn)
        {
            Assert.Equal(new Rectangle(expectedX, expectedY, expectedWidth, expectedHeight), destination);
        }
    }

    [Fact]
    public void Update_InTheSameTickAsRequestAndShow_RegistersBeforeTheImageExists()
    {
        var game = CreateHeadlessGame();
        var view = CreateView();
        SetGameManagerWithViews(game, view);
        var earlier = new DummyOverlay();
        view.RegisterPostUIOverlay(earlier);
        var component = new FrozenFrameComponent(game);

        component.Service.RequestCapture();
        component.Service.ShowBase();
        component.Update(new GameTime());

        Assert.False(component.Service.HasImage);
        Assert.Equal(new IPostUIOverlay[] { component, earlier }, view.PostUIOverlays);
    }

    [Fact]
    public void AfterAFailedCapture_WithNoImage_UnregistersOnTheNextUpdate()
    {
        var game = CreateHeadlessGame();
        var view = CreateView();
        SetGameManagerWithViews(game, view);
        var component = new FrozenFrameComponent(game);
        component.Service.RequestCapture();
        component.Service.ShowBase();
        component.Update(new GameTime());
        Assert.Single(view.PostUIOverlays);

        component.Service.FailCapture();
        component.Update(new GameTime());

        Assert.Empty(view.PostUIOverlays);
        Assert.False(component.Service.IsBaseDrawable);
    }

    private static void HoldAnImage(FrozenFrameComponent component)
    {
        component.Service.RequestCapture();
        component.Service.CompleteCapture(320, 240);
    }

    private static RenderView CreateView()
    {
        var camera = new Camera2dComponent();
        camera.OnScreenResized(320, 240);
        return new RenderView(new CasaEngine.Framework.Scene.World.World(), camera, new StubRenderSurface());
    }

    private sealed class DummyOverlay : IPostUIOverlay
    {
        public void Draw(GraphicsDevice graphicsDevice, RenderView view, in RenderFrame frame)
        {
        }
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

    private static void SetGameManagerWithViews(CasaEngineGame game, params RenderView[] views)
    {
        var gameManager = new GameManager(null);
        foreach (var view in views)
        {
            gameManager.ViewManager.Add(view);
        }

        var field = typeof(CasaEngineGame).GetField("<GameManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(game, gameManager);
    }
}
