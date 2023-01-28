
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


using System;


using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;


namespace XNAFinalEngine.Input
{

    /// <summary>
    /// XInput Gamepad.
    /// Allows to work up to four different gamepad.
    /// XInput, an API controllers introduced with the launch of the Xbox 360. It has the advantage over DirectInput of significantly easier programmability.
    /// XInput is compatible with DirectX 9 and up.
    /// http://en.wikipedia.org/wiki/DirectInput
    /// </summary>
    public class GamePad
    {


        // Gamepad state, set every frame in the update method.
        private GamePadState currentState, previousState;

        // The id number of the gamepad.
        private readonly PlayerIndex playerIndex;



        /// <summary>
        /// The current gamepad state.
        /// </summary>
        public GamePadState CurrentState { get { return currentState; } }

        /// <summary>
        /// The previous mouse state.
        /// </summary>
        public GamePadState PreviousState { get { return previousState; } }

        /// <summary>
        /// Is the gamepad connected?
        /// </summary>
        public bool IsConnected { get { return currentState.IsConnected; } }

        /// <summary>
        /// Retrieves the capabilities of this game pad.
        /// http://msdn.microsoft.com/en-us/library/microsoft.xna.framework.input.gamepadcapabilities_members.aspx
        /// </summary>
        public GamePadCapabilities Capabilities { get { return Microsoft.Xna.Framework.Input.GamePad.GetCapabilities(playerIndex); } }

        /// <summary>
        /// Indicates if the input state has changed.
        /// </summary>
        public bool Iddle { get { return currentState.PacketNumber == previousState.PacketNumber; } }

        /// <summary>
        /// Specifies a type of dead zone processing to apply to Xbox 360 Controller analog sticks.
        /// Circular: The combined X and Y position of each stick is compared to the dead zone.
        ///           This provides better control than IndependentAxes when the stick is used as a two-dimensional control surface,
        ///           such as when controlling a character's view in a first-person game.
        /// IndependentAxes: The X and Y positions of each stick are compared against the dead zone independently. This setting is the default.
        /// None: The values of each stick are not processed and are returned as "raw" values. This is best if you intend to implement your own dead zone processing.
        /// </summary>
        public GamePadDeadZone DeadZone { get; set; }


        /// <summary>
        /// Gamepad start button pressed.
        /// </summary>
        public bool StartPressed { get { return currentState.Buttons.Start == ButtonState.Pressed; } }

        /// <summary>
        /// Gamepad back button pressed.
        /// </summary>
        public bool BackPressed { get { return currentState.Buttons.Back == ButtonState.Pressed; } }

        /// <summary>
        /// Gamepad big button pressed.
        /// </summary>
        public bool BigButtonPressed { get { return currentState.Buttons.BigButton == ButtonState.Pressed; } }

        /// <summary>
        /// Gamepad start button just pressed.
        /// </summary>
        public bool StartJustPressed { get { return currentState.Buttons.Start == ButtonState.Pressed && previousState.Buttons.Start == ButtonState.Released; } }

        /// <summary>
        /// Gamepad back button just pressed.
        /// </summary>
        public bool BackJustPressed { get { return currentState.Buttons.Back == ButtonState.Pressed && previousState.Buttons.Back == ButtonState.Released; } }

        /// <summary>
        /// Gamepad big button just pressed.
        /// </summary>
        public bool BigButtonJustPressed { get { return currentState.Buttons.BigButton == ButtonState.Pressed && previousState.Buttons.BigButton == ButtonState.Released; } }



        /// <summary>
		/// Gamepad A button pressed.
		/// </summary>
		public bool APressed { get { return currentState.Buttons.A == ButtonState.Pressed; } }

        /// <summary>
        /// Gamepad B button pressed.
        /// </summary>
        public bool BPressed { get { return currentState.Buttons.B == ButtonState.Pressed; } }

        /// <summary>
        /// Gamepad X button pressed.
        /// </summary>
        public bool XPressed { get { return currentState.Buttons.X == ButtonState.Pressed; } }

        /// <summary>
        /// Gamepad Y button pressed.
        /// </summary>
        public bool YPressed { get { return currentState.Buttons.Y == ButtonState.Pressed; } }

