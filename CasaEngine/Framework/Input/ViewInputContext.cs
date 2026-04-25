using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.Input;

public readonly record struct ViewInputContext(
    ViewId ViewId,
    long FrameId,
    KeyboardState KeyboardState,
    MouseState MouseState,
    Rectangle ScreenBounds,
    Point ScreenPosition,
    Point LocalPosition,
    int VerticalWheelDelta,
    int HorizontalWheelDelta,
    InputRoutingState RoutingState)
{
    public static ViewInputContext Empty { get; } = new(
        ViewId.Empty,
        0,
        new KeyboardState(),
        new MouseState(),
        Rectangle.Empty,
        Point.Zero,
        Point.Zero,
        0,
        0,
        InputRoutingState.Empty);

    public bool IsFallback => RoutingState.Reason == InputRoutingReason.Fallback;
}
