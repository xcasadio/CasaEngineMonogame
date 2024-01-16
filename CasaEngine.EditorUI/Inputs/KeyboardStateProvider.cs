using CasaEngine.Engine.Input;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Editor.Inputs;

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