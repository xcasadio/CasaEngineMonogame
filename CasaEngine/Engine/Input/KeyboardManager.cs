using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Engine.Input;

public class KeyboardManager
{
    private KeyboardState _currentState;
    private KeyboardState _previousState;

    public KeyboardState State => _currentState;

    public bool IsKeyJustPressed(Keys key)
    {
        return _currentState.IsKeyDown(key) && !_previousState.IsKeyDown(key);
    }

    public bool IsKeyPressed(Keys key)
    {
        return _currentState.IsKeyDown(key);
    }

    public bool IsKeyReleased(Keys key)
    {
        return _currentState.IsKeyUp(key) && _previousState.IsKeyDown(key);
    }

    public bool IsKeyHeld(Keys key)
    {
        return _currentState.IsKeyDown(key) && _previousState.IsKeyDown(key);
    }

    public bool IsSpecialKey(Keys key)
    {
        // ~_|{}:"<>? !@#$%^&*().
        var keyNum = (int)key;
        if (keyNum >= (int)Keys.A && keyNum <= (int)Keys.Z ||
            keyNum >= (int)Keys.D0 && keyNum <= (int)Keys.D9 ||
            key == Keys.Space || // space
            key == Keys.OemTilde || // `~
            key == Keys.OemMinus || // -_
            key == Keys.OemPipe || // \|
            key == Keys.OemOpenBrackets || // [{
            key == Keys.OemCloseBrackets || // ]}
            key == Keys.OemSemicolon || // ;:
            key == Keys.OemQuotes || // '"
            key == Keys.OemComma || // ,<
            key == Keys.OemPeriod || // .>
            key == Keys.OemQuestion || // /?
            key == Keys.OemPlus) // =+
        {
            return false;
        }

        return true;
    }

    public string KeyToString(Keys key, bool shift, bool caps)
    {
        var uppercase = caps && !shift || !caps && shift;

        var keyNum = (int)key;
        if (keyNum >= (int)Keys.A && keyNum <= (int)Keys.Z)
        {
            return uppercase ? key.ToString() : key.ToString().ToLower();
        }

        return key switch
        {
            Keys.Space => " ",
            Keys.D1 => shift ? "!" : "1",
            Keys.D2 => shift ? "@" : "2",
            Keys.D3 => shift ? "#" : "3",
            Keys.D4 => shift ? "$" : "4",
            Keys.D5 => shift ? "%" : "5",
            Keys.D6 => shift ? "^" : "6",
            Keys.D7 => shift ? "&" : "7",
            Keys.D8 => shift ? "*" : "8",
            Keys.D9 => shift ? "(" : "9",
            Keys.D0 => shift ? ")" : "0",
            Keys.OemTilde => shift ? "~" : "`",
            Keys.OemMinus => shift ? "_" : "-",
            Keys.OemPipe => shift ? "|" : "\\",
            Keys.OemOpenBrackets => shift ? "{" : "[",
            Keys.OemCloseBrackets => shift ? "}" : "]",
            Keys.OemSemicolon => shift ? ":" : ";",
            Keys.OemQuotes => shift ? "\"" : "\\",
            Keys.OemComma => shift ? "<" : ".",
            Keys.OemPeriod => shift ? ">" : ",",
            Keys.OemQuestion => shift ? "?" : "/",
            Keys.OemPlus => shift ? "+" : "=",
            Keys.NumPad0 => shift ? "" : "0",
            Keys.NumPad1 => shift ? "" : "1",
            Keys.NumPad2 => shift ? "" : "2",
            Keys.NumPad3 => shift ? "" : "3",
            Keys.NumPad4 => shift ? "" : "4",
            Keys.NumPad5 => shift ? "" : "5",
            Keys.NumPad6 => shift ? "" : "6",
            Keys.NumPad7 => shift ? "" : "7",
            Keys.NumPad8 => shift ? "" : "8",
            Keys.NumPad9 => shift ? "" : "9",
            Keys.Divide => "/",
            Keys.Multiply => "*",
            Keys.Subtract => "-",
            Keys.Add => "+",
            _ => ""
        };
    }

    internal void Update(KeyboardState keyboardState)
    {
        _previousState = _currentState;
        _currentState = keyboardState;
    }
}
