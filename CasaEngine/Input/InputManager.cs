
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


using Microsoft.Xna.Framework.Input;


//using XNAFinalEngine.EngineCore;

namespace XNAFinalEngine.Input
{


    public enum InputDevices
    {
        NoDevice,
        Keyboard,
        GamePad,
        Mouse,
    } // InputDevices

    public enum AnalogAxes
    {
        MouseX,
        MouseY,
        MouseWheel,
        LeftStickX,
        LeftStickY,
        RightStickX,
        RightStickY,
        Triggers,
    } // AnalogAxes



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
        } // KeyButton

        public KeyButton(Buttons gamePadButton)
        {
            InputDevice = InputDevices.GamePad;
            Key = 0;
            GamePadButton = gamePadButton;
            MouseButton = 0;
        } // KeyButton

        public KeyButton(Mouse.MouseButtons mouseButton)
        {
            InputDevice = InputDevices.Mouse;
            Key = 0;
            GamePadButton = 0;
            MouseButton = mouseButton;
        } // KeyButton



        public bool Pressed(int gamePadNumber)
        {
            if (InputDevice == InputDevices.NoDevice)
                return false;
            if (InputDevice == InputDevices.Keyboard)
                return Keyboard.KeyPressed(Key);
            if (InputDevice == InputDevices.GamePad)
            {
                if (gamePadNumber == 1)
                    return GamePad.PlayerOne.ButtonPressed(GamePadButton);
                if (gamePadNumber == 2)
                    return GamePad.PlayerTwo.ButtonPressed(GamePadButton);
                if (gamePadNumber == 3)
                    return GamePad.PlayerThree.ButtonPressed(GamePadButton);
                if (gamePadNumber == 4)
                    return GamePad.PlayerFour.ButtonPressed(GamePadButton);
                // if (gamepadNumber == 0) // All gamepads at the same time.
                return GamePad.PlayerOne.ButtonPressed(GamePadButton) || GamePad.PlayerTwo.ButtonPressed(GamePadButton) ||
                       GamePad.PlayerThree.ButtonPressed(GamePadButton) || GamePad.PlayerFour.ButtonPressed(GamePadButton);
            }
            // if (keyButton.Device == Devices.Mouse)
            return Mouse.ButtonPressed(MouseButton);
        } // Pressed


    } // KeyButton


    internal class InputManager
    {


        internal static void Initialize()
        {
            //Wiimote.InitWiimotes();
        } // Initialize



        public static void UnloadInputDevices()
        {
            /*Wiimote.PlayerOne.Disconnect();
            Wiimote.PlayerTwo.Disconnect();
            Wiimote.PlayerThree.Disconnect();
            Wiimote.PlayerFour.Disconnect();*/
        } // UnloadInputDevices



        internal static void Update(float elapsedTime_)
        {
            //if (EngineManager.IsApplicationActive)
            {
                GamePad.PlayerOne.Update();
                GamePad.PlayerTwo.Update();
                GamePad.PlayerThree.Update();
                GamePad.PlayerFour.Update();
                Keyboard.Update();
#if (!XBOX)
                Mouse.Update();
                // Wiimote support was deprecated but probably still works.
                /*Wiimote.PlayerOne.Update();
                Wiimote.PlayerTwo.Update();
                Wiimote.PlayerThree.Update();
                Wiimote.PlayerFour.Update();*/
#endif

                foreach (var axis in Axis.Axes)
                {
                    axis.Update(elapsedTime_);
                }
                foreach (var button in Button.Buttons)
                {
                    button.Update();
                }
            }
        } // Update


    } // InputManager
} // XNAFinalEngine.Input
