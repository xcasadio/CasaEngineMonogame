using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Engine.Input.Providers;

public class MouseStateProvider : IMouseStateProvider
{
    public MouseState GetState()
    {
        return Mouse.GetState();
    }
}