
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/


using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Input;
using Keyboard = CasaEngine.Engine.Input.Keyboard;
using Mouse = CasaEngine.Engine.Input.Mouse;

namespace CasaEngine.Framework.UserInterface;

public enum MouseButton
{
    None = 0,
    Left,
    Right,
    Middle,
    XButton1,
    XButton2
} // MouseButton


public class Input
{


    private class InputKey
    {
        public Keys Key = Keys.None;
        public bool Pressed;
        public double Countdown = RepeatDelay;
    } // InputKey

    private class InputMouseButton
    {
        public MouseButton Button = MouseButton.None;
        public bool Pressed;
    } // InputMouseButton



    private const int RepeatDelay = 500;
    private const int RepeatRate = 50;



    private readonly List<InputKey> _keys = new();
    private readonly List<InputMouseButton> _mouseButtons = new();
    private readonly Rectangle _gameWindowClientBounds;



    public event KeyEventHandler KeyDown;
    public event KeyEventHandler KeyPress;
    public event KeyEventHandler KeyUp;

    public event MouseEventHandler MouseDown;
    public event MouseEventHandler MousePress;
    public event MouseEventHandler MouseUp;
    public event MouseEventHandler MouseMove;



    public Input(Rectangle gameWindowClientBounds)
    {
        _gameWindowClientBounds = gameWindowClientBounds;

#if (WINDOWS)
        foreach (var keyName in Enum.GetNames(typeof(Keys)))
        {
            var key = new InputKey { Key = (Keys)Enum.Parse(typeof(Keys), keyName) };
            _keys.Add(key);
        }

        foreach (var mouseButtonName in Enum.GetNames(typeof(MouseButton)))
        {
            var mouseButton = new InputMouseButton
            {
                Button = (MouseButton)Enum.Parse(typeof(MouseButton), mouseButtonName)
            };
            _mouseButtons.Add(mouseButton);
        }
#endif
    } // InputSystem



    public virtual void Update(float elapsedTime)
    {
#if (WINDOWS)
        UpdateMouse();
        UpdateKeys(elapsedTime);
#endif
    } // Update



    private void UpdateKeys(float elapsedTime)
    {
        Keyboard.Update();

        var e = new KeyEventArgs { Caps = ((ushort)GetKeyState(0x14) & 0xffff) != 0 };

        foreach (var key in Keyboard.State.GetPressedKeys())
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

            var pressed = Keyboard.State.IsKeyDown(key.Key);

            double frameTimeInMilliseconds = elapsedTime; // From seconds to milliseconds.
            if (pressed)
            {
                key.Countdown -= frameTimeInMilliseconds;
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
    } // UpdateKeys

    [DllImport("user32.dll")]
    internal static extern short GetKeyState(int key);



    private void UpdateMouse()
    {
        Mouse.Update();

        if (Mouse.State.X != Mouse.PreviousState.X || Mouse.State.Y != Mouse.PreviousState.Y)
        {
            var e = new MouseEventArgs();

            var btn = MouseButton.None;
            if (Mouse.State.LeftButton == ButtonState.Pressed)
            {
                btn = MouseButton.Left;
            }
            else if (Mouse.State.RightButton == ButtonState.Pressed)
            {
                btn = MouseButton.Right;
            }
            else if (Mouse.State.MiddleButton == ButtonState.Pressed)
            {
                btn = MouseButton.Middle;
            }
            else if (Mouse.State.XButton1 == ButtonState.Pressed)
            {
                btn = MouseButton.XButton1;
            }
            else if (Mouse.State.XButton2 == ButtonState.Pressed)
            {
                btn = MouseButton.XButton2;
            }

            BuildMouseEvent(btn, ref e, _gameWindowClientBounds);
            MouseMove?.Invoke(this, e);
        }
        UpdateButtons();
    } // UpdateMouse

    private void UpdateButtons()
    {
        var e = new MouseEventArgs();

        foreach (var btn in _mouseButtons)
        {
            var bs = ButtonState.Released;

            if (btn.Button == MouseButton.Left)
            {
                bs = Mouse.State.LeftButton;
            }
            else if (btn.Button == MouseButton.Right)
            {
                bs = Mouse.State.RightButton;
            }
            else if (btn.Button == MouseButton.Middle)
            {
                bs = Mouse.State.MiddleButton;
            }
            else if (btn.Button == MouseButton.XButton1)
            {
                bs = Mouse.State.XButton1;
            }
            else if (btn.Button == MouseButton.XButton2)
            {
                bs = Mouse.State.XButton2;
            }
            else
            {
                continue;
            }

            var pressed = bs == ButtonState.Pressed; // The current state

            if (pressed && !btn.Pressed) // If is pressed and the last frame wasn't pressed.
            {
                btn.Pressed = true;
                BuildMouseEvent(btn.Button, ref e, _gameWindowClientBounds);

                MouseDown?.Invoke(this, e);
                MousePress?.Invoke(this, e);
            }
            else if (!pressed && btn.Pressed) // If isn't pressed and the last frame was pressed.
            {
                btn.Pressed = false;
                BuildMouseEvent(btn.Button, ref e, _gameWindowClientBounds);

                MouseUp?.Invoke(this, e);
            }
            else if (pressed && btn.Pressed) // If is pressed and was pressed.
            {
                e.Button = btn.Button;
                BuildMouseEvent(btn.Button, ref e, _gameWindowClientBounds);
                MousePress?.Invoke(this, e);
            }
        }
    } // UpdateButtons

    private static void AdjustPosition(Rectangle gameWindowClientBounds, ref MouseEventArgs e)
    {
        var screen = gameWindowClientBounds;

        if (e.Position.X < 0)
        {
            e.Position.X = 0;
        }

        if (e.Position.Y < 0)
        {
            e.Position.Y = 0;
        }

        if (e.Position.X >= screen.Width)
        {
            e.Position.X = screen.Width - 1;
        }

        if (e.Position.Y >= screen.Height)
        {
            e.Position.Y = screen.Height - 1;
        }
    } // AdjustPosition

    private static void BuildMouseEvent(MouseButton button, ref MouseEventArgs e, Rectangle gameWindowClientBounds)
    {
        e.State = Mouse.State;
        e.Button = button;

        e.Position = new Point(Mouse.State.X, Mouse.State.Y);
        AdjustPosition(gameWindowClientBounds, ref e);

        e.State = new MouseState(e.Position.X, e.Position.Y, e.State.ScrollWheelValue, e.State.LeftButton, e.State.MiddleButton, e.State.RightButton, e.State.XButton1, e.State.XButton2);

        var pos = new Point(Mouse.State.X, Mouse.State.Y);
        e.Difference = new Point(e.Position.X - pos.X, e.Position.Y - pos.Y);
    } // BuildMouseEvent


} // Input
  // XNAFinalEngine.UserInterface