//-----------------------------------------------------------------------------
// InputManager.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------


namespace CasaEngine.Input
{
    public partial class InputManager
    {
        public struct KeyState
        {
            public int Key;
            public ButtonState State;
            public float Time;

            public bool Match(KeyState other_)
            {
                return Key == other_.Key
                    && State == other_.State
                    && Time >= other_.Time;
            }
        }

        public struct KeyStateFrame
        {
            public float GlobalTime;
            public List<KeyState> KeysState;

            public KeyStateFrame(float time_)
            {
                GlobalTime = time_;
                KeysState = new List<KeyState>();
            }
        }

        public List<KeyStateFrame> m_Buffer = new List<KeyStateFrame>(50);

        public KeyStateFrame[] Buffer => m_Buffer.ToArray();

        //public readonly TimeSpan MergeInputTime = TimeSpan.FromMilliseconds(100);
        public readonly float MergeInputTime = 0.1f;

        /*public InputConfigurations m_InputConfigurations = new InputConfigurations();
		public string m_CurrentInputConfig;

        public PlayerIndex PlayerIndex { get; private set; }

        public GamePadState GamePadState { get; private set; }
        public KeyboardState KeyboardState { get; private set; }

        public List<Buttons> Buffer;

        internal static readonly Dictionary<Buttons, Keys> NonDirectionButtons =
            new Dictionary<Buttons, Keys>
            {
                { Buttons.A, Keys.A },
                { Buttons.B, Keys.B },
                { Buttons.X, Keys.X },
                { Buttons.Y, Keys.Y },
                // Other available non-direction buttons:
                // Start, Back, LeftShoulder, LeftTrigger, LeftStick,
                // RightShoulder, RightTrigger, and RightStick.
            };


        public InputManager(PlayerIndex playerIndex, int bufferSize)
        {
            PlayerIndex = playerIndex;
            Buffer = new List<Buttons>(bufferSize);
        }

        public void Update(float elapsedTime_)//GameTime gameTime)
        {
            // Get latest input state.
            GamePadState lastGamePadState = GamePadState;
            KeyboardState lastKeyboardState = KeyboardState;
            GamePadState = GamePad.GetState(PlayerIndex);
#if WINDOWS
            if (PlayerIndex == PlayerIndex.One)
            {
                KeyboardState = Keyboard.GetState(PlayerIndex);
            }
#endif            

            // Expire old input.
            //TimeSpan time = gameTime.TotalRealTime;
            //TimeSpan timeSinceLast = time - LastInputTime;
			LastInputTime += elapsedTime_;
			if (LastInputTime > BufferTimeOut)
            {
                Buffer.Clear();
            }

            // Get all of the non-direction buttons pressed.
            Buttons buttons = 0;
            foreach (var buttonAndKey in NonDirectionButtons)
            {
                Buttons button = buttonAndKey.Key;
                Keys key = buttonAndKey.Value;

                // Check the game pad and keyboard for presses.
                if ((lastGamePadState.IsButtonUp(button) &&
                     GamePadState.IsButtonDown(button)) ||
                    (lastKeyboardState.IsKeyUp(key) &&
                     KeyboardState.IsKeyDown(key)))
                {
                    // Use a bitwise-or to accumulate button presses.
                    buttons |= button;
                }
            }

            // It is very hard to press two buttons on exactly the same frame.
            // If they are close enough, consider them pressed at the same time.
			bool mergeInput = (Buffer.Count > 0 && LastInputTime < MergeInputTime);

            // If there is a new direction,
            var direction = Direction.FromInput(GamePadState, KeyboardState);
            if (Direction.FromInput(lastGamePadState, lastKeyboardState) != direction)
            {
                // combine the direction with the buttons.
                buttons |= direction;

                // Don't merge two different directions. This avoids having impossible
                // directions such as Left+Up+Right. This also has the side effect that
                // the direction needs to be pressed at the same time or slightly before
                // the buttons for merging to work.
                mergeInput = false;
            }

            // If there was any new input on this update, add it to the buffer.
            if (buttons != 0)
            {
                if (mergeInput)
                {
                    // Use a bitwise-or to merge with the previous input.
                    // LastInputTime isn't updated to prevent extending the merge window.
                    Buffer[Buffer.Count - 1] = Buffer[Buffer.Count - 1] | buttons;                    
                }
                else
                {
                    // Append this input to the buffer, expiring old input if necessary.
                    if (Buffer.Count == Buffer.Capacity)
                    {
                        Buffer.RemoveAt(0);
                    }
                    Buffer.Add(buttons);

                    // Record this the time of this input to begin the merge window.
                    LastInputTime = 0;
                }
            }
        }*/

