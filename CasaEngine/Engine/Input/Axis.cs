
/*
Copyright (c) 2008-2012, Laboratorio de Investigación y Desarrollo en Visualización y Computación Gráfica - 
                         Departamento de Ciencias e Ingeniería de la Computación - Universidad Nacional del Sur.
All rights reserved.
Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

•	Redistributions of source code must retain the above copyright, this list of conditions and the following disclaimer.

•	Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer
    in the documentation and/or other materials provided with the distribution.

•	Neither the name of the Universidad Nacional del Sur nor the names of its contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS ''AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR
CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,
EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

-----------------------------------------------------------------------------------------------------------------------------------------------
Author: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/


using CasaEngine.Core.Design;
using Microsoft.Xna.Framework;
//using XNAFinalEngine.EngineCore;

namespace CasaEngine.Engine.Input;

public class Axis : Disposable
{


    public enum AxisBehaviors
    {
        DigitalInput,
        AnalogInput,
    } // AxisBehaviors



    // The value of the axis.
    private float _value;

    // The value of the axis with no smoothing filtering applied.
    // The value will be in the range -1...1 for keyboard and joystick input.
    // Since input is not smoothed, keyboard input will always be either -1, 0 or 1. 
    private float _valueRaw;

    // Previous values. Used for smooth input calculations.
    private readonly float[] _previousValues = new float[] { 0, 0 };



    public static List<Axis> Axes { get; set; }

    public string Name { get; set; }

    public AxisBehaviors AxisBehavior { get; set; }

    public AnalogAxes AnalogAxis { get; set; }

    public KeyButton NegativeKeyButton { get; set; }

    public KeyButton PositiveKeyButton { get; set; }

    public KeyButton AlternativeNegativeKeyButton { get; set; }

    public KeyButton AlternativePositiveKeyButton { get; set; }

    public float Gravity { get; set; }

    public float DeadZone { get; set; }

    public float Sensitivity { get; set; }

    public bool Snap { get; set; }

    public bool Invert { get; set; }

    public int GamePadNumber { get; set; }

    public bool TemporalSmoothing { get; set; }



    public Axis()
    {
        DeadZone = 0.2f;
        Sensitivity = 2;
        Gravity = 2;
        Axes.Add(this);
    } // Axis

    static Axis()
    {
        Axes = new List<Axis>();
    } // Axis



    protected override void DisposeManagedResources()
    {
        Axes.Remove(this);
    } // DisposeManagedResources



    internal void Update(float elapsedTime)
    {


        if (AxisBehavior == AxisBehaviors.DigitalInput)
        {
            // Check if the buttons were pressed.
            var positiveButtonPressed = PositiveKeyButton.Pressed(GamePadNumber) || AlternativePositiveKeyButton.Pressed(GamePadNumber);
            var negativeButtonPressed = NegativeKeyButton.Pressed(GamePadNumber) || AlternativeNegativeKeyButton.Pressed(GamePadNumber);

            // Raw value.
            _valueRaw = 0;
            if (positiveButtonPressed)
            {
                _valueRaw += 1;
            }

            if (negativeButtonPressed)
            {
                _valueRaw -= 1;
            }

            // Invert if necessary.
            if (Invert)
            {
                _valueRaw *= -1;
            }

            // Snap: If enabled, the axis value will reset to zero when pressing a button of the opposite direction.
            if (Snap)
            {
                if (_value > 0 && _valueRaw == -1 || _value < 0 && _valueRaw == 1) // Opposite direction
                {
                    _value = 0;
                }
            }

            // Gravity: Speed in units per second that the axis falls toward neutral when no buttons are pressed. 
            if (_valueRaw == 0)
            {
                if (_value > Gravity * elapsedTime)
                {
                    _value = _value - Gravity * elapsedTime;
                }
                else if (_value < -Gravity * elapsedTime)
                {
                    _value = _value + Gravity * elapsedTime;
                }
                else
                {
                    _value = 0;
                }
            }
            else // Sensitivity: Speed in units per second that the the axis will move toward the target value. This is for digital devices only.
            {
                _value = _value + Sensitivity * elapsedTime * _valueRaw;
                _value = MathHelper.Clamp(_value, -1, 1);
            }
        }



        else if (AxisBehavior == AxisBehaviors.AnalogInput)
        {
            _valueRaw = 0;


            if (AnalogAxis == AnalogAxes.MouseX)
            {
                _valueRaw = Mouse.DeltaX;
            }
            else if (AnalogAxis == AnalogAxes.MouseY)
            {
                _valueRaw = Mouse.DeltaY;
            }
            else if (AnalogAxis == AnalogAxes.MouseWheel)
            {
                _valueRaw = Mouse.WheelDelta;
            }



            else if (AnalogAxis == AnalogAxes.LeftStickX)
            {
                if (GamePadNumber > 0 && GamePadNumber < 5)
                {
                    _valueRaw = GamePad.Player(GamePadNumber - 1).LeftStickX;
                }
                else
                {
                    for (var i = 0; i < 4; i++)
                    {
                        if (Math.Abs(_valueRaw) < Math.Abs(GamePad.Player(i).LeftStickX))
                        {
                            _valueRaw = GamePad.Player(i).LeftStickX;
                        }
                    }
                }
            }
            else if (AnalogAxis == AnalogAxes.LeftStickY)
            {
                if (GamePadNumber > 0 && GamePadNumber < 5)
                {
                    _valueRaw = GamePad.Player(GamePadNumber - 1).LeftStickY;
                }
                else
                {
                    for (var i = 0; i < 4; i++)
                    {
                        if (Math.Abs(_valueRaw) < Math.Abs(GamePad.Player(i).LeftStickY))
                        {
                            _valueRaw = GamePad.Player(i).LeftStickY;
                        }
                    }
                }
            }
            else if (AnalogAxis == AnalogAxes.RightStickX)
            {
                if (GamePadNumber > 0 && GamePadNumber < 5)
                {
                    _valueRaw = GamePad.Player(GamePadNumber - 1).RightStickX;
                }
                else
                {
                    for (var i = 0; i < 4; i++)
                    {
                        if (Math.Abs(_valueRaw) < Math.Abs(GamePad.Player(i).RightStickX))
                        {
                            _valueRaw = GamePad.Player(i).RightStickX;
                        }
                    }
                }
            }
            else if (AnalogAxis == AnalogAxes.RightStickY)
            {
                if (GamePadNumber > 0 && GamePadNumber < 5)
                {
                    _valueRaw = GamePad.Player(GamePadNumber - 1).RightStickY;
                }
                else
                {
                    for (var i = 0; i < 4; i++)
                    {
                        if (Math.Abs(_valueRaw) < Math.Abs(GamePad.Player(i).RightStickY))
                        {
                            _valueRaw = GamePad.Player(i).RightStickY;
                        }
                    }
                }
            }
            else if (AnalogAxis == AnalogAxes.Triggers)
            {
                if (GamePadNumber > 0 && GamePadNumber < 5)
                {
                    _valueRaw = -GamePad.Player(GamePadNumber - 1).LeftTrigger + GamePad.Player(GamePadNumber - 1).RightTrigger;
                }
                else
                {
                    for (var i = 0; i < 4; i++)
                    {
                        if (Math.Abs(_valueRaw) < Math.Abs(-GamePad.Player(i).LeftTrigger + GamePad.Player(i).RightTrigger))
                        {
                            _valueRaw = -GamePad.Player(i).LeftTrigger + GamePad.Player(i).RightTrigger;
                        }
                    }
                }
            }


            // Invert if necessary.
            if (Invert)
            {
                _valueRaw *= -1;
            }

            // Mouse
            if (AnalogAxis == AnalogAxes.MouseX || AnalogAxis == AnalogAxes.MouseY || AnalogAxis == AnalogAxes.MouseWheel)
            {
                _value = _valueRaw * Sensitivity;
            }
            // GamePad
            else
            {
                var valueRawWithDeadZone = _valueRaw;
                // Dead Zone: Size of the analog dead zone. All analog device values within this range result map to neutral.
                if (_valueRaw > -DeadZone && _valueRaw < DeadZone)
                {
                    valueRawWithDeadZone = 0;
                }
                // For gamepad axes, sensitivity is an inverted exponential value that transforms the axis curve from linear to gamma (rawvalue ^ (1 / sensitivity)).
                _value = (float)Math.Pow(Math.Abs(valueRawWithDeadZone), 1 / Sensitivity);
                if (valueRawWithDeadZone < 0)
                {
                    _value *= -1;
                }
            }
        }


        if (TemporalSmoothing)
        {
            // Average the input of the current frame with the previous values.
            var storeValue = _value;
            _value = (_previousValues[0] + _previousValues[1] + _value) / (_previousValues.Length + 1);
            _previousValues[1] = _previousValues[0];
            _previousValues[0] = storeValue;
        }

    } // Update



    public static float Value(string axisName)
    {
        float maxValue = 0;
        var foundAxis = false;
        foreach (var axis in Axes)
        {
            if (axis.Name == axisName && Math.Abs(axis._value) >= Math.Abs(maxValue))
            {
                foundAxis = true;
                maxValue = axis._value;
            }
        }
        if (!foundAxis)
        {
            throw new InvalidOperationException("Input: the axis named " + axisName + " does not exist.");
        }

        return maxValue;
    } // Value

    public static float ValueRaw(string axisName)
    {
        float maxValue = 0;
        var foundAxis = false;
        foreach (var axis in Axes)
        {
            if (axis.Name == axisName && Math.Abs(axis._valueRaw) >= Math.Abs(maxValue))
            {
                foundAxis = true;
                maxValue = axis._valueRaw;
            }
        }
        if (!foundAxis)
        {
            throw new InvalidOperationException("Input: the axis named " + axisName + " does not exist.");
        }

        return maxValue;
    } // ValueRaw


} // Axis
  // XNAFinalEngine.Input