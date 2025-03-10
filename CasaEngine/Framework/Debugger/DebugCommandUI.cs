﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Graphics2D;
using CasaEngine.Framework.Game.Components.Physics;
using FontStashSharp;


namespace CasaEngine.Framework.Debugger;

public class DebugCommandUi : DrawableGameComponent, IDebugCommandHost, IGameComponentResizable
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
    private readonly List<IDebugEchoListner> _listeners = new();

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
    private SpriteFontBase? _font;


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
                {
                    maxLen = Math.Max(maxLen, cmd.Command.Length);
                }

                var fmt = $"{{0,-{maxLen}}}    {{1}}";

                foreach (var cmd in _commandTable.Values)
                {
                    Echo(string.Format(fmt, cmd.Command, cmd.Description));
                }
            });

        // Clear screen command
        RegisterCommand("cls", "Clear ScreenGui",
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
                    switch (args[0].ToLower())
                    {
                        case "on":
                            state = true;
                            break;
                        case "off":
                            state = false;
                            break;
                        default:
                            error = true;
                            break;
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
                    var physicsDebugViewRendererComponent = Game.GetGameComponent<PhysicsDebugViewRendererComponent>();
                    if (physicsDebugViewRendererComponent != null)
                    {
                        physicsDebugViewRendererComponent.DisplayPhysics = state;
                    }
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

        _font = ((CasaEngineGame)Game).FontSystem.GetFont(10);

        base.LoadContent();
    }



    public void RegisterCommand(
        string command, string description, DebugCommandExecute callback)
    {
        var lowerCommand = command.ToLower();
        if (_commandTable.ContainsKey(lowerCommand))
        {
            throw new InvalidOperationException(
                $"Command \"{command}\" is already registered.");
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
                $"Command \"{command}\" is not registered.");
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

        if (_commandTable.TryGetValue(cmdText.ToLower(), out var cmd))
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
                {
                    EchoError(line);
                }
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
        _listeners.Add(listner);
    }

    public void UnregisterEchoListner(IDebugEchoListner listner)
    {
        _listeners.Remove(listner);
    }

    public void Echo(DebugCommandMessage messageType, string text)
    {
        _lines.Enqueue(text);
        while (_lines.Count >= MaxLineCount)
            _lines.Dequeue();

        // Call registered listeners.
        foreach (var listner in _listeners)
        {
            listner.Echo(messageType, text);
        }
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

            if (KeyboardUtils.KeyToString(key, shift, out var ch))
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

        var whiteTexture = _debugManager.WhiteTexture;
        var depth = 0.0f;

        // Compute command window size and draw.
        float w = GraphicsDevice.Viewport.Width;
        float h = GraphicsDevice.Viewport.Height;
        var topMargin = h * 0.1f;
        var leftMargin = w * 0.1f;

        Rectangle rect = new Rectangle();
        rect.X = (int)leftMargin;
        rect.Y = (int)topMargin;
        rect.Width = (int)(w * 0.8f);
        rect.Height = (int)(MaxLineCount * _font.LineHeight);

        //Todo : add transformation to add transition when closing/opening
        /*Matrix mtx = Matrix.CreateTranslation(
                    new Vector3(0, -rect.Height * (1.0f - stateTransition), 0));*/

        var casaEngineGame = ((CasaEngineGame)Game);
        casaEngineGame.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

        //spriteBatch.Draw(whiteTexture, rect, new Color(0, 0, 0, 200));
        _renderer2dComponent.DrawRectangle(ref rect, _backgroundColor, depth + 0.001f);

        // Draw each lines.
        var pos = new Vector2(leftMargin, topMargin);
        foreach (var line in _lines)
        {
            casaEngineGame.SpriteBatch.DrawString(_font, line, pos, Color.White);
            pos.Y += _font.LineHeight;
        }

        // Draw prompt string.
        var leftPart = Prompt + _commandLine.Substring(0, _cursorIndex);
        var cursorPos = pos + _font.MeasureString(leftPart);
        cursorPos.Y = pos.Y;

        casaEngineGame.SpriteBatch.DrawString(_font, $"{Prompt}{_commandLine}", pos, Color.White);
        casaEngineGame.SpriteBatch.DrawString(_font, Cursor, cursorPos, Color.White);

        casaEngineGame.SpriteBatch.End();
    }


    public void OnScreenResized(int width, int height)
    {

    }

}