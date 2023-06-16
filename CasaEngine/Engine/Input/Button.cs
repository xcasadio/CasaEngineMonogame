
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

namespace CasaEngine.Engine.Input;

public class Button : Disposable
{


    public enum ButtonBehaviors
    {
        DigitalInput,
        AnalogInput,
    } // AxisBehaviors



    // Indicates if the virtual buttons was pressed.
    private bool _pressed;

    // Indicates if the virtual buttons was pressed in this frame but not in the previous.
    private bool _pressedPreviousFrame;



    public static List<Button> Buttons { get; set; }

    public string Name { get; set; }

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
    } // Button

    static Button()
    {
        Buttons = new List<Button>();
    } // Button



    protected override void DisposeManagedResources()
    {
        Buttons.Remove(this);
    } // DisposeManagedResources



    internal void Update()
    {
        _pressedPreviousFrame = _pressed;


        if (ButtonBehavior == ButtonBehaviors.DigitalInput)
        {
            // Check if the buttons were pressed.
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
                    valueRaw = -GamePad.Player(GamePadNumber - 1).LeftTrigger + GamePad.Player(GamePadNumber - 1).RightTrigger;
                }
                else
                {
                    for (var i = 0; i < 4; i++)
                    {
                        if (Math.Abs(valueRaw) < Math.Abs(-GamePad.Player(i).LeftTrigger + GamePad.Player(i).RightTrigger))
                        {
                            valueRaw = -GamePad.Player(i).LeftTrigger + GamePad.Player(i).RightTrigger;
                        }
                    }
                }
            }


            // Invert if necessary.
            if (Invert)
            {
                valueRaw *= -1;
            }

            _pressed = valueRaw > DeadZone;
        }


    } // Update



    public static bool Pressed(string buttonName)
    {
        var foundValue = false;
        var foundAxis = false;
        foreach (var axis in Buttons)
        {
            if (axis.Name == buttonName)
            {
                foundAxis = true;
                foundValue = foundValue || axis._pressed;
            }
        }
        if (!foundAxis)
        {
            throw new InvalidOperationException("Input: the button named " + buttonName + " does not exist.");
        }

        return foundValue;
    } // Pressed

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
    } // JustPressed


} // Button
  // XNAFinalEngine.Input