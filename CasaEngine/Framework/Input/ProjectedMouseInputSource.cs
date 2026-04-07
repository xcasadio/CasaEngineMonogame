using CasaEngine.Engine.Input.Providers;
using MGUI.Shared.Input;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.Input;

/// <summary>
/// Wraps an existing window input source and replaces the mouse state with a projected one.
/// Keyboard state and frame id are preserved from the wrapped source.
/// </summary>
public sealed class ProjectedMouseInputSource : IWindowInputSource, IRawInputSource, IKeyboardStateProvider, IMouseStateProvider
{
    private readonly IWindowInputSource _inner;
    private readonly Func<WindowInputSnapshot, MouseState?> _projectMouseState;

    public ProjectedMouseInputSource(IWindowInputSource inner, Func<WindowInputSnapshot, MouseState?> projectMouseState)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(projectMouseState);

        _inner = inner;
        _projectMouseState = projectMouseState;
    }

    public WindowInputSnapshot GetSnapshot()
    {
        var snapshot = _inner.GetSnapshot();
        var projectedMouseState = _projectMouseState(snapshot) ?? CreateInactiveMouseState(snapshot.MouseState);
        return new WindowInputSnapshot(snapshot.FrameId, snapshot.KeyboardState, projectedMouseState);
    }

    public KeyboardState GetKeyboardState() => GetSnapshot().KeyboardState;

    public MouseState GetMouseState() => GetSnapshot().MouseState;

    KeyboardState IKeyboardStateProvider.GetState() => GetKeyboardState();

    MouseState IMouseStateProvider.GetState() => GetMouseState();

    private static MouseState CreateInactiveMouseState(MouseState source)
    {
        return new MouseState(
            -1,
            -1,
            source.ScrollWheelValue,
            source.LeftButton,
            source.MiddleButton,
            source.RightButton,
            source.XButton1,
            source.XButton2,
            source.HorizontalScrollWheelValue);
    }
}