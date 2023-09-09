using CasaEngine.Engine.Input;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Editor.Inputs;

public class MouseStateProvider : IMouseStateProvider
{
    private readonly WpfMouse _keyboard;

    public MouseStateProvider(WpfMouse keyboard)
    {
        _keyboard = keyboard;
    }

    public MouseState GetState()
    {
        return _keyboard.GetState();
    }
}