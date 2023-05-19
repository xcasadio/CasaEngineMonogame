using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using CasaEngine.Framework.Game;
using CasaEngine.Engine.Input.InputSequence;

namespace CasaEngine.Engine.Input;

public class InputComponent : GameComponent
{
    private MouseState _mouseState, _mouseStateLastFrame;
    private int _mouseWheelDelta;

    private KeyboardState _keyboardPreviousState = Microsoft.Xna.Framework.Input.Keyboard.GetState();
    private KeyboardState _keyboardState;

    //private List<Keys> keysPressedLastFrame = new List<Keys>();

    private readonly GamePadState[] _gamePadState = new GamePadState[4];
    private readonly GamePadState[] _gamePadStateLastFrame = new GamePadState[4];
    private readonly GamePadDeadZone[] _gamePadDeadZoneMode = new GamePadDeadZone[4];
    private readonly GamePadCapabilities[] _gamePadCapabilities = new GamePadCapabilities[4];

    //private PlayerIndex playerIndex = PlayerIndex.One;

    private Point _startDraggingPos;

    public readonly float Deadzone = 0.2f;

    //InputConfigurations _InputConfigurations = new InputConfigurations();
    private ButtonConfiguration _buttonConfiguration;
    private InputSequence.InputManager.KeyState[] _keysState;

    private readonly InputSequence.InputManager[] _inputManager;

    public InputSequence.InputManager.KeyState[] KeysState => _keysState;

    public Point MousePos => new(_mouseState.X, _mouseState.Y);

    private float _mouseXMovement, _mouseYMovement;
    private float _lastMouseXMovement, _lastMouseYMovement;

    public float MouseXMovement => _mouseXMovement;

    public float MouseYMovement => _mouseYMovement;

    public bool HasMouseMoved
    {
        get
        {
            //TODO: Introduce a mouse movement threshold constant
            if (MouseXMovement > 1 || MouseYMovement > 1)
            {
                return true;
            }

            return false;
        }
    }

    public bool MouseLeftButtonPressed => _mouseState.LeftButton == ButtonState.Pressed;
    public bool MouseRightButtonPressed => _mouseState.RightButton == ButtonState.Pressed;
    public bool MouseMiddleButtonPressed => _mouseState.MiddleButton == ButtonState.Pressed;
    public bool MouseLeftButtonJustPressed => _mouseState.LeftButton == ButtonState.Pressed && _mouseStateLastFrame.LeftButton == ButtonState.Released;
    public bool MouseRightButtonJustPressed => _mouseState.RightButton == ButtonState.Pressed && _mouseStateLastFrame.RightButton == ButtonState.Released;
    public Point MouseDraggingAmount => new(_startDraggingPos.X - MousePos.X, _startDraggingPos.Y - MousePos.Y);

    public void ResetMouseDraggingAmount()
    {
        _startDraggingPos = MousePos;
    }

    public int MouseWheelDelta => _mouseWheelDelta;

    public bool MouseInBox(Rectangle rect)
    {
        var ret = _mouseState.X >= rect.X && _mouseState.Y >= rect.Y && _mouseState.X < rect.Right && _mouseState.Y < rect.Bottom;
        var lastRet = _mouseStateLastFrame.X >= rect.X && _mouseStateLastFrame.Y >= rect.Y
                                                       && _mouseStateLastFrame.X < rect.Right && _mouseStateLastFrame.Y < rect.Bottom;

        return ret;
    }

    public bool MouseInBoxRelative(Rectangle rect)
    {
        /*float widthFactor = BaseGame.Width / 1024.0f;
        float heightFactor = BaseGame.Height / 640.0f;
        return MouseInBox(new Rectangle(
            (int)System.Math.Round(rect.X * widthFactor),
            (int)System.Math.Round(rect.Y * heightFactor),
            (int)System.Math.Round(rect.Right * widthFactor),
            (int)System.Math.Round(rect.Bottom * heightFactor)));*/
        return false;
    }

    public KeyboardState Keyboard => _keyboardState;
    public MouseState MouseState => _mouseState;

    public void ResetKeyboard()
    {
        //keyboardState = Keyboard.GetState();
        //keysPressedLastFrame.Clear();
    }

