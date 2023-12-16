using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace TomShane.Neoforce.Controls;

[Flags]
public enum InputMethods
{
    None = 0x00,
    Keyboard = 0x01,
    Mouse = 0x02,
    GamePad = 0x04,
    All = Keyboard | Mouse | 0x04
}

public enum MouseButton
{
    None = 0,
    Left,
    Right,
    Middle,
    XButton1,
    XButton2
}

public enum MouseScrollDirection
{
    None = 0,
    Down = 1,
    Up = 2
}

public enum GamePadButton
{
    None = 0,
    Start = 6,
    Back,
    Up,
    Down,
    Left,
    Right,
    A,
    B,
    X,
    Y,
    BigButton,
    LeftShoulder,
    RightShoulder,
    LeftTrigger,
    RightTrigger,
    LeftStick,
    RightStick,
    LeftStickLeft,
    LeftStickRight,
    LeftStickUp,
    LeftStickDown,
    RightStickLeft,
    RightStickRight,
    RightStickUp,
    RightStickDown
}

public enum ActivePlayer
{
    None = -1,
    One = 0,
    Two = 1,
    Three = 2,
    Four = 3
}

public struct GamePadVectors
{
    public Vector2 LeftStick;
    public Vector2 RightStick;
    public float LeftTrigger;
    public float RightTrigger;
}

public struct InputOffset
{
    public readonly int X;
    public readonly int Y;
    public readonly float RatioX;
    public readonly float RatioY;

    public InputOffset(int x, int y, float rx, float ry)
    {
        X = x;
        Y = y;
        RatioX = rx;
        RatioY = ry;
    }
}

public class InputSystem : Disposable
{

    private class InputKey
    {
        public Keys Key = Keys.None;
        public bool Pressed;
        public double Countdown = RepeatDelay;
    }

    private class InputMouseButton
    {
        public MouseButton Button = MouseButton.None;
        public bool Pressed;
        public double Countdown = RepeatDelay;

        public InputMouseButton()
        {
        }

        public InputMouseButton(MouseButton button)
        {
            Button = button;
        }
    }

    private class InputMouse
    {
        public MouseState State = new();
        public Point Position = new(0, 0);
    }

    private class InputGamePadButton
    {
        public GamePadButton Button = GamePadButton.None;
        public bool Pressed;
        public double Countdown = RepeatDelay;

        public InputGamePadButton()
        {
        }

        public InputGamePadButton(GamePadButton button)
        {
            Button = button;
        }
    }

    private const int RepeatDelay = 500;
    private const int RepeatRate = 50;
    private float _clickThreshold = 0.5f;

    private List<InputKey> _keys = new();
    private List<InputMouseButton> _mouseButtons = new();
    private List<InputGamePadButton> _gamePadButtons = new();
    private MouseState _mouseState;
    private GamePadState _gamePadState;
    private Manager _manager;
    private InputOffset _inputOffset = new(0, 0, 1.0f, 1.0f);
    private IKeyboardStateProvider? _keyboardStateProvider;
    private IMouseStateProvider? _mouseStateProvider;
    private InputMethods _inputMethods = InputMethods.All;
    private ActivePlayer _activePlayer = ActivePlayer.None;

    /// <summary>
    /// Sets or gets input offset and ratio when rescaling controls in render target.
    /// </summary>
    public virtual InputOffset InputOffset
    {
        get => _inputOffset;
        set => _inputOffset = value;
    }

    /// <summary>
    /// Sets or gets input methods allowed for navigation.
    /// </summary>
    public virtual InputMethods InputMethods
    {
        get => _inputMethods;
        set => _inputMethods = value;
    }

    public virtual ActivePlayer ActivePlayer
    {
        get => _activePlayer;
        set => _activePlayer = value;
    }

    public event KeyEventHandler KeyDown;
    public event KeyEventHandler KeyPress;
    public event KeyEventHandler KeyUp;

