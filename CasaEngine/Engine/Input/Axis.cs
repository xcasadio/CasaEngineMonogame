using CasaEngine.Core.Design;
using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Input;

public class Axis : Disposable
{
    public enum AxisBehaviors
    {
        DigitalInput,
        AnalogInput,
    }

    // Previous values. Used for smooth input calculations.
    private readonly float[] _previousValues = { 0, 0 };

    // The value of the axis.
    public float Value { get; private set; }

    // The value of the axis with no smoothing filtering applied.
    // The value will be in the range -1...1 for keyboard and joystick input.
    // Since input is not smoothed, keyboard input will always be either -1, 0 or 1. 
    public float ValueRaw { get; private set; }

    public string Name { get; set; }

    public AxisBehaviors AxisBehavior { get; set; }

    public AnalogAxis AnalogAxis { get; set; }

    public KeyButton NegativeKeyButton { get; set; }

    public KeyButton PositiveKeyButton { get; set; }

    public KeyButton AlternativeNegativeKeyButton { get; set; }

    public KeyButton AlternativePositiveKeyButton { get; set; }

    public float Gravity { get; set; }

    public float DeadZone { get; set; }

    public float Sensitivity { get; set; }

    public bool Snap { get; set; }

    public bool Invert { get; set; }

    public PlayerIndex GamePadNumber { get; set; }

    public bool TemporalSmoothing { get; set; }

    public Axis()
    {
        DeadZone = 0.2f;
        Sensitivity = 2;
        Gravity = 2;
    }

    internal void Update(KeyboardManager keyboardManager, MouseManager mouseManager, GamePad gamePad, float elapsedTime)
    {
        if (AxisBehavior == AxisBehaviors.DigitalInput)
        {
            // Check if the buttons were pressed.
            var positiveButtonPressed = PositiveKeyButton.IsPressed(keyboardManager, mouseManager, gamePad)
                                        || AlternativePositiveKeyButton.IsPressed(keyboardManager, mouseManager, gamePad);
            var negativeButtonPressed = NegativeKeyButton.IsPressed(keyboardManager, mouseManager, gamePad)
                                        || AlternativeNegativeKeyButton.IsPressed(keyboardManager, mouseManager, gamePad);

            // Raw value.
            ValueRaw = 0;
            if (positiveButtonPressed)
            {
                ValueRaw += 1;
            }

            if (negativeButtonPressed)
            {
                ValueRaw -= 1;
            }

            // Invert if necessary.
            if (Invert)
            {
                ValueRaw *= -1;
            }

            // Snap: If enabled, the axis value will reset to zero when pressing a button of the opposite direction.
            if (Snap)
            {
                if (Value > 0 && ValueRaw == -1 || Value < 0 && ValueRaw == 1) // Opposite direction
                {
                    Value = 0;
                }
            }

            // Gravity: Speed in units per second that the axis falls toward neutral when no buttons are pressed. 
            if (ValueRaw == 0)
            {
                if (Value > Gravity * elapsedTime)
                {
                    Value -= Gravity * elapsedTime;
                }
                else if (Value < -Gravity * elapsedTime)
                {
                    Value += Gravity * elapsedTime;
                }
                else
                {
                    Value = 0;
                }
            }
            else // Sensitivity: Speed in units per second that the the axis will move toward the target value. This is for digital devices only.
            {
                Value += Sensitivity * elapsedTime * ValueRaw;
                Value = MathHelper.Clamp(Value, -1, 1);
            }
        }
        else if (AxisBehavior == AxisBehaviors.AnalogInput)
        {
            ValueRaw = AnalogAxis switch
            {
                AnalogAxis.MouseX => mouseManager.DeltaX,
                AnalogAxis.MouseY => mouseManager.DeltaY,
                AnalogAxis.MouseWheel => mouseManager.WheelDelta,
                AnalogAxis.LeftStickX => gamePad.LeftStickX,
                AnalogAxis.LeftStickY => gamePad.LeftStickY,
                AnalogAxis.RightStickX => gamePad.RightStickX,
                AnalogAxis.RightStickY => gamePad.RightStickY,
                AnalogAxis.Triggers => -gamePad.LeftTrigger +
                                       gamePad.RightTrigger,
                _ => 0
            };

            // Invert if necessary.
            if (Invert)
            {
                ValueRaw *= -1;
            }

            // Mouse
            if (AnalogAxis == AnalogAxis.MouseX || AnalogAxis == AnalogAxis.MouseY || AnalogAxis == AnalogAxis.MouseWheel)
            {
                Value = ValueRaw * Sensitivity;
            }
            // GamePad
            else
            {
                var valueRawWithDeadZone = ValueRaw;
                // Dead Zone: Size of the analog dead zone. All analog device values within this range result map to neutral.
                if (ValueRaw > -DeadZone && ValueRaw < DeadZone)
                {
                    valueRawWithDeadZone = 0;
                }
                // For gamepad axes, sensitivity is an inverted exponential value that transforms the axis curve from linear to gamma (rawvalue ^ (1 / sensitivity)).
                Value = (float)Math.Pow(Math.Abs(valueRawWithDeadZone), 1 / Sensitivity);
                if (valueRawWithDeadZone < 0)
                {
                    Value *= -1;
                }
            }
        }

        if (TemporalSmoothing)
        {
            // Average the input of the current frame with the previous values.
            var storeValue = Value;
            Value = (_previousValues[0] + _previousValues[1] + Value) / (_previousValues.Length + 1);
            _previousValues[1] = _previousValues[0];
            _previousValues[0] = storeValue;
        }
    }
}