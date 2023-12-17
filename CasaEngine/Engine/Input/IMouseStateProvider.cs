using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Engine.Input;

public interface IMouseStateProvider : TomShane.Neoforce.Controls.Input.IMouseStateProvider
{
    MouseState GetState();
}