    public bool IsSpecialKey(Keys key)
    {
        // All keys except A-Z, 0-9 and `-\[];',./= (and space) are special keys.
        // With shift pressed this also results in this keys:
        // ~_|{}:"<>? !@#$%^&*().
        var keyNum = (int)key;
        return (keyNum < (int)Keys.A || keyNum > (int)Keys.Z) &&
               (keyNum < (int)Keys.D0 || keyNum > (int)Keys.D9) &&
               key != Keys.Space && // well, space ^^
               key != Keys.OemTilde && // `~
               key != Keys.OemMinus && // -_
               key != Keys.OemPipe && // \|
               key != Keys.OemOpenBrackets && // [{
               key != Keys.OemCloseBrackets && // ]}
               key != Keys.OemQuotes && // '"
               key != Keys.OemQuestion && // /?
               key != Keys.OemPlus; // =+
    }

    public char KeyToChar(Keys key, bool shiftPressed)
    {
        // If key will not be found, just return space
        var ret = ' ';
        var keyNum = (int)key;
        ret = keyNum switch
        {
            >= (int)Keys.A and <= (int)Keys.Z => shiftPressed ? key.ToString()[0] : key.ToString().ToLower()[0],
            >= (int)Keys.D0 and <= (int)Keys.D9 when shiftPressed == false => (char)('0' + (keyNum - Keys.D0)),
            _ => key switch
            {
                Keys.D1 when shiftPressed => '!',
                Keys.D2 when shiftPressed => '@',
                Keys.D3 when shiftPressed => '#',
                Keys.D4 when shiftPressed => '$',
                Keys.D5 when shiftPressed => '%',
                Keys.D6 when shiftPressed => '^',
                Keys.D7 when shiftPressed => '&',
                Keys.D8 when shiftPressed => '*',
                Keys.D9 when shiftPressed => '(',
                Keys.D0 when shiftPressed => ')',
                Keys.OemTilde => shiftPressed ? '~' : '`',
                Keys.OemMinus => shiftPressed ? '_' : '-',
                Keys.OemPipe => shiftPressed ? '|' : '\\',
                Keys.OemOpenBrackets => shiftPressed ? '{' : '[',
                Keys.OemCloseBrackets => shiftPressed ? '}' : ']',
                Keys.OemSemicolon => shiftPressed ? ':' : ';',
                Keys.OemQuotes => shiftPressed ? '"' : '\'',
                Keys.OemComma => shiftPressed ? '<' : '.',
                Keys.OemPeriod => shiftPressed ? '>' : ',',
                Keys.OemQuestion => shiftPressed ? '?' : '/',
                Keys.OemPlus => shiftPressed ? '+' : '=',
                _ => ret
            }
        };

        return ret;
    }

    /*public void HandleKeyboardInput(ref string inputText)
    {
        // Is a shift key pressed (we have to check both, left and right)
        bool isShiftPressed =
            keyboardState.IsKeyDown(Keys.LeftShift) ||
            keyboardState.IsKeyDown(Keys.RightShift);

        // Go through all pressed keys
        foreach (Keys pressedKey in keyboardState.GetPressedKeys())
            // Only process if it was not pressed last frame
            if (keysPressedLastFrame.Contains(pressedKey) == false)
            {
                // No special key?
                if (IsSpecialKey(pressedKey) == false &&
                    // Max. allow 32 chars
                    inputText.Length < 32)
                {
                    // Then add the letter to our inputText.
                    // Check also the shift state!
                    inputText += KeyToChar(pressedKey, isShiftPressed);
                }
                else if (pressedKey == Keys.Back &&
                    inputText.Length > 0)
                {
                    // IsRemoved 1 character at end
                    inputText = inputText.Substring(0, inputText.Length - 1);
                }
            }
    }*/

    public bool IsKeyJustPressed(Keys key)
    {
        return _keyboardState.IsKeyDown(key)
               && _keyboardPreviousState.IsKeyDown(key) == false;
    }

    public bool IsKeyPressed(Keys key)
    {
        return _keyboardState.IsKeyDown(key);
    }

    public bool IsKeyReleased(Keys key)
    {
        return _keyboardState.IsKeyUp(key)
               && _keyboardPreviousState.IsKeyDown(key);
    }

    public bool IsKeyHeld(Keys key)
    {
        return _keyboardState.IsKeyDown(key)
               && _keyboardPreviousState.IsKeyDown(key);
    }

    public GamePadState GamePadState(PlayerIndex index)
    {
        return _gamePadState[(int)index];
    }

