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
    /// Resolves a project-relative path. False when it is rooted, holds ':' or leaves the project once normalised; the
    /// containment test compares relative paths, so a sibling folder sharing a prefix (<c>C:\proj-x</c>) is outside.
    /// </summary>
    public static bool TryResolveInsideProject(string projectDirectory, string relativePath, out string fullPath)
    {
        fullPath = null;
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

        fullPath = candidate;
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
