using CasaEngine.Engine.Input.InputDeviceStateProviders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.Input;

public sealed class ViewportRelativeMouseStateProvider : IMouseStateProvider
{
    private readonly IWindowInputSource _windowInputSource;
    private readonly Func<Rectangle> _getBounds;

    public ViewportRelativeMouseStateProvider(IWindowInputSource windowInputSource, Func<Rectangle> getBounds)
    {
        ArgumentNullException.ThrowIfNull(windowInputSource);
        ArgumentNullException.ThrowIfNull(getBounds);

        _windowInputSource = windowInputSource;
        _getBounds = getBounds;
    }

    public MouseState GetState()
    {
        var bounds = _getBounds();
        var mouseState = _windowInputSource.GetSnapshot().MouseState;

        return new MouseState(
            mouseState.X - bounds.Left,
            mouseState.Y - bounds.Top,
            mouseState.ScrollWheelValue,
            mouseState.LeftButton,
            mouseState.MiddleButton,
            mouseState.RightButton,
            mouseState.XButton1,
            mouseState.XButton2,
            mouseState.HorizontalScrollWheelValue);
    }
}