    public event MouseEventHandler MouseDown;
    public event MouseEventHandler MousePress;
    public event MouseEventHandler MouseUp;
    public event MouseEventHandler MouseMove;
    /// <summary>
    /// Occurs when the mouse is scrolled.
    /// </summary>
    public event MouseEventHandler MouseScroll;

    public event GamePadEventHandler GamePadUp;
    public event GamePadEventHandler GamePadDown;
    public event GamePadEventHandler GamePadPress;
    public event GamePadEventHandler GamePadMove;

    public InputSystem(Manager manager, InputOffset offset)
    {
        _manager = manager;
        _inputOffset = offset;
    }

    public InputSystem(Manager manager) :
        this(manager, new InputOffset(0, 0, 1.0f, 1.0f))
    {
    }

    public virtual void Initialize()
    {
        _keys.Clear();
        _mouseButtons.Clear();
        _gamePadButtons.Clear();

        foreach (var str in Enum.GetNames(typeof(Keys)))
        {
            var key = new InputKey();
            key.Key = (Keys)Enum.Parse(typeof(Keys), str);
            _keys.Add(key);
        }

        foreach (var str in Enum.GetNames(typeof(MouseButton)))
        {
            var btn = new InputMouseButton();
            btn.Button = (MouseButton)Enum.Parse(typeof(MouseButton), str);
            _mouseButtons.Add(btn);
        }

        foreach (var str in Enum.GetNames(typeof(GamePadButton)))
        {
            var btn = new InputGamePadButton();
            btn.Button = (GamePadButton)Enum.Parse(typeof(GamePadButton), str);
            _gamePadButtons.Add(btn);
        }
    }

    public virtual void SendMouseState(MouseState state, GameTime gameTime)
    {
        UpdateMouse(state, gameTime);
    }

    public virtual void SendKeyboardState(KeyboardState state, GameTime gameTime)
    {
        UpdateKeys(state, gameTime);
    }

    public virtual void SendGamePadState(PlayerIndex playerIndex, GamePadState state, GameTime gameTime)
    {
        UpdateGamePad(playerIndex, state, gameTime);
    }

    public virtual void Update(GameTime gameTime)
    {
        if (_manager.UseGuide)
        {
            return;
        }

        var ms = _mouseStateProvider?.GetState() ?? Mouse.GetState();
        var ks = _keyboardStateProvider?.GetState() ?? Keyboard.GetState();

        if ((_inputMethods & InputMethods.Mouse) == InputMethods.Mouse)
        {
            UpdateMouse(ms, gameTime);
        }

        if ((_inputMethods & InputMethods.Keyboard) == InputMethods.Keyboard)
        {
            UpdateKeys(ks, gameTime);
        }

        if ((_inputMethods & InputMethods.GamePad) == InputMethods.GamePad)
        {
            var index = PlayerIndex.One;
            if (_activePlayer == ActivePlayer.None)
            {
                var i = 0; // Have to be done this way, else it crashes for player other than one
                index = Enum.GetValues<PlayerIndex>().FirstOrDefault(x => GamePad.GetState(x).IsConnected);
            }
            else if (_activePlayer != ActivePlayer.None)
            {
                index = (PlayerIndex)_activePlayer;
            }
            var gs = GamePad.GetState(index);
            UpdateGamePad(index, gs, gameTime);
        }
    }

