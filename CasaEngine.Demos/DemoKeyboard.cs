using CasaEngine.Framework.Application;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos;

/// <summary>
/// The keyboard as a demo sees it: empty while the game window is inactive. Every demo that polls the keyboard reads it
/// here instead of calling <see cref="Keyboard.GetState()"/>. No allocation.
/// </summary>
internal static class DemoKeyboard
{
    public static KeyboardState Read(CasaEngineGame game)
    {
        if (game == null || !game.IsActive)
        {
            return default;
        }

        return Keyboard.GetState();
    }
}
