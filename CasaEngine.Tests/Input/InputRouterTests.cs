using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace CasaEngine.Tests.Input;

public class InputRouterTests
{
    [Fact]
    public void TryDispatchContext_ComputesLocalPointerFromSharedWindowSnapshot()
    {
        var viewManager = new ViewManager();
        var router = new InputRouter(viewManager);
        var source = new StubWindowInputSource();

        var view = CreateView(viewManager, new Rectangle(100, 50, 200, 120));

        router.RegisterViewInput(view.Id, source);
        router.RegisterFallbackInput(source);

        source.SetSnapshot(new WindowInputSnapshot(7, new KeyboardState(Keys.Space), CreateMouseState(130, 70, 120)));

        Assert.True(router.TryDispatchContext(out var context));

        Assert.Equal(view.Id, context.ViewId);
        Assert.Equal(7, context.FrameId);
        Assert.Equal(new Rectangle(100, 50, 200, 120), context.ScreenBounds);
        Assert.Equal(new Point(130, 70), context.ScreenPosition);
        Assert.Equal(new Point(30, 20), context.LocalPosition);
        Assert.Equal(30, context.MouseState.X);
        Assert.Equal(20, context.MouseState.Y);
        Assert.Equal(0, context.VerticalWheelDelta);
        Assert.Equal(InputRoutingReason.Pointer, context.RoutingState.Reason);
    }

    [Fact]
    public void TryDispatchContext_PrioritizesUiPointerCaptureOverPointerHoveredView()
    {
        var viewManager = new ViewManager();
        var router = new InputRouter(viewManager);
        var source = new StubWindowInputSource();

        var capturedView = CreateView(viewManager, new Rectangle(0, 0, 100, 100), new StubUIViewRuntime(new UIViewInputState(false, true, false, false)));
        var hoveredView = CreateView(viewManager, new Rectangle(120, 0, 100, 100));

        router.RegisterViewInput(capturedView.Id, source);
        router.RegisterViewInput(hoveredView.Id, source);
        router.RegisterFallbackInput(source);

        source.SetSnapshot(new WindowInputSnapshot(11, new KeyboardState(), CreateMouseState(150, 10, 0)));

        Assert.True(router.TryDispatchContext(out var context));

        Assert.Equal(capturedView.Id, context.ViewId);
        Assert.Equal(InputRoutingReason.UIPointerCapture, context.RoutingState.Reason);
        Assert.Equal(capturedView.Id, context.RoutingState.UIPointerCaptureViewId);
        Assert.Equal(hoveredView.Id, context.RoutingState.PointerViewId);
    }

    [Fact]
    public void TryDispatchContext_PrioritizesEngineCaptureOverUiPointerCapture()
    {
        var viewManager = new ViewManager();
        var router = new InputRouter(viewManager);
        var source = new StubWindowInputSource();

        var uiCapturedView = CreateView(viewManager, new Rectangle(0, 0, 100, 100), new StubUIViewRuntime(new UIViewInputState(false, true, false, false)));
        var engineCapturedView = CreateView(viewManager, new Rectangle(120, 0, 100, 100));

        router.RegisterViewInput(uiCapturedView.Id, source);
        router.RegisterViewInput(engineCapturedView.Id, source);
        router.RegisterFallbackInput(source);
        viewManager.CaptureInput(engineCapturedView);

        source.SetSnapshot(new WindowInputSnapshot(12, new KeyboardState(), CreateMouseState(10, 10, 0)));

        Assert.True(router.TryDispatchContext(out var context));

        Assert.Equal(engineCapturedView.Id, context.ViewId);
        Assert.Equal(InputRoutingReason.Capture, context.RoutingState.Reason);
        Assert.Equal(engineCapturedView.Id, context.RoutingState.CaptureViewId);
        Assert.Equal(uiCapturedView.Id, context.RoutingState.UIPointerCaptureViewId);
    }

    [Fact]
    public void TryDispatchContext_DoesNotAssignKeyboardFocusFromPointerHover()
    {
        var viewManager = new ViewManager();
        var router = new InputRouter(viewManager);
        var source = new StubWindowInputSource();

        var view = CreateView(viewManager, new Rectangle(100, 50, 200, 120));

        router.RegisterViewInput(view.Id, source);
        router.RegisterFallbackInput(source);

        source.SetSnapshot(new WindowInputSnapshot(13, new KeyboardState(Keys.A), CreateMouseState(130, 70, 0)));

        Assert.True(router.TryDispatchContext(out var context));

        Assert.Equal(view.Id, context.ViewId);
        Assert.Equal(InputRoutingReason.Pointer, context.RoutingState.Reason);
        Assert.Equal(ViewId.Empty, router.KeyboardFocusViewId);
        Assert.Equal(ViewId.Empty, context.RoutingState.KeyboardFocusViewId);
    }

