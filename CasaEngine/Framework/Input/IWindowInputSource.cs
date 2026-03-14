using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.Input;

public interface IWindowInputSource
{
    WindowInputSnapshot GetSnapshot();
}

public readonly record struct WindowInputSnapshot(long FrameId, KeyboardState KeyboardState, MouseState MouseState)
{
    public static WindowInputSnapshot Empty { get; } = new(0, new KeyboardState(), new MouseState());

    public WindowInputSnapshot(KeyboardState keyboardState, MouseState mouseState)
        : this(0, keyboardState, mouseState)
    {
    }
}
