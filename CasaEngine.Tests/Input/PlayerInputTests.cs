using CasaEngine.Engine.Input;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace CasaEngine.Tests.Input;

public class PlayerInputTests
{
    private static PlayerInput CreatePlayerInput(
        PlayerController playerController,
        out KeyboardManager keyboardManager,
        out MouseManager mouseManager,
        out GamePadManager gamePadManager,
        out InputMappingManager inputMappingManager,
        Func<InputRouter> inputRouterAccessor)
    {
        keyboardManager = new KeyboardManager();
        mouseManager = new MouseManager();
        gamePadManager = new GamePadManager();
        inputMappingManager = new InputMappingManager();

        return new PlayerInput(
            playerController,
            keyboardManager,
            mouseManager,
            gamePadManager,
            inputMappingManager,
            inputRouterAccessor);
    }

    [Fact]
    public void Availability_DisabledPlayer_AllInputUnavailable()
    {
        var playerController = new PlayerController { IsInputEnable = false };
        var playerInput = CreatePlayerInput(
            playerController,
            out var keyboardManager,
            out _,
            out _,
            out _,
            () => null);

        keyboardManager.Update(new KeyboardState(Keys.Space));

        Assert.False(playerInput.IsKeyboardAvailable);
        Assert.False(playerInput.IsKeyPressed(Keys.Space));
        Assert.False(playerInput.IsMouseAvailable);
        Assert.False(playerInput.IsGamePadAvailable);

        var buttonState = playerInput.GetButtonState("anything");
        Assert.False(buttonState.IsKeyPressed);
        Assert.False(buttonState.IsKeyJustPressed);
        Assert.Equal(0, buttonState.Value);
    }

    [Fact]
    public void Availability_NoRouter_KeyboardAvailableAndTracksJustPressed()
    {
        var playerController = new PlayerController { IsInputEnable = true };
        var playerInput = CreatePlayerInput(
            playerController,
            out var keyboardManager,
            out _,
            out _,
            out _,
            () => null);

        Assert.True(playerInput.IsKeyboardAvailable);

        keyboardManager.Update(new KeyboardState(Keys.Space));

        Assert.True(playerInput.IsKeyPressed(Keys.Space));
        Assert.True(playerInput.IsKeyJustPressed(Keys.Space));

        keyboardManager.Update(new KeyboardState(Keys.Space));

        Assert.True(playerInput.IsKeyPressed(Keys.Space));
        Assert.False(playerInput.IsKeyJustPressed(Keys.Space));
    }

    [Fact]
    public void Availability_FallbackContext_KeyboardAndMouseAvailableForUnassignedPlayer()
    {
        var viewManager = new ViewManager();
        var router = new InputRouter(viewManager);
        var source = new StubWindowInputSource();

        router.RegisterFallbackInput(source);
        source.SetSnapshot(new WindowInputSnapshot(1, new KeyboardState(Keys.Space), CreateMouseState(0, 0, 0)));

        Assert.True(router.TryDispatchContext(out var context));
        Assert.True(context.ViewId.IsEmpty);

        var playerController = new PlayerController { IsInputEnable = true };
        var playerInput = CreatePlayerInput(
            playerController,
            out _,
            out _,
            out _,
            out _,
            () => router);

        Assert.True(playerInput.IsKeyboardAvailable);
        Assert.True(playerInput.IsMouseAvailable);
    }

    [Fact]
    public void Availability_ContextRoutedToAnotherView_KeyboardAndMouseUnavailable()
    {
        var viewManager = new ViewManager();
        var router = new InputRouter(viewManager);
        var source = new StubWindowInputSource();

        var viewA = CreateView(viewManager, new Rectangle(0, 0, 100, 100));
        var viewB = CreateView(viewManager, new Rectangle(120, 0, 100, 100));

        router.RegisterViewInput(viewA.Id, source);
        router.RegisterViewInput(viewB.Id, source);
        router.RegisterFallbackInput(source);
        router.AssignPlayer(PlayerIndex.One, viewA.Id);

        source.SetSnapshot(new WindowInputSnapshot(2, new KeyboardState(), CreateMouseState(150, 10, 0)));

        Assert.True(router.TryDispatchContext(out var context));
        Assert.Equal(viewB.Id, context.ViewId);

        var playerController = new PlayerController { IsInputEnable = true };
        var playerInput = CreatePlayerInput(
            playerController,
            out _,
            out _,
            out _,
            out _,
            () => router);

        Assert.False(playerInput.IsKeyboardAvailable);
        Assert.False(playerInput.IsMouseAvailable);
    }

