using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Engine.Input.Providers;

public interface IGamePadStateProvider
{
    GamePadState GetState(PlayerIndex playerIndex, GamePadDeadZone gamePadDeadZone);
}