    private ButtonState GetVectorState(GamePadButton button, GamePadState state)
    {
        var ret = ButtonState.Released;
        var down = false;
        var t = _clickThreshold;

        switch (button)
        {
            case GamePadButton.LeftStickLeft: down = state.ThumbSticks.Left.X < -t; break;
            case GamePadButton.LeftStickRight: down = state.ThumbSticks.Left.X > t; break;
            case GamePadButton.LeftStickUp: down = state.ThumbSticks.Left.Y > t; break;
            case GamePadButton.LeftStickDown: down = state.ThumbSticks.Left.Y < -t; break;

            case GamePadButton.RightStickLeft: down = state.ThumbSticks.Right.X < -t; break;
            case GamePadButton.RightStickRight: down = state.ThumbSticks.Right.X > t; break;
            case GamePadButton.RightStickUp: down = state.ThumbSticks.Right.Y > t; break;
            case GamePadButton.RightStickDown: down = state.ThumbSticks.Right.Y < -t; break;

            case GamePadButton.LeftTrigger: down = state.Triggers.Left > t; break;
            case GamePadButton.RightTrigger: down = state.Triggers.Right > t; break;
        }

        ret = down ? ButtonState.Pressed : ButtonState.Released;

        return ret;
    }

    private void UpdateGamePad(PlayerIndex playerIndex, GamePadState state, GameTime gameTime)
    {
        var e = new GamePadEventArgs(playerIndex);

        if (state.ThumbSticks.Left != _gamePadState.ThumbSticks.Left ||
            state.ThumbSticks.Right != _gamePadState.ThumbSticks.Right ||
            state.Triggers.Left != _gamePadState.Triggers.Left ||
            state.Triggers.Right != _gamePadState.Triggers.Right)
        {
            BuildGamePadEvent(state, GamePadButton.None, ref e);
            GamePadMove?.Invoke(this, e);
        }

        foreach (var btn in _gamePadButtons)
        {
            var bs = ButtonState.Released;

            if (btn.Button == GamePadButton.None)
            {
                continue;
            }

            if (btn.Button == GamePadButton.A)
            {
                bs = state.Buttons.A;
            }
            else if (btn.Button == GamePadButton.B)
            {
                bs = state.Buttons.B;
            }
            else if (btn.Button == GamePadButton.Back)
            {
                bs = state.Buttons.Back;
            }
            else if (btn.Button == GamePadButton.Down)
            {
                bs = state.DPad.Down;
            }
            else if (btn.Button == GamePadButton.Left)
            {
                bs = state.DPad.Left;
            }
            else if (btn.Button == GamePadButton.Right)
            {
                bs = state.DPad.Right;
            }
            else if (btn.Button == GamePadButton.Start)
            {
                bs = state.Buttons.Start;
            }
            else if (btn.Button == GamePadButton.Up)
            {
                bs = state.DPad.Up;
            }
            else if (btn.Button == GamePadButton.X)
            {
                bs = state.Buttons.X;
            }
            else if (btn.Button == GamePadButton.Y)
            {
                bs = state.Buttons.Y;
            }
            else if (btn.Button == GamePadButton.BigButton)
            {
                bs = state.Buttons.BigButton;
            }
            else if (btn.Button == GamePadButton.LeftShoulder)
            {
                bs = state.Buttons.LeftShoulder;
            }
            else if (btn.Button == GamePadButton.RightShoulder)
            {
                bs = state.Buttons.RightShoulder;
            }
            else if (btn.Button == GamePadButton.LeftStick)
            {
                bs = state.Buttons.LeftStick;
            }
            else if (btn.Button == GamePadButton.RightStick)
            {
                bs = state.Buttons.RightStick;
            }
            else
            {
                bs = GetVectorState(btn.Button, state);
            }

            var pressed = bs == ButtonState.Pressed;
            if (pressed)
            {
                var ms = gameTime.ElapsedGameTime.TotalMilliseconds;
                if (pressed)
                {
                    btn.Countdown -= ms;
                }
            }

            if (pressed && !btn.Pressed)
            {
                btn.Pressed = true;
                BuildGamePadEvent(state, btn.Button, ref e);

                GamePadDown?.Invoke(this, e);

                GamePadPress?.Invoke(this, e);
            }
            else if (!pressed && btn.Pressed)
            {
                btn.Pressed = false;
                btn.Countdown = RepeatDelay;
                BuildGamePadEvent(state, btn.Button, ref e);

                GamePadUp?.Invoke(this, e);
            }
            else if (btn.Pressed && btn.Countdown < 0)
            {
                e.Button = btn.Button;
                btn.Countdown = RepeatRate;
                BuildGamePadEvent(state, btn.Button, ref e);

                GamePadPress?.Invoke(this, e);
            }
        }
        _gamePadState = state;
    }