    [Fact]
    public void Availability_KeyboardCapturedByUI_KeyboardUnavailableButMouseAndGamePadAvailable()
    {
        var viewManager = new ViewManager();
        var router = new InputRouter(viewManager);
        var source = new StubWindowInputSource();

        var viewA = CreateView(
            viewManager,
            new Rectangle(0, 0, 100, 100),
            new StubUIViewRuntime(new UIViewInputState(false, false, true, false)));

        router.RegisterViewInput(viewA.Id, source);
        router.RegisterFallbackInput(source);
        router.AssignPlayer(PlayerIndex.One, viewA.Id);

        source.SetSnapshot(new WindowInputSnapshot(3, new KeyboardState(), CreateMouseState(50, 50, 0)));

        Assert.True(router.TryDispatchContext(out var context));
        Assert.Equal(viewA.Id, context.ViewId);

        var playerController = new PlayerController { IsInputEnable = true };
        var playerInput = CreatePlayerInput(
            playerController,
            out _,
            out _,
            out _,
            out _,
            () => router);

        Assert.False(playerInput.IsKeyboardAvailable);
        Assert.True(playerInput.IsMouseAvailable);
        Assert.True(playerInput.IsGamePadAvailable);
    }

    [Fact]
    public void Availability_PointerOverUI_MouseUnavailableButKeyboardAvailable()
    {
        var viewManager = new ViewManager();
        var router = new InputRouter(viewManager);
        var source = new StubWindowInputSource();

        var viewA = CreateView(
            viewManager,
            new Rectangle(0, 0, 100, 100),
            new StubUIViewRuntime(new UIViewInputState(true, false, false, false)));

        router.RegisterViewInput(viewA.Id, source);
        router.RegisterFallbackInput(source);
        router.AssignPlayer(PlayerIndex.One, viewA.Id);

        source.SetSnapshot(new WindowInputSnapshot(4, new KeyboardState(), CreateMouseState(50, 50, 0)));

        Assert.True(router.TryDispatchContext(out var context));
        Assert.Equal(viewA.Id, context.ViewId);

        var playerController = new PlayerController { IsInputEnable = true };
        var playerInput = CreatePlayerInput(
            playerController,
            out _,
            out _,
            out _,
            out _,
            () => router);

        Assert.False(playerInput.IsMouseAvailable);
        Assert.True(playerInput.IsKeyboardAvailable);
        Assert.Equal(Point.Zero, playerInput.MousePosition);
        Assert.False(playerInput.LeftButtonPressed);
    }

    [Fact]
    public void PlayerIndex_ResolvedFromLocalPlayerOrDefaultsToOne()
    {
        var playerControllerWithPlayer = new PlayerController
        {
            Player = new LocalPlayer { ControllerId = PlayerIndex.Two },
        };
        var playerInputWithPlayer = CreatePlayerInput(
            playerControllerWithPlayer,
            out _,
            out _,
            out _,
            out _,
            () => null);

        Assert.Equal(PlayerIndex.Two, playerInputWithPlayer.PlayerIndex);

        var playerControllerNoPlayer = new PlayerController { Player = null };
        var playerInputNoPlayer = CreatePlayerInput(
            playerControllerNoPlayer,
            out _,
            out _,
            out _,
            out _,
            () => null);

        Assert.Equal(PlayerIndex.One, playerInputNoPlayer.PlayerIndex);
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
            Microsoft.Xna.Framework.Input.ButtonState.Released,
            Microsoft.Xna.Framework.Input.ButtonState.Released,
            Microsoft.Xna.Framework.Input.ButtonState.Released,
            Microsoft.Xna.Framework.Input.ButtonState.Released,
            Microsoft.Xna.Framework.Input.ButtonState.Released,
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
