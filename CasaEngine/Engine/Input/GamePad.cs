using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Engine.Input;

public class GamePad
{
    // Gamepad state, set every frame in the update method.
    private GamePadState _currentState, _previousState;

    // The id number of the gamepad.
    private readonly PlayerIndex _playerIndex;

    public GamePadState CurrentState => _currentState;

    public GamePadState PreviousState => _previousState;

    public bool IsConnected => _currentState.IsConnected;

    public GamePadCapabilities Capabilities => Microsoft.Xna.Framework.Input.GamePad.GetCapabilities(_playerIndex);

    public bool Iddle => _currentState.PacketNumber == _previousState.PacketNumber;

    public GamePadDeadZone DeadZone { get; set; }

    public bool StartPressed => _currentState.Buttons.Start == ButtonState.Pressed;

    public bool BackPressed => _currentState.Buttons.Back == ButtonState.Pressed;

    public bool BigButtonPressed => _currentState.Buttons.BigButton == ButtonState.Pressed;

    public bool StartJustPressed => _currentState.Buttons.Start == ButtonState.Pressed && _previousState.Buttons.Start == ButtonState.Released;

    public bool BackJustPressed => _currentState.Buttons.Back == ButtonState.Pressed && _previousState.Buttons.Back == ButtonState.Released;

    public bool BigButtonJustPressed => _currentState.Buttons.BigButton == ButtonState.Pressed && _previousState.Buttons.BigButton == ButtonState.Released;

    public bool APressed => _currentState.Buttons.A == ButtonState.Pressed;

    public bool BPressed => _currentState.Buttons.B == ButtonState.Pressed;

    public bool XPressed => _currentState.Buttons.X == ButtonState.Pressed;

    public bool YPressed => _currentState.Buttons.Y == ButtonState.Pressed;

    public bool AJustPressed => _currentState.Buttons.A == ButtonState.Pressed && _previousState.Buttons.A == ButtonState.Released;

    public bool BJustPressed => _currentState.Buttons.B == ButtonState.Pressed && _previousState.Buttons.B == ButtonState.Released;

    public bool XJustPressed => _currentState.Buttons.X == ButtonState.Pressed && _previousState.Buttons.X == ButtonState.Released;

    public bool YJustPressed => _currentState.Buttons.Y == ButtonState.Pressed && _previousState.Buttons.Y == ButtonState.Released;

    public bool DPadLeftPressed => _currentState.DPad.Left == ButtonState.Pressed;

    public bool DPadRightPressed => _currentState.DPad.Right == ButtonState.Pressed;

    public bool DPadUpPressed => _currentState.DPad.Up == ButtonState.Pressed;

    public bool DPadDownPressed => _currentState.DPad.Down == ButtonState.Pressed;

    public bool DPadLeftJustPressed => _currentState.DPad.Left == ButtonState.Pressed && _previousState.DPad.Left == ButtonState.Released;

    public bool DPadRightJustPressed => _currentState.DPad.Right == ButtonState.Pressed && _previousState.DPad.Right == ButtonState.Released;

    public bool DPadUpJustPressed => _currentState.DPad.Up == ButtonState.Pressed && _previousState.DPad.Up == ButtonState.Released;

    public bool DPadDownJustPressed => _currentState.DPad.Down == ButtonState.Pressed && _previousState.DPad.Down == ButtonState.Released;

    public float LeftStickX => _currentState.ThumbSticks.Left.X;

    public float LeftStickY => _currentState.ThumbSticks.Left.Y;

    public float RightStickX => _currentState.ThumbSticks.Right.X;

    public float RightStickY => _currentState.ThumbSticks.Right.Y;

    public bool LeftStickPressed => _currentState.Buttons.LeftStick == ButtonState.Pressed;

    public bool LeftStickJustPressed => _currentState.Buttons.LeftStick == ButtonState.Pressed && _previousState.Buttons.LeftStick == ButtonState.Released;

    public bool RightStickPressed => _currentState.Buttons.RightStick == ButtonState.Pressed;

    public bool RightStickJustPressed => _currentState.Buttons.RightStick == ButtonState.Pressed && _previousState.Buttons.RightStick == ButtonState.Released;

    public bool LeftButtonPressed => _currentState.Buttons.LeftShoulder == ButtonState.Pressed;

    public bool LeftButtonJustPressed => _currentState.Buttons.LeftShoulder == ButtonState.Pressed && _previousState.Buttons.LeftShoulder == ButtonState.Released;

    public bool RightButtonPressed => _currentState.Buttons.RightShoulder == ButtonState.Pressed;

    public bool RightButtonJustPressed => _currentState.Buttons.RightShoulder == ButtonState.Pressed && _previousState.Buttons.RightShoulder == ButtonState.Released;

    public float LeftTrigger => _currentState.Triggers.Left;

    public float RightTrigger => _currentState.Triggers.Right;

    private GamePad(PlayerIndex playerIndex)
    {
        _playerIndex = playerIndex;
        _currentState = Microsoft.Xna.Framework.Input.GamePad.GetState(_playerIndex);
        DeadZone = GamePadDeadZone.IndependentAxes;
    }

    public bool ButtonJustPressed(Buttons button) { return _currentState.IsButtonDown(button) && !_previousState.IsButtonDown(button); }

    public bool ButtonPressed(Buttons button) { return _currentState.IsButtonDown(button); }

    public void SetVibration(float leftMotor, float rightMotor)
    {
        Microsoft.Xna.Framework.Input.GamePad.SetVibration(_playerIndex, leftMotor, rightMotor);
    }

    internal void Update()
    {
        _previousState = _currentState;
        _currentState = Microsoft.Xna.Framework.Input.GamePad.GetState(_playerIndex, DeadZone);
    }

    // The four possible gamepads.
    private static readonly GamePad PlayerOneGamePad = new(PlayerIndex.One),
        PlayerTwoGamePad = new(PlayerIndex.Two),
        PlayerThreeGamePad = new(PlayerIndex.Three),
        PlayerFourGamePad = new(PlayerIndex.Four);

    public static GamePad PlayerOne => PlayerOneGamePad;

    public static GamePad PlayerTwo => PlayerTwoGamePad;

    public static GamePad PlayerThree => PlayerThreeGamePad;

    public static GamePad PlayerFour => PlayerFourGamePad;

    public static GamePad Player(int playerIndex)
    {
        switch (playerIndex)
        {
            case 0: return PlayerOneGamePad;
            case 1: return PlayerTwoGamePad;
            case 2: return PlayerThreeGamePad;
            case 3: return PlayerFourGamePad;
            default: throw new ArgumentOutOfRangeException(nameof(playerIndex), "GamePad: The number has to be between 0 and 3.");
        }
    }
}