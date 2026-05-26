using CasaEngine.Engine.Input.Providers;
using MGUI.Shared.Input;
using MGUI.Shared.Input.Keyboard;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.Input;

/// <summary>
/// Basic window input source backed directly by MonoGame device state APIs.
/// </summary>
public sealed class MonoGameWindowInputSource : IWindowInputSource, IRawInputSource, IKeyboardStateProvider, IMouseStateProvider, IWindowTextInputSource
{
    private readonly Func<bool>? _isWindowActive;
    private readonly Func<GameWindow?>? _getWindow;
    private readonly bool _suppressKeyboardWhenInactive;
    private readonly List<TextInputEventArgs> _textInputEvents = new();
    private GameWindow? _subscribedWindow;

    public MonoGameWindowInputSource(
        Func<bool>? isWindowActive = null,
        Func<GameWindow?>? getWindow = null,
        bool suppressKeyboardWhenInactive = true)
    {
        _isWindowActive = isWindowActive;
        _getWindow = getWindow;
        _suppressKeyboardWhenInactive = suppressKeyboardWhenInactive;
    }

    public WindowInputSnapshot GetSnapshot()
    {
        EnsureTextInputSubscription();
        return new WindowInputSnapshot(GetKeyboardState(), GetMouseState());
    }

    public KeyboardState GetKeyboardState()
        => _suppressKeyboardWhenInactive && _isWindowActive?.Invoke() == false
            ? new KeyboardState()
            : Keyboard.GetState();

    public MouseState GetMouseState()
    {
        var window = _getWindow?.Invoke();
        return window == null ? Mouse.GetState() : Mouse.GetState(window);
    }

    public void DrainTextInput(IKeyboardTextInputSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);

        EnsureTextInputSubscription();

        lock (_textInputEvents)
        {
            for (int index = 0; index < _textInputEvents.Count; index++)
            {
                var inputEvent = _textInputEvents[index];
                sink.QueueTextInput(inputEvent.Character, inputEvent.Key);
            }

            _textInputEvents.Clear();
        }
    }

    private void EnsureTextInputSubscription()
    {
        var window = _getWindow?.Invoke();
        if (ReferenceEquals(_subscribedWindow, window))
        {
            return;
        }

        if (_subscribedWindow != null)
        {
            _subscribedWindow.TextInput -= OnTextInput;
        }

        _subscribedWindow = window;

        if (_subscribedWindow != null)
        {
            _subscribedWindow.TextInput += OnTextInput;
        }
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        lock (_textInputEvents)
        {
            _textInputEvents.Add(e);
        }
    }

    KeyboardState IKeyboardStateProvider.GetState() => GetKeyboardState();

    MouseState IMouseStateProvider.GetState() => GetMouseState();
}