    public GamePadCapabilities GamePadCapabilities(PlayerIndex index)
    {
        _gamePadCapabilities[(int)index] = Microsoft.Xna.Framework.Input.GamePad.GetCapabilities(index);
        return _gamePadCapabilities[(int)index];
    }

    public void GamePadDeadZoneMode(PlayerIndex index, GamePadDeadZone mode)
    {
        _gamePadDeadZoneMode[(int)index] = mode;
    }

    public bool IsGamePadConnected(PlayerIndex index)
    {
        return _gamePadState[(int)index].IsConnected;
    }

    public bool IsButtonJustPressed(PlayerIndex index, Buttons button)
    {
        switch (button)
        {
            case Buttons.A:
                return _gamePadState[(int)index].Buttons.A == ButtonState.Pressed &&
                       _gamePadStateLastFrame[(int)index].Buttons.A == ButtonState.Released;

            case Buttons.B:
                return _gamePadState[(int)index].Buttons.B == ButtonState.Pressed &&
                       _gamePadStateLastFrame[(int)index].Buttons.B == ButtonState.Released;

            case Buttons.X:
                return _gamePadState[(int)index].Buttons.X == ButtonState.Pressed &&
                       _gamePadStateLastFrame[(int)index].Buttons.X == ButtonState.Released;

            case Buttons.Y:
                return _gamePadState[(int)index].Buttons.Y == ButtonState.Pressed &&
                       _gamePadStateLastFrame[(int)index].Buttons.Y == ButtonState.Released;

            case Buttons.Back:
                return _gamePadState[(int)index].Buttons.Back == ButtonState.Pressed &&
                       _gamePadStateLastFrame[(int)index].Buttons.Back == ButtonState.Released;

            case Buttons.Start:
                return _gamePadState[(int)index].Buttons.Start == ButtonState.Pressed &&
                       _gamePadStateLastFrame[(int)index].Buttons.Start == ButtonState.Released;

            case Buttons.LeftShoulder:
                return _gamePadState[(int)index].Buttons.LeftShoulder == ButtonState.Pressed &&
                       _gamePadStateLastFrame[(int)index].Buttons.LeftShoulder == ButtonState.Released;

            case Buttons.RightShoulder:
                return _gamePadState[(int)index].Buttons.RightShoulder == ButtonState.Pressed &&
                       _gamePadStateLastFrame[(int)index].Buttons.RightShoulder == ButtonState.Released;

            case Buttons.LeftStick:
                return _gamePadState[(int)index].Buttons.LeftStick == ButtonState.Pressed &&
                       _gamePadStateLastFrame[(int)index].Buttons.LeftStick == ButtonState.Released;

            case Buttons.RightStick:
                return _gamePadState[(int)index].Buttons.RightStick == ButtonState.Pressed &&
                       _gamePadStateLastFrame[(int)index].Buttons.RightStick == ButtonState.Released;

            case Buttons.LeftTrigger:
                return _gamePadState[(int)index].Triggers.Left > 0.75f &&
                       _gamePadStateLastFrame[(int)index].Triggers.Left == 0.0f;

            case Buttons.RightTrigger:
                return _gamePadState[(int)index].Triggers.Right > 0.75f &&
                       _gamePadStateLastFrame[(int)index].Triggers.Right == 0.0f;
        }

        throw new Exception("Unknown button : " + button);
    }

    public bool GamePadLeftJustPressed(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Left == ButtonState.Pressed &&
               _gamePadStateLastFrame[(int)index].DPad.Left == ButtonState.Released ||
               _gamePadState[(int)index].ThumbSticks.Left.X < -0.75f &&
               _gamePadStateLastFrame[(int)index].ThumbSticks.Left.X > -0.75f;
    }

    public bool GamePadRightJustPressed(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Right == ButtonState.Pressed &&
               _gamePadStateLastFrame[(int)index].DPad.Right == ButtonState.Released ||
               _gamePadState[(int)index].ThumbSticks.Left.X > 0.75f &&
               _gamePadStateLastFrame[(int)index].ThumbSticks.Left.X < 0.75f;
    }

    public bool GamePadUpJustPressed(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Up == ButtonState.Pressed &&
               _gamePadStateLastFrame[(int)index].DPad.Up == ButtonState.Released ||
               _gamePadState[(int)index].ThumbSticks.Left.Y > 0.75f &&
               _gamePadStateLastFrame[(int)index].ThumbSticks.Left.Y < 0.75f;
    }

