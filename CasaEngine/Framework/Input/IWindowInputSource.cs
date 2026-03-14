using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.Input;

public interface IWindowInputSource
{
    WindowInputSnapshot GetSnapshot();
}

public readonly record struct WindowInputSnapshot(KeyboardState KeyboardState, MouseState MouseState);
