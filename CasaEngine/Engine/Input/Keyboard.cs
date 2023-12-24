using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Engine.Input;

public static class Keyboard
{
    private static KeyboardState _currentState = Microsoft.Xna.Framework.Input.Keyboard.GetState();
    private static KeyboardState _previousState = Microsoft.Xna.Framework.Input.Keyboard.GetState();

    public static KeyboardState State => _currentState;
    public static KeyboardState PreviousState => _previousState;

    public static bool KeyJustPressed(Keys key)
    {
        return _currentState.IsKeyDown(key) && !_previousState.IsKeyDown(key);
    }

    public static bool KeyPressed(Keys key)
    {
        return _currentState.IsKeyDown(key);
    }

    public static bool IsSpecialKey(Keys key)
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

    public static string KeyToString(Keys key, bool shift, bool caps)
    {
        var uppercase = caps && !shift || !caps && shift;

        var keyNum = (int)key;
        if (keyNum >= (int)Keys.A && keyNum <= (int)Keys.Z)
        {
            if (uppercase)
            {
                return key.ToString();
            }
            else
            {
                return key.ToString().ToLower();
            }
        }

        switch (key)
        {
            case Keys.Space: return " ";
            case Keys.D1: return shift ? "!" : "1";
            case Keys.D2: return shift ? "@" : "2";
            case Keys.D3: return shift ? "#" : "3";
            case Keys.D4: return shift ? "$" : "4";
            case Keys.D5: return shift ? "%" : "5";
            case Keys.D6: return shift ? "^" : "6";
            case Keys.D7: return shift ? "&" : "7";
            case Keys.D8: return shift ? "*" : "8";
            case Keys.D9: return shift ? "(" : "9";
            case Keys.D0: return shift ? ")" : "0";
            case Keys.OemTilde: return shift ? "~" : "`";
            case Keys.OemMinus: return shift ? "_" : "-";
            case Keys.OemPipe: return shift ? "|" : "\\";
            case Keys.OemOpenBrackets: return shift ? "{" : "[";
            case Keys.OemCloseBrackets: return shift ? "}" : "]";
            case Keys.OemSemicolon: return shift ? ":" : ";";
            case Keys.OemQuotes: return shift ? "\"" : "\\";
            case Keys.OemComma: return shift ? "<" : ".";
            case Keys.OemPeriod: return shift ? ">" : ",";
            case Keys.OemQuestion: return shift ? "?" : "/";
            case Keys.OemPlus: return shift ? "+" : "=";
            case Keys.NumPad0: return shift ? "" : "0";
            case Keys.NumPad1: return shift ? "" : "1";
            case Keys.NumPad2: return shift ? "" : "2";
            case Keys.NumPad3: return shift ? "" : "3";
            case Keys.NumPad4: return shift ? "" : "4";
            case Keys.NumPad5: return shift ? "" : "5";
            case Keys.NumPad6: return shift ? "" : "6";
            case Keys.NumPad7: return shift ? "" : "7";
            case Keys.NumPad8: return shift ? "" : "8";
            case Keys.NumPad9: return shift ? "" : "9";
            case Keys.Divide: return "/";
            case Keys.Multiply: return "*";
            case Keys.Subtract: return "-";
            case Keys.Add: return "+";
            default: return "";
        }
    }

    internal static void Update()
    {
        _previousState = _currentState;
        _currentState = Microsoft.Xna.Framework.Input.Keyboard.GetState();
    }
}