    public bool GamePadDownJustPressed(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Down == ButtonState.Pressed &&
               _gamePadStateLastFrame[(int)index].DPad.Down == ButtonState.Released ||
               _gamePadState[(int)index].ThumbSticks.Left.Y < -0.75f &&
               _gamePadStateLastFrame[(int)index].ThumbSticks.Left.Y > -0.75f;
    }

    public bool GamePadDPadUpPressed(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Up == ButtonState.Pressed;
    }

    public bool GamePadDPadDownJustPressed(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Down == ButtonState.Pressed &&
               _gamePadStateLastFrame[(int)index].DPad.Down == ButtonState.Released;
    }

    public bool GamePadDPadRightJustPressed(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Right == ButtonState.Pressed &&
               _gamePadStateLastFrame[(int)index].DPad.Right == ButtonState.Released;
    }

    public bool GamePadDPadLeftJustPressed(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Left == ButtonState.Pressed &&
               _gamePadStateLastFrame[(int)index].DPad.Left == ButtonState.Released;
    }

    public bool GamePadDPadUpJustPressed(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Up == ButtonState.Pressed &&
               _gamePadStateLastFrame[(int)index].DPad.Up == ButtonState.Released;
    }

    public bool IsButtonPressed(PlayerIndex index, Buttons button)
    {
        return _gamePadState[(int)index].IsButtonDown(button);
    }

    public bool GamePadLeftPressed(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Left == ButtonState.Pressed ||
               _gamePadState[(int)index].ThumbSticks.Left.X <= -0.75f;
    }

    public bool GamePadRightPressed(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Right == ButtonState.Pressed ||
               _gamePadState[(int)index].ThumbSticks.Left.X >= 0.75f;
    }

    public bool GamePadUpPressed(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Up == ButtonState.Pressed ||
               _gamePadState[(int)index].ThumbSticks.Left.Y >= 0.75f;
    }

    public bool GamePadDownPressed(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Down == ButtonState.Pressed ||
               _gamePadState[(int)index].ThumbSticks.Left.Y <= -0.75f;
    }

    public bool IsButtonJustReleased(PlayerIndex index, Buttons button)
    {
        switch (button)
        {
            case Buttons.A:
                return _gamePadState[(int)index].Buttons.A == ButtonState.Released &&
                       _gamePadStateLastFrame[(int)index].Buttons.A == ButtonState.Pressed;

            case Buttons.B:
                return _gamePadState[(int)index].Buttons.B == ButtonState.Released &&
                       _gamePadStateLastFrame[(int)index].Buttons.B == ButtonState.Pressed;

            case Buttons.X:
                return _gamePadState[(int)index].Buttons.X == ButtonState.Released &&
                       _gamePadStateLastFrame[(int)index].Buttons.X == ButtonState.Pressed;

            case Buttons.Y:
                return _gamePadState[(int)index].Buttons.Y == ButtonState.Released &&
                       _gamePadStateLastFrame[(int)index].Buttons.Y == ButtonState.Pressed;

            case Buttons.Back:
                return _gamePadState[(int)index].Buttons.Back == ButtonState.Released &&
                       _gamePadStateLastFrame[(int)index].Buttons.Back == ButtonState.Pressed;

            case Buttons.Start:
                return _gamePadState[(int)index].Buttons.Start == ButtonState.Released &&
                       _gamePadStateLastFrame[(int)index].Buttons.Start == ButtonState.Pressed;

            case Buttons.LeftShoulder:
                return _gamePadState[(int)index].Buttons.LeftShoulder == ButtonState.Released &&
                       _gamePadStateLastFrame[(int)index].Buttons.LeftShoulder == ButtonState.Pressed;

            case Buttons.RightShoulder:
                return _gamePadState[(int)index].Buttons.RightShoulder == ButtonState.Released &&
                       _gamePadStateLastFrame[(int)index].Buttons.RightShoulder == ButtonState.Pressed;

            case Buttons.LeftStick:
                return _gamePadState[(int)index].Buttons.LeftStick == ButtonState.Released &&
                       _gamePadStateLastFrame[(int)index].Buttons.LeftStick == ButtonState.Pressed;

            case Buttons.RightStick:
                return _gamePadState[(int)index].Buttons.RightStick == ButtonState.Released &&
                       _gamePadStateLastFrame[(int)index].Buttons.RightStick == ButtonState.Pressed;

            case Buttons.LeftTrigger:
                return _gamePadStateLastFrame[(int)index].Triggers.Left > 0.75f &&
                       _gamePadState[(int)index].Triggers.Left == 0.0f; // 0.1f ??

            case Buttons.RightTrigger:
                return _gamePadStateLastFrame[(int)index].Triggers.Right > 0.75f &&
                       _gamePadState[(int)index].Triggers.Right == 0.0f; // 0.1f ??
        }

        throw new Exception("Buttons not supported : " + button);
    }

