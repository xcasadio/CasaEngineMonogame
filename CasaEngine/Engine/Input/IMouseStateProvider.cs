using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Engine.Input;

public interface IMouseStateProvider
{
    MouseState GetState();
}