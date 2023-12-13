using Microsoft.Xna.Framework.Input;

namespace TomShane.Neoforce.Controls;

public interface IKeyboardStateProvider
{
    KeyboardState GetState();
}