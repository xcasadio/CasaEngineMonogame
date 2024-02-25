using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Engine.Input;

public class KeyButton : ISerializable
{
    public Keys Key { get; set; }
    public Buttons GamePadButton { get; set; }
    public MouseButtons MouseButton { get; set; }
    public InputDevices InputDevice { get; set; }

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

    public bool Pressed(PlayerIndex playerIndex)
    {
        return InputDevice switch
        {
            InputDevices.NoDevice => false,
            InputDevices.Keyboard => Keyboard.KeyPressed(Key),
            InputDevices.GamePad => GamePad.GetByPlayerIndex(playerIndex).ButtonPressed(GamePadButton),
            InputDevices.Mouse => Mouse.ButtonPressed(MouseButton),
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

#if EDITOR

    public void Save(JObject node)
    {
        node.Add("key", Key.ToString());
        node.Add("game_pad_button", GamePadButton.ToString());
        node.Add("mouse_button", MouseButton.ToString());
        node.Add("input_device", InputDevice.ToString());
    }

#endif
}