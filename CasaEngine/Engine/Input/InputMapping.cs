using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Entities;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Engine.Input;

public class InputMapping : ObjectBase
{
    public bool PressedPreviousFrame { get; private set; }
    public bool Pressed { get; private set; }

    public PlayerIndex GamePadNumber { get; set; }
    public ButtonBehaviors ButtonBehavior { get; set; }
    public AnalogAxis AnalogAxis { get; set; }
    public KeyButton KeyButton { get; set; }
    public KeyButton AlternativeKeyButton { get; set; }
    public bool Invert { get; set; }
    public float DeadZone { get; set; }
    public float Value { get; private set; }

    public InputMapping()
    {
        DeadZone = 0.75f;
    }

    internal void Update(KeyboardManager keyboardManager, MouseManager mouseManager, GamePadManager gamePadManager)
    {
        PressedPreviousFrame = Pressed;

        if (ButtonBehavior == ButtonBehaviors.DigitalInput)
        {
            Pressed = KeyButton.IsPressed(keyboardManager, mouseManager, gamePadManager.GetGamePad(GamePadNumber))
                      || AlternativeKeyButton.IsPressed(keyboardManager, mouseManager, gamePadManager.GetGamePad(GamePadNumber));
        }
        else if (ButtonBehavior == ButtonBehaviors.AnalogInput)
        {
            float valueRaw = AnalogAxis switch
            {
                AnalogAxis.MouseX => mouseManager.DeltaX,
                AnalogAxis.MouseY => mouseManager.DeltaY,
                AnalogAxis.MouseWheel => mouseManager.WheelDelta,
                AnalogAxis.LeftStickX => gamePadManager.GetGamePad(GamePadNumber).LeftStickX,
                AnalogAxis.LeftStickY => gamePadManager.GetGamePad(GamePadNumber).LeftStickY,
                AnalogAxis.RightStickX => gamePadManager.GetGamePad(GamePadNumber).RightStickX,
                AnalogAxis.RightStickY => gamePadManager.GetGamePad(GamePadNumber).RightStickY,
                AnalogAxis.Triggers => -gamePadManager.GetGamePad(GamePadNumber).LeftTrigger +
                                       gamePadManager.GetGamePad(GamePadNumber).RightTrigger,
                _ => 0
            };

            if (Invert)
            {
                valueRaw *= -1;
            }

            Value = valueRaw;
            Pressed = valueRaw >= DeadZone;
        }
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        GamePadNumber = element["game_pad_number"].GetEnum<PlayerIndex>();
        ButtonBehavior = element["button_behavior"].GetEnum<ButtonBehaviors>();
        AnalogAxis = element["analog_axis"].GetEnum<AnalogAxis>();
        Invert = element["invert"].GetBoolean();
        DeadZone = element["dead_zone"].GetSingle();

        KeyButton = new();
        KeyButton.Load((JObject)element["key_button"]);

        AlternativeKeyButton = new();
        AlternativeKeyButton.Load((JObject)element["alternative_key_button"]);
    }

#if EDITOR

    public override void Save(JObject node)
    {
        base.Save(node);

        node.Add("game_pad_number", GamePadNumber.ToString());
        node.Add("button_behavior", ButtonBehavior.ToString());
        node.Add("analog_axis", AnalogAxis.ToString());
        node.Add("invert", Invert);
        node.Add("dead_zone", DeadZone);

        var keyNode = new JObject();
        KeyButton.Save(keyNode);
        node.Add("key_button", keyNode);

        keyNode = new JObject();
        AlternativeKeyButton.Save(keyNode);
        node.Add("alternative_key_button", keyNode);
    }

#endif
}