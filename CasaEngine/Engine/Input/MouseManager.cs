using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Engine.Input;

public class MouseManager
{
    private MouseState _currentState, _previousState;
    private int _deltaX, _deltaY;
    private int _positionX, _positionY;
    private int _wheelDelta, _wheelValue;

#if EDITOR
    public MouseState State => _currentState;
#endif

    public Point Position => new(_currentState.X, _currentState.Y);
    public float DeltaX => _deltaX;
    public float DeltaY => _deltaY;
    public bool HasMoved => DeltaX > 1 || DeltaY > 1;

    public bool LeftButtonPressed => _currentState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
    public bool RightButtonPressed => _currentState.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
    public bool MiddleButtonPressed => _currentState.MiddleButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
    public bool XButton1Pressed => _currentState.XButton1 == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
    public bool XButton2Pressed => _currentState.XButton2 == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
    public bool LeftButtonJustPressed => _currentState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released;
    public bool RightButtonJustPressed => _currentState.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousState.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Released;
    public bool MiddleButtonJustPressed => _currentState.MiddleButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousState.MiddleButton == Microsoft.Xna.Framework.Input.ButtonState.Released;
    public bool XButton1JustPressed => _currentState.XButton1 == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousState.XButton1 == Microsoft.Xna.Framework.Input.ButtonState.Released;
    public bool XButton2JustPressed => _currentState.XButton2 == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousState.XButton2 == Microsoft.Xna.Framework.Input.ButtonState.Released;
    public bool LeftButtonJustReleased => _currentState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released && _previousState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
    public bool RightButtonJustReleased => _currentState.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Released && _previousState.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
    public bool MiddleButtonJustReleased => _currentState.MiddleButton == Microsoft.Xna.Framework.Input.ButtonState.Released && _previousState.MiddleButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
    public bool XButton1JustReleased => _currentState.XButton1 == Microsoft.Xna.Framework.Input.ButtonState.Released && _previousState.XButton1 == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
    public bool XButton2JustReleased => _currentState.XButton2 == Microsoft.Xna.Framework.Input.ButtonState.Released && _previousState.XButton2 == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

    public int WheelDelta => _wheelDelta;
    public int WheelValue => _wheelValue;

    public bool MouseInsideRectangle(Rectangle rectangle)
    {
        return _positionX >= rectangle.X &&
               _positionY >= rectangle.Y &&
               _positionX < rectangle.Right &&
               _positionY < rectangle.Bottom;
    }

    public bool ButtonJustPressed(MouseButtons mouseButton)
    {
        return mouseButton switch
        {
            MouseButtons.LeftButton => LeftButtonJustPressed,
            MouseButtons.MiddleButton => MiddleButtonJustPressed,
            MouseButtons.RightButton => RightButtonJustPressed,
            MouseButtons.XButton1 => XButton1JustPressed,
            MouseButtons.XButton2 => XButton2JustPressed,
            _ => throw new ArgumentOutOfRangeException(nameof(mouseButton))
        };
    }

    public bool ButtonPressed(MouseButtons button)
    {
        return button switch
        {
            MouseButtons.LeftButton => LeftButtonPressed,
            MouseButtons.MiddleButton => MiddleButtonPressed,
            MouseButtons.RightButton => RightButtonPressed,
            MouseButtons.XButton1 => XButton1Pressed,
            _ => XButton2Pressed
        };
    }

    internal void Update(MouseState mouseState)
    {
        _previousState = _currentState;
        _currentState = mouseState;

        _deltaX = _currentState.X - _positionX;
        _deltaY = _currentState.Y - _positionY;
        _positionX = _currentState.X;
        _positionY = _currentState.Y;

        _wheelDelta = _currentState.ScrollWheelValue - _wheelValue;
        _wheelValue = _currentState.ScrollWheelValue;
    }
}
