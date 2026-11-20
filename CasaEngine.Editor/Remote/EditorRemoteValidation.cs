using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using CasaEngine.Framework.Configuration;
using Microsoft.Xna.Framework;

namespace CasaEngine.Editor.Remote;

/// <summary>
/// Checks of what a remote client sends (ADR-0092, plan P5 and P8, security review S5 and S7): world names and paths
/// confined to the project, and property values limited to the inspector's types.
/// </summary>
internal static partial class EditorRemoteValidation
{
    public const int MaxEntityNameLength = 128;
    public const int MaxPropertyTextLength = 4096;

    private static readonly string[] ReservedDeviceNames =
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    [GeneratedRegex("^[A-Za-z0-9_][A-Za-z0-9 _-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex WorldNamePattern();

    /// <summary>A world name: letters, digits, space, '_' and '-', 64 characters at most, no device name.</summary>
    public static bool IsValidWorldName(string name)
    {
        if (string.IsNullOrEmpty(name) || !WorldNamePattern().IsMatch(name) || name.EndsWith(' '))
        {
            return false;
        }

        for (int i = 0; i < ReservedDeviceNames.Length; i++)
        {
            if (string.Equals(name, ReservedDeviceNames[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The project-relative file of a world created by name: <c>&lt;name&gt;.world</c> at the project root, as new projects do.</summary>
    public static string GetWorldFileName(string worldName) => worldName + Constants.FileNameExtensions.World;

    /// <summary>
    /// Resolves a project-relative path. False when it is rooted, holds ':', leaves the project once normalised or
    /// crosses a link below the project root (ADR-0093, A3); the containment test compares relative paths, so a sibling
    /// folder sharing a prefix (<c>C:\proj-x</c>) is outside.
    /// </summary>
    public static bool TryResolveInsideProject(string projectDirectory, string relativePath, out string fullPath)
        => TryResolveInsideProject(projectDirectory, relativePath, out fullPath, out _);

    /// <summary>
    /// <see cref="TryResolveInsideProject(string, string, out string)"/>, telling a link refusal apart:
    /// <paramref name="linkPath"/> names the existing component below the project root that is a link (or cannot be
    /// read), or the component, existing or missing, whose name ends with a dot or a white space (see
    /// <see cref="IsFreeOfLinksBelowRoot"/>); it is null when the refusal is textual (rooted, ':' or outside).
    /// </summary>
    public static bool TryResolveInsideProject(string projectDirectory, string relativePath, out string fullPath, out string linkPath)
    {
        fullPath = null;
        linkPath = null;
        if (string.IsNullOrWhiteSpace(projectDirectory)
            || string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathRooted(relativePath)
            || relativePath.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        string root = Path.GetFullPath(projectDirectory);
        string candidate = Path.GetFullPath(Path.Combine(root, relativePath));
        string relative = Path.GetRelativePath(root, candidate);
        if (relative == "." || Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return false;
        }

        if (!IsFreeOfLinksBelowRoot(root, candidate, out linkPath))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }

    /// <summary>
    /// The refusal of a path whose component <paramref name="linkPath"/> was refused by <see cref="IsFreeOfLinksBelowRoot"/>
    /// (ADR-0093, A3), naming it: a name ending with a dot or a white space when its last character says so, a link
    /// otherwise.
    /// </summary>
    public static EditorRemoteArgumentException LinkRefusal(string path, string linkPath)
    {
        string ending = DescribeNameEnding(linkPath);
        return ending != null
            ? new EditorRemoteArgumentException($"'{path}' goes through '{linkPath}', a name that ends with {ending}: Windows would read another entry than the one the path reaches, so such a name is refused (ADR-0093, A3).")
            : new EditorRemoteArgumentException($"'{path}' crosses '{linkPath}', a link (or an entry that cannot be read) inside the project: paths under a link are refused (ADR-0093, A3).");
    }

    /// <summary>
    /// ADR-0093 A3: true when no component of <paramref name="fullPath"/> strictly below <paramref name="root"/> has a name
    /// ending with a dot or a white space, and no existing one is a symbolic link, a junction or a mount point; the root
    /// and its ancestors are not checked. The name rule is textual, applies to existing and missing components alike and
    /// comes first: Windows reads such a name as another entry than the one a later access goes through. The link walk
    /// then stops at the first missing component (the rest is to be created). A component whose attributes cannot be read
    /// is refused (fail closed). <paramref name="linkPath"/> names the refused component, whether refused for its name or
    /// as a link. Both are full paths, <paramref name="fullPath"/> inside <paramref name="root"/>. A hard link carries no
    /// reparse attribute and is not detected.
    /// </summary>
    public static bool IsFreeOfLinksBelowRoot(string root, string fullPath, out string linkPath)
    {
        linkPath = null;
        string relative = Path.GetRelativePath(root, fullPath);
        if (relative == ".")
        {
            return true;
        }

        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{fullPath}' is not inside '{root}'.", nameof(fullPath));
        }

        string[] segments = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

        // Text only, before any read: the attributes of 'Real ' are those of 'Real', while 'Real \x' goes through 'Real '.
        string component = root;
        for (int i = 0; i < segments.Length; i++)
        {
            component = Path.Combine(component, segments[i]);
            if (DescribeNameEnding(segments[i]) != null)
            {
                linkPath = component;
                return false;
            }
        }

        string current = root;
        for (int i = 0; i < segments.Length; i++)
        {
            current = Path.Combine(current, segments[i]);
            if (IsLinkOrUnreadable(current, out bool missing))
            {
                linkPath = current;
                return false;
            }

            if (missing)
            {
                return true;
            }
        }

        return true;
    }

    /// <summary>
    /// "a dot", "a space" or "a white-space character" when the last character of <paramref name="name"/> (a segment, or a
    /// path whose last component is the segment) is '.', U+0020 or another white space; null otherwise.
    /// </summary>
    private static string DescribeNameEnding(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        char last = name[^1];
        if (last == '.')
        {
            return "a dot";
        }

        if (last == ' ')
        {
            return "a space";
        }

        return char.IsWhiteSpace(last) ? "a white-space character" : null;
    }

    /// <summary>
    /// True when <paramref name="path"/> is a link (a reparse point with a link target) or its attributes cannot be read;
    /// <paramref name="missing"/> is true when it does not exist. The attributes are those of the entry itself: its last
    /// component is not followed.
    /// </summary>
    private static bool IsLinkOrUnreadable(string path, out bool missing)
    {
        missing = false;
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            missing = true;
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            missing = true;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch (IOException)
        {
            return true;
        }

        if ((attributes & FileAttributes.ReparsePoint) == 0)
        {
            return false;
        }

        try
        {
            FileSystemInfo entry = (attributes & FileAttributes.Directory) != 0 ? new DirectoryInfo(path) : new FileInfo(path);
            return entry.LinkTarget != null;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch (IOException)
        {
            return true;
        }
    }

    /// <summary>
    /// The path rule of ADR-0093 P11 for the automation flags and the startup <c>--open-asset</c>: the resolved
    /// <paramref name="fullPath"/> must be inside <paramref name="projectPath"/>, hold no ':' below it (alternate data
    /// streams) and cross no link below it (A3). <paramref name="relativePath"/> is empty for the project folder itself.
    /// </summary>
    public static bool TryResolveAutomationPath(string projectPath, string fullPath, out string relativePath, out string error)
    {
        relativePath = null;
        if (string.IsNullOrWhiteSpace(projectPath) || string.IsNullOrWhiteSpace(fullPath))
        {
            error = "no project folder or no path: automation asset paths must be inside the project (ADR-0093 P11).";
            return false;
        }

        string root = Path.GetFullPath(projectPath);
        string candidate = Path.GetFullPath(fullPath);
        string relative = Path.GetRelativePath(root, candidate);
        if (relative == ".")
        {
            relative = string.Empty;
        }

        if (Path.IsPathRooted(relative)
            || relative == ".."
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            error = $"'{candidate}' is outside the project folder '{root}': automation asset paths must be inside the project (ADR-0093 P11).";
            return false;
        }

        if (relative.Contains(':', StringComparison.Ordinal))
        {
            error = $"'{candidate}' holds ':' inside the project folder '{root}': alternate data streams are refused (ADR-0093 P11).";
            return false;
        }

        if (!IsFreeOfLinksBelowRoot(root, candidate, out string linkPath))
        {
            string ending = DescribeNameEnding(linkPath);
            error = ending != null
                ? $"'{candidate}' goes through '{linkPath}', a name that ends with {ending} inside the project folder '{root}': Windows would read another entry than the one the path reaches, so such a name is refused (ADR-0093 P11, A3)."
                : $"'{candidate}' crosses '{linkPath}', a link (or an entry that cannot be read) inside the project folder '{root}': paths under a link are refused (ADR-0093 P11, A3).";
            return false;
        }

        relativePath = relative;
        error = null;
        return true;
    }

    public static string ValidateEntityName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxEntityNameLength)
        {
            throw new EditorRemoteArgumentException($"An entity name must be 1 to {MaxEntityNameLength} characters.");
        }

        return name;
    }

    /// <summary>Converts a JSON value to <paramref name="propertyType"/>, one of the inspector's types; throws on anything else.</summary>
    public static object ConvertPropertyValue(JsonElement value, Type propertyType, string propertyName)
    {
        Type underlying = Nullable.GetUnderlyingType(propertyType);
        if (underlying != null && value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        Type type = underlying ?? propertyType;
        if (type == typeof(string))
        {
            if (value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            string text = value.ValueKind == JsonValueKind.String ? value.GetString() : throw Invalid(propertyName, "a string");
            return text.Length <= MaxPropertyTextLength ? text : throw Invalid(propertyName, $"a string of at most {MaxPropertyTextLength} characters");
        }

        if (type == typeof(bool))
        {
            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => throw Invalid(propertyName, "true or false"),
            };
        }

        if (type == typeof(int))
        {
            return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number) ? number : throw Invalid(propertyName, "a 32-bit integer");
        }

        if (type == typeof(float))
        {
            double number = ReadFinite(value, propertyName);
            return Math.Abs(number) <= float.MaxValue ? (float)number : throw Invalid(propertyName, "a number in the float range");
        }

        if (type == typeof(double))
        {
            return ReadFinite(value, propertyName);
        }

        if (type == typeof(Vector3))
        {
            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 3)
            {
                throw Invalid(propertyName, "an array [x, y, z]");
            }

            return new Vector3(ReadFloat(value[0], propertyName), ReadFloat(value[1], propertyName), ReadFloat(value[2], propertyName));
        }

        if (type == typeof(Color))
        {
            int length = value.ValueKind == JsonValueKind.Array ? value.GetArrayLength() : 0;
            if (length is not (3 or 4))
            {
                throw Invalid(propertyName, "an array [r, g, b] or [r, g, b, a] of integers from 0 to 255");
            }

            int r = ReadByte(value[0], propertyName);
            int g = ReadByte(value[1], propertyName);
            int b = ReadByte(value[2], propertyName);
            int a = length == 4 ? ReadByte(value[3], propertyName) : 255;
            return new Color(r, g, b, a);
        }

        if (type == typeof(Guid))
        {
            return value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out Guid guid) ? guid : throw Invalid(propertyName, "a GUID string");
        }

        if (type.IsEnum)
        {
            string name = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            string[] names = Enum.GetNames(type);
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], name, StringComparison.Ordinal))
                {
                    return Enum.Parse(type, name);
                }
            }

            throw Invalid(propertyName, "one of " + string.Join(", ", names));
        }

        throw Invalid(propertyName, "a supported value");
    }

    private static double ReadFinite(JsonElement value, string propertyName)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double number) || !double.IsFinite(number))
        {
            throw Invalid(propertyName, "a finite number");
        }

        return number;
    }

    private static float ReadFloat(JsonElement value, string propertyName)
    {
        double number = ReadFinite(value, propertyName);
        return Math.Abs(number) <= float.MaxValue ? (float)number : throw Invalid(propertyName, "numbers in the float range");
    }

    private static int ReadByte(JsonElement value, string propertyName)
    {
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number) && number is >= 0 and <= 255
            ? number
            : throw Invalid(propertyName, "integers from 0 to 255");
    }

    private static EditorRemoteArgumentException Invalid(string propertyName, string expected)
        => new($"Property '{propertyName}' expects {expected}.");
}
