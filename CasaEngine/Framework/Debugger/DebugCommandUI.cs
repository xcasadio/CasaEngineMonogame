//-----------------------------------------------------------------------------
// DebugCommandUI.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------


using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Graphics2D;
using CasaEngine.Framework.Game.Components;

#if EDITOR
//using CasaEngine.Editor.GameComponent;
#endif


namespace CasaEngine.Framework.Debugger
{
    public class DebugCommandUi
        : DrawableGameComponent,
        IDebugCommandHost,
        IGameComponentResizable
    {
        private const int MaxLineCount = 20;

        private const int MaxCommandHistory = 32;

        private const string Cursor = "_";

        public const string DefaultPrompt = "CMD>";



        public bool CanSetVisible => true;

        public bool CanSetEnable => true;

        public string Prompt { get; set; }

        public bool Focused => _state != State.Closed;


        // Command window states.
        private enum State
        {
            Closed,
            Opening,
            Opened,
            Closing
        }

        private class CommandInfo
        {
            public CommandInfo(
                string command, string description, DebugCommandExecute callback)
            {
                Command = command;
                Description = description;
                Callback = callback;
            }

            // command name
            public readonly string Command;

            // Description of command.
            public readonly string Description;

            // delegate for execute the command.
            public readonly DebugCommandExecute Callback;
        }

        // Reference to DebugManager.
        private DebugManager _debugManager;

        // Current state
        private State _state = State.Closed;

        // timer for state transition.
        private float _stateTransition;

        // Registered echo listeners.
        private readonly List<IDebugEchoListner> _listenrs = new();

        // Registered command executioner.
        private readonly Stack<IDebugCommandExecutioner> _executioners = new();

        // Registered commands
        private readonly Dictionary<string, CommandInfo> _commandTable = new();

        // Current command line string and cursor position.
        private string _commandLine = string.Empty;
        private int _cursorIndex;

        private readonly Queue<string> _lines = new();

        // Command history buffer.
        private readonly List<string> _commandHistory = new();

        // Selecting command history index.
        private int _commandHistoryIndex;

        private Renderer2dComponent _renderer2dComponent;

        private readonly Color _backgroundColor = new(0, 0, 0, 200);


        // Previous frame keyboard state.
        private KeyboardState _prevKeyState;

        // Key that pressed last frame.
        private Keys _pressedKey;

        // Timer for key repeating.
        private float _keyRepeatTimer;

        // Key repeat duration in seconds for the first key press.
        private readonly float _keyRepeatStartDuration = 0.3f;

        // Key repeat duration in seconds after the first key press.
        private readonly float _keyRepeatDuration = 0.03f;





        public DebugCommandUi(Microsoft.Xna.Framework.Game game)
            : base(game)
        {
            Prompt = DefaultPrompt;

            // Add this instance as a service.
            Game.Services.AddService(typeof(IDebugCommandHost), this);

            // Draw the command UI on top of everything
            DrawOrder = int.MaxValue;

            // Adding default commands.

            // Help command displays registered command information.
            RegisterCommand("help", "Show Command helps",
                delegate (IDebugCommandHost host, string command, IList<string> args)
                {
                    var maxLen = 0;
                    foreach (var cmd in _commandTable.Values)
                        maxLen = Math.Max(maxLen, cmd.Command.Length);

                    var fmt = string.Format("{{0,-{0}}}    {{1}}", maxLen);

                    foreach (var cmd in _commandTable.Values)
                    {
                        Echo(string.Format(fmt, cmd.Command, cmd.Description));
                    }
                });

            // Clear screen command
            RegisterCommand("cls", "Clear Screen",
                delegate (IDebugCommandHost host, string command, IList<string> args)
                {
                    _lines.Clear();
                });

            // Echo command
            RegisterCommand("echo", "Display Messages",
                delegate (IDebugCommandHost host, string command, IList<string> args)
                {
                    Echo(command.Substring(5));
                });

            // DisplayCollisions command
            RegisterCommand("displayPhysics", "Display Physics. Argument 'on'/'off'",
                delegate (IDebugCommandHost host, string command, IList<string> args)
                {
                    var error = false;
                    var state = false;

                    if (args.Count == 1)
                    {
                        if (args[0].ToLower().Equals("on"))
                        {
                            state = true;
                        }
                        else if (args[0].ToLower().Equals("off"))
                        {
                            state = false;
                        }
                        else
                        {
                            error = true;
                        }
                    }
                    else
                    {
                        error = true;
                    }

                    if (error)
                    {
                        EchoError("Please use DisplayCollisions with one argument : 'on' or 'off'");
                    }
                    else
                    {
                        Physics2dDebugViewRendererComponent.DisplayPhysics = state;
                    }

                });

            // AnimationSpeed command
            RegisterCommand("AnimationSpeed", "Speed of animations.",
            delegate (IDebugCommandHost host, string command, IList<string> args)
            {
                var ok = true;
                var value = 1.0f;

                if (args.Count == 1)
                {
                    args[0] = args[0].Replace(".", ",");
                    ok = float.TryParse(args[0], out value);
                }

                if (ok == false)
                {
                    EchoError("Please use AnimationSpeed with one argument");
                }
                else
                {
                    Animation2DPlayer.AnimationSpeed = value;
                }

            });

            // Character2DActor Display Debug Information command
            RegisterCommand("DisplayCharacterDebugInformation", "Display Debug Information from character.",
            delegate (IDebugCommandHost host, string command, IList<string> args)
            {
                var error = false;
                var state = false;

                if (args.Count == 1)
                {
                    if (args[0].ToLower().Equals("on"))
                    {
                        state = true;
                    }
                    else if (args[0].ToLower().Equals("off"))
                    {
                        state = false;
                    }
                    else
                    {
                        error = true;
                    }
                }
                else
                {
                    error = true;
                }

                if (error)
                {
                    EchoError("Please use DisplayCollisions with one argument : 'on' or 'off'");
                }
                else
                {
                    CharacterActor2D.DisplayDebugInformation = state;
                }
            });

            UpdateOrder = (int)ComponentUpdateOrder.DebugManager;
            DrawOrder = (int)ComponentDrawOrder.DebugManager;
        }

        public override void Initialize()
        {
            _debugManager = DebugSystem.Instance.DebugManager;

            if (_debugManager == null)
            {
                throw new InvalidOperationException("Coudn't find DebugManager.");
            }

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _renderer2dComponent = Game.GetGameComponent<Renderer2dComponent>();

            if (_renderer2dComponent == null)
            {
                throw new InvalidOperationException("DebugCommandUI.LoadContent() : Renderer2dComponent is null");
            }

            base.LoadContent();
        }



        public void RegisterCommand(
            string command, string description, DebugCommandExecute callback)
        {
            var lowerCommand = command.ToLower();
            if (_commandTable.ContainsKey(lowerCommand))
            {
                throw new InvalidOperationException(
                    string.Format("Command \"{0}\" is already registered.", command));
            }

            _commandTable.Add(
                lowerCommand, new CommandInfo(command, description, callback));
        }

        public void UnregisterCommand(string command)
        {
            var lowerCommand = command.ToLower();
            if (!_commandTable.ContainsKey(lowerCommand))
            {
                throw new InvalidOperationException(
                    string.Format("Command \"{0}\" is not registered.", command));
            }

            _commandTable.Remove(command);
        }

        public void ExecuteCommand(string command)
        {
            // Call registered executioner.
            if (_executioners.Count != 0)
            {
                _executioners.Peek().ExecuteCommand(command);
                return;
            }

            // Run the command.
            var spaceChars = new char[] { ' ' };

            Echo(Prompt + command);

            command = command.TrimStart(spaceChars);

            var args = new List<string>(command.Split(spaceChars));
            var cmdText = args[0];
            args.RemoveAt(0);

            CommandInfo cmd;
            if (_commandTable.TryGetValue(cmdText.ToLower(), out cmd))
            {
                try
                {
                    // Call registered command delegate.
                    cmd.Callback(this, command, args);
                }
                catch (Exception e)
                {
                    // Exception occurred while running command.
                    EchoError("Unhandled Exception occurred");

                    var lines = e.Message.Split(new char[] { '\n' });
                    foreach (var line in lines)
                        EchoError(line);
                }
            }
            else
            {
                Echo("Unknown Command");
            }

            // Add to command history.
            _commandHistory.Add(command);
            while (_commandHistory.Count > MaxCommandHistory)
                _commandHistory.RemoveAt(0);

            _commandHistoryIndex = _commandHistory.Count;
        }

        public void RegisterEchoListner(IDebugEchoListner listner)
        {
            _listenrs.Add(listner);
        }

        public void UnregisterEchoListner(IDebugEchoListner listner)
        {
            _listenrs.Remove(listner);
        }

        public void Echo(DebugCommandMessage messageType, string text)
        {
            _lines.Enqueue(text);
            while (_lines.Count >= MaxLineCount)
                _lines.Dequeue();

            // Call registered listeners.
            foreach (var listner in _listenrs)
                listner.Echo(messageType, text);
        }

        public void Echo(string text)
        {
            Echo(DebugCommandMessage.Standard, text);
        }

        public void EchoWarning(string text)
        {
            Echo(DebugCommandMessage.Warning, text);
        }

        public void EchoError(string text)
        {
            Echo(DebugCommandMessage.Error, text);
        }

        public void PushExecutioner(IDebugCommandExecutioner executioner)
        {
            _executioners.Push(executioner);
        }

        public void PopExecutioner()
        {
            _executioners.Pop();
        }



        public void Show()
        {
            if (_state == State.Closed)
            {
                _stateTransition = 0.0f;
                _state = State.Opening;
            }
        }

        public void Hide()
        {
            if (_state == State.Opened)
            {
                _stateTransition = 1.0f;
                _state = State.Closing;
            }
        }

        public override void Update(GameTime gameTime)
        {
            var keyState = Keyboard.GetState();

            var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            const float openSpeed = 8.0f;
            const float closeSpeed = 8.0f;

            switch (_state)
            {
                case State.Closed:
                    if (keyState.IsKeyDown(Keys.OemQuotes))
                    {
                        Show();
                    }

                    break;
                case State.Opening:
                    _stateTransition += dt * openSpeed;
                    if (_stateTransition > 1.0f)
                    {
                        _stateTransition = 1.0f;
                        _state = State.Opened;
                    }
                    break;
                case State.Opened:
                    ProcessKeyInputs(dt);
                    break;
                case State.Closing:
                    _stateTransition -= dt * closeSpeed;
                    if (_stateTransition < 0.0f)
                    {
                        _stateTransition = 0.0f;
                        _state = State.Closed;
                    }
                    break;
            }

            _prevKeyState = keyState;

            base.Update(gameTime);
        }

        public void ProcessKeyInputs(float dt)
        {
            var keyState = Keyboard.GetState();
            var keys = keyState.GetPressedKeys();

            var shift = keyState.IsKeyDown(Keys.LeftShift) ||
                        keyState.IsKeyDown(Keys.RightShift);

            foreach (var key in keys)
            {
                if (!IsKeyPressed(key, dt))
                {
                    continue;
                }

                char ch;
                if (KeyboardUtils.KeyToString(key, shift, out ch))
                {
                    // Handle typical character input.
                    _commandLine = _commandLine.Insert(_cursorIndex, new string(ch, 1));
                    _cursorIndex++;
                }
                else
                {
                    switch (key)
                    {
                        case Keys.Back:
                            if (_cursorIndex > 0)
                            {
                                _commandLine = _commandLine.Remove(--_cursorIndex, 1);
                            }

                            break;
                        case Keys.Delete:
                            if (_cursorIndex < _commandLine.Length)
                            {
                                _commandLine = _commandLine.Remove(_cursorIndex, 1);
                            }

                            break;
                        case Keys.Left:
                            if (_cursorIndex > 0)
                            {
                                _cursorIndex--;
                            }

                            break;
                        case Keys.Right:
                            if (_cursorIndex < _commandLine.Length)
                            {
                                _cursorIndex++;
                            }

                            break;
                        case Keys.Enter:
                            // Run the command.
                            ExecuteCommand(_commandLine);
                            _commandLine = string.Empty;
                            _cursorIndex = 0;
                            break;
                        case Keys.Up:
                            // Show command history.
                            if (_commandHistory.Count > 0)
                            {
                                _commandHistoryIndex =
                                    Math.Max(0, _commandHistoryIndex - 1);

                                _commandLine = _commandHistory[_commandHistoryIndex];
                                _cursorIndex = _commandLine.Length;
                            }
                            break;
                        case Keys.Down:
                            // Show command history.
                            if (_commandHistory.Count > 0)
                            {
                                _commandHistoryIndex = Math.Min(_commandHistory.Count - 1,
                                                                _commandHistoryIndex + 1);
                                _commandLine = _commandHistory[_commandHistoryIndex];
                                _cursorIndex = _commandLine.Length;
                            }
                            break;
                        case Keys.Escape: //OemQuotes
                            Hide();
                            break;
                    }
                }
            }

        }

        private bool IsKeyPressed(Keys key, float dt)
        {
            // Treat it as pressed if given key has not pressed in previous frame.
            if (_prevKeyState.IsKeyUp(key))
            {
                _keyRepeatTimer = _keyRepeatStartDuration;
                _pressedKey = key;
                return true;
            }

            // Handling key repeating if given key has pressed in previous frame.
            if (key == _pressedKey)
            {
                _keyRepeatTimer -= dt;
                if (_keyRepeatTimer <= 0.0f)
                {
                    _keyRepeatTimer += _keyRepeatDuration;
                    return true;
                }
            }

            return false;
        }

        public override void Draw(GameTime gameTime)
        {
            // Do nothing when command window is completely closed.
            if (_state == State.Closed)
            {
                return;
            }

            var font = EngineComponents.DefaultSpriteFont;
            var whiteTexture = _debugManager.WhiteTexture;
            var depth = 0.0f;

            // Compute command window size and draw.
            float w = GraphicsDevice.Viewport.Width;
            float h = GraphicsDevice.Viewport.Height;
            var topMargin = h * 0.1f;
            var leftMargin = w * 0.1f;

            /*Rectangle rect = new Rectangle();
            rect.X = (int)leftMargin;
            rect.Y = (int)topMargin;
            rect.Width = (int)(w * 0.8f);
            rect.Height = (int)(MaxLineCount * font.LineSpacing);*/

            //Todo : add transformation to add transition when closing/opening
            /*Matrix mtx = Matrix.CreateTranslation(
                        new Vector3(0, -rect.Height * (1.0f - stateTransition), 0));*/

            //spriteBatch.Begin(SpriteBlendMode.AlphaBlend, SpriteSortMode.Deferred, SaveStateMode.SaveState, mtx);

            //spriteBatch.Draw(whiteTexture, rect, new Color(0, 0, 0, 200));
            _renderer2dComponent.AddSprite2D(
                whiteTexture,
                new Vector2(leftMargin, topMargin), // position
                0.0f,
                new Vector2(w * 0.8f, MaxLineCount * font.LineSpacing), // scale
                _backgroundColor, depth + 0.001f, SpriteEffects.None);

            // Draw each lines.
            var pos = new Vector2(leftMargin, topMargin);
            foreach (var line in _lines)
            {
                //spriteBatch.DrawString(font, line, pos, Color.White);
                _renderer2dComponent.AddText2d(font, line, pos, 0.0f, Vector2.One, Color.White, depth);
                pos.Y += font.LineSpacing;
            }

            // Draw prompt string.
            var leftPart = Prompt + _commandLine.Substring(0, _cursorIndex);
            var cursorPos = pos + font.MeasureString(leftPart);
            cursorPos.Y = pos.Y;

            // spriteBatch.DrawString(font,
            //String.Format("{0}{1}", Prompt, commandLine), pos, Color.White);
            _renderer2dComponent.AddText2d(font, string.Format("{0}{1}", Prompt, _commandLine), pos, 0.0f, Vector2.One, Color.White, depth);
            //spriteBatch.DrawString(font, Cursor, cursorPos, Color.White);
            _renderer2dComponent.AddText2d(font, Cursor, cursorPos, 0.0f, Vector2.One, Color.White, depth);

            //spriteBatch.End();
        }


        public void OnResize()
        {

        }

    }
}
