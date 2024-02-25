using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Engine.Input.InputDeviceStateProviders;

public class MouseStateProvider : IMouseStateProvider
{
    public MouseState GetState()
    {
        return Microsoft.Xna.Framework.Input.Mouse.GetState();
    }
}