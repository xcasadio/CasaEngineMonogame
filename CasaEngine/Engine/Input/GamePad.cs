using CasaEngine.Engine.Input.InputDeviceStateProviders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Engine.Input;

public class GamePad
{
    private GamePadState _currentState, _previousState;

    private readonly PlayerIndex _playerIndex;

    public GamePadState CurrentState => _currentState;

    public bool IsConnected => _currentState.IsConnected;

    public GamePadCapabilities Capabilities => Microsoft.Xna.Framework.Input.GamePad.GetCapabilities(_playerIndex);

    public bool Idle => _currentState.PacketNumber == _previousState.PacketNumber;

    public GamePadDeadZone DeadZoneMode { get; set; }

    public bool StartPressed => _currentState.Buttons.Start == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

    public bool BackPressed => _currentState.Buttons.Back == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

    public bool BigButtonPressed => _currentState.Buttons.BigButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

    public bool StartJustPressed => _currentState.Buttons.Start == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousState.Buttons.Start == Microsoft.Xna.Framework.Input.ButtonState.Released;

    public bool BackJustPressed => _currentState.Buttons.Back == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousState.Buttons.Back == Microsoft.Xna.Framework.Input.ButtonState.Released;

    public bool BigButtonJustPressed => _currentState.Buttons.BigButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousState.Buttons.BigButton == Microsoft.Xna.Framework.Input.ButtonState.Released;

    public bool APressed => _currentState.Buttons.A == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

    public bool BPressed => _currentState.Buttons.B == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

    public bool XPressed => _currentState.Buttons.X == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

    public bool YPressed => _currentState.Buttons.Y == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

    public bool AJustPressed => _currentState.Buttons.A == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousState.Buttons.A == Microsoft.Xna.Framework.Input.ButtonState.Released;

    public bool BJustPressed => _currentState.Buttons.B == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousState.Buttons.B == Microsoft.Xna.Framework.Input.ButtonState.Released;

    public bool XJustPressed => _currentState.Buttons.X == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousState.Buttons.X == Microsoft.Xna.Framework.Input.ButtonState.Released;

    public bool YJustPressed => _currentState.Buttons.Y == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousState.Buttons.Y == Microsoft.Xna.Framework.Input.ButtonState.Released;

    public bool DPadLeftPressed => _currentState.DPad.Left == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

    public bool DPadRightPressed => _currentState.DPad.Right == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

    public bool DPadUpPressed => _currentState.DPad.Up == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

    public bool DPadDownPressed => _currentState.DPad.Down == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

    public bool DPadLeftJustPressed => _currentState.DPad.Left == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousState.DPad.Left == Microsoft.Xna.Framework.Input.ButtonState.Released;

    public bool DPadRightJustPressed => _currentState.DPad.Right == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousState.DPad.Right == Microsoft.Xna.Framework.Input.ButtonState.Released;

    public bool DPadUpJustPressed => _currentState.DPad.Up == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousState.DPad.Up == Microsoft.Xna.Framework.Input.ButtonState.Released;

    public bool DPadDownJustPressed => _currentState.DPad.Down == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousState.DPad.Down == Microsoft.Xna.Framework.Input.ButtonState.Released;

    public float LeftStickX => _currentState.ThumbSticks.Left.X;

    public float LeftStickY => _currentState.ThumbSticks.Left.Y;

    public float RightStickX => _currentState.ThumbSticks.Right.X;

    public float RightStickY => _currentState.ThumbSticks.Right.Y;

    public bool LeftStickPressed => _currentState.Buttons.LeftStick == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

    public bool LeftStickJustPressed => _currentState.Buttons.LeftStick == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousState.Buttons.LeftStick == Microsoft.Xna.Framework.Input.ButtonState.Released;

    public bool RightStickPressed => _currentState.Buttons.RightStick == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

    public bool RightStickJustPressed => _currentState.Buttons.RightStick == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousState.Buttons.RightStick == Microsoft.Xna.Framework.Input.ButtonState.Released;

    public bool LeftButtonPressed => _currentState.Buttons.LeftShoulder == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

    public bool LeftButtonJustPressed => _currentState.Buttons.LeftShoulder == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousState.Buttons.LeftShoulder == Microsoft.Xna.Framework.Input.ButtonState.Released;

    public bool RightButtonPressed => _currentState.Buttons.RightShoulder == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

    public bool RightButtonJustPressed => _currentState.Buttons.RightShoulder == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousState.Buttons.RightShoulder == Microsoft.Xna.Framework.Input.ButtonState.Released;

    public float LeftTrigger => _currentState.Triggers.Left;

    public float RightTrigger => _currentState.Triggers.Right;

    public GamePad(PlayerIndex playerIndex)
    {
        _playerIndex = playerIndex;
        DeadZoneMode = GamePadDeadZone.IndependentAxes;
    }

    public bool ButtonJustPressed(Buttons button) { return _currentState.IsButtonDown(button) && !_previousState.IsButtonDown(button); }

    public bool ButtonPressed(Buttons button) { return _currentState.IsButtonDown(button); }

    public void SetVibration(float leftMotor, float rightMotor)
    {
        Microsoft.Xna.Framework.Input.GamePad.SetVibration(_playerIndex, leftMotor, rightMotor);
    }

    internal void Update(IGamePadStateProvider gamePadStateProvider)
    {
        _previousState = _currentState;
        _currentState = gamePadStateProvider.GetState(_playerIndex, DeadZoneMode);
    }
}