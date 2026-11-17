using System.Globalization;
using CasaEngine.Framework.Cutscenes;
using Microsoft.Xna.Framework;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>How the text of a cutscene action property is written and read.</summary>
public enum CutsceneActionPropertyKind
{
    /// <summary>A finite decimal number, written as a <see cref="float"/>.</summary>
    Number,

    /// <summary>A whole number, written as an <see cref="int"/>.</summary>
    Integer,

    /// <summary>A whole number from 0 to 255, written as a <see cref="byte"/>.</summary>
    Byte,

    /// <summary><c>true</c> or <c>false</c>.</summary>
    Boolean,

    /// <summary>A <see cref="Guid"/>.</summary>
    Guid,

    /// <summary><c>r, g, b, a</c>, each from 0 to 255; <c>r, g, b</c> is read as opaque.</summary>
    Color,

    /// <summary><c>x, y, z</c>.</summary>
    Vector3,

    /// <summary>The name of a defined value of an enumeration.</summary>
    Enum,

    /// <summary>Free text, the empty string included.</summary>
    String,

    /// <summary>Comma-separated, non-empty entries.</summary>
    StringList,
}

/// <summary>
/// One editable field of a cutscene action, under its JSON key. It reads and writes the typed value on an action and
/// converts that value to and from text, in the invariant culture.
/// </summary>
/// <remarks>
/// The value is a boxed <see cref="float"/>, <see cref="int"/>, <see cref="byte"/>, <see cref="bool"/>,
/// <see cref="System.Guid"/>, <see cref="Microsoft.Xna.Framework.Color"/>, <see cref="Microsoft.Xna.Framework.Vector3"/>,
/// enumeration value or <see cref="string"/>, or <c>null</c> for a nullable property. A list property
/// (<see cref="CutsceneActionPropertyKind.StringList"/>) reads as a copy and is written from a copy, so a captured
/// value never changes afterwards. The text of a list joins its entries with ", ": an entry that holds a comma, or
/// leading or trailing blanks, does not survive a parse of its text.
/// </remarks>
public sealed class CutsceneActionProperty
{
    private readonly Func<CutsceneActionData, object?> _get;
    private readonly Action<CutsceneActionData, object?> _set;

    internal CutsceneActionProperty(
        string key,
        CutsceneActionPropertyKind kind,
        bool isNullable,
        Type valueType,
        Func<CutsceneActionData, object?> get,
        Action<CutsceneActionData, object?> set)
    {
        Key = key;
        Kind = kind;
        IsNullable = isNullable;
        ValueType = valueType;
        _get = get;
        _set = set;
    }

    /// <summary>The JSON key the serializer writes the field under.</summary>
    public string Key { get; }

    public CutsceneActionPropertyKind Kind { get; }

    /// <summary>
    /// True when the C# property can be null. A string property always accepts null on <see cref="SetValue"/>, since a
    /// loaded action can hold it, but a text never produces it (<see cref="TryParse"/> gives the empty string).
    /// </summary>
    public bool IsNullable { get; }

    /// <summary>The type of a non-null value: <see cref="float"/>, an enumeration type, <c>List&lt;string&gt;</c>...</summary>
    public Type ValueType { get; }

    /// <summary>The current value; a copy for a list.</summary>
    public object? GetValue(CutsceneActionData action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return _get(action);
    }

    /// <summary>Writes the value; a list replaces the content of the action's list from a copy of the given one.</summary>
    /// <exception cref="ArgumentException">The value does not have the type of the property.</exception>
    public void SetValue(CutsceneActionData action, object? value)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (value == null)
        {
            if (!IsNullable && Kind != CutsceneActionPropertyKind.String)
            {
                throw new ArgumentException($"The property '{Key}' does not accept null.", nameof(value));
            }
        }
        else if (Kind == CutsceneActionPropertyKind.StringList)
        {
            if (value is not IEnumerable<string>)
            {
                throw new ArgumentException($"The property '{Key}' expects a list of strings, not {value.GetType().Name}.", nameof(value));
            }
        }
        else if (value.GetType() != ValueType)
        {
            throw new ArgumentException($"The property '{Key}' expects a {ValueType.Name}, not {value.GetType().Name}.", nameof(value));
        }

