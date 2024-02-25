using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Engine.Input.InputDeviceStateProviders;

public interface IKeyboardStateProvider
{
    KeyboardState GetState();
}