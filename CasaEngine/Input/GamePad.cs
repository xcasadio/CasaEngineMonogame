
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


using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;


namespace XNAFinalEngine.Input
{

    public class GamePad
    {


        // Gamepad state, set every frame in the update method.
        private GamePadState currentState, previousState;

        // The id number of the gamepad.
        private readonly PlayerIndex playerIndex;



        public GamePadState CurrentState => currentState;

        public GamePadState PreviousState => previousState;

        public bool IsConnected => currentState.IsConnected;

        public GamePadCapabilities Capabilities => Microsoft.Xna.Framework.Input.GamePad.GetCapabilities(playerIndex);

        public bool Iddle => currentState.PacketNumber == previousState.PacketNumber;

        public GamePadDeadZone DeadZone { get; set; }


        public bool StartPressed => currentState.Buttons.Start == ButtonState.Pressed;

        public bool BackPressed => currentState.Buttons.Back == ButtonState.Pressed;

        public bool BigButtonPressed => currentState.Buttons.BigButton == ButtonState.Pressed;

        public bool StartJustPressed => currentState.Buttons.Start == ButtonState.Pressed && previousState.Buttons.Start == ButtonState.Released;

        public bool BackJustPressed => currentState.Buttons.Back == ButtonState.Pressed && previousState.Buttons.Back == ButtonState.Released;

        public bool BigButtonJustPressed => currentState.Buttons.BigButton == ButtonState.Pressed && previousState.Buttons.BigButton == ButtonState.Released;


        public bool APressed => currentState.Buttons.A == ButtonState.Pressed;

        public bool BPressed => currentState.Buttons.B == ButtonState.Pressed;

        public bool XPressed => currentState.Buttons.X == ButtonState.Pressed;

        public bool YPressed => currentState.Buttons.Y == ButtonState.Pressed;

        public bool AJustPressed => currentState.Buttons.A == ButtonState.Pressed && previousState.Buttons.A == ButtonState.Released;

        public bool BJustPressed => currentState.Buttons.B == ButtonState.Pressed && previousState.Buttons.B == ButtonState.Released;

        public bool XJustPressed => currentState.Buttons.X == ButtonState.Pressed && previousState.Buttons.X == ButtonState.Released;

        public bool YJustPressed => currentState.Buttons.Y == ButtonState.Pressed && previousState.Buttons.Y == ButtonState.Released;


        public bool DPadLeftPressed => currentState.DPad.Left == ButtonState.Pressed;

        public bool DPadRightPressed => currentState.DPad.Right == ButtonState.Pressed;

        public bool DPadUpPressed => currentState.DPad.Up == ButtonState.Pressed;

        public bool DPadDownPressed => currentState.DPad.Down == ButtonState.Pressed;

        public bool DPadLeftJustPressed => currentState.DPad.Left == ButtonState.Pressed && previousState.DPad.Left == ButtonState.Released;

        public bool DPadRightJustPressed => currentState.DPad.Right == ButtonState.Pressed && previousState.DPad.Right == ButtonState.Released;

        public bool DPadUpJustPressed => currentState.DPad.Up == ButtonState.Pressed && previousState.DPad.Up == ButtonState.Released;

        public bool DPadDownJustPressed => currentState.DPad.Down == ButtonState.Pressed && previousState.DPad.Down == ButtonState.Released;


        public float LeftStickX => currentState.ThumbSticks.Left.X;

        public float LeftStickY => currentState.ThumbSticks.Left.Y;

        public float RightStickX => currentState.ThumbSticks.Right.X;

        public float RightStickY => currentState.ThumbSticks.Right.Y;

        public bool LeftStickPressed => currentState.Buttons.LeftStick == ButtonState.Pressed;

        public bool LeftStickJustPressed => currentState.Buttons.LeftStick == ButtonState.Pressed && previousState.Buttons.LeftStick == ButtonState.Released;

        public bool RightStickPressed => currentState.Buttons.RightStick == ButtonState.Pressed;

        public bool RightStickJustPressed => currentState.Buttons.RightStick == ButtonState.Pressed && previousState.Buttons.RightStick == ButtonState.Released;


        public bool LeftButtonPressed => currentState.Buttons.LeftShoulder == ButtonState.Pressed;

        public bool LeftButtonJustPressed => currentState.Buttons.LeftShoulder == ButtonState.Pressed && previousState.Buttons.LeftShoulder == ButtonState.Released;

        public bool RightButtonPressed => currentState.Buttons.RightShoulder == ButtonState.Pressed;

        public bool RightButtonJustPressed => currentState.Buttons.RightShoulder == ButtonState.Pressed && previousState.Buttons.RightShoulder == ButtonState.Released;

        public float LeftTrigger => currentState.Triggers.Left;

        public float RightTrigger => currentState.Triggers.Right;


        private GamePad(PlayerIndex _playerIndex)
        {
            playerIndex = _playerIndex;
            currentState = Microsoft.Xna.Framework.Input.GamePad.GetState(playerIndex);
            DeadZone = GamePadDeadZone.IndependentAxes;
        } // GamePad



        public bool ButtonJustPressed(Buttons button) { return currentState.IsButtonDown(button) && !previousState.IsButtonDown(button); }

        public bool ButtonPressed(Buttons button) { return currentState.IsButtonDown(button); }



        public void SetVibration(float leftMotor, float rightMotor)
        {
            Microsoft.Xna.Framework.Input.GamePad.SetVibration(playerIndex, leftMotor, rightMotor);
        } // SetVibration



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




        public static GamePad PlayerOne => PlayerOneGamePad;

        public static GamePad PlayerTwo => PlayerTwoGamePad;

        public static GamePad PlayerThree => PlayerThreeGamePad;

        public static GamePad PlayerFour => PlayerFourGamePad;

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
