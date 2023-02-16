using CasaEngine.Core.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

using CasaEngine.Game;
using CasaEngine.Input.InputSequence;
using CasaEngineCommon.Helper;





namespace CasaEngine.Input
{
    public class InputComponent :
        Microsoft.Xna.Framework.GameComponent
    {

#if !XBOX360
        private MouseState _mouseState, _mouseStateLastFrame;
#endif

        private bool _mouseDetected;

        private KeyboardState _keyboardPreviousState = Microsoft.Xna.Framework.Input.Keyboard.GetState();
        private KeyboardState _keyboardState;

        //private List<Keys> keysPressedLastFrame = new List<Keys>();

        private readonly GamePadState[] _gamePadState = new GamePadState[4];
        private readonly GamePadState[] _gamePadStateLastFrame = new GamePadState[4];
        private readonly GamePadDeadZone[] _gamePadDeadZoneMode = new GamePadDeadZone[4];
        private readonly GamePadCapabilities[] _gamePadCapabilities = new GamePadCapabilities[4];

        //private PlayerIndex playerIndex = PlayerIndex.One;

#if !XBOX360
        private int _mouseWheelDelta;
#endif

#if EDITOR
        public static int MsMouseWheel;
#endif

        private Point _startDraggingPos;

        public readonly float Deadzone = 0.2f;

        //InputConfigurations _InputConfigurations = new InputConfigurations();
        ButtonConfiguration _buttonConfiguration;
        InputSequence.InputManager.KeyState[] _keysState;

        readonly InputSequence.InputManager[] _inputManager;



        public InputSequence.InputManager.KeyState[] KeysState => _keysState;


        public bool MouseDetected => _mouseDetected;

        public Point MousePos
        {
            get
            {
#if !XBOX360
                return new Point(_mouseState.X, _mouseState.Y);
#else
                return Point.Zero;
#endif
            }
        }

#if !XBOX360
        private float _mouseXMovement, _mouseYMovement;
        private float _lastMouseXMovement, _lastMouseYMovement;
#endif

        public float MouseXMovement
        {
            get
            {
#if !XBOX360
                return _mouseXMovement;
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
                return _mouseYMovement;
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
                {
                    return true;
                }
#endif
                return false;
            }
        }

        public bool MouseLeftButtonPressed
        {
            get
            {
#if !XBOX360
                return _mouseState.LeftButton == ButtonState.Pressed;
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
                return _mouseState.RightButton == ButtonState.Pressed;
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
                return _mouseState.MiddleButton == ButtonState.Pressed;
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
                return _mouseState.LeftButton == ButtonState.Pressed &&
                       _mouseStateLastFrame.LeftButton == ButtonState.Released;
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
                return _mouseState.RightButton == ButtonState.Pressed &&
                       _mouseStateLastFrame.RightButton == ButtonState.Released;
#else
                return false;
#endif
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode",
            Justification = "Makes this class reuseable.")]
        public Point MouseDraggingAmount =>
            new(
                _startDraggingPos.X - MousePos.X,
                _startDraggingPos.Y - MousePos.Y);

        public void ResetMouseDraggingAmount()
        {
            _startDraggingPos = MousePos;
        }

        public int MouseWheelDelta
        {
            get
            {
                return
#if EDITOR 
                    MsMouseWheel;
#else
                    mouseWheelDelta;
#endif
            }
        }

        public bool MouseInBox(Rectangle rect)
        {
#if !XBOX360
            var ret = _mouseState.X >= rect.X &&
                      _mouseState.Y >= rect.Y &&
                      _mouseState.X < rect.Right &&
                      _mouseState.Y < rect.Bottom;
            var lastRet = _mouseStateLastFrame.X >= rect.X &&
                          _mouseStateLastFrame.Y >= rect.Y &&
                          _mouseStateLastFrame.X < rect.Right &&
                          _mouseStateLastFrame.Y < rect.Bottom;

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

        public KeyboardState Keyboard => _keyboardState;

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
            var keyNum = (int)key;
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
            var ret = ' ';
            var keyNum = (int)key;
            if (keyNum >= (int)Keys.A && keyNum <= (int)Keys.Z)
            {
                if (shiftPressed)
                {
                    ret = key.ToString()[0];
                }
                else
                {
                    ret = key.ToString().ToLower()[0];
                }
            }
            else if (keyNum >= (int)Keys.D0 && keyNum <= (int)Keys.D9 &&
                shiftPressed == false)
            {
                ret = (char)((int)'0' + (keyNum - Keys.D0));
            }
            else if (key == Keys.D1 && shiftPressed)
            {
                ret = '!';
            }
            else if (key == Keys.D2 && shiftPressed)
            {
                ret = '@';
            }
            else if (key == Keys.D3 && shiftPressed)
            {
                ret = '#';
            }
            else if (key == Keys.D4 && shiftPressed)
            {
                ret = '$';
            }
            else if (key == Keys.D5 && shiftPressed)
            {
                ret = '%';
            }
            else if (key == Keys.D6 && shiftPressed)
            {
                ret = '^';
            }
            else if (key == Keys.D7 && shiftPressed)
            {
                ret = '&';
            }
            else if (key == Keys.D8 && shiftPressed)
            {
                ret = '*';
            }
            else if (key == Keys.D9 && shiftPressed)
            {
                ret = '(';
            }
            else if (key == Keys.D0 && shiftPressed)
            {
                ret = ')';
            }
            else if (key == Keys.OemTilde)
            {
                ret = shiftPressed ? '~' : '`';
            }
            else if (key == Keys.OemMinus)
            {
                ret = shiftPressed ? '_' : '-';
            }
            else if (key == Keys.OemPipe)
            {
                ret = shiftPressed ? '|' : '\\';
            }
            else if (key == Keys.OemOpenBrackets)
            {
                ret = shiftPressed ? '{' : '[';
            }
            else if (key == Keys.OemCloseBrackets)
            {
                ret = shiftPressed ? '}' : ']';
            }
            else if (key == Keys.OemSemicolon)
            {
                ret = shiftPressed ? ':' : ';';
            }
            else if (key == Keys.OemQuotes)
            {
                ret = shiftPressed ? '"' : '\'';
            }
            else if (key == Keys.OemComma)
            {
                ret = shiftPressed ? '<' : '.';
            }
            else if (key == Keys.OemPeriod)
            {
                ret = shiftPressed ? '>' : ',';
            }
            else if (key == Keys.OemQuestion)
            {
                ret = shiftPressed ? '?' : '/';
            }
            else if (key == Keys.OemPlus)
            {
                ret = shiftPressed ? '+' : '=';
            }

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
                        // IsRemoved 1 character at end
                        inputText = inputText.Substring(0, inputText.Length - 1);
                    }
                }
        }*/

        public bool IsKeyJustPressed(Keys key)
        {
            return _keyboardState.IsKeyDown(key)
                && _keyboardPreviousState.IsKeyDown(key) == false;
        }

        public bool IsKeyPressed(Keys key)
        {
            return _keyboardState.IsKeyDown(key);
        }

        public bool IsKeyReleased(Keys key)
        {
            return _keyboardState.IsKeyUp(key)
                && _keyboardPreviousState.IsKeyDown(key);
        }

        public bool IsKeyHeld(Keys key)
        {
            return _keyboardState.IsKeyDown(key)
                && _keyboardPreviousState.IsKeyDown(key);
        }


        public GamePadState GamePadState(PlayerIndex index)
        {
            return _gamePadState[(int)index];
        }

        public GamePadCapabilities GamePadCapabilities(PlayerIndex index)
        {
            _gamePadCapabilities[(int)index] = Microsoft.Xna.Framework.Input.GamePad.GetCapabilities(index);
            return _gamePadCapabilities[(int)index];
        }

        public void GamePadDeadZoneMode(PlayerIndex index, GamePadDeadZone mode)
        {
            _gamePadDeadZoneMode[(int)index] = mode;
        }

        public bool IsGamePadConnected(PlayerIndex index)
        {
            return _gamePadState[(int)index].IsConnected;
        }


        public bool IsButtonJustPressed(PlayerIndex index, Buttons button)
        {
            switch (button)
            {
                case Buttons.A:
                    return _gamePadState[(int)index].Buttons.A == ButtonState.Pressed &&
                        _gamePadStateLastFrame[(int)index].Buttons.A == ButtonState.Released;

                case Buttons.B:
                    return _gamePadState[(int)index].Buttons.B == ButtonState.Pressed &&
                        _gamePadStateLastFrame[(int)index].Buttons.B == ButtonState.Released;

                case Buttons.X:
                    return _gamePadState[(int)index].Buttons.X == ButtonState.Pressed &&
                        _gamePadStateLastFrame[(int)index].Buttons.X == ButtonState.Released;

                case Buttons.Y:
                    return _gamePadState[(int)index].Buttons.Y == ButtonState.Pressed &&
                        _gamePadStateLastFrame[(int)index].Buttons.Y == ButtonState.Released;

                case Buttons.Back:
                    return _gamePadState[(int)index].Buttons.Back == ButtonState.Pressed &&
                        _gamePadStateLastFrame[(int)index].Buttons.Back == ButtonState.Released;

                case Buttons.Start:
                    return _gamePadState[(int)index].Buttons.Start == ButtonState.Pressed &&
                        _gamePadStateLastFrame[(int)index].Buttons.Start == ButtonState.Released;

                case Buttons.LeftShoulder:
                    return _gamePadState[(int)index].Buttons.LeftShoulder == ButtonState.Pressed &&
                        _gamePadStateLastFrame[(int)index].Buttons.LeftShoulder == ButtonState.Released;

                case Buttons.RightShoulder:
                    return _gamePadState[(int)index].Buttons.RightShoulder == ButtonState.Pressed &&
                        _gamePadStateLastFrame[(int)index].Buttons.RightShoulder == ButtonState.Released;

                case Buttons.LeftStick:
                    return _gamePadState[(int)index].Buttons.LeftStick == ButtonState.Pressed &&
                        _gamePadStateLastFrame[(int)index].Buttons.LeftStick == ButtonState.Released;

                case Buttons.RightStick:
                    return _gamePadState[(int)index].Buttons.RightStick == ButtonState.Pressed &&
                        _gamePadStateLastFrame[(int)index].Buttons.RightStick == ButtonState.Released;

                case Buttons.LeftTrigger:
                    return _gamePadState[(int)index].Triggers.Left > 0.75f &&
                        _gamePadStateLastFrame[(int)index].Triggers.Left == 0.0f;

                case Buttons.RightTrigger:
                    return _gamePadState[(int)index].Triggers.Right > 0.75f &&
                        _gamePadStateLastFrame[(int)index].Triggers.Right == 0.0f;
            }

            throw new Exception("Buttons non géré : " + button);
        }

        public bool GamePadLeftJustPressed(PlayerIndex index)
        {
            return (_gamePadState[(int)index].DPad.Left == ButtonState.Pressed &&
                _gamePadStateLastFrame[(int)index].DPad.Left == ButtonState.Released) ||
                (_gamePadState[(int)index].ThumbSticks.Left.X < -0.75f &&
                _gamePadStateLastFrame[(int)index].ThumbSticks.Left.X > -0.75f);
        }

        public bool GamePadRightJustPressed(PlayerIndex index)
        {
            return (_gamePadState[(int)index].DPad.Right == ButtonState.Pressed &&
                _gamePadStateLastFrame[(int)index].DPad.Right == ButtonState.Released) ||
                (_gamePadState[(int)index].ThumbSticks.Left.X > 0.75f &&
                _gamePadStateLastFrame[(int)index].ThumbSticks.Left.X < 0.75f);
        }

        public bool GamePadUpJustPressed(PlayerIndex index)
        {
            return (_gamePadState[(int)index].DPad.Up == ButtonState.Pressed &&
                _gamePadStateLastFrame[(int)index].DPad.Up == ButtonState.Released) ||
                (_gamePadState[(int)index].ThumbSticks.Left.Y > 0.75f &&
                _gamePadStateLastFrame[(int)index].ThumbSticks.Left.Y < 0.75f);
        }

        public bool GamePadDownJustPressed(PlayerIndex index)
        {
            return (_gamePadState[(int)index].DPad.Down == ButtonState.Pressed &&
                _gamePadStateLastFrame[(int)index].DPad.Down == ButtonState.Released) ||
                (_gamePadState[(int)index].ThumbSticks.Left.Y < -0.75f &&
                _gamePadStateLastFrame[(int)index].ThumbSticks.Left.Y > -0.75f);
        }

        public bool GamePadDPadUpPressed(PlayerIndex index)
        {
            return _gamePadState[(int)index].DPad.Up == ButtonState.Pressed;
        }

        public bool GamePadDPadDownJustPressed(PlayerIndex index)
        {
            return (_gamePadState[(int)index].DPad.Down == ButtonState.Pressed &&
                _gamePadStateLastFrame[(int)index].DPad.Down == ButtonState.Released);
        }

        public bool GamePadDPadRightJustPressed(PlayerIndex index)
        {
            return (_gamePadState[(int)index].DPad.Right == ButtonState.Pressed &&
                _gamePadStateLastFrame[(int)index].DPad.Right == ButtonState.Released);
        }

        public bool GamePadDPadLeftJustPressed(PlayerIndex index)
        {
            return (_gamePadState[(int)index].DPad.Left == ButtonState.Pressed &&
                _gamePadStateLastFrame[(int)index].DPad.Left == ButtonState.Released);
        }

        public bool GamePadDPadUpJustPressed(PlayerIndex index)
        {
            return (_gamePadState[(int)index].DPad.Up == ButtonState.Pressed &&
                _gamePadStateLastFrame[(int)index].DPad.Up == ButtonState.Released);
        }



        public bool IsButtonPressed(PlayerIndex index, Buttons button)
        {
            return _gamePadState[(int)index].IsButtonDown(button);

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

            throw new Exception("Buttons not supported : " + button);
        }

        public bool GamePadLeftPressed(PlayerIndex index)
        {
            return _gamePadState[(int)index].DPad.Left == ButtonState.Pressed ||
                _gamePadState[(int)index].ThumbSticks.Left.X <= -0.75f;
        }

        public bool GamePadRightPressed(PlayerIndex index)
        {
            return _gamePadState[(int)index].DPad.Right == ButtonState.Pressed ||
                _gamePadState[(int)index].ThumbSticks.Left.X >= 0.75f;
        }

        public bool GamePadUpPressed(PlayerIndex index)
        {
            return _gamePadState[(int)index].DPad.Up == ButtonState.Pressed ||
                _gamePadState[(int)index].ThumbSticks.Left.Y >= 0.75f;
        }

        public bool GamePadDownPressed(PlayerIndex index)
        {
            return _gamePadState[(int)index].DPad.Down == ButtonState.Pressed ||
                _gamePadState[(int)index].ThumbSticks.Left.Y <= -0.75f;
        }



        public bool IsButtonJustReleased(PlayerIndex index, Buttons button)
        {
            switch (button)
            {
                case Buttons.A:
                    return _gamePadState[(int)index].Buttons.A == ButtonState.Released &&
                        _gamePadStateLastFrame[(int)index].Buttons.A == ButtonState.Pressed;

                case Buttons.B:
                    return _gamePadState[(int)index].Buttons.B == ButtonState.Released &&
                        _gamePadStateLastFrame[(int)index].Buttons.B == ButtonState.Pressed;

                case Buttons.X:
                    return _gamePadState[(int)index].Buttons.X == ButtonState.Released &&
                        _gamePadStateLastFrame[(int)index].Buttons.X == ButtonState.Pressed;

                case Buttons.Y:
                    return _gamePadState[(int)index].Buttons.Y == ButtonState.Released &&
                        _gamePadStateLastFrame[(int)index].Buttons.Y == ButtonState.Pressed;

                case Buttons.Back:
                    return _gamePadState[(int)index].Buttons.Back == ButtonState.Released &&
                        _gamePadStateLastFrame[(int)index].Buttons.Back == ButtonState.Pressed;

                case Buttons.Start:
                    return _gamePadState[(int)index].Buttons.Start == ButtonState.Released &&
                        _gamePadStateLastFrame[(int)index].Buttons.Start == ButtonState.Pressed;

                case Buttons.LeftShoulder:
                    return _gamePadState[(int)index].Buttons.LeftShoulder == ButtonState.Released &&
                        _gamePadStateLastFrame[(int)index].Buttons.LeftShoulder == ButtonState.Pressed;

                case Buttons.RightShoulder:
                    return _gamePadState[(int)index].Buttons.RightShoulder == ButtonState.Released &&
                        _gamePadStateLastFrame[(int)index].Buttons.RightShoulder == ButtonState.Pressed;

                case Buttons.LeftStick:
                    return _gamePadState[(int)index].Buttons.LeftStick == ButtonState.Released &&
                        _gamePadStateLastFrame[(int)index].Buttons.LeftStick == ButtonState.Pressed;

                case Buttons.RightStick:
                    return _gamePadState[(int)index].Buttons.RightStick == ButtonState.Released &&
                        _gamePadStateLastFrame[(int)index].Buttons.RightStick == ButtonState.Pressed;

                case Buttons.LeftTrigger:
                    return _gamePadStateLastFrame[(int)index].Triggers.Left > 0.75f &&
                        _gamePadState[(int)index].Triggers.Left == 0.0f; // 0.1f ??

                case Buttons.RightTrigger:
                    return _gamePadStateLastFrame[(int)index].Triggers.Right > 0.75f &&
                        _gamePadState[(int)index].Triggers.Right == 0.0f; // 0.1f ??
            }

            throw new Exception("Buttons not supported : " + button);
        }

        public bool GamePadLeftReleased(PlayerIndex index)
        {
            return (_gamePadState[(int)index].DPad.Left == ButtonState.Pressed &&
                _gamePadStateLastFrame[(int)index].DPad.Left == ButtonState.Released) ||
                (_gamePadState[(int)index].ThumbSticks.Left.X <= -0.75f &&
                _gamePadStateLastFrame[(int)index].ThumbSticks.Left.X >= -0.75f);
        }

        public bool GamePadRightReleased(PlayerIndex index)
        {
            return (_gamePadState[(int)index].DPad.Right == ButtonState.Pressed &&
                _gamePadStateLastFrame[(int)index].DPad.Right == ButtonState.Released) ||
                (_gamePadState[(int)index].ThumbSticks.Left.X >= 0.75f &&
                _gamePadStateLastFrame[(int)index].ThumbSticks.Left.X <= 0.75f);
        }

        public bool GamePadUpReleased(PlayerIndex index)
        {
            return (_gamePadState[(int)index].DPad.Up == ButtonState.Pressed &&
                _gamePadStateLastFrame[(int)index].DPad.Up == ButtonState.Released) ||
                (_gamePadState[(int)index].ThumbSticks.Left.Y >= 0.75f &&
                _gamePadStateLastFrame[(int)index].ThumbSticks.Left.Y <= 0.75f);
        }

        public bool GamePadDownReleased(PlayerIndex index)
        {
            return (_gamePadState[(int)index].DPad.Down == ButtonState.Pressed &&
                _gamePadStateLastFrame[(int)index].DPad.Down == ButtonState.Released) ||
                (_gamePadState[(int)index].ThumbSticks.Left.Y <= -0.75f &&
                _gamePadStateLastFrame[(int)index].ThumbSticks.Left.Y >= -0.75f);
        }

        public bool GamePadDPadUpReleased(PlayerIndex index)
        {
            return _gamePadState[(int)index].DPad.Up == ButtonState.Pressed;
        }

        public bool GamePadDPadDownReleased(PlayerIndex index)
        {
            return (_gamePadState[(int)index].DPad.Down == ButtonState.Released &&
                _gamePadStateLastFrame[(int)index].DPad.Down == ButtonState.Pressed);
        }

        public bool GamePadDPadRightReleased(PlayerIndex index)
        {
            return (_gamePadState[(int)index].DPad.Right == ButtonState.Released &&
                _gamePadStateLastFrame[(int)index].DPad.Right == ButtonState.Pressed);
        }

        public bool GamePadDPadLeftReleased(PlayerIndex index)
        {
            return (_gamePadState[(int)index].DPad.Left == ButtonState.Released &&
                _gamePadStateLastFrame[(int)index].DPad.Left == ButtonState.Pressed);
        }



        public bool IsButtonJustHeld(PlayerIndex index, Buttons button)
        {
            switch (button)
            {
                case Buttons.A:
                    return _gamePadState[(int)index].Buttons.A == ButtonState.Pressed &&
                        _gamePadStateLastFrame[(int)index].Buttons.A == ButtonState.Pressed;

                case Buttons.B:
                    return _gamePadState[(int)index].Buttons.B == ButtonState.Pressed &&
                        _gamePadStateLastFrame[(int)index].Buttons.B == ButtonState.Pressed;

                case Buttons.X:
                    return _gamePadState[(int)index].Buttons.X == ButtonState.Pressed &&
                        _gamePadStateLastFrame[(int)index].Buttons.X == ButtonState.Pressed;

                case Buttons.Y:
                    return _gamePadState[(int)index].Buttons.Y == ButtonState.Pressed &&
                        _gamePadStateLastFrame[(int)index].Buttons.Y == ButtonState.Pressed;

                case Buttons.Back:
                    return _gamePadState[(int)index].Buttons.Back == ButtonState.Pressed &&
                        _gamePadStateLastFrame[(int)index].Buttons.Back == ButtonState.Pressed;

                case Buttons.Start:
                    return _gamePadState[(int)index].Buttons.Start == ButtonState.Pressed &&
                        _gamePadStateLastFrame[(int)index].Buttons.Start == ButtonState.Pressed;

                case Buttons.LeftShoulder:
                    return _gamePadState[(int)index].Buttons.LeftShoulder == ButtonState.Pressed &&
                        _gamePadStateLastFrame[(int)index].Buttons.LeftShoulder == ButtonState.Pressed;

                case Buttons.RightShoulder:
                    return _gamePadState[(int)index].Buttons.RightShoulder == ButtonState.Pressed &&
                        _gamePadStateLastFrame[(int)index].Buttons.RightShoulder == ButtonState.Pressed;

                case Buttons.LeftStick:
                    return _gamePadState[(int)index].Buttons.LeftStick == ButtonState.Pressed &&
                        _gamePadStateLastFrame[(int)index].Buttons.LeftStick == ButtonState.Pressed;

                case Buttons.RightStick:
                    return _gamePadState[(int)index].Buttons.RightStick == ButtonState.Pressed &&
                        _gamePadStateLastFrame[(int)index].Buttons.RightStick == ButtonState.Pressed;

                case Buttons.LeftTrigger:
                    return _gamePadState[(int)index].Triggers.Left >= 0.75f &&
                        _gamePadStateLastFrame[(int)index].Triggers.Left >= 0.75f;

                case Buttons.RightTrigger:
                    return _gamePadState[(int)index].Triggers.Right >= 0.75f &&
                        _gamePadStateLastFrame[(int)index].Triggers.Right >= 0.75f;
            }

            throw new Exception("Button not supported : " + button);
        }

        public bool GamePadLeftHeld(PlayerIndex index)
        {
            return (_gamePadState[(int)index].DPad.Left == ButtonState.Pressed &&
                _gamePadStateLastFrame[(int)index].DPad.Left == ButtonState.Pressed) ||
                (_gamePadState[(int)index].ThumbSticks.Left.X >= -0.75f &&
                _gamePadStateLastFrame[(int)index].ThumbSticks.Left.X >= -0.75f);
        }

        public bool GamePadRightHeld(PlayerIndex index)
        {
            return (_gamePadState[(int)index].DPad.Right == ButtonState.Pressed &&
                _gamePadStateLastFrame[(int)index].DPad.Right == ButtonState.Pressed) ||
                (_gamePadState[(int)index].ThumbSticks.Left.X >= 0.75f &&
                _gamePadStateLastFrame[(int)index].ThumbSticks.Left.X >= 0.75f);
        }

        public bool GamePadUpHeld(PlayerIndex index)
        {
            return (_gamePadState[(int)index].DPad.Up == ButtonState.Pressed &&
                _gamePadStateLastFrame[(int)index].DPad.Up == ButtonState.Pressed) ||
                (_gamePadState[(int)index].ThumbSticks.Left.Y >= 0.75f &&
                _gamePadStateLastFrame[(int)index].ThumbSticks.Left.Y >= 0.75f);
        }

        public bool GamePadDownHeld(PlayerIndex index)
        {
            return (_gamePadState[(int)index].DPad.Down == ButtonState.Pressed &&
                _gamePadStateLastFrame[(int)index].DPad.Down == ButtonState.Pressed) ||
                (_gamePadState[(int)index].ThumbSticks.Left.Y >= -0.75f &&
                _gamePadStateLastFrame[(int)index].ThumbSticks.Left.Y >= -0.75f);
        }

        public bool GamePadDPadDownHeld(PlayerIndex index)
        {
            return (_gamePadState[(int)index].DPad.Down == ButtonState.Pressed &&
                _gamePadStateLastFrame[(int)index].DPad.Down == ButtonState.Pressed);
        }

        public bool GamePadDPadRightHeld(PlayerIndex index)
        {
            return (_gamePadState[(int)index].DPad.Right == ButtonState.Pressed &&
                _gamePadStateLastFrame[(int)index].DPad.Right == ButtonState.Pressed);
        }

        public bool GamePadDPadLeftHeld(PlayerIndex index)
        {
            return (_gamePadState[(int)index].DPad.Left == ButtonState.Pressed &&
                _gamePadStateLastFrame[(int)index].DPad.Left == ButtonState.Pressed);
        }

        public bool GamePadDPadUpHeld(PlayerIndex index)
        {
            return (_gamePadState[(int)index].DPad.Up == ButtonState.Pressed &&
                _gamePadStateLastFrame[(int)index].DPad.Up == ButtonState.Pressed);
        }




        public void SetCurrentConfiguration(ButtonConfiguration buttonConfiguration)
        {
            _buttonConfiguration = buttonConfiguration;
            _keysState = new InputSequence.InputManager.KeyState[_buttonConfiguration.ButtonCount];
        }

        /*public bool InputConfigButtonJustPressed(PlayerIndex index_, int code_)
		{
			Buttons but = _InputConfigurations.GetConfig(_CurrentInputConfigurationName).GetButton(code_);

			bool res = gamePadState[(int)index_].IsButtonDown(but) &&
				gamePadStateLastFrame[(int)index_].IsButtonUp(but);

			res |= keyboardState.IsKeyDown(Keys.Down) &&
					keysPressedLastFrame.Contains(Keys.Down) == false;

			return res;
		}*/



        public InputComponent(Microsoft.Xna.Framework.Game game)
            : base(game)
        {
            if (game == null)
            {
                throw new ArgumentNullException("game");
            }

            Game.Components.Add(this);

            UpdateOrder = (int)ComponentUpdateOrder.Input;

            _inputManager = new InputSequence.InputManager[4];

            for (var i = 0; i < _inputManager.Length; i++)
            {
                _inputManager[i] = new InputSequence.InputManager();
            }

            //TODO : add default config
            _buttonConfiguration = new ButtonConfiguration();
            var map = new ButtonMapper();
            map.Buttons = Buttons.A;
            map.Key = Keys.Enter;
            map.Name = "A";
            _buttonConfiguration.AddButton(1, map);

            SetCurrentConfiguration(_buttonConfiguration);
        }



        public override void Initialize()
        {
            base.Initialize();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (this)
                {
                    // IsRemoved self from the service container.
                    Game.RemoveGameComponent<InputComponent>();
                }
            }

            base.Dispose(disposing);
        }


        public override void Update(GameTime gameTime)
        {
#if XBOX360
            // No mouse support on the XBox360 yet :(
            mouseDetected = false;
#else
            // Handle mouse input variables
            _mouseStateLastFrame = _mouseState;
            _mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();

            // Update mouseXMovement and mouseYMovement
            _lastMouseXMovement += _mouseState.X - _mouseStateLastFrame.X;
            _lastMouseYMovement += _mouseState.Y - _mouseStateLastFrame.Y;

            if (Math.Abs(_lastMouseXMovement) < 1.0f)
            {
                _lastMouseXMovement = 0.0f;
                _mouseXMovement = 0.0f;
            }
            else
            {
                _mouseXMovement = _lastMouseXMovement / 2.0f;
                _lastMouseXMovement -= _lastMouseXMovement / 2.0f;
            }

            if (Math.Abs(_lastMouseYMovement) < 1.0f)
            {
                _lastMouseYMovement = 0.0f;
                _mouseYMovement = 0.0f;
            }
            else
            {
                _mouseYMovement = _lastMouseYMovement / 2.0f;
                _lastMouseYMovement -= _lastMouseYMovement / 2.0f;
            }

            if (MouseLeftButtonPressed == false)
            {
                _startDraggingPos = MousePos;
            }
#if EDITOR
            _mouseWheelDelta = MsMouseWheel / 120;
            MsMouseWheel = 0;
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
            if (_mouseDetected == false)// &&
                                        //always returns false: Microsoft.Xna.Framework.Input.Mouse.IsCaptured)
            {
                _mouseDetected = _mouseState.X != _mouseStateLastFrame.X ||
                                 _mouseState.Y != _mouseStateLastFrame.Y ||
                                 _mouseState.LeftButton != _mouseStateLastFrame.LeftButton;
            }
#endif

            // Handle keyboard input
            //keysPressedLastFrame = new List<Keys>(keyboardState.GetPressedKeys());
            _keyboardPreviousState = _keyboardState;
            _keyboardState = Microsoft.Xna.Framework.Input.Keyboard.GetState();

            // And finally catch the XBox Controller input
            for (var i = 0; i < 4; i++)
            {
                _gamePadStateLastFrame[i] = _gamePadState[i];
                _gamePadState[i] = Microsoft.Xna.Framework.Input.GamePad.GetState((PlayerIndex)i, _gamePadDeadZoneMode[i]);
            }

            // Update all InputManager
            var enumerator = _buttonConfiguration.Buttons;
            var j = 0;

            var elapsedTime = GameTimeHelper.GameTimeToMilliseconds(gameTime);

            //create button buffer
            while (enumerator.MoveNext())
            {
                _keysState[j].Time = elapsedTime;
                _keysState[j].Key = enumerator.Current.Key;

                if (IsButtonPressed(_buttonConfiguration.PlayerIndex, enumerator.Current.Value.Buttons))
                {
                    _keysState[j].State = ButtonState.Pressed;
                }
                else
                {
                    _keysState[j].State = ButtonState.Released;
                }

                j++;
            }

            foreach (var input in _inputManager)
            {
                input.Update(_keysState,
                    GameTimeHelper.TotalGameTimeToMilliseconds(gameTime));
            }

            base.Update(gameTime);
        }



        public InputSequence.InputManager GetInputManager(PlayerIndex index)
        {
            return _inputManager[(int)index];
        }

    }
}
