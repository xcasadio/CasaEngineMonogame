using CasaEngine.Framework.Application;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos;

/// <summary>
/// The keyboard as a demo sees it. Empty while the game is inactive, and while the demo browser owns the keyboard
/// (plan point P9 of `ai-agent/tasks/demo-browser-split-tasks.md`: the pointer is over the browser), so the arrows,
/// Space and Enter typed in the browser's tree do not also drive the demo. Every demo that polls the keyboard reads it
/// here instead of calling <see cref="Keyboard.GetState()"/>. No allocation.
/// </summary>
internal static class DemoKeyboard
{
    public static KeyboardState Read(CasaEngineGame game)
    {
        if (game == null || !game.IsActive || game is DemosGame { BrowserOwnsKeyboard: true })
        {
            return default;
        }

        return Keyboard.GetState();
    }
}
