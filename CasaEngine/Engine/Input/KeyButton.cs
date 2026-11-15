using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Input;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Engine.Input;

public class KeyButton : ISerializable
{
    public Keys Key { get; set; }
    public Buttons GamePadButton { get; set; }
    public MouseButtons MouseButton { get; set; }
    public InputDevices InputDevice { get; set; }

    public KeyButton()
    {
    }

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

    public KeyButton(MouseButtons mouseButton)
    {
        InputDevice = InputDevices.Mouse;
        Key = 0;
        GamePadButton = 0;
        MouseButton = mouseButton;
    }

    /// <summary>
    /// The key to read on the keyboard: <see cref="Key"/> names a position on a US QWERTY keyboard, and this is the key
    /// the active layout puts there (ADR-0096). <see cref="Key"/> itself is never changed, so a saved mapping keeps it.
    /// </summary>
    public Keys KeyOnLayout() => KeyboardLayout.FromQwertyPosition(Key);

    public bool IsPressed(KeyboardManager keyboardManager, MouseManager mouseManager, GamePad gamePad)
    {
        return InputDevice switch
        {
            InputDevices.NoDevice => false,
            InputDevices.Keyboard => keyboardManager.IsKeyPressed(KeyOnLayout()),
            InputDevices.GamePad => gamePad.ButtonPressed(GamePadButton),
            InputDevices.Mouse => mouseManager.ButtonPressed(MouseButton),
            _ => throw new ArgumentOutOfRangeException(nameof(InputDevice))
        };
    }

    public void Load(JObject element)
    {
        Key = element["key"].GetEnum<Keys>();
        GamePadButton = element["game_pad_button"].GetEnum<Buttons>();
        MouseButton = element["mouse_button"].GetEnum<MouseButtons>();
        InputDevice = element["input_device"].GetEnum<InputDevices>();
    }

}