        public void Update(KeyState[] keysState_, float globalTime_)
        {
            List<KeyState> keyStateList = new List<KeyState>(keysState_);
            List<KeyStateFrame> toDelete = new List<KeyStateFrame>();

            // Delete old frame (> 2 secondes)
            foreach (KeyStateFrame keyStateFrame in m_Buffer)
            {
                if (globalTime_ - keyStateFrame.GlobalTime >= 2.0f)
                {
                    toDelete.Add(keyStateFrame);
                }
            }

            foreach (KeyStateFrame keyStateFrame in toDelete)
            {
                m_Buffer.Remove(keyStateFrame);
            }

            //check buffer no empty
            bool IsEntryEmpty = true;

            foreach (KeyState k in keysState_)
            {
                if (k.State != ButtonState.Released)
                {
                    IsEntryEmpty = false;
                    break;
                }
            }

            int last = m_Buffer.Count - 1;
            // It is very hard to press two buttons on exactly the same frame.
            // If they are close enough, consider them pressed at the same time.
            bool merged = false;

            if (m_Buffer.Count > 0)
            {
                merged = globalTime_ - m_Buffer[m_Buffer.Count - 1].GlobalTime <= MergeInputTime
                    && CheckDirectionCollisionForMerge(keyStateList) == true
                    && CheckIfReleasedButtonCollisionForMerge(keyStateList) == false;
            }

            bool found = false;
            int i = 0;

            if (merged == true)
            {
                foreach (KeyState k2 in keysState_)
                {
                    found = false;
                    i = 0;

                    foreach (KeyState k1 in m_Buffer[last].KeysState)
                    {
                        if (k1.Key == k2.Key)
                        {
                            found = true;

                            KeyState k = k1;
                            k.Time += k2.Time;
                            m_Buffer[last].KeysState[i] = k;
                            break;
                        }

                        i++;
                    }

                    if (found == false
                        && k2.State == ButtonState.Pressed)
                    {
                        //verif elapsedTime
                        m_Buffer[last].KeysState.Add(k2);
                    }
                }
            }
            else if (IsEntryEmpty == false)
            {
                List<KeyState> keysToAdd = new List<KeyState>();

                if (m_Buffer.Count > 0)
                {
                    Dictionary<int, KeyState> tmp = new Dictionary<int, KeyState>();

                    foreach (KeyState k2 in keysState_)
                    {
                        i = 0;
                        found = false;

                        foreach (KeyState k1 in m_Buffer[last].KeysState)
                        {
                            if (k1.Key == k2.Key)
                            {
                                found = true;

                                // merge
                                if (k1.State == ButtonState.Pressed
                                    && k2.State == ButtonState.Pressed
                                    /*&& verif released == false*/)
                                {
                                    tmp.Add(i, k1);
                                    KeyState ks = new KeyState();
                                    ks.Key = k1.Key;
                                    ks.State = k1.State;
                                    ks.Time = k2.Time + k1.Time;
                                    keysToAdd.Add(ks);
                                }
                                // ajout
                                else /*if (k1.State == ButtonState.Released
                                    && k2.State == ButtonState.Pressed)*/
                                {
                                    keysToAdd.Add(k2);
                                }

                                break;
                            }

                            i++;
                        }

                        if (found == false
                            && k2.State == ButtonState.Pressed)
                        {
                            keysToAdd.Add(k2);
                        }
                    }

                    // remove old button => moved in new frame
                    //if same buttons (case hold button)
                    if (m_Buffer[last].KeysState.Count == tmp.Count)
                    {
                        foreach (KeyValuePair<int, KeyState> pair in tmp)
                        {
                            found = false;

                            foreach (KeyState k in m_Buffer[last].KeysState)
                            {
                                if (k.Key == pair.Value.Key)
                                {
                                    found = true;
                                    break;
                                }
                            }

                            if (found == false)
                            {
                                break;
                            }
                        }

                        if (found == true)
                        {
                            foreach (KeyValuePair<int, KeyState> pair in tmp)
                            {
                                m_Buffer[last].KeysState.Remove(pair.Value);
                            }
                        }
                    }

                    //Check empty buffer (can append if held button is detected)
                    if (m_Buffer[last].KeysState.Count == 0)
                    {
                        m_Buffer.RemoveAt(last);
                    }

                    //ajout new frame
                    if (keysToAdd.Count > 0)
                    {
                        KeyStateFrame ksf = new KeyStateFrame(globalTime_); // factory
                        ksf.KeysState = keysToAdd;
                        m_Buffer.Add(ksf);
                    }
                }
                // first element
                else
                {
                    foreach (KeyState k in keyStateList)
                    {
                        if (k.State == ButtonState.Pressed)
                        {
                            keysToAdd.Add(k);
                        }
                    }

                    if (keysToAdd.Count > 0)
                    {
                        KeyStateFrame ksf = new KeyStateFrame(globalTime_); // factory
                        ksf.KeysState = keysToAdd;
                        m_Buffer.Add(ksf);
                    }
                }
            }
        }

        private bool CheckDirectionCollisionForMerge(IEnumerable<KeyState> keys_)
        {
            /*int last = m_Buffer.Count - 1;
            int updown = 0;
            int leftRight = 0;

            foreach (KeyState key in m_Buffer[last].KeysState)
            {
                if (key.)
            }*/

            //throw new NotImplementedException();
            return true; // false;
        }

        private bool CheckIfReleasedButtonCollisionForMerge(IEnumerable<KeyState> keys_)
        {
            return false; //false;
        }

        public bool Matches(Move move)
        {
            int moveSequencecount = 0;

#if EDITOR
            moveSequencecount = move.Sequence.Count;
#else
            moveSequencecount = move.Sequence.Length;
#endif

            // If the move is longer than the buffer, it can't possibly match.
            if (m_Buffer.Count < moveSequencecount)
                return false;

            // Loop backwards to match against the most recent input.
            for (int i = 1; i <= moveSequencecount; ++i)
            {
                if (move.Match(moveSequencecount - i, m_Buffer[m_Buffer.Count - i].KeysState.ToArray()) == false)
                {
                    return false;
                }
            }

            // unless this move is a component of a larger sequence,
            if (!move.IsSubMove)
            {
                // consume the used inputs.
                m_Buffer.Clear();
            }

            return true;
        }
    }
}
