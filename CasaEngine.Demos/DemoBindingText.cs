using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos;

/// <summary>
/// What one action of the demos' input configuration is bound to: a key, an optional second key and an optional
/// gamepad button. <see cref="Keys.None"/> and a button value of 0 mean "none".
/// </summary>
internal readonly record struct DemoBinding(Keys Key, Keys AlternativeKey, Buttons Button);

/// <summary>
/// Turns bindings into the words a sheet shows ("Space", "Arrow keys", "D-pad", "LB"). Pure text, called when a
/// screen is built, never per frame.
/// </summary>
internal static class DemoBindingText
{
    private const string ArrowKeys = "Arrow keys";
    private const string DPad = "D-pad";

    /// <summary>The label of a key, or an empty string for <see cref="Keys.None"/>.</summary>
    public static string KeyLabel(Keys key)
    {
        switch (key)
        {
            case Keys.None: return string.Empty;
            case Keys.Escape: return "Esc";
            case Keys.Back: return "Backspace";
            case Keys.PageUp: return "Page Up";
            case Keys.PageDown: return "Page Down";
            case Keys.LeftShift: return "Left Shift";
            case Keys.RightShift: return "Right Shift";
            case Keys.Add: return "Numpad +";
            case Keys.Subtract: return "Numpad -";
        }

        if (key >= Keys.D0 && key <= Keys.D9)
        {
            return ((char)('0' + (key - Keys.D0))).ToString();
        }

        if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
        {
            return "Numpad " + (char)('0' + (key - Keys.NumPad0));
        }

        return key.ToString();
    }

    /// <summary>The label of a gamepad button, or an empty string for no button.</summary>
    public static string ButtonLabel(Buttons button)
    {
        switch (button)
        {
            case 0: return string.Empty;
            case Buttons.Back: return "Select";
            case Buttons.LeftShoulder: return "LB";
            case Buttons.RightShoulder: return "RB";
            case Buttons.LeftStick: return "LS";
            case Buttons.RightStick: return "RS";
            case Buttons.DPadUp: return "D-pad up";
            case Buttons.DPadDown: return "D-pad down";
            case Buttons.DPadLeft: return "D-pad left";
            case Buttons.DPadRight: return "D-pad right";
            default: return button.ToString();
        }
    }

    /// <summary>
    /// The keyboard side of one sheet line: the keys of every binding, separated by commas. The four arrows together
    /// read "Arrow keys".
    /// </summary>
    public static string KeyboardText(IReadOnlyList<DemoBinding> bindings)
    {
        if (CoversExactly(bindings, Keys.Left, Keys.Right, Keys.Up, Keys.Down))
        {
            return ArrowKeys;
        }

        var text = new StringBuilder();
        for (int i = 0; i < bindings.Count; i++)
        {
            Append(text, KeyLabel(bindings[i].Key));
            Append(text, KeyLabel(bindings[i].AlternativeKey));
        }

        return text.ToString();
    }

    /// <summary>The gamepad side of one sheet line; the four directions of the pad together read "D-pad".</summary>
    public static string GamePadText(IReadOnlyList<DemoBinding> bindings)
    {
        if (bindings.Count == 4
            && HasButton(bindings, Buttons.DPadLeft) && HasButton(bindings, Buttons.DPadRight)
            && HasButton(bindings, Buttons.DPadUp) && HasButton(bindings, Buttons.DPadDown))
        {
            return DPad;
        }

        var text = new StringBuilder();
        for (int i = 0; i < bindings.Count; i++)
        {
            Append(text, ButtonLabel(bindings[i].Button));
        }

        return text.ToString();
    }

    private static void Append(StringBuilder text, string label)
    {
        if (label.Length == 0)
        {
            return;
        }

        if (text.Length > 0)
        {
            text.Append(", ");
        }

        text.Append(label);
    }

    private static bool CoversExactly(IReadOnlyList<DemoBinding> bindings, Keys first, Keys second, Keys third, Keys fourth)
    {
        if (bindings.Count != 4)
        {
            return false;
        }

        for (int i = 0; i < bindings.Count; i++)
        {
            if (bindings[i].AlternativeKey != Keys.None)
            {
                return false;
            }
        }

        return HasKey(bindings, first) && HasKey(bindings, second) && HasKey(bindings, third) && HasKey(bindings, fourth);
    }

    private static bool HasKey(IReadOnlyList<DemoBinding> bindings, Keys key)
    {
        for (int i = 0; i < bindings.Count; i++)
        {
            if (bindings[i].Key == key)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasButton(IReadOnlyList<DemoBinding> bindings, Buttons button)
    {
        for (int i = 0; i < bindings.Count; i++)
        {
            if (bindings[i].Button == button)
            {
                return true;
            }
        }

        return false;
    }
}
