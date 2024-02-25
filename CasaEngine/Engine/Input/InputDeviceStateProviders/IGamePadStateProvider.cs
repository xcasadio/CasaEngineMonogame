using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Engine.Input.InputDeviceStateProviders;

public interface IGamePadStateProvider
{
    GamePadState GetState(PlayerIndex playerIndex, GamePadDeadZone gamePadDeadZone);
}