    [Fact]
    public void TryDispatchContext_DoesNotOverrideExplicitKeyboardFocusWhenPointerMoves()
    {
        var viewManager = new ViewManager();
        var router = new InputRouter(viewManager);
        var source = new StubWindowInputSource();

        var focusedView = CreateView(viewManager, new Rectangle(0, 0, 100, 100));
        var hoveredView = CreateView(viewManager, new Rectangle(120, 0, 100, 100));

        router.RegisterViewInput(focusedView.Id, source);
        router.RegisterViewInput(hoveredView.Id, source);
        router.RegisterFallbackInput(source);
        router.SetKeyboardFocus(focusedView.Id);

        source.SetSnapshot(new WindowInputSnapshot(14, new KeyboardState(Keys.B), CreateMouseState(150, 10, 0)));

        Assert.True(router.TryDispatchContext(out var context));

        Assert.Equal(hoveredView.Id, context.ViewId);
        Assert.Equal(InputRoutingReason.Pointer, context.RoutingState.Reason);
        Assert.Equal(focusedView.Id, router.KeyboardFocusViewId);
        Assert.Equal(focusedView.Id, context.RoutingState.KeyboardFocusViewId);
    }

    private static RenderView CreateView(ViewManager viewManager, Rectangle screenBounds, IUIViewRuntime? uiView = null)
    {
        var surface = new StubRenderSurface(new Rectangle(0, 0, screenBounds.Width, screenBounds.Height));
        var view = new RenderView(new CasaEngine.Framework.Scene.World.World(), new ArcBallCameraComponent(), surface)
        {
            Host = new StubViewHost(screenBounds),
            UIView = uiView,
        };

        viewManager.Add(view);
        return view;
    }

    private static MouseState CreateMouseState(int x, int y, int wheelValue)
    {
        return new MouseState(
            x,
            y,
            wheelValue,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            0);
    }

    private sealed class StubWindowInputSource : IWindowInputSource
    {
        private WindowInputSnapshot _snapshot = WindowInputSnapshot.Empty;

        public WindowInputSnapshot GetSnapshot() => _snapshot;

        public void SetSnapshot(WindowInputSnapshot snapshot)
        {
            _snapshot = snapshot;
        }
    }

    private sealed class StubRenderSurface : IRenderSurface
    {
        public StubRenderSurface(Rectangle viewportRect)
        {
            ViewportRect = viewportRect;
        }

        public bool IsBackBuffer => false;

        public Rectangle ViewportRect { get; }

        public RenderTarget2D? RenderTarget => null;

        public void Apply(GraphicsDevice graphicsDevice)
        {
        }

        public void Restore(GraphicsDevice graphicsDevice)
        {
        }
    }

    private sealed class StubViewHost : IViewHost, IViewScreenBoundsHost
    {
        public StubViewHost(Rectangle screenBounds)
        {
            ScreenBounds = screenBounds;
        }

        public ViewId ViewId => ViewId.Empty;

        public int Width => ScreenBounds.Width;

        public int Height => ScreenBounds.Height;

        public bool IsVisible => true;

        public Rectangle ScreenBounds { get; }

        public event Action<IViewHost, int, int>? Resized;

        public event Action<IViewHost>? Closed;

        public void NotifyResized(int newWidth, int newHeight)
        {
            Resized?.Invoke(this, newWidth, newHeight);
        }

        public void Dispose()
        {
            Closed?.Invoke(this);
        }
    }

    private sealed class StubUIViewRuntime : IUIViewRuntime
    {
        public StubUIViewRuntime(UIViewInputState inputState)
        {
            InputState = inputState;
        }

        public bool IsPointerOverUI => InputState.IsPointerOverUI;

        public bool IsPointerCaptured => InputState.IsPointerCaptured;

        public bool IsKeyboardCaptured => InputState.IsKeyboardCaptured;

        public UIViewInputState InputState { get; }

        public bool HasModalInput => InputState.HasModalInput;

        public UIViewMetrics Metrics { get; private set; } = new(new Point(1, 1), new Point(1, 1), 1.0f, Rectangle.Empty);

        public void Update(GameTime gameTime)
        {
        }

        public void Draw()
        {
        }

        public void UpdateMetrics(UIViewMetrics metrics)
        {
            Metrics = metrics;
        }

        public void PushScreen(IUIScreen screen)
        {
        }

        public IUIScreen? PopScreen() => null;

        public void RemoveScreen(IUIScreen screen)
        {
        }

        public void Dispose()
        {
        }
    }
}