    private void BuildGamePadEvent(GamePadState state, GamePadButton button, ref GamePadEventArgs e)
    {
        e.State = state;
        e.Button = button;
        e.Vectors.LeftStick = new Vector2(state.ThumbSticks.Left.X, state.ThumbSticks.Left.Y);
        e.Vectors.RightStick = new Vector2(state.ThumbSticks.Right.X, state.ThumbSticks.Right.Y);
        e.Vectors.LeftTrigger = state.Triggers.Left;
        e.Vectors.RightTrigger = state.Triggers.Right;
    }

    private void UpdateKeys(KeyboardState state, GameTime gameTime)
    {

        var e = new KeyEventArgs();

        e.Caps = ((ushort)NativeMethods.GetKeyState(0x14) & 0xffff) != 0;

        foreach (var key in state.GetPressedKeys())
        {
            if (key == Keys.LeftAlt || key == Keys.RightAlt)
            {
                e.Alt = true;
            }
            else if (key == Keys.LeftShift || key == Keys.RightShift)
            {
                e.Shift = true;
            }
            else if (key == Keys.LeftControl || key == Keys.RightControl)
            {
                e.Control = true;
            }
        }

        foreach (var key in _keys)
        {
            if (key.Key == Keys.LeftAlt || key.Key == Keys.RightAlt ||
                key.Key == Keys.LeftShift || key.Key == Keys.RightShift ||
                key.Key == Keys.LeftControl || key.Key == Keys.RightControl)
            {
                continue;
            }

            var pressed = state.IsKeyDown(key.Key);

            var ms = gameTime.ElapsedGameTime.TotalMilliseconds;
            if (pressed)
            {
                key.Countdown -= ms;
            }

            if (pressed && !key.Pressed)
            {
                key.Pressed = true;
                e.Key = key.Key;

                KeyDown?.Invoke(this, e);

                KeyPress?.Invoke(this, e);
            }
            else if (!pressed && key.Pressed)
            {
                key.Pressed = false;
                key.Countdown = RepeatDelay;
                e.Key = key.Key;

                KeyUp?.Invoke(this, e);
            }
            else if (key.Pressed && key.Countdown < 0)
            {
                key.Countdown = RepeatRate;
                e.Key = key.Key;

                KeyPress?.Invoke(this, e);
            }
        }
    }

    private Point RecalcPosition(Point pos)
    {
        return new Point((int)((pos.X - InputOffset.X) / InputOffset.RatioX), (int)((pos.Y - InputOffset.Y) / InputOffset.RatioY));
    }

    private void AdjustPosition(ref MouseEventArgs e)
    {
        var screenWidth = _manager.Game.GraphicsDevice.PresentationParameters.BackBufferWidth;
        var screenHeight = _manager.Game.GraphicsDevice.PresentationParameters.BackBufferHeight;

        if (e.Position.X < 0)
        {
            e.Position.X = 0;
        }

        if (e.Position.Y < 0)
        {
            e.Position.Y = 0;
        }

        if (e.Position.X >= screenWidth)
        {
            e.Position.X = screenWidth - 1;
        }

        if (e.Position.Y >= screenHeight)
        {
            e.Position.Y = screenHeight - 1;
        }
    }

    private void BuildMouseEvent(MouseState state, MouseButton button, ref MouseEventArgs e)
    {
        e.State = state;
        e.Button = button;

        e.Position = new Point(state.X, state.Y);
        AdjustPosition(ref e);

        e.Position = RecalcPosition(e.Position);
        e.State = new MouseState(e.Position.X, e.Position.Y, e.State.ScrollWheelValue, e.State.LeftButton, e.State.MiddleButton, e.State.RightButton, e.State.XButton1, e.State.XButton2);

        var pos = RecalcPosition(new Point(_mouseState.X, _mouseState.Y));
        e.Difference = new Point(e.Position.X - pos.X, e.Position.Y - pos.Y);
    }

