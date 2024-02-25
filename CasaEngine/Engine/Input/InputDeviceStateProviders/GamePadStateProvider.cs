using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Engine.Input.InputDeviceStateProviders;

public class GamePadStateProvider : IGamePadStateProvider
{
    public GamePadState GetState(PlayerIndex playerIndex, GamePadDeadZone gamePadDeadZone)
    {
        return Microsoft.Xna.Framework.Input.GamePad.GetState(playerIndex, gamePadDeadZone);
    }
}