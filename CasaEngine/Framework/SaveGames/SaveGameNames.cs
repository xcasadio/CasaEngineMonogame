using System.Text.RegularExpressions;

namespace CasaEngine.Framework.SaveGames;

/// <summary>
/// Whitelist name rules for save-game slots and for the project folder that holds them (ADR-0044).
/// Names are never sanitized: a name that fails a rule is refused, so two different names can never map
/// to the same file or folder.
/// </summary>
internal static class SaveGameNames
{
    public const int MaxSlotNameLength = 32;
    public const int MaxProjectFolderNameLength = 64;
    public const string SlotFileExtension = ".sav";

    // \A and \z instead of ^ and $: in .NET, '$' also matches before a final '\n', which would let
    // "slot\n" through.
    private static readonly Regex SlotNameRegex = new(@"\A[a-z0-9_-]{1,32}\z", RegexOptions.CultureInvariant);
    private static readonly Regex ProjectFolderNameRegex = new(@"\A[A-Za-z0-9](?:[A-Za-z0-9 _-]{0,62}[A-Za-z0-9_-])?\z", RegexOptions.CultureInvariant);

    private static readonly string[] WindowsReservedNames =
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// A slot name: 1 to 32 characters among lowercase ASCII letters, digits, '_' and '-' (lowercase only, so no
    /// case collision between Windows and Linux), and not a Windows reserved device name.
    /// </summary>
    public static bool IsValidSlotName(string name)
    {
        return name != null
            && SlotNameRegex.IsMatch(name)
            && !IsWindowsReservedName(name);
    }

    /// <summary>
    /// A project folder name: 1 to 64 ASCII letters, digits, spaces, '_' and '-', starting with a letter or a
    /// digit, not ending with a space, and not a Windows reserved device name.
    /// </summary>
    public static bool IsValidProjectFolderName(string name)
    {
        return name != null
            && ProjectFolderNameRegex.IsMatch(name)
            && !IsWindowsReservedName(name);
    }

    public static void ThrowIfInvalidSlotName(string name, string paramName)
    {
        if (!IsValidSlotName(name))
        {
            throw new ArgumentException(
                $"Invalid save-game slot name: expected 1 to {MaxSlotNameLength} characters among 'a'-'z', '0'-'9', '_' and '-', and not a Windows reserved device name.",
                paramName);
        }
    }

    public static string GetSlotFileName(string slotName)
    {
        return slotName + SlotFileExtension;
    }

    /// <summary>
    /// Windows refuses these device names in any case, even followed by an extension ("con.txt").
    /// </summary>
    private static bool IsWindowsReservedName(string name)
    {
        int extensionStart = name.IndexOf('.');
        string stem = extensionStart >= 0 ? name.Substring(0, extensionStart) : name;

        for (int i = 0; i < WindowsReservedNames.Length; i++)
        {
            if (string.Equals(stem, WindowsReservedNames[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
