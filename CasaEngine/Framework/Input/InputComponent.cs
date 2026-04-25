
using CasaEngine.Engine.Input;
using CasaEngine.Engine.Input.Providers;
using CasaEngine.Framework.Application;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.Input;

public class InputComponent : GameComponent
{
    private IKeyboardStateProvider _keyboardStateProvider;
    private IGamePadStateProvider _gamePadStateProvider;
    private IMouseStateProvider _mouseStateProvider;

    private readonly AxisManager _axisManager = new();
    public InputMappingManager InputMappingManager { get; } = new();

    public MouseManager MouseManager { get; } = new();
    public KeyboardManager KeyboardManager { get; } = new();
    public GamePadManager GamePadManager { get; } = new();
    public ViewInputContext CurrentViewInputContext { get; private set; } = ViewInputContext.Empty;

    /// <summary>
    /// Per-view input router. Set by <see cref="CasaEngineGame"/> after initialization.
    /// Provides player → view mapping and UI-first arbitration helpers.
    /// </summary>
    public InputRouter? InputRouter { get; set; }

    public InputComponent(Game game)
        : base(game)
    {
        if (game == null)
        {
            throw new ArgumentNullException(nameof(game));
        }

        Game.Components.Add(this);

        UpdateOrder = (int)ComponentUpdateOrder.Input;

        SetProviders(new KeyboardStateProvider(), new MouseStateProvider(), new GamePadStateProvider());
    }

    public void SetProviders(IKeyboardStateProvider keyboardStateProvider, IMouseStateProvider mouseStateProvider, IGamePadStateProvider gamePadStateProvider)
    {
        _keyboardStateProvider = keyboardStateProvider;
        _mouseStateProvider = mouseStateProvider;
        _gamePadStateProvider = gamePadStateProvider;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (this)
            {
                Game.RemoveGameComponent<InputComponent>();
            }
        }

        base.Dispose(disposing);
    }

    public override void Update(GameTime gameTime)
    {
        var elapsedTime = GameTimeHelper.ConvertElapsedTimeToSeconds(gameTime);
        KeyboardState keyboardState;
        MouseState mouseState;

        if (InputRouter != null)
        {
            if (InputRouter.TryDispatchContext(out var routedContext))
            {
                CurrentViewInputContext = routedContext;
                keyboardState = routedContext.KeyboardState;
                mouseState = routedContext.MouseState;
            }
            else if (InputRouter.HasAnyRegisteredInputSources)
            {
                CurrentViewInputContext = ViewInputContext.Empty;
                keyboardState = new KeyboardState();
                mouseState = new MouseState();
            }
            else
            {
                CurrentViewInputContext = ViewInputContext.Empty;
                keyboardState = _keyboardStateProvider.GetState();
                mouseState = _mouseStateProvider.GetState();
            }
        }
        else
        {
            CurrentViewInputContext = ViewInputContext.Empty;
            keyboardState = _keyboardStateProvider.GetState();
            mouseState = _mouseStateProvider.GetState();
        }

        GamePadManager.Update(_gamePadStateProvider, elapsedTime);
        KeyboardManager.Update(keyboardState);
        MouseManager.Update(mouseState);

        _axisManager.Update(KeyboardManager, MouseManager, GamePadManager, elapsedTime);
        InputMappingManager.Update(KeyboardManager, MouseManager, GamePadManager);

        base.Update(gameTime);
    }

    public KeyboardState Keyboard => KeyboardManager.State;
    public MouseState MouseState => MouseManager.State;

    public void SetFallbackInputSource(IWindowInputSource windowInputSource)
    {
        if (InputRouter != null)
        {
            InputRouter.RegisterFallbackInput(windowInputSource);
            return;
        }

        if (windowInputSource is not IKeyboardStateProvider keyboardStateProvider
            || windowInputSource is not IMouseStateProvider mouseStateProvider)
        {
            throw new InvalidOperationException("Fallback input source must implement keyboard and mouse state providers.");
        }

        SetProviders(keyboardStateProvider, mouseStateProvider, new GamePadStateProvider());
    }
}