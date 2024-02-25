using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Engine.Input;

public class KeyboardManager
{
    private KeyboardState _currentState;
    private KeyboardState _previousState;

#if EDITOR
    public KeyboardState State => _currentState;
#endif

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

    internal void Update(KeyboardState keyboardState)
    {
        _previousState = _currentState;
        _currentState = keyboardState;
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
}
