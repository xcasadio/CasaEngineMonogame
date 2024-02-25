using CasaEngine.Engine.Input;
using CasaEngine.Engine.Input.InputDeviceStateProviders;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.EditorUI.Inputs;

public class KeyboardStateProvider : IKeyboardStateProvider
{
    private readonly WpfKeyboard _keyboard;

    public KeyboardStateProvider(WpfKeyboard keyboard)
    {
        _keyboard = keyboard;
    }

    public KeyboardState GetState()
    {
        return _keyboard.GetState();
    }
}