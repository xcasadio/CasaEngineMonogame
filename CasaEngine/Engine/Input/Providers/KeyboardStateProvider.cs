using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Engine.Input.Providers;

public class KeyboardStateProvider : IKeyboardStateProvider
{
    public KeyboardState GetState()
    {
        return Keyboard.GetState();
    }
}