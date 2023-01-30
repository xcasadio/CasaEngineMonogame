//-----------------------------------------------------------------------------
// DebugCommandUI.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------


using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;

using CasaEngine.Game;
using CasaEngine.Graphics2D;
using CasaEngine.Helper;
using CasaEngine.Gameplay;
using CasaEngine.CoreSystems.Game;

#if EDITOR
//using CasaEngine.Editor.GameComponent;
#endif


namespace CasaEngine.Debugger
{
    public class DebugCommandUI
        : Microsoft.Xna.Framework.DrawableGameComponent,
        IDebugCommandHost,
        IGameComponentResizable
    {

        const int MaxLineCount = 20;

        const int MaxCommandHistory = 32;

        const string Cursor = "_";

        public const string DefaultPrompt = "CMD>";



        public bool CanSetVisible => true;

        public bool CanSetEnable => true;

        public string Prompt { get; set; }

        public bool Focused => state != State.Closed;


        // Command window states.
        enum State
        {
            Closed,
            Opening,
            Opened,
            Closing
        }

        class CommandInfo
        {
            public CommandInfo(
                string command, string description, DebugCommandExecute callback)
            {
                this.command = command;
                this.description = description;
                this.callback = callback;
            }

            // command name
            public readonly string command;

            // Description of command.
            public readonly string description;

            // delegate for execute the command.
            public readonly DebugCommandExecute callback;
        }

        // Reference to DebugManager.
        private DebugManager debugManager;

        // Current state
        private State state = State.Closed;

        // timer for state transition.
        private float stateTransition;

        // Registered echo listeners.
        private readonly List<IDebugEchoListner> listenrs = new List<IDebugEchoListner>();

        // Registered command executioner.
        private readonly Stack<IDebugCommandExecutioner> executioners = new Stack<IDebugCommandExecutioner>();

        // Registered commands
        private readonly Dictionary<string, CommandInfo> commandTable =
                                                new Dictionary<string, CommandInfo>();

        // Current command line string and cursor position.
        private string commandLine = String.Empty;
        private int cursorIndex = 0;

        private readonly Queue<string> lines = new Queue<string>();

        // Command history buffer.
        private readonly List<string> commandHistory = new List<string>();

        // Selecting command history index.
        private int commandHistoryIndex;

        private Renderer2DComponent m_Renderer2DComponent = null;

        private readonly Color m_BackgroundColor = new Color(0, 0, 0, 200);


        // Previous frame keyboard state.
        private KeyboardState prevKeyState;

        // Key that pressed last frame.
        private Keys pressedKey;

        // Timer for key repeating.
        private float keyRepeatTimer;

        // Key repeat duration in seconds for the first key press.
        private readonly float keyRepeatStartDuration = 0.3f;

        // Key repeat duration in seconds after the first key press.
        private readonly float keyRepeatDuration = 0.03f;





        public DebugCommandUI(Microsoft.Xna.Framework.Game game)
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
                    int maxLen = 0;
                    foreach (CommandInfo cmd in commandTable.Values)
                        maxLen = System.Math.Max(maxLen, cmd.command.Length);

                    string fmt = String.Format("{{0,-{0}}}    {{1}}", maxLen);

                    foreach (CommandInfo cmd in commandTable.Values)
                    {
                        Echo(String.Format(fmt, cmd.command, cmd.description));
                    }
                });

            // Clear screen command
            RegisterCommand("cls", "Clear Screen",
                delegate (IDebugCommandHost host, string command, IList<string> args)
                {
                    lines.Clear();
                });

            // Echo command
            RegisterCommand("echo", "Display Messages",
                delegate (IDebugCommandHost host, string command, IList<string> args)
                {
                    Echo(command.Substring(5));
                });

            // DisplayCollisions command
            RegisterCommand("displayCollisions", "Display Collisions of each sprite. Argument 'on'/'off'",
                delegate (IDebugCommandHost host, string command, IList<string> args)
                {
                    bool error = false;
                    bool state = false;

                    if (args.Count == 1)
                    {
                        if (args[0].ToLower().Equals("on") == true)
                        {
                            state = true;
                        }
                        else if (args[0].ToLower().Equals("off") == true)
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

                    if (error == true)
                    {
                        EchoError("Please use DisplayCollisions with one argument : 'on' or 'off'");
                    }
                    else
                    {
                        ShapeRendererComponent.DisplayCollisions = state;
                    }

                });

            // AnimationSpeed command
            RegisterCommand("AnimationSpeed", "Speed of animations.",
            delegate (IDebugCommandHost host, string command, IList<string> args)
            {
                bool ok = true;
                float value = 1.0f;

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
                bool error = false;
                bool state = false;

                if (args.Count == 1)
                {
                    if (args[0].ToLower().Equals("on") == true)
                    {
                        state = true;
                    }
                    else if (args[0].ToLower().Equals("off") == true)
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

                if (error == true)
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
            debugManager =
                Game.Services.GetService(typeof(DebugManager)) as DebugManager;

            if (debugManager == null)
                throw new InvalidOperationException("Coudn't find DebugManager.");

            base.Initialize();
        }

        protected override void LoadContent()
        {
            m_Renderer2DComponent = GameHelper.GetGameComponent<Renderer2DComponent>(Game);

            if (m_Renderer2DComponent == null)
            {
                throw new InvalidOperationException("DebugCommandUI.LoadContent() : Renderer2DComponent is null");
            }

            base.LoadContent();
        }



        public void RegisterCommand(
            string command, string description, DebugCommandExecute callback)
        {
            string lowerCommand = command.ToLower();
            if (commandTable.ContainsKey(lowerCommand))
            {
                throw new InvalidOperationException(
                    String.Format("Command \"{0}\" is already registered.", command));
            }

            commandTable.Add(
                lowerCommand, new CommandInfo(command, description, callback));
        }

        public void UnregisterCommand(string command)
        {
            string lowerCommand = command.ToLower();
            if (!commandTable.ContainsKey(lowerCommand))
            {
                throw new InvalidOperationException(
                    String.Format("Command \"{0}\" is not registered.", command));
            }

            commandTable.Remove(command);
        }

        public void ExecuteCommand(string command)
        {
            // Call registered executioner.
            if (executioners.Count != 0)
            {
                executioners.Peek().ExecuteCommand(command);
                return;
            }

            // Run the command.
            char[] spaceChars = new char[] { ' ' };

            Echo(Prompt + command);

            command = command.TrimStart(spaceChars);

            List<string> args = new List<string>(command.Split(spaceChars));
            string cmdText = args[0];
            args.RemoveAt(0);

            CommandInfo cmd;
            if (commandTable.TryGetValue(cmdText.ToLower(), out cmd))
            {
                try
                {
                    // Call registered command delegate.
                    cmd.callback(this, command, args);
                }
                catch (Exception e)
                {
                    // Exception occurred while running command.
                    EchoError("Unhandled Exception occurred");

                    string[] lines = e.Message.Split(new char[] { '\n' });
                    foreach (string line in lines)
                        EchoError(line);
                }
            }
            else
            {
                Echo("Unknown Command");
            }

            // Add to command history.
            commandHistory.Add(command);
            while (commandHistory.Count > MaxCommandHistory)
                commandHistory.RemoveAt(0);

            commandHistoryIndex = commandHistory.Count;
        }

        public void RegisterEchoListner(IDebugEchoListner listner)
        {
            listenrs.Add(listner);
        }

        public void UnregisterEchoListner(IDebugEchoListner listner)
        {
            listenrs.Remove(listner);
        }

        public void Echo(DebugCommandMessage messageType, string text)
        {
            lines.Enqueue(text);
            while (lines.Count >= MaxLineCount)
                lines.Dequeue();

            // Call registered listeners.
            foreach (IDebugEchoListner listner in listenrs)
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
            executioners.Push(executioner);
        }

        public void PopExecutioner()
        {
            executioners.Pop();
        }



        public void Show()
        {
            if (state == State.Closed)
            {
                stateTransition = 0.0f;
                state = State.Opening;
            }
        }

        public void Hide()
        {
            if (state == State.Opened)
            {
                stateTransition = 1.0f;
                state = State.Closing;
            }
        }

        public override void Update(GameTime gameTime)
        {
            KeyboardState keyState = Keyboard.GetState();

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            const float OpenSpeed = 8.0f;
            const float CloseSpeed = 8.0f;

            switch (state)
            {
                case State.Closed:
                    if (keyState.IsKeyDown(Keys.OemQuotes))
                        Show();
                    break;
                case State.Opening:
                    stateTransition += dt * OpenSpeed;
                    if (stateTransition > 1.0f)
                    {
                        stateTransition = 1.0f;
                        state = State.Opened;
                    }
                    break;
                case State.Opened:
                    ProcessKeyInputs(dt);
                    break;
                case State.Closing:
                    stateTransition -= dt * CloseSpeed;
                    if (stateTransition < 0.0f)
                    {
                        stateTransition = 0.0f;
                        state = State.Closed;
                    }
                    break;
            }

            prevKeyState = keyState;

            base.Update(gameTime);
        }

        public void ProcessKeyInputs(float dt)
        {
            KeyboardState keyState = Keyboard.GetState();
            Keys[] keys = keyState.GetPressedKeys();

            bool shift = keyState.IsKeyDown(Keys.LeftShift) ||
                            keyState.IsKeyDown(Keys.RightShift);

            foreach (Keys key in keys)
            {
                if (!IsKeyPressed(key, dt)) continue;

                char ch;
                if (KeyboardUtils.KeyToString(key, shift, out ch))
                {
                    // Handle typical character input.
                    commandLine = commandLine.Insert(cursorIndex, new string(ch, 1));
                    cursorIndex++;
                }
                else
                {
                    switch (key)
                    {
                        case Keys.Back:
                            if (cursorIndex > 0)
                                commandLine = commandLine.Remove(--cursorIndex, 1);
                            break;
                        case Keys.Delete:
                            if (cursorIndex < commandLine.Length)
                                commandLine = commandLine.Remove(cursorIndex, 1);
                            break;
                        case Keys.Left:
                            if (cursorIndex > 0)
                                cursorIndex--;
                            break;
                        case Keys.Right:
                            if (cursorIndex < commandLine.Length)
                                cursorIndex++;
                            break;
                        case Keys.Enter:
                            // Run the command.
                            ExecuteCommand(commandLine);
                            commandLine = string.Empty;
                            cursorIndex = 0;
                            break;
                        case Keys.Up:
                            // Show command history.
                            if (commandHistory.Count > 0)
                            {
                                commandHistoryIndex =
                                    System.Math.Max(0, commandHistoryIndex - 1);

                                commandLine = commandHistory[commandHistoryIndex];
                                cursorIndex = commandLine.Length;
                            }
                            break;
                        case Keys.Down:
                            // Show command history.
                            if (commandHistory.Count > 0)
                            {
                                commandHistoryIndex = System.Math.Min(commandHistory.Count - 1,
                                                                commandHistoryIndex + 1);
                                commandLine = commandHistory[commandHistoryIndex];
                                cursorIndex = commandLine.Length;
                            }
                            break;
                        case Keys.Escape: //OemQuotes
                            Hide();
                            break;
                    }
                }
            }

        }

        bool IsKeyPressed(Keys key, float dt)
        {
            // Treat it as pressed if given key has not pressed in previous frame.
            if (prevKeyState.IsKeyUp(key))
            {
                keyRepeatTimer = keyRepeatStartDuration;
                pressedKey = key;
                return true;
            }

            // Handling key repeating if given key has pressed in previous frame.
            if (key == pressedKey)
            {
                keyRepeatTimer -= dt;
                if (keyRepeatTimer <= 0.0f)
                {
                    keyRepeatTimer += keyRepeatDuration;
                    return true;
                }
            }

            return false;
        }

        public override void Draw(GameTime gameTime)
        {
            // Do nothing when command window is completely closed.
            if (state == State.Closed)
                return;

            SpriteFont font = Engine.Instance.DefaultSpriteFont;
            Texture2D whiteTexture = debugManager.WhiteTexture;
            float depth = 0.0f;

            // Compute command window size and draw.
            float w = GraphicsDevice.Viewport.Width;
            float h = GraphicsDevice.Viewport.Height;
            float topMargin = h * 0.1f;
            float leftMargin = w * 0.1f;

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
            m_Renderer2DComponent.AddSprite2D(
                whiteTexture,
                new Vector2(leftMargin, topMargin), // position
                0.0f,
                new Vector2(w * 0.8f, MaxLineCount * font.LineSpacing), // scale
                m_BackgroundColor, depth + 0.001f, SpriteEffects.None);

            // Draw each lines.
            Vector2 pos = new Vector2(leftMargin, topMargin);
            foreach (string line in lines)
            {
                //spriteBatch.DrawString(font, line, pos, Color.White);
                m_Renderer2DComponent.AddText2D(font, line, pos, 0.0f, Vector2.One, Color.White, depth);
                pos.Y += font.LineSpacing;
            }

            // Draw prompt string.
            string leftPart = Prompt + commandLine.Substring(0, cursorIndex);
            Vector2 cursorPos = pos + font.MeasureString(leftPart);
            cursorPos.Y = pos.Y;

            // spriteBatch.DrawString(font,
            //String.Format("{0}{1}", Prompt, commandLine), pos, Color.White);
            m_Renderer2DComponent.AddText2D(font, String.Format("{0}{1}", Prompt, commandLine), pos, 0.0f, Vector2.One, Color.White, depth);
            //spriteBatch.DrawString(font, Cursor, cursorPos, Color.White);
            m_Renderer2DComponent.AddText2D(font, Cursor, cursorPos, 0.0f, Vector2.One, Color.White, depth);

            //spriteBatch.End();
        }


        public void OnResize()
        {

        }

    }
}
