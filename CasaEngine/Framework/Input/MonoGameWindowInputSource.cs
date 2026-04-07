using CasaEngine.Engine.Input.Providers;
using MGUI.Shared.Input;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.Input;

/// <summary>
/// Basic window input source backed directly by MonoGame device state APIs.
/// </summary>
public sealed class MonoGameWindowInputSource : IWindowInputSource, IRawInputSource, IKeyboardStateProvider, IMouseStateProvider
{
    private readonly Func<bool>? _isWindowActive;

    public MonoGameWindowInputSource(Func<bool>? isWindowActive = null)
    {
        _isWindowActive = isWindowActive;
    }

    public WindowInputSnapshot GetSnapshot()
    {
        return new WindowInputSnapshot(GetKeyboardState(), GetMouseState());
    }

    public KeyboardState GetKeyboardState()
        => _isWindowActive?.Invoke() == false
            ? new KeyboardState()
            : Keyboard.GetState();

    public MouseState GetMouseState() => Mouse.GetState();

    KeyboardState IKeyboardStateProvider.GetState() => GetKeyboardState();

    MouseState IMouseStateProvider.GetState() => GetMouseState();
}