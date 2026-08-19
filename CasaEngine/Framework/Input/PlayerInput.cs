using CasaEngine.Engine.Input;
using CasaEngine.Framework.Gameplay;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.Input;

/// <summary>
/// Per-player input facade for gameplay code. Reads are filtered through three gates:
/// (1) <see cref="PlayerController.IsInputEnable"/>, (2) per-view routing via
/// <see cref="InputRouter"/> (the player's assigned view must be the one currently
/// receiving input), and (3) UI-first arbitration (MGUI capture wins over gameplay).
///
/// V1 limitation: <see cref="GetButtonState"/> treats keyboard and gamepad mappings as a
/// single button; it is only suppressed by keyboard/UI capture when no gamepad is connected
/// for this player, so a mixed keyboard+gamepad mapping may still read the gamepad side while
/// the keyboard side is captured by UI.
/// </summary>
public sealed class PlayerInput
{
    private readonly PlayerController _playerController;
    private readonly KeyboardManager _keyboardManager;
    private readonly MouseManager _mouseManager;
    private readonly GamePadManager _gamePadManager;
    private readonly InputMappingManager _inputMappingManager;
    private readonly Func<InputRouter> _inputRouterAccessor;

    public PlayerInput(PlayerController playerController, InputComponent inputComponent)
        : this(
            playerController,
            RequireInputComponent(inputComponent).KeyboardManager,
            inputComponent.MouseManager,
            inputComponent.GamePadManager,
            inputComponent.InputMappingManager,
            () => inputComponent.InputRouter)
    {
        ArgumentNullException.ThrowIfNull(playerController);
    }

    internal PlayerInput(
        PlayerController playerController,
        KeyboardManager keyboardManager,
        MouseManager mouseManager,
        GamePadManager gamePadManager,
        InputMappingManager inputMappingManager,
        Func<InputRouter> inputRouterAccessor)
    {
        _playerController = playerController;
        _keyboardManager = keyboardManager;
        _mouseManager = mouseManager;
        _gamePadManager = gamePadManager;
        _inputMappingManager = inputMappingManager;
        _inputRouterAccessor = inputRouterAccessor;
    }

    private static InputComponent RequireInputComponent(InputComponent inputComponent)
    {
        ArgumentNullException.ThrowIfNull(inputComponent);
        return inputComponent;
    }

    public PlayerIndex PlayerIndex => _playerController.PlayerIndex;

    public bool IsInputEnabled => _playerController.IsInputEnable;

    /// <summary>
    /// True when this player is allowed to read keyboard input: input is enabled for the
    /// player, no UI has captured the keyboard for the player's view, and the current input
    /// snapshot is either a shared fallback (no per-view routing active) or routed to the
    /// player's own view.
    /// </summary>
    public bool IsKeyboardAvailable
    {
        get
        {
            if (!IsInputEnabled)
            {
                return false;
            }

            var router = _inputRouterAccessor();
            if (router == null)
            {
                return true;
            }

            var view = router.GetRenderViewForPlayer(PlayerIndex);
            if (view != null && router.IsKeyboardCapturedByUI(view))
            {
                return false;
            }

            var contextViewId = router.CurrentInputContext.ViewId;
            if (contextViewId.IsEmpty)
            {
                return true;
            }

            if (view == null)
            {
                return false;
            }

            return contextViewId == view.Id;
        }
    }

    /// <summary>
    /// True when this player is allowed to read mouse input. Same gating as
    /// <see cref="IsKeyboardAvailable"/>, except UI capture is tested with
    /// <see cref="InputRouter.IsMouseHandledByUI"/>.
    /// </summary>
    public bool IsMouseAvailable
    {
        get
        {
            if (!IsInputEnabled)
            {
                return false;
            }

            var router = _inputRouterAccessor();
            if (router == null)
            {
                return true;
            }

            var view = router.GetRenderViewForPlayer(PlayerIndex);
            if (view != null && router.IsMouseHandledByUI(view))
            {
                return false;
            }

            var contextViewId = router.CurrentInputContext.ViewId;
            if (contextViewId.IsEmpty)
            {
                return true;
            }

            if (view == null)
            {
                return false;
            }

            return contextViewId == view.Id;
        }
    }

    public bool IsGamePadAvailable => IsInputEnabled;

    public bool IsKeyPressed(Keys key) => IsKeyboardAvailable && _keyboardManager.IsKeyPressed(key);

    public bool IsKeyJustPressed(Keys key) => IsKeyboardAvailable && _keyboardManager.IsKeyJustPressed(key);

    public bool IsKeyReleased(Keys key) => IsKeyboardAvailable && _keyboardManager.IsKeyReleased(key);

    public Point MousePosition => IsMouseAvailable ? _mouseManager.Position : Point.Zero;

    public bool LeftButtonPressed => IsMouseAvailable && _mouseManager.LeftButtonPressed;

    public bool LeftButtonJustPressed => IsMouseAvailable && _mouseManager.LeftButtonJustPressed;

    public bool RightButtonPressed => IsMouseAvailable && _mouseManager.RightButtonPressed;

    public bool RightButtonJustPressed => IsMouseAvailable && _mouseManager.RightButtonJustPressed;

    public bool IsGamePadConnected => IsGamePadAvailable && _gamePadManager.IsGamePadConnected(PlayerIndex);

    public GamePadState GamePadState => IsGamePadAvailable ? _gamePadManager.GamePadState(PlayerIndex) : new GamePadState();

    public bool IsButtonJustPressed(Buttons button) => IsGamePadAvailable && IsGamePadConnected && _gamePadManager.GetGamePad(PlayerIndex).ButtonJustPressed(button);

    public Vector2 ThumbStickLeft => IsGamePadAvailable ? GamePadState.ThumbSticks.Left : Vector2.Zero;

    public Engine.Input.ButtonState GetButtonState(string name)
    {
        if (!IsInputEnabled || !IsKeyboardAvailable && !IsGamePadConnected)
        {
            return new Engine.Input.ButtonState();
        }

        return _inputMappingManager.GetButtonState(name);
    }
}
