using System.Runtime.InteropServices;
using CasaEngine.Core.Logging;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.Input;

/// <summary>
/// Reads keyboard bindings by key position (ADR-0096). A key written in an input mapping or in the camera navigation
/// names the position of that key on a US QWERTY keyboard; <see cref="FromQwertyPosition"/> returns the key the active
/// keyboard layout puts at that position. On QWERTY it returns the key unchanged; on AZERTY, W gives Z, A gives Q, Q gives
/// A, Z gives W and M gives the comma key.
/// <para/>
/// Only the keys that move from one layout to another are translated: letters, the digits of the top row and the
/// punctuation keys. Function keys, arrows, the numeric keypad, Escape, Enter, Space and the modifiers are kept.
/// <para/>
/// The table is built once, the first time it is read, from the layout active then (Windows: <c>MapVirtualKeyEx</c>).
/// A layout changed while the game runs is not followed. Reading it never allocates.
/// </summary>
public static class KeyboardLayout
{
    // US QWERTY position of each translated key, as a set-1 scan code. MonoGame's Keys values are the Windows virtual-key
    // codes, so the key the layout puts at a position is the virtual key Windows maps that scan code to.
    private static readonly (Keys Key, int ScanCode)[] QwertyScanCodes =
    {
        (Keys.D1, 0x02), (Keys.D2, 0x03), (Keys.D3, 0x04), (Keys.D4, 0x05), (Keys.D5, 0x06),
        (Keys.D6, 0x07), (Keys.D7, 0x08), (Keys.D8, 0x09), (Keys.D9, 0x0A), (Keys.D0, 0x0B),
        (Keys.OemMinus, 0x0C), (Keys.OemPlus, 0x0D),
        (Keys.Q, 0x10), (Keys.W, 0x11), (Keys.E, 0x12), (Keys.R, 0x13), (Keys.T, 0x14),
        (Keys.Y, 0x15), (Keys.U, 0x16), (Keys.I, 0x17), (Keys.O, 0x18), (Keys.P, 0x19),
        (Keys.OemOpenBrackets, 0x1A), (Keys.OemCloseBrackets, 0x1B),
        (Keys.A, 0x1E), (Keys.S, 0x1F), (Keys.D, 0x20), (Keys.F, 0x21), (Keys.G, 0x22),
        (Keys.H, 0x23), (Keys.J, 0x24), (Keys.K, 0x25), (Keys.L, 0x26),
        (Keys.OemSemicolon, 0x27), (Keys.OemQuotes, 0x28), (Keys.OemTilde, 0x29), (Keys.OemPipe, 0x2B),
        (Keys.Z, 0x2C), (Keys.X, 0x2D), (Keys.C, 0x2E), (Keys.V, 0x2F), (Keys.B, 0x30),
        (Keys.N, 0x31), (Keys.M, 0x32),
        (Keys.OemComma, 0x33), (Keys.OemPeriod, 0x34), (Keys.OemQuestion, 0x35),
        (Keys.OemBackslash, 0x56),
    };

    private const int TableSize = 256;

    private static Keys[] _table;

    /// <summary>The key the active layout puts at the position <paramref name="qwertyKey"/> has on a US QWERTY keyboard.</summary>
    public static Keys FromQwertyPosition(Keys qwertyKey)
    {
        var table = _table ?? BuildFromSystem();
        int index = (int)qwertyKey;
        return index >= 0 && index < TableSize ? table[index] : qwertyKey;
    }

    /// <summary>
    /// Builds the table from a layout given as a function from a set-1 scan code to the key at that position, or null when
    /// the layout has no key there (the position then keeps its QWERTY key). Used by the system detection and by tests.
    /// </summary>
    public static void UseLayout(Func<int, Keys?> keyAtScanCode)
    {
        ArgumentNullException.ThrowIfNull(keyAtScanCode);
        _table = BuildTable(keyAtScanCode);
    }

    /// <summary>Forgets the table: the next read builds it again from the active layout.</summary>
    public static void Reset()
    {
        _table = null;
    }

    private static Keys[] BuildFromSystem()
    {
        Keys[] table;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                IntPtr layout = GetKeyboardLayout(0);
                table = BuildTable(scanCode =>
                {
                    uint virtualKey = MapVirtualKeyEx((uint)scanCode, MapScanCodeToVirtualKey, layout);
                    return virtualKey == 0 ? null : (Keys)virtualKey;
                });
            }
            catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
            {
                Logs.WriteWarning($"The keyboard layout could not be read, keys are read as on a US QWERTY keyboard: {exception.Message}");
                table = BuildTable(_ => null);
            }
        }
        else
        {
            Logs.WriteWarning("The keyboard layout is only read on Windows: keys are read as on a US QWERTY keyboard.");
            table = BuildTable(_ => null);
        }

        _table = table;
        return table;
    }

    private static Keys[] BuildTable(Func<int, Keys?> keyAtScanCode)
    {
        var table = new Keys[TableSize];
        for (int index = 0; index < TableSize; index++)
        {
            table[index] = (Keys)index;
        }

        for (int index = 0; index < QwertyScanCodes.Length; index++)
        {
            var (key, scanCode) = QwertyScanCodes[index];
            if (keyAtScanCode(scanCode) is { } layoutKey && layoutKey != Keys.None)
            {
                table[(int)key] = layoutKey;
            }
        }

        return table;
    }

    private const uint MapScanCodeToVirtualKey = 1; // MAPVK_VSC_TO_VK

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKeyEx(uint code, uint mapType, IntPtr layout);
}
