using CasaEngine.Core.Design;

namespace CasaEngine.Engine.Input;

public class Button : Disposable
{
    public enum ButtonBehaviors
    {
        DigitalInput,
        AnalogInput
    }

    public static List<Button> Buttons { get; } = new();

    private bool _pressed;
    private bool _pressedPreviousFrame;

    public string Name { get; set; }
    public float Value { get; private set; }

    public ButtonBehaviors ButtonBehavior { get; set; }

    public AnalogAxes AnalogAxis { get; set; }

    public KeyButton KeyButton { get; set; }

    public KeyButton AlternativeKeyButton { get; set; }

    public bool Invert { get; set; }

    public float DeadZone { get; set; }

    public int GamePadNumber { get; set; }

    public Button()
    {
        DeadZone = 0.75f;
        Buttons.Add(this);
    }

    protected override void DisposeManagedResources()
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
            float valueRaw = 0;

            if (AnalogAxis == AnalogAxes.MouseX)
            {
                valueRaw = Mouse.DeltaX;
            }
            else if (AnalogAxis == AnalogAxes.MouseY)
            {
                valueRaw = Mouse.DeltaY;
            }
            else if (AnalogAxis == AnalogAxes.MouseWheel)
            {
                valueRaw = Mouse.WheelDelta;
            }
            else if (AnalogAxis == AnalogAxes.LeftStickX)
            {
                if (GamePadNumber > 0 && GamePadNumber < 5)
                {
                    valueRaw = GamePad.Player(GamePadNumber - 1).LeftStickX;
                }
                else
                {
                    for (var i = 0; i < 4; i++)
                    {
                        if (Math.Abs(valueRaw) < Math.Abs(GamePad.Player(i).LeftStickX))
                        {
                            valueRaw = GamePad.Player(i).LeftStickX;
                        }
                    }
                }
            }
            else if (AnalogAxis == AnalogAxes.LeftStickY)
            {
                if (GamePadNumber > 0 && GamePadNumber < 5)
                {
                    valueRaw = GamePad.Player(GamePadNumber - 1).LeftStickY;
                }
                else
                {
                    for (var i = 0; i < 4; i++)
                    {
                        if (Math.Abs(valueRaw) < Math.Abs(GamePad.Player(i).LeftStickY))
                        {
                            valueRaw = GamePad.Player(i).LeftStickY;
                        }
                    }
                }
            }
            else if (AnalogAxis == AnalogAxes.RightStickX)
            {
                if (GamePadNumber > 0 && GamePadNumber < 5)
                {
                    valueRaw = GamePad.Player(GamePadNumber - 1).RightStickX;
                }
                else
                {
                    for (var i = 0; i < 4; i++)
                    {
                        if (Math.Abs(valueRaw) < Math.Abs(GamePad.Player(i).RightStickX))
                        {
                            valueRaw = GamePad.Player(i).RightStickX;
                        }
                    }
                }
            }
            else if (AnalogAxis == AnalogAxes.RightStickY)
            {
                if (GamePadNumber > 0 && GamePadNumber < 5)
                {
                    valueRaw = GamePad.Player(GamePadNumber - 1).RightStickY;
                }
                else
                {
                    for (var i = 0; i < 4; i++)
                    {
                        if (Math.Abs(valueRaw) < Math.Abs(GamePad.Player(i).RightStickY))
                        {
                            valueRaw = GamePad.Player(i).RightStickY;
                        }
                    }
                }
            }
            else if (AnalogAxis == AnalogAxes.Triggers)
            {
                if (GamePadNumber > 0 && GamePadNumber < 5)
                {
                    valueRaw = -GamePad.Player(GamePadNumber - 1).LeftTrigger +
                               GamePad.Player(GamePadNumber - 1).RightTrigger;
                }
                else
                {
                    for (var i = 0; i < 4; i++)
                    {
                        if (Math.Abs(valueRaw) <
                            Math.Abs(-GamePad.Player(i).LeftTrigger + GamePad.Player(i).RightTrigger))
                        {
                            valueRaw = -GamePad.Player(i).LeftTrigger + GamePad.Player(i).RightTrigger;
                        }
                    }
                }
            }

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
}