using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Engine.Input;

public static class Mouse
{
    private static MouseState _currentState, _previousState;
    private static int _deltaX, _deltaY;
    private static int _positionX, _positionY;
    private static int _wheelDelta, _wheelValue;

    public static MouseState State => _currentState;
    public static MouseState PreviousState => _previousState;

    public static float DeltaX => _deltaX;
    public static float DeltaY => _deltaY;

    public static bool LeftButtonPressed => _currentState.LeftButton == ButtonState.Pressed;
    public static bool RightButtonPressed => _currentState.RightButton == ButtonState.Pressed;
    public static bool MiddleButtonPressed => _currentState.MiddleButton == ButtonState.Pressed;
    public static bool XButton1Pressed => _currentState.XButton1 == ButtonState.Pressed;
    public static bool XButton2Pressed => _currentState.XButton2 == ButtonState.Pressed;
    public static bool LeftButtonJustPressed => _currentState.LeftButton == ButtonState.Pressed && _previousState.LeftButton == ButtonState.Released;
    public static bool RightButtonJustPressed => _currentState.RightButton == ButtonState.Pressed && _previousState.RightButton == ButtonState.Released;
    public static bool MiddleButtonJustPressed => _currentState.MiddleButton == ButtonState.Pressed && _previousState.MiddleButton == ButtonState.Released;
    public static bool XButton1JustPressed => _currentState.XButton1 == ButtonState.Pressed && _previousState.XButton1 == ButtonState.Released;
    public static bool XButton2JustPressed => _currentState.XButton2 == ButtonState.Pressed && _previousState.XButton2 == ButtonState.Released;
    public static bool LeftButtonJustReleased => _currentState.LeftButton == ButtonState.Released && _previousState.LeftButton == ButtonState.Pressed;
    public static bool RightButtonJustReleased => _currentState.RightButton == ButtonState.Released && _previousState.RightButton == ButtonState.Pressed;
    public static bool MiddleButtonJustReleased => _currentState.MiddleButton == ButtonState.Released && _previousState.MiddleButton == ButtonState.Pressed;
    public static bool XButton1JustReleased => _currentState.XButton1 == ButtonState.Released && _previousState.XButton1 == ButtonState.Pressed;
    public static bool XButton2JustReleased => _currentState.XButton2 == ButtonState.Released && _previousState.XButton2 == ButtonState.Pressed;

    public static int WheelDelta => _wheelDelta;
    public static int WheelValue => _wheelValue;

    public static bool MouseInsideRectangle(Rectangle rectangle)
    {
        return _positionX >= rectangle.X &&
               _positionY >= rectangle.Y &&
               _positionX < rectangle.Right &&
               _positionY < rectangle.Bottom;
    }

    public static bool ButtonJustPressed(MouseButtons button)
    {
        return button switch
        {
            MouseButtons.LeftButton => LeftButtonJustPressed,
            MouseButtons.MiddleButton => MiddleButtonJustPressed,
            MouseButtons.RightButton => RightButtonJustPressed,
            MouseButtons.XButton1 => XButton1JustPressed,
            _ => XButton2JustPressed
        };
    }

    public static bool ButtonPressed(MouseButtons button)
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

    internal static void Update()
    {
        _previousState = _currentState;
        _currentState = Microsoft.Xna.Framework.Input.Mouse.GetState();

        _deltaX = _currentState.X - _positionX;
        _deltaY = _currentState.Y - _positionY;
        _positionX = _currentState.X;
        _positionY = _currentState.Y;

        _wheelDelta = _currentState.ScrollWheelValue - _wheelValue;
        _wheelValue = _currentState.ScrollWheelValue;
    }
}
