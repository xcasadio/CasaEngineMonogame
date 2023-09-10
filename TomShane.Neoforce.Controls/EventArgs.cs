using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace TomShane.Neoforce.Controls;

public class EventArgs : System.EventArgs
{
    public bool Handled = false;

    public EventArgs()
    {
    }
}

public class KeyEventArgs : EventArgs
{
    public Keys Key = Keys.None;
    public bool Control;
    public bool Shift;
    public bool Alt;
    public bool Caps;

    public KeyEventArgs()
    {
    }

    public KeyEventArgs(Keys key)
    {
        Key = key;
        Control = false;
        Shift = false;
        Alt = false;
        Caps = false;
    }

    public KeyEventArgs(Keys key, bool control, bool shift, bool alt, bool caps)
    {
        Key = key;
        Control = control;
        Shift = shift;
        Alt = alt;
        Caps = caps;
    }

}

public class MouseEventArgs : EventArgs
{

    public MouseState State;
    public MouseButton Button = MouseButton.None;
    public Point Position = new(0, 0);
    public Point Difference = new(0, 0);
    /// <summary>
    /// Mouse scroll direction
    /// </summary>
    public MouseScrollDirection ScrollDirection = MouseScrollDirection.None;

    public MouseEventArgs()
    {
    }

    public MouseEventArgs(MouseState state, MouseButton button, Point position)
    {
        State = state;
        Button = button;
        Position = position;
    }

    /// <summary>
    /// Creates a new initialized instace of the MouseEventArgs class.
    /// <param name="state">Mouse state at the time of the event.</param>
    /// <param name="button">Mouse button state at the time of the event.</param>
    /// <param name="position">Mosue cursor position at the time of the event.</param>
    /// <param name="scrollDirection">Mouse scroll direction at the time of the event.</param>
    public MouseEventArgs(MouseState state, MouseButton button, Point position, MouseScrollDirection scrollDirection)
        : this(state, button, position)
    {
        ScrollDirection = scrollDirection;
    }

    public MouseEventArgs(MouseEventArgs e)
        : this(e.State, e.Button, e.Position)
    {
        Difference = e.Difference;
        ScrollDirection = e.ScrollDirection;
    }

}

public class GamePadEventArgs : EventArgs
{

    public readonly PlayerIndex PlayerIndex = PlayerIndex.One;
    public GamePadState State = new();
    public GamePadButton Button = GamePadButton.None;
    public GamePadVectors Vectors;

    /*
     public GamePadEventArgs()
     {                      
     }*/

    public GamePadEventArgs(PlayerIndex playerIndex)
    {
        PlayerIndex = playerIndex;
    }

    public GamePadEventArgs(PlayerIndex playerIndex, GamePadButton button)
    {
        PlayerIndex = playerIndex;
        Button = button;
    }

}

public class DrawEventArgs : EventArgs
{

    public Renderer Renderer;
    public Rectangle Rectangle = Rectangle.Empty;
    public GameTime GameTime;

    public DrawEventArgs()
    {
    }

    public DrawEventArgs(Renderer renderer, Rectangle rectangle, GameTime gameTime)
    {
        Renderer = renderer;
        Rectangle = rectangle;
        GameTime = gameTime;
    }

}

public class ResizeEventArgs : EventArgs
{

    public readonly int Width;
    public readonly int Height;
    public readonly int OldWidth;
    public readonly int OldHeight;

    public ResizeEventArgs()
    {
    }

    public ResizeEventArgs(int width, int height, int oldWidth, int oldHeight)
    {
        Width = width;
        Height = height;
        OldWidth = oldWidth;
        OldHeight = oldHeight;
    }

}

public class MoveEventArgs : EventArgs
{

    public readonly int Left;
    public readonly int Top;
    public int OldLeft;
    public int OldTop;

    public MoveEventArgs()
    {
    }

    public MoveEventArgs(int left, int top, int oldLeft, int oldTop)
    {
        Left = left;
        Top = top;
        OldLeft = oldLeft;
        OldTop = oldTop;
    }

}

public class DeviceEventArgs : EventArgs
{

    public PreparingDeviceSettingsEventArgs DeviceSettings;

    public DeviceEventArgs()
    {
    }

    public DeviceEventArgs(PreparingDeviceSettingsEventArgs deviceSettings)
    {
        DeviceSettings = deviceSettings;
    }

}

public class WindowClosingEventArgs : EventArgs
{

    public readonly bool Cancel = false;

    public WindowClosingEventArgs()
    {
    }

}

public class WindowClosedEventArgs : EventArgs
{

    public readonly bool Dispose = false;

    public WindowClosedEventArgs()
    {
    }

}

public class ConsoleMessageEventArgs : EventArgs
{

    public ConsoleMessage Message;

    public ConsoleMessageEventArgs()
    {
    }

    public ConsoleMessageEventArgs(ConsoleMessage message)
    {
        Message = message;
    }

}