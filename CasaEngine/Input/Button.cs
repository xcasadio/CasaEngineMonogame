
#region License
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
#endregion

#region Using directives
using System;
using System.Collections.Generic;
using XNAFinalEngine.Helpers;
#endregion

namespace XNAFinalEngine.Input
{

	/// <summary>
    /// Virtual button serve two purposes:
    /// * They allow you to reference your inputs by button name in scripting.
    /// * They allow the players of your game to customize the controls to their liking.
	/// </summary>
	public class Button : Disposable
    {

        #region Enumerates

        /// <summary>
        /// Indicates the behavior of the axis.
        /// </summary>
        public enum ButtonBehaviors
        {
            /// <summary>
            /// Use Digital Input for any kind of buttons or keys.
            /// </summary>
            DigitalInput,
            /// <summary>
            /// Use Analog Input for mouse delta, scrollwheels and gamepad sticks and triggers.
            /// </summary>
            AnalogInput,
        } // AxisBehaviors

        #endregion

        #region Variables

	    // Indicates if the virtual buttons was pressed.
	    private bool pressed;
        
	    // Indicates if the virtual buttons was pressed in this frame but not in the previous.
	    private bool pressedPreviousFrame;

        #endregion

        #region Properties

        /// <summary>
        /// The list of all axes.
        /// </summary>
        public static List<Button> Buttons { get; set; }
       
	    /// <summary>
        /// The string that refers to the axis.
        /// </summary>
        public string Name { get; set; }

	    /// <summary>
        /// Use Key / Button for any kind of buttons or use Analog Movement for mouse delta, scrollwheels and gamepad sticks and triggers.
        /// </summary>
        public ButtonBehaviors ButtonBehavior { get; set; }

        /// <summary>
        /// The axis of a connected device that will control this axis.
        /// </summary>
        public AnalogAxes AnalogAxis { get; set; }

        /// <summary>
        /// The button used to push the axis in the negative direction.
        /// </summary>
        public KeyButton KeyButton { get; set; }

        /// <summary>
        /// Alternative button used to push the axis in the negative direction.
        /// </summary>
        public KeyButton AlternativeKeyButton { get; set; }

        /// <summary>
        /// If enabled, the axis invert its direction.
        /// </summary>
        public bool Invert { get; set; }

        /// <summary>
        /// Size of the analog dead zone. All analog device values within this range result map to neutral.
        /// </summary>
        public float DeadZone { get; set; }

	    /// <summary>
	    /// Which gamepad should be used. By default (0) this is set to retrieve the input from all gamepads.
	    /// </summary>
	    public int GamePadNumber { get; set; }

        #endregion

        #region Constructor

        public Button()
        {
            DeadZone = 0.75f;
            Buttons.Add(this);
        } // Button

        static Button()
        {
            Buttons = new List<Button>();
        } // Button

        #endregion

        #region Dispose

        /// <summary>
        /// Dispose managed resources.
        /// </summary>
        protected override void DisposeManagedResources()
        {
            Buttons.Remove(this);
        } // DisposeManagedResources

        #endregion

        #region Update

        /// <summary>
        /// Update.
        /// </summary>
        internal void Update()
        {
            pressedPreviousFrame = pressed;
            
            #region Digital

            if (ButtonBehavior == ButtonBehaviors.DigitalInput)
            {
                // Check if the buttons were pressed.
                pressed = KeyButton.Pressed(GamePadNumber) || AlternativeKeyButton.Pressed(GamePadNumber);
            }

            #endregion

            #region Analog

            else if (ButtonBehavior == ButtonBehaviors.AnalogInput)
            {
                float valueRaw = 0;
                
                #region Raw values for mouse

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

                #endregion

                #region Raw values for game pad

                else if (AnalogAxis == AnalogAxes.LeftStickX)
                {
                    if (GamePadNumber > 0 && GamePadNumber < 5)
                        valueRaw = GamePad.Player(GamePadNumber - 1).LeftStickX;
                    else
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (Math.Abs(valueRaw) < Math.Abs(GamePad.Player(i).LeftStickX))
                                valueRaw = GamePad.Player(i).LeftStickX;
                        }
                    }
                }
                else if (AnalogAxis == AnalogAxes.LeftStickY)
                {
                    if (GamePadNumber > 0 && GamePadNumber < 5)
                        valueRaw = GamePad.Player(GamePadNumber - 1).LeftStickY;
                    else
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (Math.Abs(valueRaw) < Math.Abs(GamePad.Player(i).LeftStickY))
                                valueRaw = GamePad.Player(i).LeftStickY;
                        }
                    }
                }
                else if (AnalogAxis == AnalogAxes.RightStickX)
                {
                    if (GamePadNumber > 0 && GamePadNumber < 5)
                        valueRaw = GamePad.Player(GamePadNumber - 1).RightStickX;
                    else
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (Math.Abs(valueRaw) < Math.Abs(GamePad.Player(i).RightStickX))
                                valueRaw = GamePad.Player(i).RightStickX;
                        }
                    }
                }
                else if (AnalogAxis == AnalogAxes.RightStickY)
                {
                    if (GamePadNumber > 0 && GamePadNumber < 5)
                        valueRaw = GamePad.Player(GamePadNumber - 1).RightStickY;
                    else
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (Math.Abs(valueRaw) < Math.Abs(GamePad.Player(i).RightStickY))
                                valueRaw = GamePad.Player(i).RightStickY;
                        }
                    }
                }
                else if (AnalogAxis == AnalogAxes.Triggers)
                {
                    if (GamePadNumber > 0 && GamePadNumber < 5)
                        valueRaw = -GamePad.Player(GamePadNumber - 1).LeftTrigger + GamePad.Player(GamePadNumber - 1).RightTrigger;
                    else
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (Math.Abs(valueRaw) < Math.Abs(-GamePad.Player(i).LeftTrigger + GamePad.Player(i).RightTrigger))
                                valueRaw = -GamePad.Player(i).LeftTrigger + GamePad.Player(i).RightTrigger;
                        }
                    }
                }

                #endregion

                // Invert if necessary.
                if (Invert)
                    valueRaw *= -1;

                pressed = valueRaw > DeadZone;
            }

            #endregion

        } // Update

        #endregion

        #region Pressed and Just Pressed

        /// <summary>
        /// Returns if the virtual button identified by buttonName was pressed.
        /// </summary>
        public static bool Pressed(string buttonName)
        {
            bool foundValue = false;
            bool foundAxis = false;
            foreach (var axis in Buttons)
            {
                if (axis.Name == buttonName)
                {
                    foundAxis = true;
                    foundValue = foundValue || axis.pressed;
                }
            }
            if (!foundAxis)
                throw new InvalidOperationException("Input: the button named " + buttonName + " does not exist.");
            return foundValue;
        } // Pressed

        /// <summary>
        /// Returns if the virtual button identified by buttonName was pressed in this frame but not in the previous.
        /// </summary>
        public static bool JustPressed(string buttonName)
        {
            bool foundPressed = false;
            bool foundPressedPreviousFrame = false;
            bool foundAxis = false;
            foreach (var axis in Buttons)
            {
                if (axis.Name == buttonName)
                {
                    foundAxis = true;
                    foundPressed = foundPressed || axis.pressed;
                    foundPressedPreviousFrame = foundPressedPreviousFrame || axis.pressedPreviousFrame;
                }
            }
            if (!foundAxis)
                throw new InvalidOperationException("Input: the button named " + buttonName + " does not exist.");
            return foundPressed && !foundPressedPreviousFrame;
        } // JustPressed

        #endregion
        
    } // Button
} // XNAFinalEngine.Input