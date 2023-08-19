//-----------------------------------------------------------------------------
// InputManager.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

namespace CasaEngine.Engine.Input.InputSequence;

public partial class InputManager
{
    public struct KeyState
    {
        public int Key;
        public ButtonState State;
        public float Time;

        public bool Match(KeyState other)
        {
            return Key == other.Key
                   && State == other.State
                   && Time >= other.Time;
        }
    }

    public struct KeyStateFrame
    {
        public float GlobalTime;
        public List<KeyState> KeysState;

        public KeyStateFrame(float time)
        {
            GlobalTime = time;
            KeysState = new List<KeyState>();
        }
    }

    public List<KeyStateFrame> _buffer = new(50);

    public KeyStateFrame[] Buffer => _buffer.ToArray();

    //public readonly TimeSpan MergeInputTime = TimeSpan.FromMilliseconds(100);
    public readonly float MergeInputTime = 0.1f;

    /*public InputConfigurations _InputConfigurations = new InputConfigurations();
    public string _CurrentInputConfig;

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

    public void Update(KeyState[] keysState, float globalTime)
    {
        var keyStateList = new List<KeyState>(keysState);
        var toDelete = new List<KeyStateFrame>();

        // Delete old frame (> 2 secondes)
        foreach (var keyStateFrame in _buffer)
        {
            if (globalTime - keyStateFrame.GlobalTime >= 2.0f)
            {
                toDelete.Add(keyStateFrame);
            }
        }

        foreach (var keyStateFrame in toDelete)
        {
            _buffer.Remove(keyStateFrame);
        }

        //check buffer no empty
        var isEntryEmpty = true;

        foreach (var k in keysState)
        {
            if (k.State != ButtonState.Released)
            {
                isEntryEmpty = false;
                break;
            }
        }

        var last = _buffer.Count - 1;
        // It is very hard to press two buttons on exactly the same frame.
        // If they are close enough, consider them pressed at the same time.
        var merged = false;

        if (_buffer.Count > 0)
        {
            merged = globalTime - _buffer[_buffer.Count - 1].GlobalTime <= MergeInputTime
                     && CheckDirectionCollisionForMerge(keyStateList)
                     && CheckIfReleasedButtonCollisionForMerge(keyStateList) == false;
        }

        var found = false;
        var i = 0;

        if (merged)
        {
            foreach (var k2 in keysState)
            {
                found = false;
                i = 0;

                foreach (var k1 in _buffer[last].KeysState)
                {
                    if (k1.Key == k2.Key)
                    {
                        found = true;

                        var k = k1;
                        k.Time += k2.Time;
                        _buffer[last].KeysState[i] = k;
                        break;
                    }

                    i++;
                }

                if (found == false
                    && k2.State == ButtonState.Pressed)
                {
                    //verif elapsedTime
                    _buffer[last].KeysState.Add(k2);
                }
            }
        }
        else if (isEntryEmpty == false)
        {
            var keysToAdd = new List<KeyState>();

            if (_buffer.Count > 0)
            {
                var tmp = new Dictionary<int, KeyState>();

                foreach (var k2 in keysState)
                {
                    i = 0;
                    found = false;

                    foreach (var k1 in _buffer[last].KeysState)
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
                                var ks = new KeyState();
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
                if (_buffer[last].KeysState.Count == tmp.Count)
                {
                    foreach (var pair in tmp)
                    {
                        found = false;

                        foreach (var k in _buffer[last].KeysState)
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

                    if (found)
                    {
                        foreach (var pair in tmp)
                        {
                            _buffer[last].KeysState.Remove(pair.Value);
                        }
                    }
                }

                //Check empty buffer (can append if held button is detected)
                if (_buffer[last].KeysState.Count == 0)
                {
                    _buffer.RemoveAt(last);
                }

                //ajout new frame
                if (keysToAdd.Count > 0)
                {
                    var ksf = new KeyStateFrame(globalTime); // factory
                    ksf.KeysState = keysToAdd;
                    _buffer.Add(ksf);
                }
            }
            // first element
            else
            {
                foreach (var k in keyStateList)
                {
                    if (k.State == ButtonState.Pressed)
                    {
                        keysToAdd.Add(k);
                    }
                }

                if (keysToAdd.Count > 0)
                {
                    var ksf = new KeyStateFrame(globalTime); // factory
                    ksf.KeysState = keysToAdd;
                    _buffer.Add(ksf);
                }
            }
        }
    }

    private bool CheckDirectionCollisionForMerge(IEnumerable<KeyState> keys)
    {
        /*int last = _buffer.Count - 1;
        int updown = 0;
        int leftRight = 0;

        foreach (KeyState key in _buffer[last].KeysState)
        {
            if (key.)
        }*/

        //throw new NotImplementedException();
        return true; // false;
    }

    private bool CheckIfReleasedButtonCollisionForMerge(IEnumerable<KeyState> keys)
    {
        return false; //false;
    }

    public bool Matches(Move move)
    {
        var moveSequencecount = 0;

        moveSequencecount = move.Sequence.Count;

        // If the move is longer than the buffer, it can't possibly match.
        if (_buffer.Count < moveSequencecount)
        {
            return false;
        }

        // Loop backwards to match against the most recent input.
        for (var i = 1; i <= moveSequencecount; ++i)
        {
            if (move.Match(moveSequencecount - i, _buffer[_buffer.Count - i].KeysState.ToArray()) == false)
            {
                return false;
            }
        }

        // unless this move is a component of a larger sequence,
        if (!move.IsSubMove)
        {
            // consume the used inputs.
            _buffer.Clear();
        }

        return true;
    }
}