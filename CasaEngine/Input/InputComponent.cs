using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

using CasaEngine.Game;
using CasaEngine.CoreSystems.Game;
using CasaEngineCommon.Helper;





namespace CasaEngine.Input
{
    public class InputComponent :
        Microsoft.Xna.Framework.GameComponent
    {

#if !XBOX360
        private MouseState mouseState, mouseStateLastFrame;
#endif

        private bool mouseDetected = false;

        private KeyboardState keyboardPreviousState = Microsoft.Xna.Framework.Input.Keyboard.GetState();
        private KeyboardState keyboardState;

        //private List<Keys> keysPressedLastFrame = new List<Keys>();

        private readonly GamePadState[] gamePadState = new GamePadState[4];
        private readonly GamePadState[] gamePadStateLastFrame = new GamePadState[4];
        private readonly GamePadDeadZone[] gamePadDeadZoneMode = new GamePadDeadZone[4];
        private readonly GamePadCapabilities[] gamePadCapabilities = new GamePadCapabilities[4];

        //private PlayerIndex playerIndex = PlayerIndex.One;

#if !XBOX360
        private int mouseWheelDelta = 0;
#endif

#if EDITOR
        public static int ms_MouseWheel = 0;
#endif

        private Point startDraggingPos;

        public readonly float DEADZONE = 0.2f;

        //InputConfigurations m_InputConfigurations = new InputConfigurations();
        ButtonConfiguration m_ButtonConfiguration;
        InputManager.KeyState[] m_KeysState;

        readonly InputManager[] m_InputManager;



        public InputManager.KeyState[] KeysState => m_KeysState;


        public bool MouseDetected => mouseDetected;

        public Point MousePos
        {
            get
            {
#if !XBOX360
                return new Point(mouseState.X, mouseState.Y);
#else
                return Point.Zero;
#endif
            }
        }

#if !XBOX360
        private float mouseXMovement, mouseYMovement;
        private float lastMouseXMovement, lastMouseYMovement;
#endif

        public float MouseXMovement
        {
            get
            {
#if !XBOX360
                return mouseXMovement;
#else
                return 0;
#endif
            }
        }

        public float MouseYMovement
        {
            get
            {
#if !XBOX360
                return mouseYMovement;
#else
                    return 0;
#endif
            }
        }

        public bool HasMouseMoved
        {
            get
            {
#if !XBOX360
                //TODO: Introduce a mouse movement threshold constant
                if (MouseXMovement > 1 || MouseYMovement > 1)
                    return true;
#endif
                return false;
            }
        }

        public bool MouseLeftButtonPressed
        {
            get
            {
#if !XBOX360
                return mouseState.LeftButton == ButtonState.Pressed;
#else
                return false;
#endif
            }
        }

        public bool MouseRightButtonPressed
        {
            get
            {
#if !XBOX360
                return mouseState.RightButton == ButtonState.Pressed;
#else
                return false;
#endif
            }
        }

        public bool MouseMiddleButtonPressed
        {
            get
            {
#if !XBOX360
                return mouseState.MiddleButton == ButtonState.Pressed;
#else
                return false;
#endif
            }
        }

        public bool MouseLeftButtonJustPressed
        {
            get
            {
#if !XBOX360
                return mouseState.LeftButton == ButtonState.Pressed &&
                       mouseStateLastFrame.LeftButton == ButtonState.Released;
#else
                return false;
#endif
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode",
            Justification = "Makes this class reuseable.")]
        public bool MouseRightButtonJustPressed
        {
            get
            {
#if !XBOX360
                return mouseState.RightButton == ButtonState.Pressed &&
                       mouseStateLastFrame.RightButton == ButtonState.Released;
#else
                return false;
#endif
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode",
            Justification = "Makes this class reuseable.")]
        public Point MouseDraggingAmount =>
            new Point(
                startDraggingPos.X - MousePos.X,
                startDraggingPos.Y - MousePos.Y);

        public void ResetMouseDraggingAmount()
        {
            startDraggingPos = MousePos;
        }

        public int MouseWheelDelta
        {
            get
            {
                return
#if EDITOR 
                    ms_MouseWheel;
#else
                    mouseWheelDelta;
#endif
            }
        }

        public bool MouseInBox(Rectangle rect)
        {
#if !XBOX360
            bool ret = mouseState.X >= rect.X &&
                       mouseState.Y >= rect.Y &&
                       mouseState.X < rect.Right &&
                       mouseState.Y < rect.Bottom;
            bool lastRet = mouseStateLastFrame.X >= rect.X &&
                           mouseStateLastFrame.Y >= rect.Y &&
                           mouseStateLastFrame.X < rect.Right &&
                           mouseStateLastFrame.Y < rect.Bottom;

            return ret;
#else
            return false;
#endif
        }

        public bool MouseInBoxRelative(Rectangle rect)
        {
            /*float widthFactor = BaseGame.Width / 1024.0f;
            float heightFactor = BaseGame.Height / 640.0f;
            return MouseInBox(new Rectangle(
                (int)System.Math.Round(rect.X * widthFactor),
                (int)System.Math.Round(rect.Y * heightFactor),
                (int)System.Math.Round(rect.Right * widthFactor),
                (int)System.Math.Round(rect.Bottom * heightFactor)));*/
            return false;
        }

        public KeyboardState Keyboard => keyboardState;

        public void ResetKeyboard()
        {
            //keyboardState = Keyboard.GetState();
            //keysPressedLastFrame.Clear();
        }

        public bool IsSpecialKey(Keys key)
        {
            // All keys except A-Z, 0-9 and `-\[];',./= (and space) are special keys.
            // With shift pressed this also results in this keys:
            // ~_|{}:"<>? !@#$%^&*().
            int keyNum = (int)key;
            if ((keyNum >= (int)Keys.A && keyNum <= (int)Keys.Z) ||
                (keyNum >= (int)Keys.D0 && keyNum <= (int)Keys.D9) ||
                key == Keys.Space || // well, space ^^
                key == Keys.OemTilde || // `~
                key == Keys.OemMinus || // -_
                key == Keys.OemPipe || // \|
                key == Keys.OemOpenBrackets || // [{
                key == Keys.OemCloseBrackets || // ]}
                key == Keys.OemQuotes || // '"
                key == Keys.OemQuestion || // /?
                key == Keys.OemPlus) // =+
            {
                return false;
            }

            // Else is is a special key
            return true;
        }

        public char KeyToChar(Keys key, bool shiftPressed)
        {
            // If key will not be found, just return space
            char ret = ' ';
            int keyNum = (int)key;
            if (keyNum >= (int)Keys.A && keyNum <= (int)Keys.Z)
            {
                if (shiftPressed)
                    ret = key.ToString()[0];
                else
                    ret = key.ToString().ToLower()[0];
            }
            else if (keyNum >= (int)Keys.D0 && keyNum <= (int)Keys.D9 &&
                shiftPressed == false)
            {
                ret = (char)((int)'0' + (keyNum - Keys.D0));
            }
            else if (key == Keys.D1 && shiftPressed)
                ret = '!';
            else if (key == Keys.D2 && shiftPressed)
                ret = '@';
            else if (key == Keys.D3 && shiftPressed)
                ret = '#';
            else if (key == Keys.D4 && shiftPressed)
                ret = '$';
            else if (key == Keys.D5 && shiftPressed)
                ret = '%';
            else if (key == Keys.D6 && shiftPressed)
                ret = '^';
            else if (key == Keys.D7 && shiftPressed)
                ret = '&';
            else if (key == Keys.D8 && shiftPressed)
                ret = '*';
            else if (key == Keys.D9 && shiftPressed)
                ret = '(';
            else if (key == Keys.D0 && shiftPressed)
                ret = ')';
            else if (key == Keys.OemTilde)
                ret = shiftPressed ? '~' : '`';
            else if (key == Keys.OemMinus)
                ret = shiftPressed ? '_' : '-';
            else if (key == Keys.OemPipe)
                ret = shiftPressed ? '|' : '\\';
            else if (key == Keys.OemOpenBrackets)
                ret = shiftPressed ? '{' : '[';
            else if (key == Keys.OemCloseBrackets)
                ret = shiftPressed ? '}' : ']';
            else if (key == Keys.OemSemicolon)
                ret = shiftPressed ? ':' : ';';
            else if (key == Keys.OemQuotes)
                ret = shiftPressed ? '"' : '\'';
            else if (key == Keys.OemComma)
                ret = shiftPressed ? '<' : '.';
            else if (key == Keys.OemPeriod)
                ret = shiftPressed ? '>' : ',';
            else if (key == Keys.OemQuestion)
                ret = shiftPressed ? '?' : '/';
            else if (key == Keys.OemPlus)
                ret = shiftPressed ? '+' : '=';

            // Return result
            return ret;
        }

        /*public void HandleKeyboardInput(ref string inputText)
        {
            // Is a shift key pressed (we have to check both, left and right)
            bool isShiftPressed =
                keyboardState.IsKeyDown(Keys.LeftShift) ||
                keyboardState.IsKeyDown(Keys.RightShift);

            // Go through all pressed keys
            foreach (Keys pressedKey in keyboardState.GetPressedKeys())
                // Only process if it was not pressed last frame
                if (keysPressedLastFrame.Contains(pressedKey) == false)
                {
                    // No special key?
                    if (IsSpecialKey(pressedKey) == false &&
                        // Max. allow 32 chars
                        inputText.Length < 32)
                    {
                        // Then add the letter to our inputText.
                        // Check also the shift state!
                        inputText += KeyToChar(pressedKey, isShiftPressed);
                    }
                    else if (pressedKey == Keys.Back &&
                        inputText.Length > 0)
                    {
                        // Remove 1 character at end
                        inputText = inputText.Substring(0, inputText.Length - 1);
                    }
                }
        }*/

        public bool IsKeyJustPressed(Keys key_)
        {
            return keyboardState.IsKeyDown(key_)
                && keyboardPreviousState.IsKeyDown(key_) == false;
        }

        public bool IsKeyPressed(Keys key_)
        {
            return keyboardState.IsKeyDown(key_);
        }

        public bool IsKeyReleased(Keys key_)
        {
            return keyboardState.IsKeyUp(key_)
                && keyboardPreviousState.IsKeyDown(key_);
        }

        public bool IsKeyHeld(Keys key_)
        {
            return keyboardState.IsKeyDown(key_)
                && keyboardPreviousState.IsKeyDown(key_);
        }


        public GamePadState GamePadState(PlayerIndex index_)
        {
            return gamePadState[(int)index_];
        }

        public GamePadCapabilities GamePadCapabilities(PlayerIndex index_)
        {
            gamePadCapabilities[(int)index_] = GamePad.GetCapabilities(index_);
            return gamePadCapabilities[(int)index_];
        }

        public void GamePadDeadZoneMode(PlayerIndex index_, GamePadDeadZone mode_)
        {
            gamePadDeadZoneMode[(int)index_] = mode_;
        }

        public bool IsGamePadConnected(PlayerIndex index_)
        {
            return gamePadState[(int)index_].IsConnected;
        }


        public bool IsButtonJustPressed(PlayerIndex index_, Buttons button_)
        {
            switch (button_)
            {
                case Buttons.A:
                    return gamePadState[(int)index_].Buttons.A == ButtonState.Pressed &&
                        gamePadStateLastFrame[(int)index_].Buttons.A == ButtonState.Released;

                case Buttons.B:
                    return gamePadState[(int)index_].Buttons.B == ButtonState.Pressed &&
                        gamePadStateLastFrame[(int)index_].Buttons.B == ButtonState.Released;

                case Buttons.X:
                    return gamePadState[(int)index_].Buttons.X == ButtonState.Pressed &&
                        gamePadStateLastFrame[(int)index_].Buttons.X == ButtonState.Released;

                case Buttons.Y:
                    return gamePadState[(int)index_].Buttons.Y == ButtonState.Pressed &&
                        gamePadStateLastFrame[(int)index_].Buttons.Y == ButtonState.Released;

                case Buttons.Back:
                    return gamePadState[(int)index_].Buttons.Back == ButtonState.Pressed &&
                        gamePadStateLastFrame[(int)index_].Buttons.Back == ButtonState.Released;

                case Buttons.Start:
                    return gamePadState[(int)index_].Buttons.Start == ButtonState.Pressed &&
                        gamePadStateLastFrame[(int)index_].Buttons.Start == ButtonState.Released;

                case Buttons.LeftShoulder:
                    return gamePadState[(int)index_].Buttons.LeftShoulder == ButtonState.Pressed &&
                        gamePadStateLastFrame[(int)index_].Buttons.LeftShoulder == ButtonState.Released;

                case Buttons.RightShoulder:
                    return gamePadState[(int)index_].Buttons.RightShoulder == ButtonState.Pressed &&
                        gamePadStateLastFrame[(int)index_].Buttons.RightShoulder == ButtonState.Released;

                case Buttons.LeftStick:
                    return gamePadState[(int)index_].Buttons.LeftStick == ButtonState.Pressed &&
                        gamePadStateLastFrame[(int)index_].Buttons.LeftStick == ButtonState.Released;

                case Buttons.RightStick:
                    return gamePadState[(int)index_].Buttons.RightStick == ButtonState.Pressed &&
                        gamePadStateLastFrame[(int)index_].Buttons.RightStick == ButtonState.Released;

                case Buttons.LeftTrigger:
                    return gamePadState[(int)index_].Triggers.Left > 0.75f &&
                        gamePadStateLastFrame[(int)index_].Triggers.Left == 0.0f;

                case Buttons.RightTrigger:
                    return gamePadState[(int)index_].Triggers.Right > 0.75f &&
                        gamePadStateLastFrame[(int)index_].Triggers.Right == 0.0f;
            }

            throw new Exception("Buttons non géré : " + button_);
        }

        public bool GamePadLeftJustPressed(PlayerIndex index_)
        {
            return (gamePadState[(int)index_].DPad.Left == ButtonState.Pressed &&
                gamePadStateLastFrame[(int)index_].DPad.Left == ButtonState.Released) ||
                (gamePadState[(int)index_].ThumbSticks.Left.X < -0.75f &&
                gamePadStateLastFrame[(int)index_].ThumbSticks.Left.X > -0.75f);
        }

        public bool GamePadRightJustPressed(PlayerIndex index_)
        {
            return (gamePadState[(int)index_].DPad.Right == ButtonState.Pressed &&
                gamePadStateLastFrame[(int)index_].DPad.Right == ButtonState.Released) ||
                (gamePadState[(int)index_].ThumbSticks.Left.X > 0.75f &&
                gamePadStateLastFrame[(int)index_].ThumbSticks.Left.X < 0.75f);
        }

        public bool GamePadUpJustPressed(PlayerIndex index_)
        {
            return (gamePadState[(int)index_].DPad.Up == ButtonState.Pressed &&
                gamePadStateLastFrame[(int)index_].DPad.Up == ButtonState.Released) ||
                (gamePadState[(int)index_].ThumbSticks.Left.Y > 0.75f &&
                gamePadStateLastFrame[(int)index_].ThumbSticks.Left.Y < 0.75f);
        }

        public bool GamePadDownJustPressed(PlayerIndex index_)
        {
            return (gamePadState[(int)index_].DPad.Down == ButtonState.Pressed &&
                gamePadStateLastFrame[(int)index_].DPad.Down == ButtonState.Released) ||
                (gamePadState[(int)index_].ThumbSticks.Left.Y < -0.75f &&
                gamePadStateLastFrame[(int)index_].ThumbSticks.Left.Y > -0.75f);
        }

        public bool GamePadDPadUpPressed(PlayerIndex index_)
        {
            return gamePadState[(int)index_].DPad.Up == ButtonState.Pressed;
        }

        public bool GamePadDPadDownJustPressed(PlayerIndex index_)
        {
            return (gamePadState[(int)index_].DPad.Down == ButtonState.Pressed &&
                gamePadStateLastFrame[(int)index_].DPad.Down == ButtonState.Released);
        }

        public bool GamePadDPadRightJustPressed(PlayerIndex index_)
        {
            return (gamePadState[(int)index_].DPad.Right == ButtonState.Pressed &&
                gamePadStateLastFrame[(int)index_].DPad.Right == ButtonState.Released);
        }

        public bool GamePadDPadLeftJustPressed(PlayerIndex index_)
        {
            return (gamePadState[(int)index_].DPad.Left == ButtonState.Pressed &&
                gamePadStateLastFrame[(int)index_].DPad.Left == ButtonState.Released);
        }

        public bool GamePadDPadUpJustPressed(PlayerIndex index_)
        {
            return (gamePadState[(int)index_].DPad.Up == ButtonState.Pressed &&
                gamePadStateLastFrame[(int)index_].DPad.Up == ButtonState.Released);
        }



        public bool IsButtonPressed(PlayerIndex index_, Buttons button_)
        {
            return gamePadState[(int)index_].IsButtonDown(button_);

            /*switch (button_)
            {
                case Buttons.A:
                    return gamePadState[(int)index_].Buttons.A == ButtonState.Pressed;

                case Buttons.B:
                    return gamePadState[(int)index_].Buttons.B == ButtonState.Pressed;

                case Buttons.X:
                    return gamePadState[(int)index_].Buttons.X == ButtonState.Pressed;

                case Buttons.Y:
                    return gamePadState[(int)index_].Buttons.Y == ButtonState.Pressed;

                case Buttons.Back:
                    return gamePadState[(int)index_].Buttons.Back == ButtonState.Pressed;

                case Buttons.Start:
                    return gamePadState[(int)index_].Buttons.Start == ButtonState.Pressed;

                case Buttons.LeftShoulder:
                    return gamePadState[(int)index_].Buttons.LeftShoulder == ButtonState.Pressed;

                case Buttons.RightShoulder:
                    return gamePadState[(int)index_].Buttons.RightShoulder == ButtonState.Pressed;

                case Buttons.LeftStick:
                    return gamePadState[(int)index_].Buttons.LeftStick == ButtonState.Pressed;

                case Buttons.RightStick:
                    return gamePadState[(int)index_].Buttons.RightStick == ButtonState.Pressed;

                case Buttons.LeftTrigger:
                    return gamePadState[(int)index_].Triggers.Left > 0.75f;

                case Buttons.RightTrigger:
                    return gamePadState[(int)index_].Triggers.Right > 0.75f;

                case Buttons.LeftThumbstickDown:
                    return gamePadState[(int)index_].IsButtonDown(Buttons.LeftThumbstickDown);

                case Buttons.LeftThumbstickUp:
                    return gamePadState[(int)index_].IsButtonDown(Buttons.LeftThumbstickUp);

                case Buttons.LeftThumbstickRight:
                    return gamePadState[(int)index_].IsButtonDown(Buttons.LeftThumbstickRight);

                case Buttons.LeftThumbstickLeft:
                    return gamePadState[(int)index_].IsButtonDown(Buttons.LeftThumbstickLeft);

                case Buttons.RightThumbstickDown:
                    return gamePadState[(int)index_].IsButtonDown(Buttons.RightThumbstickDown);

                case Buttons.RightThumbstickUp:
                    return gamePadState[(int)index_].IsButtonDown(Buttons.RightThumbstickUp);

                case Buttons.RightThumbstickRight:
                    return gamePadState[(int)index_].IsButtonDown(Buttons.RightThumbstickRight);

                case Buttons.RightThumbstickLeft:
                    return gamePadState[(int)index_].IsButtonDown(Buttons.RightThumbstickLeft);

                case Buttons.DPadDown:
                    return gamePadState[(int)index_].IsButtonDown(Buttons.DPadDown);

                case Buttons.DPadLeft:
                    return gamePadState[(int)index_].IsButtonDown(Buttons.DPadLeft);

                case Buttons.DPadRight:
                    return gamePadState[(int)index_].IsButtonDown(Buttons.DPadRight);

                case Buttons.DPadUp:
                    return gamePadState[(int)index_].IsButtonDown(Buttons.DPadUp);
            }*/

            throw new Exception("Buttons not supported : " + button_);
        }

        public bool GamePadLeftPressed(PlayerIndex index_)
        {
            return gamePadState[(int)index_].DPad.Left == ButtonState.Pressed ||
                gamePadState[(int)index_].ThumbSticks.Left.X <= -0.75f;
        }

        public bool GamePadRightPressed(PlayerIndex index_)
        {
            return gamePadState[(int)index_].DPad.Right == ButtonState.Pressed ||
                gamePadState[(int)index_].ThumbSticks.Left.X >= 0.75f;
        }

        public bool GamePadUpPressed(PlayerIndex index_)
        {
            return gamePadState[(int)index_].DPad.Up == ButtonState.Pressed ||
                gamePadState[(int)index_].ThumbSticks.Left.Y >= 0.75f;
        }

        public bool GamePadDownPressed(PlayerIndex index_)
        {
            return gamePadState[(int)index_].DPad.Down == ButtonState.Pressed ||
                gamePadState[(int)index_].ThumbSticks.Left.Y <= -0.75f;
        }



        public bool IsButtonJustReleased(PlayerIndex index_, Buttons button_)
        {
            switch (button_)
            {
                case Buttons.A:
                    return gamePadState[(int)index_].Buttons.A == ButtonState.Released &&
                        gamePadStateLastFrame[(int)index_].Buttons.A == ButtonState.Pressed;

                case Buttons.B:
                    return gamePadState[(int)index_].Buttons.B == ButtonState.Released &&
                        gamePadStateLastFrame[(int)index_].Buttons.B == ButtonState.Pressed;

                case Buttons.X:
                    return gamePadState[(int)index_].Buttons.X == ButtonState.Released &&
                        gamePadStateLastFrame[(int)index_].Buttons.X == ButtonState.Pressed;

                case Buttons.Y:
                    return gamePadState[(int)index_].Buttons.Y == ButtonState.Released &&
                        gamePadStateLastFrame[(int)index_].Buttons.Y == ButtonState.Pressed;

                case Buttons.Back:
                    return gamePadState[(int)index_].Buttons.Back == ButtonState.Released &&
                        gamePadStateLastFrame[(int)index_].Buttons.Back == ButtonState.Pressed;

                case Buttons.Start:
                    return gamePadState[(int)index_].Buttons.Start == ButtonState.Released &&
                        gamePadStateLastFrame[(int)index_].Buttons.Start == ButtonState.Pressed;

                case Buttons.LeftShoulder:
                    return gamePadState[(int)index_].Buttons.LeftShoulder == ButtonState.Released &&
                        gamePadStateLastFrame[(int)index_].Buttons.LeftShoulder == ButtonState.Pressed;

                case Buttons.RightShoulder:
                    return gamePadState[(int)index_].Buttons.RightShoulder == ButtonState.Released &&
                        gamePadStateLastFrame[(int)index_].Buttons.RightShoulder == ButtonState.Pressed;

                case Buttons.LeftStick:
                    return gamePadState[(int)index_].Buttons.LeftStick == ButtonState.Released &&
                        gamePadStateLastFrame[(int)index_].Buttons.LeftStick == ButtonState.Pressed;

                case Buttons.RightStick:
                    return gamePadState[(int)index_].Buttons.RightStick == ButtonState.Released &&
                        gamePadStateLastFrame[(int)index_].Buttons.RightStick == ButtonState.Pressed;

                case Buttons.LeftTrigger:
                    return gamePadStateLastFrame[(int)index_].Triggers.Left > 0.75f &&
                        gamePadState[(int)index_].Triggers.Left == 0.0f; // 0.1f ??

                case Buttons.RightTrigger:
                    return gamePadStateLastFrame[(int)index_].Triggers.Right > 0.75f &&
                        gamePadState[(int)index_].Triggers.Right == 0.0f; // 0.1f ??
            }

            throw new Exception("Buttons not supported : " + button_);
        }

        public bool GamePadLeftReleased(PlayerIndex index_)
        {
            return (gamePadState[(int)index_].DPad.Left == ButtonState.Pressed &&
                gamePadStateLastFrame[(int)index_].DPad.Left == ButtonState.Released) ||
                (gamePadState[(int)index_].ThumbSticks.Left.X <= -0.75f &&
                gamePadStateLastFrame[(int)index_].ThumbSticks.Left.X >= -0.75f);
        }

        public bool GamePadRightReleased(PlayerIndex index_)
        {
            return (gamePadState[(int)index_].DPad.Right == ButtonState.Pressed &&
                gamePadStateLastFrame[(int)index_].DPad.Right == ButtonState.Released) ||
                (gamePadState[(int)index_].ThumbSticks.Left.X >= 0.75f &&
                gamePadStateLastFrame[(int)index_].ThumbSticks.Left.X <= 0.75f);
        }

        public bool GamePadUpReleased(PlayerIndex index_)
        {
            return (gamePadState[(int)index_].DPad.Up == ButtonState.Pressed &&
                gamePadStateLastFrame[(int)index_].DPad.Up == ButtonState.Released) ||
                (gamePadState[(int)index_].ThumbSticks.Left.Y >= 0.75f &&
                gamePadStateLastFrame[(int)index_].ThumbSticks.Left.Y <= 0.75f);
        }

        public bool GamePadDownReleased(PlayerIndex index_)
        {
            return (gamePadState[(int)index_].DPad.Down == ButtonState.Pressed &&
                gamePadStateLastFrame[(int)index_].DPad.Down == ButtonState.Released) ||
                (gamePadState[(int)index_].ThumbSticks.Left.Y <= -0.75f &&
                gamePadStateLastFrame[(int)index_].ThumbSticks.Left.Y >= -0.75f);
        }

        public bool GamePadDPadUpReleased(PlayerIndex index_)
        {
            return gamePadState[(int)index_].DPad.Up == ButtonState.Pressed;
        }

        public bool GamePadDPadDownReleased(PlayerIndex index_)
        {
            return (gamePadState[(int)index_].DPad.Down == ButtonState.Released &&
                gamePadStateLastFrame[(int)index_].DPad.Down == ButtonState.Pressed);
        }

        public bool GamePadDPadRightReleased(PlayerIndex index_)
        {
            return (gamePadState[(int)index_].DPad.Right == ButtonState.Released &&
                gamePadStateLastFrame[(int)index_].DPad.Right == ButtonState.Pressed);
        }

        public bool GamePadDPadLeftReleased(PlayerIndex index_)
        {
            return (gamePadState[(int)index_].DPad.Left == ButtonState.Released &&
                gamePadStateLastFrame[(int)index_].DPad.Left == ButtonState.Pressed);
        }



        public bool IsButtonJustHeld(PlayerIndex index_, Buttons button_)
        {
            switch (button_)
            {
                case Buttons.A:
                    return gamePadState[(int)index_].Buttons.A == ButtonState.Pressed &&
                        gamePadStateLastFrame[(int)index_].Buttons.A == ButtonState.Pressed;

                case Buttons.B:
                    return gamePadState[(int)index_].Buttons.B == ButtonState.Pressed &&
                        gamePadStateLastFrame[(int)index_].Buttons.B == ButtonState.Pressed;

                case Buttons.X:
                    return gamePadState[(int)index_].Buttons.X == ButtonState.Pressed &&
                        gamePadStateLastFrame[(int)index_].Buttons.X == ButtonState.Pressed;

                case Buttons.Y:
                    return gamePadState[(int)index_].Buttons.Y == ButtonState.Pressed &&
                        gamePadStateLastFrame[(int)index_].Buttons.Y == ButtonState.Pressed;

                case Buttons.Back:
                    return gamePadState[(int)index_].Buttons.Back == ButtonState.Pressed &&
                        gamePadStateLastFrame[(int)index_].Buttons.Back == ButtonState.Pressed;

                case Buttons.Start:
                    return gamePadState[(int)index_].Buttons.Start == ButtonState.Pressed &&
                        gamePadStateLastFrame[(int)index_].Buttons.Start == ButtonState.Pressed;

                case Buttons.LeftShoulder:
                    return gamePadState[(int)index_].Buttons.LeftShoulder == ButtonState.Pressed &&
                        gamePadStateLastFrame[(int)index_].Buttons.LeftShoulder == ButtonState.Pressed;

                case Buttons.RightShoulder:
                    return gamePadState[(int)index_].Buttons.RightShoulder == ButtonState.Pressed &&
                        gamePadStateLastFrame[(int)index_].Buttons.RightShoulder == ButtonState.Pressed;

                case Buttons.LeftStick:
                    return gamePadState[(int)index_].Buttons.LeftStick == ButtonState.Pressed &&
                        gamePadStateLastFrame[(int)index_].Buttons.LeftStick == ButtonState.Pressed;

                case Buttons.RightStick:
                    return gamePadState[(int)index_].Buttons.RightStick == ButtonState.Pressed &&
                        gamePadStateLastFrame[(int)index_].Buttons.RightStick == ButtonState.Pressed;

                case Buttons.LeftTrigger:
                    return gamePadState[(int)index_].Triggers.Left >= 0.75f &&
                        gamePadStateLastFrame[(int)index_].Triggers.Left >= 0.75f;

                case Buttons.RightTrigger:
                    return gamePadState[(int)index_].Triggers.Right >= 0.75f &&
                        gamePadStateLastFrame[(int)index_].Triggers.Right >= 0.75f;
            }

            throw new Exception("Button not supported : " + button_);
        }

        public bool GamePadLeftHeld(PlayerIndex index_)
        {
            return (gamePadState[(int)index_].DPad.Left == ButtonState.Pressed &&
                gamePadStateLastFrame[(int)index_].DPad.Left == ButtonState.Pressed) ||
                (gamePadState[(int)index_].ThumbSticks.Left.X >= -0.75f &&
                gamePadStateLastFrame[(int)index_].ThumbSticks.Left.X >= -0.75f);
        }

        public bool GamePadRightHeld(PlayerIndex index_)
        {
            return (gamePadState[(int)index_].DPad.Right == ButtonState.Pressed &&
                gamePadStateLastFrame[(int)index_].DPad.Right == ButtonState.Pressed) ||
                (gamePadState[(int)index_].ThumbSticks.Left.X >= 0.75f &&
                gamePadStateLastFrame[(int)index_].ThumbSticks.Left.X >= 0.75f);
        }

        public bool GamePadUpHeld(PlayerIndex index_)
        {
            return (gamePadState[(int)index_].DPad.Up == ButtonState.Pressed &&
                gamePadStateLastFrame[(int)index_].DPad.Up == ButtonState.Pressed) ||
                (gamePadState[(int)index_].ThumbSticks.Left.Y >= 0.75f &&
                gamePadStateLastFrame[(int)index_].ThumbSticks.Left.Y >= 0.75f);
        }

        public bool GamePadDownHeld(PlayerIndex index_)
        {
            return (gamePadState[(int)index_].DPad.Down == ButtonState.Pressed &&
                gamePadStateLastFrame[(int)index_].DPad.Down == ButtonState.Pressed) ||
                (gamePadState[(int)index_].ThumbSticks.Left.Y >= -0.75f &&
                gamePadStateLastFrame[(int)index_].ThumbSticks.Left.Y >= -0.75f);
        }

        public bool GamePadDPadDownHeld(PlayerIndex index_)
        {
            return (gamePadState[(int)index_].DPad.Down == ButtonState.Pressed &&
                gamePadStateLastFrame[(int)index_].DPad.Down == ButtonState.Pressed);
        }

        public bool GamePadDPadRightHeld(PlayerIndex index_)
        {
            return (gamePadState[(int)index_].DPad.Right == ButtonState.Pressed &&
                gamePadStateLastFrame[(int)index_].DPad.Right == ButtonState.Pressed);
        }

        public bool GamePadDPadLeftHeld(PlayerIndex index_)
        {
            return (gamePadState[(int)index_].DPad.Left == ButtonState.Pressed &&
                gamePadStateLastFrame[(int)index_].DPad.Left == ButtonState.Pressed);
        }

        public bool GamePadDPadUpHeld(PlayerIndex index_)
        {
            return (gamePadState[(int)index_].DPad.Up == ButtonState.Pressed &&
                gamePadStateLastFrame[(int)index_].DPad.Up == ButtonState.Pressed);
        }




        public void SetCurrentConfiguration(ButtonConfiguration buttonConfiguration_)
        {
            m_ButtonConfiguration = buttonConfiguration_;
            m_KeysState = new InputManager.KeyState[m_ButtonConfiguration.ButtonCount];
        }

        /*public bool InputConfigButtonJustPressed(PlayerIndex index_, int code_)
		{
			Buttons but = m_InputConfigurations.GetConfig(m_CurrentInputConfigurationName).GetButton(code_);

			bool res = gamePadState[(int)index_].IsButtonDown(but) &&
				gamePadStateLastFrame[(int)index_].IsButtonUp(but);

			res |= keyboardState.IsKeyDown(Keys.Down) &&
					keysPressedLastFrame.Contains(Keys.Down) == false;

			return res;
		}*/



        public InputComponent(Microsoft.Xna.Framework.Game game_)
            : base(game_)
        {
            if (game_ == null)
            {
                throw new ArgumentNullException("game");
            }

            Game.Components.Add(this);

            UpdateOrder = (int)ComponentUpdateOrder.Input;

            m_InputManager = new InputManager[4];

            for (int i = 0; i < m_InputManager.Length; i++)
            {
                m_InputManager[i] = new InputManager();
            }

            //TODO : add default config
            m_ButtonConfiguration = new ButtonConfiguration();
            ButtonMapper map = new ButtonMapper();
            map.Buttons = Buttons.A;
            map.Key = Keys.Enter;
            map.Name = "A";
            m_ButtonConfiguration.AddButton(1, map);

            SetCurrentConfiguration(m_ButtonConfiguration);
        }



        public override void Initialize()
        {
            base.Initialize();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing == true)
            {
                lock (this)
                {
                    // Remove self from the service container.
                    GameHelper.RemoveGameComponent<InputComponent>(this.Game);
                }
            }

            base.Dispose(disposing);
        }


