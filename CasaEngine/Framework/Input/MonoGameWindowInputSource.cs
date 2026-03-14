using CasaEngine.Engine.Input.InputDeviceStateProviders;
using MGUI.Shared.Input;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.Input;

/// <summary>
/// Basic window input source backed directly by MonoGame device state APIs.
/// </summary>
public sealed class MonoGameWindowInputSource : IWindowInputSource, IRawInputSource, IKeyboardStateProvider, IMouseStateProvider
{
    public WindowInputSnapshot GetSnapshot()
    {
        return new WindowInputSnapshot(GetKeyboardState(), GetMouseState());
    }

    public KeyboardState GetKeyboardState() => Keyboard.GetState();

    public MouseState GetMouseState() => Mouse.GetState();

    KeyboardState IKeyboardStateProvider.GetState() => GetKeyboardState();

    MouseState IMouseStateProvider.GetState() => GetMouseState();
}