        /// <summary>
        /// Gamepad A button just pressed.
        /// </summary>
        public bool AJustPressed { get { return currentState.Buttons.A == ButtonState.Pressed && previousState.Buttons.A == ButtonState.Released; } }

        /// <summary>
        /// Gamepad B button just pressed.
        /// </summary>
        public bool BJustPressed { get { return currentState.Buttons.B == ButtonState.Pressed && previousState.Buttons.B == ButtonState.Released; } }

        /// <summary>
        /// Gamepad X button just pressed.
        /// </summary>
        public bool XJustPressed { get { return currentState.Buttons.X == ButtonState.Pressed && previousState.Buttons.X == ButtonState.Released; } }

        /// <summary>
        /// Gamepad Y button just pressed.
        /// </summary>
        public bool YJustPressed { get { return currentState.Buttons.Y == ButtonState.Pressed && previousState.Buttons.Y == ButtonState.Released; } }



        /// <summary>
        /// Gamepad DPad left pressed.
        /// </summary>
        public bool DPadLeftPressed { get { return currentState.DPad.Left == ButtonState.Pressed; } }

        /// <summary>
        /// Gamepad DPad right pressed.
        /// </summary>
        public bool DPadRightPressed { get { return currentState.DPad.Right == ButtonState.Pressed; } }

        /// <summary>
        /// Gamepad DPad up pressed.
        /// </summary>
        public bool DPadUpPressed { get { return currentState.DPad.Up == ButtonState.Pressed; } }

        /// <summary>
        /// Gamepad DPad down pressed.
        /// </summary>
        public bool DPadDownPressed { get { return currentState.DPad.Down == ButtonState.Pressed; } }

        /// <summary>
        /// Gamepad DPad left just pressed.
        /// </summary>
        public bool DPadLeftJustPressed { get { return currentState.DPad.Left == ButtonState.Pressed && previousState.DPad.Left == ButtonState.Released; } }

        /// <summary>
        /// Gamepad DPad right just pressed.
        /// </summary>
        public bool DPadRightJustPressed { get { return currentState.DPad.Right == ButtonState.Pressed && previousState.DPad.Right == ButtonState.Released; } }

        /// <summary>
        /// Gamepad DPad up just pressed.
        /// </summary>
        public bool DPadUpJustPressed { get { return currentState.DPad.Up == ButtonState.Pressed && previousState.DPad.Up == ButtonState.Released; } }

        /// <summary>
        /// Gamepad DPad down just pressed.
        /// </summary>
        public bool DPadDownJustPressed { get { return currentState.DPad.Down == ButtonState.Pressed && previousState.DPad.Down == ButtonState.Released; } }



        /// <summary>
        /// Gamepad left thumb stick X movement.
        /// </summary>
        public float LeftStickX { get { return currentState.ThumbSticks.Left.X; } }

        /// <summary>
        /// Gamepad left thumb stick Y movement.
        /// </summary>
        public float LeftStickY { get { return currentState.ThumbSticks.Left.Y; } }

        /// <summary>
        /// Gamepad right thumb stick X movement.
        /// </summary>
        public float RightStickX { get { return currentState.ThumbSticks.Right.X; } }

        /// <summary>
        /// Gamepad right thumb stick Y movement.
        /// </summary>
        public float RightStickY { get { return currentState.ThumbSticks.Right.Y; } }

        /// <summary>
        /// Gamepad left stick button pressed.
        /// </summary>
        public bool LeftStickPressed { get { return currentState.Buttons.LeftStick == ButtonState.Pressed; } }

        /// <summary>
        /// Gamepad left stick button just pressed.
        /// </summary>
        public bool LeftStickJustPressed { get { return currentState.Buttons.LeftStick == ButtonState.Pressed && previousState.Buttons.LeftStick == ButtonState.Released; } }

        /// <summary>
        /// Gamepad right stick button pressed.
        /// </summary>
        public bool RightStickPressed { get { return currentState.Buttons.RightStick == ButtonState.Pressed; } }

        /// <summary>
        /// Gamepad right stick button just pressed.
        /// </summary>
        public bool RightStickJustPressed { get { return currentState.Buttons.RightStick == ButtonState.Pressed && previousState.Buttons.RightStick == ButtonState.Released; } }