    public bool GamePadLeftReleased(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Left == ButtonState.Pressed &&
               _gamePadStateLastFrame[(int)index].DPad.Left == ButtonState.Released ||
               _gamePadState[(int)index].ThumbSticks.Left.X <= -0.75f &&
               _gamePadStateLastFrame[(int)index].ThumbSticks.Left.X >= -0.75f;
    }

    public bool GamePadRightReleased(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Right == ButtonState.Pressed &&
               _gamePadStateLastFrame[(int)index].DPad.Right == ButtonState.Released ||
               _gamePadState[(int)index].ThumbSticks.Left.X >= 0.75f &&
               _gamePadStateLastFrame[(int)index].ThumbSticks.Left.X <= 0.75f;
    }

    public bool GamePadUpReleased(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Up == ButtonState.Pressed &&
               _gamePadStateLastFrame[(int)index].DPad.Up == ButtonState.Released ||
               _gamePadState[(int)index].ThumbSticks.Left.Y >= 0.75f &&
               _gamePadStateLastFrame[(int)index].ThumbSticks.Left.Y <= 0.75f;
    }

    public bool GamePadDownReleased(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Down == ButtonState.Pressed &&
               _gamePadStateLastFrame[(int)index].DPad.Down == ButtonState.Released ||
               _gamePadState[(int)index].ThumbSticks.Left.Y <= -0.75f &&
               _gamePadStateLastFrame[(int)index].ThumbSticks.Left.Y >= -0.75f;
    }

    public bool GamePadDPadUpReleased(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Up == ButtonState.Pressed;
    }

    public bool GamePadDPadDownReleased(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Down == ButtonState.Released &&
               _gamePadStateLastFrame[(int)index].DPad.Down == ButtonState.Pressed;
    }

    public bool GamePadDPadRightReleased(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Right == ButtonState.Released &&
               _gamePadStateLastFrame[(int)index].DPad.Right == ButtonState.Pressed;
    }

    public bool GamePadDPadLeftReleased(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Left == ButtonState.Released &&
               _gamePadStateLastFrame[(int)index].DPad.Left == ButtonState.Pressed;
    }

    public bool IsButtonJustHeld(PlayerIndex index, Buttons button)
    {
        switch (button)
        {
            case Buttons.A:
                return _gamePadState[(int)index].Buttons.A == ButtonState.Pressed &&
                       _gamePadStateLastFrame[(int)index].Buttons.A == ButtonState.Pressed;

            case Buttons.B:
                return _gamePadState[(int)index].Buttons.B == ButtonState.Pressed &&
                       _gamePadStateLastFrame[(int)index].Buttons.B == ButtonState.Pressed;

            case Buttons.X:
                return _gamePadState[(int)index].Buttons.X == ButtonState.Pressed &&
                       _gamePadStateLastFrame[(int)index].Buttons.X == ButtonState.Pressed;

            case Buttons.Y:
                return _gamePadState[(int)index].Buttons.Y == ButtonState.Pressed &&
                       _gamePadStateLastFrame[(int)index].Buttons.Y == ButtonState.Pressed;

            case Buttons.Back:
                return _gamePadState[(int)index].Buttons.Back == ButtonState.Pressed &&
                       _gamePadStateLastFrame[(int)index].Buttons.Back == ButtonState.Pressed;

            case Buttons.Start:
                return _gamePadState[(int)index].Buttons.Start == ButtonState.Pressed &&
                       _gamePadStateLastFrame[(int)index].Buttons.Start == ButtonState.Pressed;

            case Buttons.LeftShoulder:
                return _gamePadState[(int)index].Buttons.LeftShoulder == ButtonState.Pressed &&
                       _gamePadStateLastFrame[(int)index].Buttons.LeftShoulder == ButtonState.Pressed;

            case Buttons.RightShoulder:
                return _gamePadState[(int)index].Buttons.RightShoulder == ButtonState.Pressed &&
                       _gamePadStateLastFrame[(int)index].Buttons.RightShoulder == ButtonState.Pressed;

            case Buttons.LeftStick:
                return _gamePadState[(int)index].Buttons.LeftStick == ButtonState.Pressed &&
                       _gamePadStateLastFrame[(int)index].Buttons.LeftStick == ButtonState.Pressed;

            case Buttons.RightStick:
                return _gamePadState[(int)index].Buttons.RightStick == ButtonState.Pressed &&
                       _gamePadStateLastFrame[(int)index].Buttons.RightStick == ButtonState.Pressed;

            case Buttons.LeftTrigger:
                return _gamePadState[(int)index].Triggers.Left >= 0.75f &&
                       _gamePadStateLastFrame[(int)index].Triggers.Left >= 0.75f;

            case Buttons.RightTrigger:
                return _gamePadState[(int)index].Triggers.Right >= 0.75f &&
                       _gamePadStateLastFrame[(int)index].Triggers.Right >= 0.75f;
        }

        throw new Exception("Button not supported : " + button);
    }