    private void BuildMouseEvent(MouseState state, MouseButton button, MouseScrollDirection direction, ref MouseEventArgs e)
    {
        BuildMouseEvent(state, button, ref e);

        e.ScrollDirection = direction;
    }

    private void UpdateMouse(MouseState state, GameTime gameTime)
    {
        if (state.X != _mouseState.X || state.Y != _mouseState.Y)
        {
            var e = new MouseEventArgs();

            var btn = MouseButton.None;
            if (state.LeftButton == ButtonState.Pressed)
            {
                btn = MouseButton.Left;
            }
            else if (state.RightButton == ButtonState.Pressed)
            {
                btn = MouseButton.Right;
            }
            else if (state.MiddleButton == ButtonState.Pressed)
            {
                btn = MouseButton.Middle;
            }
            else if (state.XButton1 == ButtonState.Pressed)
            {
                btn = MouseButton.XButton1;
            }
            else if (state.XButton2 == ButtonState.Pressed)
            {
                btn = MouseButton.XButton2;
            }

            BuildMouseEvent(state, btn, ref e);
            MouseMove?.Invoke(this, e);
        }

        // Mouse wheel position changed
        if (state.ScrollWheelValue != _mouseState.ScrollWheelValue)
        {
            var e = new MouseEventArgs();
            var direction = state.ScrollWheelValue < _mouseState.ScrollWheelValue ? MouseScrollDirection.Down : MouseScrollDirection.Up;

            BuildMouseEvent(state, MouseButton.None, direction, ref e);

            MouseScroll?.Invoke(this, e);
        }

        UpdateButtons(state, gameTime);

        _mouseState = state;
    }

    private void UpdateButtons(MouseState state, GameTime gameTime)
    {

        var e = new MouseEventArgs();

        foreach (var btn in _mouseButtons)
        {
            var bs = ButtonState.Released;

            if (btn.Button == MouseButton.Left)
            {
                bs = state.LeftButton;
            }
            else if (btn.Button == MouseButton.Right)
            {
                bs = state.RightButton;
            }
            else if (btn.Button == MouseButton.Middle)
            {
                bs = state.MiddleButton;
            }
            else if (btn.Button == MouseButton.XButton1)
            {
                bs = state.XButton1;
            }
            else if (btn.Button == MouseButton.XButton2)
            {
                bs = state.XButton2;
            }
            else
            {
                continue;
            }

            var pressed = bs == ButtonState.Pressed;
            if (pressed)
            {
                var ms = gameTime.ElapsedGameTime.TotalMilliseconds;
                if (pressed)
                {
                    btn.Countdown -= ms;
                }
            }

            if (pressed && !btn.Pressed)
            {
                btn.Pressed = true;
                BuildMouseEvent(state, btn.Button, ref e);

                MouseDown?.Invoke(this, e);

                MousePress?.Invoke(this, e);
            }
            else if (!pressed && btn.Pressed)
            {
                btn.Pressed = false;
                btn.Countdown = RepeatDelay;
                BuildMouseEvent(state, btn.Button, ref e);

                MouseUp?.Invoke(this, e);
            }
            else if (btn.Pressed && btn.Countdown < 0)
            {
                e.Button = btn.Button;
                btn.Countdown = RepeatRate;
                BuildMouseEvent(state, btn.Button, ref e);

                MousePress?.Invoke(this, e);
            }
        }
    }

    public void SetProviders(IKeyboardStateProvider keyboardStateProvider, IMouseStateProvider mouseStateProvider)
    {
        _keyboardStateProvider = keyboardStateProvider;
        _mouseStateProvider = mouseStateProvider;
    }
}