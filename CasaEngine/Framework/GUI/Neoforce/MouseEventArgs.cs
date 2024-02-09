using Microsoft.Xna.Framework.Input;
using TomShane.Neoforce.Controls.Input;

namespace TomShane.Neoforce.Controls;

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