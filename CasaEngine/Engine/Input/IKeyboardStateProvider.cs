using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Engine.Input;

public interface IKeyboardStateProvider
{
    KeyboardState GetState();
}