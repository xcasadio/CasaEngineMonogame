using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Engine.Input.InputDeviceStateProviders;

public class KeyboardStateProvider : IKeyboardStateProvider
{
    public KeyboardState GetState()
    {
        return Microsoft.Xna.Framework.Input.Keyboard.GetState();
    }
}