using CasaEngine.Engine.Input.InputDeviceStateProviders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Engine.Input;

public class GamePadManager
{
    private readonly GamePad _gamePad1 = new(PlayerIndex.One);
    private readonly GamePad _gamePad2 = new(PlayerIndex.Two);
    private readonly GamePad _gamePad3 = new(PlayerIndex.Three);
    private readonly GamePad _gamePad4 = new(PlayerIndex.Four);

    public GamePad GetGamePad(PlayerIndex playerIndex)
    {
        return playerIndex switch
        {
            PlayerIndex.One => _gamePad1,
            PlayerIndex.Two => _gamePad2,
            PlayerIndex.Three => _gamePad3,
            PlayerIndex.Four => _gamePad4,
            _ => throw new ArgumentOutOfRangeException(nameof(playerIndex), "GamePad: The number has to be between 0 and 3.")
        };
    }

    public GamePadState GamePadState(PlayerIndex playerIndex)
    {
        return playerIndex switch
        {
            PlayerIndex.One => _gamePad1.CurrentState,
            PlayerIndex.Two => _gamePad2.CurrentState,
            PlayerIndex.Three => _gamePad3.CurrentState,
            PlayerIndex.Four => _gamePad4.CurrentState,
            _ => throw new ArgumentOutOfRangeException(nameof(playerIndex), "GamePad: The number has to be between 0 and 3.")
        };
    }

    public void GamePadDeadZoneMode(PlayerIndex index, GamePadDeadZone mode)
    {
        switch (index)
        {
            case PlayerIndex.One:
                _gamePad1.DeadZoneMode = mode;
                break;
            case PlayerIndex.Two:
                _gamePad2.DeadZoneMode = mode;
                break;
            case PlayerIndex.Three:
                _gamePad3.DeadZoneMode = mode;
                break;
            case PlayerIndex.Four:
                _gamePad4.DeadZoneMode = mode;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(index), index, null);
        }
    }

    public bool IsGamePadConnected(PlayerIndex index)
    {
        return GamePadState(index).IsConnected;
    }

    public void Update(IGamePadStateProvider gamePadStateProvider, float elapsedTime)
    {
        _gamePad1.Update(gamePadStateProvider);
        _gamePad2.Update(gamePadStateProvider);
        _gamePad3.Update(gamePadStateProvider);
        _gamePad4.Update(gamePadStateProvider);
    }
}