        public override void Update(GameTime gameTime_)
        {
#if XBOX360
            // No mouse support on the XBox360 yet :(
            mouseDetected = false;
#else
            // Handle mouse input variables
            mouseStateLastFrame = mouseState;
            mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();

            // Update mouseXMovement and mouseYMovement
            lastMouseXMovement += mouseState.X - mouseStateLastFrame.X;
            lastMouseYMovement += mouseState.Y - mouseStateLastFrame.Y;

            if (System.Math.Abs(lastMouseXMovement) < 1.0f)
            {
                lastMouseXMovement = 0.0f;
                mouseXMovement = 0.0f;
            }
            else
            {
                mouseXMovement = lastMouseXMovement / 2.0f;
                lastMouseXMovement -= lastMouseXMovement / 2.0f;
            }

            if (System.Math.Abs(lastMouseYMovement) < 1.0f)
            {
                lastMouseYMovement = 0.0f;
                mouseYMovement = 0.0f;
            }
            else
            {
                mouseYMovement = lastMouseYMovement / 2.0f;
                lastMouseYMovement -= lastMouseYMovement / 2.0f;
            }

            if (MouseLeftButtonPressed == false)
                startDraggingPos = MousePos;
#if EDITOR
            mouseWheelDelta = ms_MouseWheel / 120;
            ms_MouseWheel = 0;
#else
			//mouseWheelDelta = mouseState.ScrollWheelValue - mouseWheelValue;
			//mouseWheelValue = mouseState.ScrollWheelValue;
#endif

            // If we are in the game and don't show the mouse cursor anyway,
            // reset it to the center to allow moving it around.
            //TODO
            /*if (RacingGameManager.InMenu == false &&
                // App must be active
                RacingGameManager.IsAppActive)
            {
                Microsoft.Xna.Framework.Input.Mouse.SetPosition(
                    BaseGame.Width / 2, BaseGame.Height / 2);
                // Also use this for the current mouse pos for next frame,
                // else the mouseXMovement is messed up!
                mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
            }*/

            // Check if mouse was moved this frame if it is not detected yet.
            // This allows us to ignore the mouse even when it is captured
            // on a windows machine if just the gamepad or keyboard is used.
            if (mouseDetected == false)// &&
                //always returns false: Microsoft.Xna.Framework.Input.Mouse.IsCaptured)
                mouseDetected = mouseState.X != mouseStateLastFrame.X ||
                    mouseState.Y != mouseStateLastFrame.Y ||
                    mouseState.LeftButton != mouseStateLastFrame.LeftButton;
#endif

            // Handle keyboard input
            //keysPressedLastFrame = new List<Keys>(keyboardState.GetPressedKeys());
            keyboardPreviousState = keyboardState;
            keyboardState = Microsoft.Xna.Framework.Input.Keyboard.GetState();

            // And finally catch the XBox Controller input
            for (int i = 0; i < 4; i++)
            {
                gamePadStateLastFrame[i] = gamePadState[i];
                gamePadState[i] =
                    Microsoft.Xna.Framework.Input.GamePad.GetState((PlayerIndex)i, gamePadDeadZoneMode[i]);
            }

            // Update all InputManager
            Dictionary<int, ButtonMapper>.Enumerator enumerator = m_ButtonConfiguration.Buttons;
            int j = 0;

            float elapsedTime = GameTimeHelper.GameTimeToMilliseconds(gameTime_);

            //create button buffer
            while (enumerator.MoveNext())
            {
                m_KeysState[j].Time = elapsedTime;
                m_KeysState[j].Key = enumerator.Current.Key;

                if (IsButtonPressed(m_ButtonConfiguration.PlayerIndex, enumerator.Current.Value.Buttons) == true)
                {
                    m_KeysState[j].State = ButtonState.Pressed;
                }
                else
                {
                    m_KeysState[j].State = ButtonState.Released;
                }

                j++;
            }

            foreach (var input in m_InputManager)
            {
                input.Update(m_KeysState,
                    GameTimeHelper.TotalGameTimeToMilliseconds(gameTime_));
            }

            base.Update(gameTime_);
        }



        public InputManager GetInputManager(PlayerIndex index_)
        {
            return m_InputManager[(int)index_];
        }

    }
}
