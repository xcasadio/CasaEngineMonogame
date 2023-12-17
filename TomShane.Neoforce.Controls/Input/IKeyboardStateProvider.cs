using Microsoft.Xna.Framework.Input;

namespace TomShane.Neoforce.Controls.Input;

public interface IKeyboardStateProvider
{
    KeyboardState GetState();
}