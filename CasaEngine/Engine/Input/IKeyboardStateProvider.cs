using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Engine.Input;

public interface IKeyboardStateProvider : TomShane.Neoforce.Controls.IKeyboardStateProvider
{
    KeyboardState GetState();
}