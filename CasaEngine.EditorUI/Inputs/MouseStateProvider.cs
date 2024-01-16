using CasaEngine.Engine.Input;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Editor.Inputs;

public class MouseStateProvider : IMouseStateProvider
{
    private readonly WpfMouse _mouse;

    public MouseStateProvider(WpfMouse mouse)
    {
        _mouse = mouse;
    }

    public MouseState GetState()
    {
        return _mouse.GetState();
    }
}