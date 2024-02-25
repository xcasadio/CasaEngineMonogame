using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Entities;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Engine.Input;

public class Button : ObjectBase
{
    public static List<Button> Buttons { get; } = new();

    private bool _pressed;
    private bool _pressedPreviousFrame;

    public PlayerIndex GamePadNumber { get; set; }
    public ButtonBehaviors ButtonBehavior { get; set; }
    public AnalogAxis AnalogAxis { get; set; }
    public KeyButton KeyButton { get; set; }
    public KeyButton AlternativeKeyButton { get; set; }
    public bool Invert { get; set; }
    public float DeadZone { get; set; }
    public float Value { get; private set; }

    public Button()
    {
        DeadZone = 0.75f;
        Buttons.Add(this);
    }

    public void Dispose()
    {
        Buttons.Remove(this);
    }

    internal void Update()
    {
        _pressedPreviousFrame = _pressed;

        if (ButtonBehavior == ButtonBehaviors.DigitalInput)
        {
            _pressed = KeyButton.Pressed(GamePadNumber) || AlternativeKeyButton.Pressed(GamePadNumber);
        }
        else if (ButtonBehavior == ButtonBehaviors.AnalogInput)
        {
            float valueRaw = AnalogAxis switch
            {
                AnalogAxis.MouseX => Mouse.DeltaX,
                AnalogAxis.MouseY => Mouse.DeltaY,
                AnalogAxis.MouseWheel => Mouse.WheelDelta,
                AnalogAxis.LeftStickX => GamePad.GetByPlayerIndex(GamePadNumber).LeftStickX,
                AnalogAxis.LeftStickY => GamePad.GetByPlayerIndex(GamePadNumber).LeftStickY,
                AnalogAxis.RightStickX => GamePad.GetByPlayerIndex(GamePadNumber).RightStickX,
                AnalogAxis.RightStickY => GamePad.GetByPlayerIndex(GamePadNumber).RightStickY,
                AnalogAxis.Triggers => -GamePad.GetByPlayerIndex(GamePadNumber).LeftTrigger +
                                       GamePad.GetByPlayerIndex(GamePadNumber).RightTrigger,
                _ => 0
            };

            if (Invert)
            {
                valueRaw *= -1;
            }

            Value = valueRaw;
            _pressed = valueRaw > DeadZone;
        }
    }

    public static bool Pressed(string buttonName)
    {
        var foundValue = false;
        var foundAxis = false;

        foreach (var button in Buttons)
        {
            if (button.Name == buttonName)
            {
                foundAxis = true;
                foundValue = foundValue || button._pressed;
            }
        }

        if (!foundAxis)
        {
            throw new InvalidOperationException("Input: the button named " + buttonName + " does not exist.");
        }

        return foundValue;
    }

    public static bool JustPressed(string buttonName)
    {
        var foundPressed = false;
        var foundPressedPreviousFrame = false;
        var foundAxis = false;

        foreach (var axis in Buttons)
        {
            if (axis.Name == buttonName)
            {
                foundAxis = true;
                foundPressed = foundPressed || axis._pressed;
                foundPressedPreviousFrame = foundPressedPreviousFrame || axis._pressedPreviousFrame;
            }
        }

        if (!foundAxis)
        {
            throw new InvalidOperationException("Input: the button named " + buttonName + " does not exist.");
        }

        return foundPressed && !foundPressedPreviousFrame;
    }

    public static float GetValue(string buttonName)
    {
        foreach (var axis in Buttons)
        {
            if (axis.Name == buttonName)
            {
                return axis.Value;
            }
        }

        throw new InvalidOperationException("Input: the button named " + buttonName + " does not exist.");
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