    public bool GamePadLeftHeld(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Left == ButtonState.Pressed &&
               _gamePadStateLastFrame[(int)index].DPad.Left == ButtonState.Pressed ||
               _gamePadState[(int)index].ThumbSticks.Left.X >= -0.75f &&
               _gamePadStateLastFrame[(int)index].ThumbSticks.Left.X >= -0.75f;
    }

    public bool GamePadRightHeld(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Right == ButtonState.Pressed &&
               _gamePadStateLastFrame[(int)index].DPad.Right == ButtonState.Pressed ||
               _gamePadState[(int)index].ThumbSticks.Left.X >= 0.75f &&
               _gamePadStateLastFrame[(int)index].ThumbSticks.Left.X >= 0.75f;
    }

    public bool GamePadUpHeld(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Up == ButtonState.Pressed &&
               _gamePadStateLastFrame[(int)index].DPad.Up == ButtonState.Pressed ||
               _gamePadState[(int)index].ThumbSticks.Left.Y >= 0.75f &&
               _gamePadStateLastFrame[(int)index].ThumbSticks.Left.Y >= 0.75f;
    }

    public bool GamePadDownHeld(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Down == ButtonState.Pressed &&
               _gamePadStateLastFrame[(int)index].DPad.Down == ButtonState.Pressed ||
               _gamePadState[(int)index].ThumbSticks.Left.Y >= -0.75f &&
               _gamePadStateLastFrame[(int)index].ThumbSticks.Left.Y >= -0.75f;
    }

    public bool GamePadDPadDownHeld(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Down == ButtonState.Pressed &&
               _gamePadStateLastFrame[(int)index].DPad.Down == ButtonState.Pressed;
    }

    public bool GamePadDPadRightHeld(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Right == ButtonState.Pressed &&
               _gamePadStateLastFrame[(int)index].DPad.Right == ButtonState.Pressed;
    }

    public bool GamePadDPadLeftHeld(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Left == ButtonState.Pressed &&
               _gamePadStateLastFrame[(int)index].DPad.Left == ButtonState.Pressed;
    }

    public bool GamePadDPadUpHeld(PlayerIndex index)
    {
        return _gamePadState[(int)index].DPad.Up == ButtonState.Pressed &&
               _gamePadStateLastFrame[(int)index].DPad.Up == ButtonState.Pressed;
    }

    public void SetCurrentConfiguration(ButtonConfiguration buttonConfiguration)
    {
        _buttonConfiguration = buttonConfiguration;
        _keysState = new InputSequence.InputManager.KeyState[_buttonConfiguration.ButtonCount];
    }

    /*public bool InputConfigButtonJustPressed(PlayerIndex index_, int code_)
    {
        Buttons but = _InputConfigurations.GetConfig(_CurrentInputConfigurationName).GetButton(code_);

        bool res = gamePadState[(int)index_].IsButtonDown(but) &&
            gamePadStateLastFrame[(int)index_].IsButtonUp(but);

        res |= keyboardState.IsKeyDown(Keys.Down) &&
                keysPressedLastFrame.Contains(Keys.Down) == false;

        return res;
    }*/

