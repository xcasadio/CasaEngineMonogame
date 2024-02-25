using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Engine.Input.InputDeviceStateProviders;

public interface IMouseStateProvider
{
    MouseState GetState();
}