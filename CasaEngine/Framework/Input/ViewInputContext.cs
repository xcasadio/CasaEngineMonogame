using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.Input;

public readonly record struct ViewInputContext(
    ViewId ViewId,
    KeyboardState KeyboardState,
    MouseState MouseState,
    Point ScreenPosition,
    Point LocalPosition,
    int VerticalWheelDelta,
    int HorizontalWheelDelta,
    InputRoutingState RoutingState)
{
    public static ViewInputContext Empty { get; } = new(
        ViewId.Empty,
        new KeyboardState(),
        new MouseState(),
        Point.Zero,
        Point.Zero,
        0,
        0,
        InputRoutingState.Empty);
}
