using CasaEngine.Engine.Input.Providers;
using MGUI.Shared.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.Input;

/// <summary>
/// Basic window input source backed directly by MonoGame device state APIs.
/// </summary>
public sealed class MonoGameWindowInputSource : IWindowInputSource, IRawInputSource, IKeyboardStateProvider, IMouseStateProvider
{
    private readonly Func<bool>? _isWindowActive;
    private readonly Func<GameWindow?>? _getWindow;

    public MonoGameWindowInputSource(Func<bool>? isWindowActive = null, Func<GameWindow?>? getWindow = null)
    {
        _isWindowActive = isWindowActive;
        _getWindow = getWindow;
    }

    public WindowInputSnapshot GetSnapshot()
    {
        return new WindowInputSnapshot(GetKeyboardState(), GetMouseState());
    }

    public KeyboardState GetKeyboardState()
        => _isWindowActive?.Invoke() == false
            ? new KeyboardState()
            : Keyboard.GetState();

    public MouseState GetMouseState()
    {
        var window = _getWindow?.Invoke();
        return window == null ? Mouse.GetState() : Mouse.GetState(window);
    }

    KeyboardState IKeyboardStateProvider.GetState() => GetKeyboardState();

    MouseState IMouseStateProvider.GetState() => GetMouseState();
}