        _set(action, value);
    }

    /// <summary>The text of the current value.</summary>
    public string GetText(CutsceneActionData action) => FormatValue(GetValue(action));

    /// <summary>The text of a value, without loss: <see cref="TryParse"/> reads it back to an equal value.</summary>
    public string FormatValue(object? value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        switch (Kind)
        {
            case CutsceneActionPropertyKind.Number:
                return FormatNumber((float)value);
            case CutsceneActionPropertyKind.Integer:
                return ((int)value).ToString(CultureInfo.InvariantCulture);
            case CutsceneActionPropertyKind.Byte:
                return ((byte)value).ToString(CultureInfo.InvariantCulture);
            case CutsceneActionPropertyKind.Boolean:
                return (bool)value ? "true" : "false";
            case CutsceneActionPropertyKind.Guid:
                return ((Guid)value).ToString();
            case CutsceneActionPropertyKind.Color:
                var color = (Color)value;
                return string.Create(CultureInfo.InvariantCulture, $"{color.R}, {color.G}, {color.B}, {color.A}");
            case CutsceneActionPropertyKind.Vector3:
                var vector = (Vector3)value;
                return $"{FormatNumber(vector.X)}, {FormatNumber(vector.Y)}, {FormatNumber(vector.Z)}";
            case CutsceneActionPropertyKind.Enum:
                return value.ToString() ?? string.Empty;
            case CutsceneActionPropertyKind.String:
                return (string)value;
            default:
                return string.Join(", ", (IEnumerable<string>)value);
        }
    }

    /// <summary>
    /// Reads a text. On success <paramref name="value"/> is ready for <see cref="SetValue"/>; otherwise it is null and
    /// <paramref name="error"/> says why. A blank text gives null for a nullable property that is not a string, the
    /// empty string for a string, an empty list for a list, and is refused for any other property.
    /// </summary>
    public bool TryParse(string text, out object? value, out string error)
    {
        value = null;
        error = string.Empty;
        text ??= string.Empty;

        if (Kind == CutsceneActionPropertyKind.String)
        {
            value = text;
            return true;
        }

        string trimmed = text.Trim();
        if (Kind == CutsceneActionPropertyKind.StringList)
        {
            return TryParseList(trimmed, out value, out error);
        }

        if (trimmed.Length == 0)
        {
            if (IsNullable)
            {
                return true;
            }

            error = $"{Key} cannot be empty.";
            return false;
        }

        switch (Kind)
        {
            case CutsceneActionPropertyKind.Number:
                if (TryParseNumber(trimmed, out float number))
                {
                    value = number;
                    return true;
                }

                error = $"{Key} must be a finite number, but is '{text}'.";
                return false;

            case CutsceneActionPropertyKind.Integer:
                if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer))
                {
                    value = integer;
                    return true;
                }

                error = $"{Key} must be an integer, but is '{text}'.";
                return false;

            case CutsceneActionPropertyKind.Byte:
                if (TryParseChannel(trimmed, out byte channel))
                {
                    value = channel;
                    return true;
                }

                error = $"{Key} must be an integer from 0 to 255, but is '{text}'.";
                return false;

            case CutsceneActionPropertyKind.Boolean:
                if (bool.TryParse(trimmed, out bool boolean))
                {
                    value = boolean;
                    return true;
                }

                error = $"{Key} must be true or false, but is '{text}'.";
                return false;

            case CutsceneActionPropertyKind.Guid:
                if (Guid.TryParse(trimmed, out Guid guid))
                {
                    value = guid;
                    return true;
                }

                error = $"{Key} must be a GUID, but is '{text}'.";
                return false;

            case CutsceneActionPropertyKind.Color:
                return TryParseColor(trimmed, text, out value, out error);

            case CutsceneActionPropertyKind.Vector3:
                return TryParseVector3(trimmed, text, out value, out error);

            default:
                return TryParseEnum(trimmed, text, out value, out error);
        }
    }

    /// <summary>True when both values are equal; two lists are equal when their entries are.</summary>
    public bool AreEqual(object? left, object? right)
    {
        if (left == null || right == null)
        {
            return left == null && right == null;
        }

        if (Kind == CutsceneActionPropertyKind.StringList)
        {
            return ((IEnumerable<string>)left).SequenceEqual((IEnumerable<string>)right);
        }

        return left.Equals(right);
    }

    private static string FormatNumber(float number) => number.ToString("R", CultureInfo.InvariantCulture);

    private static bool TryParseNumber(string text, out float number)
        => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && float.IsFinite(number);

    private static bool TryParseChannel(string text, out byte channel)
    {
        channel = 0;
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer) || integer is < 0 or > 255)
        {
            return false;
        }

        channel = (byte)integer;
        return true;
    }

    private bool TryParseColor(string trimmed, string text, out object? value, out string error)
    {
        value = null;
        string[] parts = trimmed.Split(',');
        if (parts.Length is 3 or 4)
        {
            var channels = new byte[] { 0, 0, 0, byte.MaxValue };
            int read = 0;
            while (read < parts.Length && TryParseChannel(parts[read].Trim(), out channels[read]))
            {
                read++;
            }

            if (read == parts.Length)
            {
                value = new Color(channels[0], channels[1], channels[2], channels[3]);
                error = string.Empty;
                return true;
            }
        }

        error = $"{Key} must be r, g, b, a, each an integer from 0 to 255, but is '{text}'.";
        return false;
    }

    private bool TryParseVector3(string trimmed, string text, out object? value, out string error)
    {
        value = null;
        string[] parts = trimmed.Split(',');
        if (parts.Length == 3
            && TryParseNumber(parts[0].Trim(), out float x)
            && TryParseNumber(parts[1].Trim(), out float y)
            && TryParseNumber(parts[2].Trim(), out float z))
        {
            value = new Vector3(x, y, z);
            error = string.Empty;
            return true;
        }

        error = $"{Key} must be x, y, z, each a finite number, but is '{text}'.";
        return false;
    }

    private bool TryParseEnum(string trimmed, string text, out object? value, out string error)
    {
        value = null;
        foreach (string name in Enum.GetNames(ValueType))
        {
            if (string.Equals(name, trimmed, StringComparison.Ordinal))
            {
                value = Enum.Parse(ValueType, name);
                error = string.Empty;
                return true;
            }
        }

        error = $"{Key} must be one of {string.Join(", ", Enum.GetNames(ValueType))}, but is '{text}'.";
        return false;
    }

    private bool TryParseList(string trimmed, out object? value, out string error)
    {
        value = null;
        error = string.Empty;

        var entries = new List<string>();
        if (trimmed.Length > 0)
        {
            string[] parts = trimmed.Split(',');
            for (int index = 0; index < parts.Length; index++)
            {
                string entry = parts[index].Trim();
                if (entry.Length == 0)
                {
                    error = $"{Key} must not hold an empty entry (entry {index + 1}).";
                    return false;
                }

                entries.Add(entry);
            }
        }

        value = entries;
        return true;
    }
}
