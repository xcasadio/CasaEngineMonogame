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
    private const int FirstTrackedKey = 0;
    private const int LastTrackedKey = 255;

    private readonly Func<bool> _isWindowActive;
    private readonly Func<GameWindow> _getWindow;
    private readonly bool _suppressKeyboardWhenInactive;
    private readonly Dictionary<KeyboardStateCacheKey, Keys[]> _cachedKeyArrays = new();
    private readonly Keys[] _pressedKeysBuffer = new Keys[LastTrackedKey - FirstTrackedKey + 1];
    private readonly List<TextInputEventArgs> _textInputEvents = new();
    private GameWindow _subscribedWindow;
    private uint _eventKeys0;
    private uint _eventKeys1;
    private uint _eventKeys2;
    private uint _eventKeys3;
    private uint _eventKeys4;
    private uint _eventKeys5;
    private uint _eventKeys6;
    private uint _eventKeys7;

    public MonoGameWindowInputSource(
        Func<bool> isWindowActive = null,
        Func<GameWindow> getWindow = null,
        bool suppressKeyboardWhenInactive = true)
    {
        _isWindowActive = isWindowActive;
        _getWindow = getWindow;
        _suppressKeyboardWhenInactive = suppressKeyboardWhenInactive;
    }

    public WindowInputSnapshot GetSnapshot()
    {
        EnsureWindowSubscription();
        return new WindowInputSnapshot(GetKeyboardState(), GetMouseState());
    }

    public KeyboardState GetKeyboardState()
    {
        EnsureWindowSubscription();

        if (_isWindowActive?.Invoke() == false)
        {
            ClearEventKeys();
            if (_suppressKeyboardWhenInactive)
            {
                return new KeyboardState();
            }
        }

        var keyboardState = Keyboard.GetState();
        return HasEventKeys ? MergeWithEventState(keyboardState) : keyboardState;
    }

    public MouseState GetMouseState()
    {
        var window = _getWindow?.Invoke();
        return window == null ? Mouse.GetState() : Mouse.GetState(window);
    }

    public void DrainTextInput(IKeyboardTextInputSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);

        EnsureWindowSubscription();

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

    private void EnsureWindowSubscription()
    {
        var window = _getWindow?.Invoke();
        if (ReferenceEquals(_subscribedWindow, window))
        {
            return;
        }

        if (_subscribedWindow != null)
        {
            _subscribedWindow.TextInput -= OnTextInput;
            _subscribedWindow.KeyDown -= OnKeyDown;
            _subscribedWindow.KeyUp -= OnKeyUp;
            ClearEventKeys();
        }

        _subscribedWindow = window;

        if (_subscribedWindow != null)
        {
            _subscribedWindow.TextInput += OnTextInput;
            _subscribedWindow.KeyDown += OnKeyDown;
            _subscribedWindow.KeyUp += OnKeyUp;
        }
    }

    private KeyboardState MergeWithEventState(KeyboardState keyboardState)
    {
        int pressedKeyCount = keyboardState.GetPressedKeyCount();
        if (pressedKeyCount > 0)
        {
            keyboardState.GetPressedKeys(_pressedKeysBuffer);
        }

        var cacheKey = KeyboardStateCacheKey.Empty;
        for (int index = 0; index < pressedKeyCount; index++)
        {
            AddKey(_pressedKeysBuffer[index], ref cacheKey);
        }

        int mergedPressedKeyCount = pressedKeyCount;
        for (int keyValue = FirstTrackedKey; keyValue <= LastTrackedKey; keyValue++)
        {
            var key = (Keys)keyValue;
            if (!IsEventKeyDown(key) || cacheKey.Contains(key))
            {
                continue;
            }

            _pressedKeysBuffer[mergedPressedKeyCount] = key;
            mergedPressedKeyCount++;
            AddKey(key, ref cacheKey);
        }

        if (mergedPressedKeyCount == pressedKeyCount)
        {
            return keyboardState;
        }

        if (!_cachedKeyArrays.TryGetValue(cacheKey, out var pressedKeys))
        {
            pressedKeys = new Keys[mergedPressedKeyCount];
            Array.Copy(_pressedKeysBuffer, pressedKeys, mergedPressedKeyCount);
            _cachedKeyArrays.Add(cacheKey, pressedKeys);
        }

        return new KeyboardState(pressedKeys, keyboardState.CapsLock, keyboardState.NumLock);
    }

    private void OnKeyDown(object sender, InputKeyEventArgs e)
    {
        SetEventKey(e.Key, true);
    }

    private void OnKeyUp(object sender, InputKeyEventArgs e)
    {
        SetEventKey(e.Key, false);
    }

    private void OnTextInput(object sender, TextInputEventArgs e)
    {
        lock (_textInputEvents)
        {
            _textInputEvents.Add(e);
        }
    }

    private bool HasEventKeys
        => _eventKeys0 != 0
        || _eventKeys1 != 0
        || _eventKeys2 != 0
        || _eventKeys3 != 0
        || _eventKeys4 != 0
        || _eventKeys5 != 0
        || _eventKeys6 != 0
        || _eventKeys7 != 0;

    private void SetEventKey(Keys key, bool isDown)
    {
        int value = (int)key;
        if ((uint)value > LastTrackedKey)
        {
            return;
        }

        uint mask = 1u << (value & 31);
        switch (value >> 5)
        {
            case 0:
                _eventKeys0 = isDown ? _eventKeys0 | mask : _eventKeys0 & ~mask;
                break;
            case 1:
                _eventKeys1 = isDown ? _eventKeys1 | mask : _eventKeys1 & ~mask;
                break;
            case 2:
                _eventKeys2 = isDown ? _eventKeys2 | mask : _eventKeys2 & ~mask;
                break;
            case 3:
                _eventKeys3 = isDown ? _eventKeys3 | mask : _eventKeys3 & ~mask;
                break;
            case 4:
                _eventKeys4 = isDown ? _eventKeys4 | mask : _eventKeys4 & ~mask;
                break;
            case 5:
                _eventKeys5 = isDown ? _eventKeys5 | mask : _eventKeys5 & ~mask;
                break;
            case 6:
                _eventKeys6 = isDown ? _eventKeys6 | mask : _eventKeys6 & ~mask;
                break;
            case 7:
                _eventKeys7 = isDown ? _eventKeys7 | mask : _eventKeys7 & ~mask;
                break;
        }
    }

    private bool IsEventKeyDown(Keys key)
    {
        int value = (int)key;
        if ((uint)value > LastTrackedKey)
        {
            return false;
        }

        uint mask = 1u << (value & 31);
        return (value >> 5) switch
        {
            0 => (_eventKeys0 & mask) != 0,
            1 => (_eventKeys1 & mask) != 0,
            2 => (_eventKeys2 & mask) != 0,
            3 => (_eventKeys3 & mask) != 0,
            4 => (_eventKeys4 & mask) != 0,
            5 => (_eventKeys5 & mask) != 0,
            6 => (_eventKeys6 & mask) != 0,
            7 => (_eventKeys7 & mask) != 0,
            _ => false,
        };
    }

    private void ClearEventKeys()
    {
        _eventKeys0 = 0;
        _eventKeys1 = 0;
        _eventKeys2 = 0;
        _eventKeys3 = 0;
        _eventKeys4 = 0;
        _eventKeys5 = 0;
        _eventKeys6 = 0;
        _eventKeys7 = 0;
    }

    private static void AddKey(Keys key, ref KeyboardStateCacheKey cacheKey)
    {
        int value = (int)key;
        if ((uint)value > LastTrackedKey)
        {
            return;
        }

        cacheKey = cacheKey.WithKey(value);
    }

    private readonly struct KeyboardStateCacheKey : IEquatable<KeyboardStateCacheKey>
    {
        public static KeyboardStateCacheKey Empty => new(0, 0, 0, 0, 0, 0, 0, 0);

        private readonly uint _keys0;
        private readonly uint _keys1;
        private readonly uint _keys2;
        private readonly uint _keys3;
        private readonly uint _keys4;
        private readonly uint _keys5;
        private readonly uint _keys6;
        private readonly uint _keys7;

        private KeyboardStateCacheKey(uint keys0, uint keys1, uint keys2, uint keys3, uint keys4, uint keys5, uint keys6, uint keys7)
        {
            _keys0 = keys0;
            _keys1 = keys1;
            _keys2 = keys2;
            _keys3 = keys3;
            _keys4 = keys4;
            _keys5 = keys5;
            _keys6 = keys6;
            _keys7 = keys7;
        }

        public bool Contains(Keys key)
        {
            int value = (int)key;
            if ((uint)value > LastTrackedKey)
            {
                return false;
            }

            uint mask = 1u << (value & 31);
            return (value >> 5) switch
            {
                0 => (_keys0 & mask) != 0,
                1 => (_keys1 & mask) != 0,
                2 => (_keys2 & mask) != 0,
                3 => (_keys3 & mask) != 0,
                4 => (_keys4 & mask) != 0,
                5 => (_keys5 & mask) != 0,
                6 => (_keys6 & mask) != 0,
                7 => (_keys7 & mask) != 0,
                _ => false,
            };
        }

        public KeyboardStateCacheKey WithKey(int keyValue)
        {
            uint mask = 1u << (keyValue & 31);
            return (keyValue >> 5) switch
            {
                0 => new KeyboardStateCacheKey(_keys0 | mask, _keys1, _keys2, _keys3, _keys4, _keys5, _keys6, _keys7),
                1 => new KeyboardStateCacheKey(_keys0, _keys1 | mask, _keys2, _keys3, _keys4, _keys5, _keys6, _keys7),
                2 => new KeyboardStateCacheKey(_keys0, _keys1, _keys2 | mask, _keys3, _keys4, _keys5, _keys6, _keys7),
                3 => new KeyboardStateCacheKey(_keys0, _keys1, _keys2, _keys3 | mask, _keys4, _keys5, _keys6, _keys7),
                4 => new KeyboardStateCacheKey(_keys0, _keys1, _keys2, _keys3, _keys4 | mask, _keys5, _keys6, _keys7),
                5 => new KeyboardStateCacheKey(_keys0, _keys1, _keys2, _keys3, _keys4, _keys5 | mask, _keys6, _keys7),
                6 => new KeyboardStateCacheKey(_keys0, _keys1, _keys2, _keys3, _keys4, _keys5, _keys6 | mask, _keys7),
                7 => new KeyboardStateCacheKey(_keys0, _keys1, _keys2, _keys3, _keys4, _keys5, _keys6, _keys7 | mask),
                _ => this,
            };
        }

        public bool Equals(KeyboardStateCacheKey other)
        {
            return _keys0 == other._keys0
                && _keys1 == other._keys1
                && _keys2 == other._keys2
                && _keys3 == other._keys3
                && _keys4 == other._keys4
                && _keys5 == other._keys5
                && _keys6 == other._keys6
                && _keys7 == other._keys7;
        }

        public override bool Equals(object obj)
        {
            return obj is KeyboardStateCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (int)_keys0;
                hashCode = (hashCode * 397) ^ (int)_keys1;
                hashCode = (hashCode * 397) ^ (int)_keys2;
                hashCode = (hashCode * 397) ^ (int)_keys3;
                hashCode = (hashCode * 397) ^ (int)_keys4;
                hashCode = (hashCode * 397) ^ (int)_keys5;
                hashCode = (hashCode * 397) ^ (int)_keys6;
                hashCode = (hashCode * 397) ^ (int)_keys7;
                return hashCode;
            }
        }
    }

    KeyboardState IKeyboardStateProvider.GetState() => GetKeyboardState();

    MouseState IMouseStateProvider.GetState() => GetMouseState();
}