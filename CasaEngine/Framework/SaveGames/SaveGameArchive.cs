using System.Globalization;
using System.Numerics;

namespace CasaEngine.Framework.SaveGames;

/// <summary>
/// The integer widths a save-game format encodes. The archive checks every read value against the range of its
/// kind, so a format may hand back any <see cref="long"/>.
/// </summary>
internal enum SaveGameIntegerKind
{
    Byte,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Int64,
}

/// <summary>
/// The single read-and-write API of a save game (ADR-0044, modeled on Unreal's <c>FArchive</c>):
/// <see cref="ISaveGameData.Serialize"/> calls <c>archive.Value("hp", ref hp)</c>, which writes the field when
/// saving and overwrites it when loading. No reflection: the calls themselves describe the data.
/// </summary>
/// <remarks>
/// <para>
/// Formats: the JSON format finds each field by its name inside the current object; the binary format writes
/// the fields in call order and ignores their names, so the calls must be the same, in the same order, in both
/// directions, and <see cref="DataVersion"/> is what keeps an older layout readable. Field names are
/// non-empty and unique inside one object.
/// </para>
/// <para>
/// Supported values: <see cref="bool"/>, <see cref="byte"/>, <see cref="short"/>, <see cref="ushort"/>,
/// <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="float"/> and <see cref="string"/>
/// fields; fixed-length arrays of <see cref="byte"/>, <see cref="short"/>, <see cref="ushort"/>,
/// <see cref="int"/>, <see cref="uint"/> and <see cref="long"/>; nested objects through
/// <see cref="BeginObject"/> and <see cref="EndObject"/>, at most <see cref="MaxObjectDepth"/> levels deep.
/// </para>
/// <para>
/// Refused values, in both directions and in both formats: a non-finite <see cref="float"/> (NaN, infinities),
/// a null <see cref="string"/> or one that is not well-formed UTF-16 (unpaired surrogate), an integer read
/// outside the range of its C# type, an array read with a length different from the caller's. A refused value
/// throws an internal save-game data exception naming the field with its nesting path
/// (<c>player.inventory[3]</c>), which the save-game service turns into an <c>InvalidData</c> result.
/// String length is not capped by the archive: the slot size limit (1 MiB) bounds every string.
/// </para>
/// <para>
/// Developer misuse throws instead and is not turned into a result: a null or empty name
/// (<see cref="ArgumentException"/>), a null array (<see cref="ArgumentNullException"/>), unbalanced or
/// too deep <see cref="BeginObject"/> / <see cref="EndObject"/> calls (<see cref="InvalidOperationException"/>).
/// </para>
/// <para>
/// An archive serves a single <see cref="ISaveGameData.Serialize"/> call. After an exception it is discarded,
/// and so is the object being loaded: a failed load may have overwritten some of its fields, including part of
/// an array, but the save-game service never returns such an object.
/// </para>
/// <para>
/// Only the engine implements archives: the constructor is not accessible to games.
/// </para>
/// </remarks>
public abstract class SaveGameArchive
{
    /// <summary>
    /// Deepest <see cref="BeginObject"/> nesting. It keeps every written JSON document well inside the JSON reader's
    /// depth limit (64) once the file envelope and an array level are added.
    /// </summary>
    public const int MaxObjectDepth = 32;

    private readonly List<string> _objectPath = new();
    private string _currentArrayName;

