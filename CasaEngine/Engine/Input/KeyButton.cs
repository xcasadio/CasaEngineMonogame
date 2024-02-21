using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Engine.Input;

public struct KeyButton
{
    public Keys Key;
    public Buttons GamePadButton;
    public Mouse.MouseButtons MouseButton;

    // 0 = no key or button, 1 = keyboard, 2 = gamepad, 3 = mouse.
    public InputDevices InputDevice;

    public KeyButton(Keys key)
    {
        InputDevice = InputDevices.Keyboard;
        Key = key;
        GamePadButton = 0;
        MouseButton = 0;
    }

    public KeyButton(Buttons gamePadButton)
    {
        InputDevice = InputDevices.GamePad;
        Key = 0;
        GamePadButton = gamePadButton;
        MouseButton = 0;
    }

    public KeyButton(Mouse.MouseButtons mouseButton)
    {
        InputDevice = InputDevices.Mouse;
        Key = 0;
        GamePadButton = 0;
        MouseButton = mouseButton;
    }

    public bool Pressed(int gamePadNumber)
    {
        if (InputDevice == InputDevices.NoDevice)
        {
            return false;
        }

        if (InputDevice == InputDevices.Keyboard)
        {
            return Keyboard.KeyPressed(Key);
        }

        if (InputDevice == InputDevices.GamePad)
        {
            if (gamePadNumber == 1)
            {
                return GamePad.PlayerOne.ButtonPressed(GamePadButton);
            }

            if (gamePadNumber == 2)
            {
                return GamePad.PlayerTwo.ButtonPressed(GamePadButton);
            }

            if (gamePadNumber == 3)
            {
                return GamePad.PlayerThree.ButtonPressed(GamePadButton);
            }

            if (gamePadNumber == 4)
            {
                return GamePad.PlayerFour.ButtonPressed(GamePadButton);
            }

            // if (gamepadNumber == 0) // All gamepads at the same time.
            return GamePad.PlayerOne.ButtonPressed(GamePadButton) || GamePad.PlayerTwo.ButtonPressed(GamePadButton) ||
                   GamePad.PlayerThree.ButtonPressed(GamePadButton) || GamePad.PlayerFour.ButtonPressed(GamePadButton);
        }

        // if (keyButton.Device == Devices.Mouse)
        return Mouse.ButtonPressed(MouseButton);
    }
}