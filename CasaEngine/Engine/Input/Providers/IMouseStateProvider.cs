using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Engine.Input.Providers;

public interface IMouseStateProvider
{
    MouseState GetState();
}