    /// <param name="isLoading">True when the archive reads a file into the object, false when it writes the object.</param>
    /// <param name="dataVersion">
    /// The file's data version when loading, the object's latest one when saving. Negative values are refused:
    /// a format checks the version it reads before constructing the archive.
    /// </param>
    private protected SaveGameArchive(bool isLoading, int dataVersion)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dataVersion);

        IsLoading = isLoading;
        DataVersion = dataVersion;
    }

    /// <summary>True when the archive reads a file into the object, false when it writes the object.</summary>
    public bool IsLoading { get; }

    /// <summary>
    /// The data version the file was written with when loading (at most <see cref="ISaveGameData.LatestDataVersion"/>),
    /// <see cref="ISaveGameData.LatestDataVersion"/> when saving. Branch on it to read an older layout.
    /// </summary>
    public int DataVersion { get; }

    /// <summary>Writes or reads a <see cref="bool"/> field.</summary>
    public void Value(string name, ref bool value)
    {
        ThrowIfInvalidName(name);

        if (IsLoading)
        {
            value = ReadBooleanCore(name);
        }
        else
        {
            WriteBooleanCore(name, value);
        }
    }

    /// <summary>Writes or reads a <see cref="byte"/> field; a value read outside 0-255 is refused.</summary>
    public void Value(string name, ref byte value)
    {
        IntegerValue(name, ref value, SaveGameIntegerKind.Byte);
    }

    /// <summary>Writes or reads a <see cref="short"/> field; a value read outside its range is refused.</summary>
    public void Value(string name, ref short value)
    {
        IntegerValue(name, ref value, SaveGameIntegerKind.Int16);
    }

    /// <summary>Writes or reads a <see cref="ushort"/> field; a value read outside its range is refused.</summary>
    public void Value(string name, ref ushort value)
    {
        IntegerValue(name, ref value, SaveGameIntegerKind.UInt16);
    }

    /// <summary>Writes or reads an <see cref="int"/> field; a value read outside its range is refused.</summary>
    public void Value(string name, ref int value)
    {
        IntegerValue(name, ref value, SaveGameIntegerKind.Int32);
    }

    /// <summary>Writes or reads a <see cref="uint"/> field; a value read outside its range is refused.</summary>
    public void Value(string name, ref uint value)
    {
        IntegerValue(name, ref value, SaveGameIntegerKind.UInt32);
    }

    /// <summary>Writes or reads a <see cref="long"/> field.</summary>
    public void Value(string name, ref long value)
    {
        IntegerValue(name, ref value, SaveGameIntegerKind.Int64);
    }

    /// <summary>
    /// Writes or reads a <see cref="float"/> field. NaN and infinities are refused when writing and when reading.
    /// </summary>
    public void Value(string name, ref float value)
    {
        ThrowIfInvalidName(name);

        if (IsLoading)
        {
            float readValue = ReadSingleCore(name);
            ThrowIfNotFinite(name, readValue);
            value = readValue;
        }
        else
        {
            ThrowIfNotFinite(name, value);
            WriteSingleCore(name, value);
        }
    }

    /// <summary>
    /// Writes or reads a <see cref="string"/> field. A null string, or one with an unpaired surrogate, is refused
    /// when writing and when reading: write <see cref="string.Empty"/> for "no text".
    /// </summary>
    public void Value(string name, ref string value)
    {
        ThrowIfInvalidName(name);

        if (IsLoading)
        {
            string readValue = ReadStringCore(name);
            ThrowIfInvalidString(name, readValue);
            value = readValue;
        }
        else
        {
            ThrowIfInvalidString(name, value);
            WriteStringCore(name, value);
        }
    }

    /// <summary>
    /// Writes <paramref name="values"/>, or fills it when loading. The array length is fixed by the caller:
    /// a file array of another length is refused before any element is read.
    /// </summary>
    public void Value(string name, byte[] values)
    {
        IntegerArray(name, values, SaveGameIntegerKind.Byte);
    }

    /// <inheritdoc cref="Value(string, byte[])"/>
    public void Value(string name, short[] values)
    {
        IntegerArray(name, values, SaveGameIntegerKind.Int16);
    }

    /// <inheritdoc cref="Value(string, byte[])"/>
    public void Value(string name, ushort[] values)
    {
        IntegerArray(name, values, SaveGameIntegerKind.UInt16);
    }

    /// <inheritdoc cref="Value(string, byte[])"/>
    public void Value(string name, int[] values)
    {
        IntegerArray(name, values, SaveGameIntegerKind.Int32);
    }

    /// <inheritdoc cref="Value(string, byte[])"/>
    public void Value(string name, uint[] values)
    {
        IntegerArray(name, values, SaveGameIntegerKind.UInt32);
    }

    /// <inheritdoc cref="Value(string, byte[])"/>
    public void Value(string name, long[] values)
    {
        IntegerArray(name, values, SaveGameIntegerKind.Int64);
    }

    /// <summary>
    /// Opens a nested object; the following fields belong to it until the matching <see cref="EndObject"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The nesting would exceed <see cref="MaxObjectDepth"/>.</exception>
    public void BeginObject(string name)
    {
        ThrowIfInvalidName(name);

        if (_objectPath.Count >= MaxObjectDepth)
        {
            throw new InvalidOperationException(
                $"Save-game object '{GetFieldPath(name)}' would nest deeper than {MaxObjectDepth} levels.");
        }

        // The path is extended after the format has found or opened the object, so that an error about the
        // object itself names it once ("player", not "player.player").
        BeginObjectCore(name);
        _objectPath.Add(name);
    }

    /// <summary>Closes the object opened by the last <see cref="BeginObject"/>.</summary>
    /// <exception cref="InvalidOperationException">No object is open.</exception>
    public void EndObject()
    {
        if (_objectPath.Count == 0)
        {
            throw new InvalidOperationException("Save-game EndObject called without a matching BeginObject.");
        }

        EndObjectCore();
        _objectPath.RemoveAt(_objectPath.Count - 1);
    }

    /// <summary>
    /// Called by the save-game service after <see cref="ISaveGameData.Serialize"/> returns: every
    /// <see cref="BeginObject"/> must have been closed.
    /// </summary>
    /// <exception cref="InvalidOperationException">An object is still open (developer misuse).</exception>
    internal void ThrowIfObjectOpen()
    {
        if (_objectPath.Count > 0)
        {
            throw new InvalidOperationException(
                $"Save-game object '{string.Join('.', _objectPath)}' was opened by BeginObject and never closed by EndObject.");
        }
    }

    /// <summary>
    /// The nesting path of <paramref name="name"/> in the current object, for error messages:
    /// <c>name</c> at the root, <c>outer.inner.name</c> inside nested objects.
    /// </summary>
    private protected string GetFieldPath(string name)
    {
        if (_objectPath.Count == 0)
        {
            return name;
        }

        return string.Join('.', _objectPath) + "." + name;
    }

    /// <summary>A data error about the field <paramref name="name"/> of the current object.</summary>
    private protected SaveGameDataException CreateDataException(string name, string reason)
    {
        return new SaveGameDataException(GetFieldPath(name), reason);
    }

    /// <summary>
    /// A data error about the element <paramref name="index"/> of the array being read or written; only valid
    /// between <see cref="ReadArrayStartCore"/> or <see cref="WriteArrayStartCore"/> and <see cref="EndArrayCore"/>.
    /// </summary>
    private protected SaveGameDataException CreateElementDataException(int index, string reason)
    {
        return new SaveGameDataException(GetElementPath(index), reason);
    }

    // Format primitives. The archive has already validated the name and, when writing, the value; it validates
    // every value a Read*Core returns (integer range, finite float, well-formed string, array length). A format
    // reports a missing or mistyped value with CreateDataException or CreateElementDataException, never by
    // letting its own parser's exception escape.

    private protected abstract bool ReadBooleanCore(string name);

    private protected abstract void WriteBooleanCore(string name, bool value);

    /// <summary>
    /// Reads an integer field of the given width. The archive checks the range of <paramref name="kind"/>
    /// afterwards, so a format that stores wider integers (JSON) returns the value as it is.
    /// </summary>
    private protected abstract long ReadIntegerCore(string name, SaveGameIntegerKind kind);

    /// <summary>Writes an integer field; <paramref name="value"/> always fits <paramref name="kind"/>.</summary>
    private protected abstract void WriteIntegerCore(string name, long value, SaveGameIntegerKind kind);

    /// <summary>
    /// Reads a float field. The archive refuses a non-finite result, so a format that stores doubles (JSON) returns
    /// the narrowed value as it is: a double beyond the float range becomes an infinity and is refused.
    /// </summary>
    private protected abstract float ReadSingleCore(string name);

    /// <summary>Writes a float field; <paramref name="value"/> is always finite.</summary>
    private protected abstract void WriteSingleCore(string name, float value);

    /// <summary>
    /// Reads a string field. The archive refuses a null or malformed result afterwards; the format bounds the
    /// length it decodes by the bytes actually left in the file.
    /// </summary>
    private protected abstract string ReadStringCore(string name);

    /// <summary>Writes a string field; <paramref name="value"/> is never null and is well-formed UTF-16.</summary>
    private protected abstract void WriteStringCore(string name, string value);

    /// <summary>
    /// Opens an integer array field and returns the element count stored in the file, without allocating for it:
    /// the archive compares it to the caller's length before reading any element.
    /// </summary>
    private protected abstract int ReadArrayStartCore(string name, SaveGameIntegerKind kind);

    /// <summary>Opens an integer array field of <paramref name="length"/> elements.</summary>
    private protected abstract void WriteArrayStartCore(string name, int length, SaveGameIntegerKind kind);

    /// <summary>Reads the element <paramref name="index"/> of the open array; the archive checks its range.</summary>
    private protected abstract long ReadArrayElementCore(int index, SaveGameIntegerKind kind);

    /// <summary>Writes the element <paramref name="index"/> of the open array.</summary>
    private protected abstract void WriteArrayElementCore(int index, long value, SaveGameIntegerKind kind);

    /// <summary>Closes the open array, in either direction.</summary>
    private protected abstract void EndArrayCore();

    /// <summary>
    /// Finds (loading) or opens (saving) the nested object <paramref name="name"/> of the current object.
    /// </summary>
    private protected abstract void BeginObjectCore(string name);

    /// <summary>Returns to the enclosing object, in either direction.</summary>
    private protected abstract void EndObjectCore();

    private void IntegerValue<T>(string name, ref T value, SaveGameIntegerKind kind)
        where T : struct, IBinaryInteger<T>
    {
        ThrowIfInvalidName(name);

        if (IsLoading)
        {
            long readValue = ReadIntegerCore(name, kind);
            if (!IsInRange(readValue, kind))
            {
                throw CreateDataException(name, DescribeOutOfRange(readValue, kind));
            }

            value = T.CreateTruncating(readValue);
        }
        else
        {
            WriteIntegerCore(name, long.CreateTruncating(value), kind);
        }
    }

    private void IntegerArray<T>(string name, T[] values, SaveGameIntegerKind kind)
        where T : struct, IBinaryInteger<T>
    {
        ThrowIfInvalidName(name);
        ArgumentNullException.ThrowIfNull(values);

        if (IsLoading)
        {
            int length = ReadArrayStartCore(name, kind);
            if (length != values.Length)
            {
                throw CreateDataException(
                    name,
                    $"expected an array of {values.Length} elements, found {length.ToString(CultureInfo.InvariantCulture)}.");
            }

            _currentArrayName = name;
            for (int i = 0; i < values.Length; i++)
            {
                long readValue = ReadArrayElementCore(i, kind);
                if (!IsInRange(readValue, kind))
                {
                    throw CreateElementDataException(i, DescribeOutOfRange(readValue, kind));
                }

                values[i] = T.CreateTruncating(readValue);
            }
        }
        else
        {
            WriteArrayStartCore(name, values.Length, kind);

            _currentArrayName = name;
            for (int i = 0; i < values.Length; i++)
            {
                WriteArrayElementCore(i, long.CreateTruncating(values[i]), kind);
            }
        }

        EndArrayCore();
        _currentArrayName = null;
    }

    private string GetElementPath(int index)
    {
        return GetFieldPath(_currentArrayName) + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
    }

    private void ThrowIfNotFinite(string name, float value)
    {
        if (!float.IsFinite(value))
        {
            throw CreateDataException(
                name,
                $"the float value {value.ToString(CultureInfo.InvariantCulture)} is not finite; NaN and infinities are refused.");
        }
    }

    private void ThrowIfInvalidString(string name, string value)
    {
        if (value == null)
        {
            throw CreateDataException(name, "a null string is refused; use an empty string for no text.");
        }

        // Both formats encode UTF-8, which cannot represent an unpaired surrogate: refusing it here keeps the two
        // formats symmetric (strict UTF-8 would fail on write, a JSON "\uD800" escape would read back one).
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(c))
            {
                throw CreateDataException(
                    name,
                    $"the string has an unpaired UTF-16 surrogate at index {i.ToString(CultureInfo.InvariantCulture)}.");
            }
        }
    }

    private static void ThrowIfInvalidName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
    }

    private static bool IsInRange(long value, SaveGameIntegerKind kind)
    {
        GetRange(kind, out long minValue, out long maxValue);

        return value >= minValue && value <= maxValue;
    }

    private static string DescribeOutOfRange(long value, SaveGameIntegerKind kind)
    {
        GetRange(kind, out long minValue, out long maxValue);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{value} is outside the {GetTypeName(kind)} range [{minValue}, {maxValue}].");
    }

    private static void GetRange(SaveGameIntegerKind kind, out long minValue, out long maxValue)
    {
        switch (kind)
        {
            case SaveGameIntegerKind.Byte:
                minValue = byte.MinValue;
                maxValue = byte.MaxValue;
                break;
            case SaveGameIntegerKind.Int16:
                minValue = short.MinValue;
                maxValue = short.MaxValue;
                break;
            case SaveGameIntegerKind.UInt16:
                minValue = ushort.MinValue;
                maxValue = ushort.MaxValue;
                break;
            case SaveGameIntegerKind.Int32:
                minValue = int.MinValue;
                maxValue = int.MaxValue;
                break;
            case SaveGameIntegerKind.UInt32:
                minValue = uint.MinValue;
                maxValue = uint.MaxValue;
                break;
            case SaveGameIntegerKind.Int64:
                minValue = long.MinValue;
                maxValue = long.MaxValue;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown save-game integer kind.");
        }
    }

    private static string GetTypeName(SaveGameIntegerKind kind)
    {
        switch (kind)
        {
            case SaveGameIntegerKind.Byte:
                return "byte";
            case SaveGameIntegerKind.Int16:
                return "short";
            case SaveGameIntegerKind.UInt16:
                return "ushort";
            case SaveGameIntegerKind.Int32:
                return "int";
            case SaveGameIntegerKind.UInt32:
                return "uint";
            case SaveGameIntegerKind.Int64:
                return "long";
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown save-game integer kind.");
        }
    }
}