    public InputComponent(Game game)
        : base(game)
    {
        if (game == null)
        {
            throw new ArgumentNullException(nameof(game));
        }

        Game.Components.Add(this);

        UpdateOrder = (int)ComponentUpdateOrder.Input;

        _inputManager = new InputSequence.InputManager[4];

        for (var i = 0; i < _inputManager.Length; i++)
        {
            _inputManager[i] = new InputSequence.InputManager();
        }

        //TODO : add default config
        _buttonConfiguration = new ButtonConfiguration();
        var map = new ButtonMapper();
        map.Buttons = Buttons.A;
        map.Key = Keys.Enter;
        map.Name = "A";
        _buttonConfiguration.AddButton(1, map);

        SetCurrentConfiguration(_buttonConfiguration);
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
#if !EDITOR
        // Handle mouse input variables
        _mouseStateLastFrame = _mouseState;
        _mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();

        //keysPressedLastFrame = new List<Keys>(keyboardState.GetPressedKeys());
        _keyboardPreviousState = _keyboardState;
        _keyboardState = Microsoft.Xna.Framework.Input.Keyboard.GetState();

#else
        _keyboardPreviousState = _keyboardState;
        _keyboardState = _keyboardStateProvider.GetState();
        /////
        _mouseStateLastFrame = _mouseState;
        _mouseState = _mouseStateProvider.GetState();
#endif

        // Update mouseXMovement and mouseYMovement
        _lastMouseXMovement += _mouseState.X - _mouseStateLastFrame.X;
        _lastMouseYMovement += _mouseState.Y - _mouseStateLastFrame.Y;

        if (Math.Abs(_lastMouseXMovement) < 1.0f)
        {
            _lastMouseXMovement = 0.0f;
            _mouseXMovement = 0.0f;
        }
        else
        {
            _mouseXMovement = _lastMouseXMovement / 2.0f;
            _lastMouseXMovement -= _lastMouseXMovement / 2.0f;
        }

        if (Math.Abs(_lastMouseYMovement) < 1.0f)
        {
            _lastMouseYMovement = 0.0f;
            _mouseYMovement = 0.0f;
        }
        else
        {
            _mouseYMovement = _lastMouseYMovement / 2.0f;
            _lastMouseYMovement -= _lastMouseYMovement / 2.0f;
        }

        if (MouseLeftButtonPressed == false)
        {
            _startDraggingPos = MousePos;
        }

        _mouseWheelDelta = _mouseState.ScrollWheelValue; // 120

        // If we are in the game and don't show the mouse cursor anyway,
        // reset it to the center to allow moving it around.
        //TODO
        /*if (RacingGameManager.InMenu == false &&
            // App must be active
            RacingGameManager.IsAppActive)
        {
            Microsoft.Xna.Framework.Input.Mouse.SetPosition(
                BaseGame.Width / 2, BaseGame.Height / 2);
            // Also use this for the current mouse pos for next frame,
            // else the mouseXMovement is messed up!
            mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
        }*/

        // And finally catch the XBox Controller input
        for (var i = 0; i < 4; i++)
        {
            _gamePadStateLastFrame[i] = _gamePadState[i];
            _gamePadState[i] = Microsoft.Xna.Framework.Input.GamePad.GetState((PlayerIndex)i, _gamePadDeadZoneMode[i]);
        }

        // Update all InputManager
        var enumerator = _buttonConfiguration.Buttons;
        var j = 0;

        var elapsedTime = GameTimeHelper.GameTimeToMilliseconds(gameTime);

        //create button buffer
        while (enumerator.MoveNext())
        {
            _keysState[j].Time = elapsedTime;
            _keysState[j].Key = enumerator.Current.Key;
            _keysState[j].State = IsButtonPressed(_buttonConfiguration.PlayerIndex, enumerator.Current.Value.Buttons) ? ButtonState.Pressed : ButtonState.Released;
            j++;
        }

        foreach (var input in _inputManager)
        {
            input.Update(_keysState, GameTimeHelper.TotalGameTimeToMilliseconds(gameTime));
        }

        base.Update(gameTime);
    }

    public InputSequence.InputManager GetInputManager(PlayerIndex index)
    {
        return _inputManager[(int)index];
    }

#if EDITOR
    private IKeyboardStateProvider _keyboardStateProvider;
    private IMouseStateProvider _mouseStateProvider;

    public void SetProviders(IKeyboardStateProvider keyboardStateProvider, IMouseStateProvider mouseStateProvider)
    {
        _keyboardStateProvider = keyboardStateProvider;
        _mouseStateProvider = mouseStateProvider;
    }
#endif
}