        /// <summary>
        /// Gamepad Left Button pressed (LB)
        /// </summary>
        public bool LeftButtonPressed { get { return currentState.Buttons.LeftShoulder == ButtonState.Pressed; } }

        /// <summary>
        /// Gamepad Left Button just pressed (LB)
        /// </summary>
        public bool LeftButtonJustPressed { get { return currentState.Buttons.LeftShoulder == ButtonState.Pressed && previousState.Buttons.LeftShoulder == ButtonState.Released; } }

        /// <summary>
        /// Gamepad Right Button pressed (RB)
        /// </summary>
        public bool RightButtonPressed { get { return currentState.Buttons.RightShoulder == ButtonState.Pressed; } }

        /// <summary>
        /// Gamepad Right Button just pressed (RB)
        /// </summary>
        public bool RightButtonJustPressed { get { return currentState.Buttons.RightShoulder == ButtonState.Pressed && previousState.Buttons.RightShoulder == ButtonState.Released; } }

        /// <summary>
        /// Gamepad left trigger axis value (LT)
        /// </summary>
        public float LeftTrigger { get { return currentState.Triggers.Left; } }

        /// <summary>
        /// Gamepad right trigger axis value (RT)
        /// </summary>
        public float RightTrigger { get { return currentState.Triggers.Right; } }




        /// <summary>
        /// Init the xinput gamepad of this player index.
        /// </summary>
        private GamePad(PlayerIndex _playerIndex)
        {
            playerIndex = _playerIndex;
            currentState = Microsoft.Xna.Framework.Input.GamePad.GetState(playerIndex);
            DeadZone = GamePadDeadZone.IndependentAxes;
        } // GamePad



        /// <summary>
        /// Button just pressed.
        /// </summary>
        public bool ButtonJustPressed(Buttons button) { return currentState.IsButtonDown(button) && !previousState.IsButtonDown(button); }

        /// <summary>
        /// Button pressed.
        /// </summary>
        public bool ButtonPressed(Buttons button) { return currentState.IsButtonDown(button); }



        /// <summary>
        /// Sets the vibration motor speeds on an Xbox 360 Controller.
        /// The speed of the left and right motor, between 0.0 and 1.0.
        /// </summary>
        public void SetVibration(float leftMotor, float rightMotor)
        {
            Microsoft.Xna.Framework.Input.GamePad.SetVibration(playerIndex, leftMotor, rightMotor);
        } // SetVibration



        /// <summary>
        /// Update.
        /// If the gamepad is not connected the operation won't trow exception, the state will be empty. 
        /// </summary>
        internal void Update()
        {
            previousState = currentState;
            currentState = Microsoft.Xna.Framework.Input.GamePad.GetState(playerIndex, DeadZone);
        } // Update




        // The four possible gamepads.
        private readonly static GamePad PlayerOneGamePad = new GamePad(PlayerIndex.One),
                                        PlayerTwoGamePad = new GamePad(PlayerIndex.Two),
                                        PlayerThreeGamePad = new GamePad(PlayerIndex.Three),
                                        PlayerFourGamePad = new GamePad(PlayerIndex.Four);




        /// <summary>
        /// GamePad assigned to player one. 
        /// </summary>
        public static GamePad PlayerOne { get { return PlayerOneGamePad; } }

        /// <summary>
        /// GamePad assigned to player two. 
        /// </summary>
        public static GamePad PlayerTwo { get { return PlayerTwoGamePad; } }

        /// <summary>
        /// GamePad assigned to player three. 
        /// </summary>
        public static GamePad PlayerThree { get { return PlayerThreeGamePad; } }

        /// <summary>
        /// GamePad assigned to player four. 
        /// </summary>
        public static GamePad PlayerFour { get { return PlayerFourGamePad; } }

        /// <summary>
        /// Returns the gamepad that is indicated in playerIndex (between 0 and 3).
        /// </summary>
        public static GamePad Player(int playerIndex)
        {
            switch (playerIndex)
            {
                case 0: return PlayerOneGamePad;
                case 1: return PlayerTwoGamePad;
                case 2: return PlayerThreeGamePad;
                case 3: return PlayerFourGamePad;
                default: throw new ArgumentOutOfRangeException("playerIndex", "GamePad: The number has to be between 0 and 3.");
            }
        } // Player



    } // GamePad
} // XNAFinalEngine.Input
