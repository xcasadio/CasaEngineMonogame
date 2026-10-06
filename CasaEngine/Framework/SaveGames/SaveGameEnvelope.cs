using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.SaveGames;

/// <summary>The two save-game file formats (ADR-0044).</summary>
internal enum SaveGameEnvelopeFormat
{
    /// <summary>Readable JSON document, fields found by name; no checksum (debugging format).</summary>
    Json,

    /// <summary>Compact positional little-endian payload with a final CRC-32 (game format).</summary>
    Binary,
}

internal enum SaveGameEnvelopeStatus
{
    Success,

    /// <summary>The binary file is truncated or fails its CRC-32: accidental corruption.</summary>
    Corrupted,

    /// <summary>The file is readable but its content is refused: unknown format, malformed document, bad value.</summary>
    InvalidData,

    /// <summary>The container version is zero, negative or newer than this engine reads.</summary>
    UnsupportedContainer,
}

/// <summary>
/// Outcome of a <see cref="SaveGameEnvelope"/> operation. <see cref="Message"/> gives the context (format, field,
/// cause) for every status but <see cref="SaveGameEnvelopeStatus.Success"/>, where it is empty; the caller adds the
/// slot and logs it.
/// </summary>
internal readonly struct SaveGameEnvelopeResult
{
    private SaveGameEnvelopeResult(SaveGameEnvelopeStatus status, string message)
    {
        Status = status;
        Message = message;
    }

    public SaveGameEnvelopeStatus Status { get; }
    public string Message { get; }
    public bool IsSuccess => Status == SaveGameEnvelopeStatus.Success;

    public static SaveGameEnvelopeResult Success { get; } = new(SaveGameEnvelopeStatus.Success, string.Empty);

    public static SaveGameEnvelopeResult Corrupted(string message) => new(SaveGameEnvelopeStatus.Corrupted, message);
    public static SaveGameEnvelopeResult InvalidData(string message) => new(SaveGameEnvelopeStatus.InvalidData, message);
    public static SaveGameEnvelopeResult UnsupportedContainer(string message) => new(SaveGameEnvelopeStatus.UnsupportedContainer, message);
}

/// <summary>
/// A save-game file (ADR-0044): the container around the data a game's <see cref="ISaveGameData.Serialize"/>
/// writes. Both formats carry a container version, the data version, string metadata and the data.
/// </summary>
/// <remarks>
/// <para>
/// JSON: <c>{ "container": "casaengine-savegame", "containerVersion": 1, "dataVersion": n, "metadata": { ... },
/// "data": { ... } }</c>, UTF-8 without BOM, indented with '\n' line ends. Binary: see
/// <see cref="BinarySaveGameArchive"/>. Both outputs are deterministic: the same object and metadata always give
/// the same bytes (metadata is written in ordinal key order).
/// </para>
/// <para>
/// Reading never trusts the file (it may come from another player or be edited by hand). <see cref="TryOpen"/>
/// detects the format, checks the container and reads the data version and the metadata without decoding the
/// data: that is also the metadata-only read of a slot listing. <see cref="TryDeserialize"/> then decodes the data
/// into the game's object. Neither throws for a hostile file: every refusal is a <see cref="SaveGameEnvelopeResult"/>.
/// </para>
/// <para>
/// Exception boundary: <see cref="TryOpen"/> runs no game code and turns every parser exception a hostile file can
/// raise into a result. <see cref="TryWrite"/> and <see cref="TryDeserialize"/> run the game's
/// <see cref="ISaveGameData.Serialize"/>: they turn only <see cref="SaveGameDataException"/> into a result, and any
/// other exception (a game bug, a developer misuse of the archive) propagates to the caller.
/// </para>
/// </remarks>
internal sealed class SaveGameEnvelope
{
    /// <summary>The only container version this engine writes and reads.</summary>
    public const int CurrentContainerVersion = 1;

    /// <summary>The JSON <c>container</c> field value.</summary>
    public const string JsonContainerName = "casaengine-savegame";

    /// <summary>Strict UTF-8 without BOM: invalid bytes (reading) or unpaired surrogates (writing) throw.</summary>
    internal static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private static readonly byte[] Utf8ByteOrderMark = { 0xEF, 0xBB, 0xBF };

    private readonly JObject _jsonData;
    private readonly byte[] _binaryBytes;
    private readonly int _binaryPayloadStart;
    private readonly int _binaryPayloadEnd;

    private SaveGameEnvelope(
        SaveGameEnvelopeFormat format,
        int dataVersion,
        IReadOnlyDictionary<string, string> metadata,
        JObject jsonData,
        byte[] binaryBytes,
        int binaryPayloadStart,
        int binaryPayloadEnd)
    {
        Format = format;
        DataVersion = dataVersion;
        Metadata = metadata;
        _jsonData = jsonData;
        _binaryBytes = binaryBytes;
        _binaryPayloadStart = binaryPayloadStart;
        _binaryPayloadEnd = binaryPayloadEnd;
    }

    public SaveGameEnvelopeFormat Format { get; }

    /// <summary>The data version the file was written with, zero or greater.</summary>
    public int DataVersion { get; }

    /// <summary>
    /// The file's metadata. Untrusted plain text: well-formed strings of any length the file holds, unique keys
    /// (ordinal comparison).
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>
    /// Serializes <paramref name="data"/> and its <paramref name="metadata"/> into a file image.
    /// <see cref="SaveGameEnvelopeStatus.InvalidData"/> when the object or the metadata writes a refused value
    /// (non-finite float, null or malformed string); <paramref name="fileBytes"/> is then null.
    /// </summary>
    /// <param name="metadata">Optional; null writes no metadata. A null value or an unpaired surrogate is refused.</param>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="format"/> is unknown, or <see cref="ISaveGameData.LatestDataVersion"/> is negative.
    /// </exception>
    /// <remarks>
    /// Developer misuse propagates: a field name written twice in one object in JSON
    /// (<see cref="ArgumentException"/>), unbalanced objects (<see cref="InvalidOperationException"/>), and any
    /// exception the game's <see cref="ISaveGameData.Serialize"/> throws on its own.
    /// </remarks>
    public static SaveGameEnvelopeResult TryWrite(
        SaveGameEnvelopeFormat format,
        ISaveGameData data,
        IReadOnlyDictionary<string, string> metadata,
        out byte[] fileBytes)
    {
        ArgumentNullException.ThrowIfNull(data);
        fileBytes = null;

        try
        {
            List<KeyValuePair<string, string>> sortedMetadata = ValidateAndSortMetadata(metadata);

            switch (format)
            {
                case SaveGameEnvelopeFormat.Json:
                    fileBytes = JsonSaveGameArchive.WriteFile(data, sortedMetadata);
                    break;
                case SaveGameEnvelopeFormat.Binary:
                    fileBytes = BinarySaveGameArchive.WriteFile(data, sortedMetadata);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown save-game format.");
            }

            return SaveGameEnvelopeResult.Success;
        }
        catch (SaveGameDataException exception)
        {
            return SaveGameEnvelopeResult.InvalidData($"Save-game {format} write refused: {exception.Message}");
        }
    }

    /// <summary>
    /// Detects the format of <paramref name="fileBytes"/>, checks its container and reads its data version and
    /// metadata, without decoding the data. Never throws for a hostile file. <paramref name="envelope"/> is null
    /// unless the result is <see cref="SaveGameEnvelopeStatus.Success"/>.
    /// </summary>
    /// <remarks>
    /// Detection: a binary file starts with its 4-byte magic at offset 0; a JSON file starts with '{' after an
    /// optional UTF-8 BOM and whitespace. A non-empty file shorter than the magic that matches its start is a
    /// truncated binary file (<see cref="SaveGameEnvelopeStatus.Corrupted"/>); anything else, including an empty
    /// file, is <see cref="SaveGameEnvelopeStatus.InvalidData"/>. The JSON document is parsed whole, so a listing
    /// refuses exactly the documents a load refuses at this stage (syntax, duplicate names, depth).
    /// </remarks>
    public static SaveGameEnvelopeResult TryOpen(byte[] fileBytes, out SaveGameEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(fileBytes);
        envelope = null;

        string formatName = "file";
        try
        {
            if (BinarySaveGameArchive.StartsWithMagic(fileBytes))
            {
                formatName = "binary";
                SaveGameEnvelopeResult result = BinarySaveGameArchive.TryOpenFile(
                    fileBytes, out int dataVersion, out IReadOnlyDictionary<string, string> metadata, out int payloadStart, out int payloadEnd);
                if (result.IsSuccess)
                {
                    envelope = new SaveGameEnvelope(SaveGameEnvelopeFormat.Binary, dataVersion, metadata, null, fileBytes, payloadStart, payloadEnd);
                }

                return result;
            }

            if (BinarySaveGameArchive.IsTruncatedMagic(fileBytes))
            {
                return SaveGameEnvelopeResult.Corrupted(
                    $"Save-game binary file truncated: {fileBytes.Length} bytes, shorter than its magic.");
            }

            int jsonStart = StartsWith(fileBytes, Utf8ByteOrderMark) ? Utf8ByteOrderMark.Length : 0;
            if (IsJsonDocumentStart(fileBytes, jsonStart))
            {
                formatName = "JSON";
                SaveGameEnvelopeResult result = JsonSaveGameArchive.TryOpenFile(
                    fileBytes, jsonStart, out int dataVersion, out IReadOnlyDictionary<string, string> metadata, out JObject data);
                if (result.IsSuccess)
                {
                    envelope = new SaveGameEnvelope(SaveGameEnvelopeFormat.Json, dataVersion, metadata, data, null, 0, 0);
                }

                return result;
            }

            return SaveGameEnvelopeResult.InvalidData(fileBytes.Length == 0
                ? "Save-game file is empty."
                : "Save-game file is neither binary (magic) nor JSON ('{').");
        }
        catch (Exception exception) when (IsHostileInputException(exception))
        {
            envelope = null;
            return SaveGameEnvelopeResult.InvalidData($"Save-game {formatName} file could not be parsed: {exception.Message}");
        }
    }

    /// <summary>
    /// Decodes the data into <paramref name="target"/> through its <see cref="ISaveGameData.Serialize"/>, with
    /// <see cref="SaveGameArchive.DataVersion"/> set to <see cref="DataVersion"/>. A refused value, a missing field,
    /// or binary data left unread after the last field is <see cref="SaveGameEnvelopeStatus.InvalidData"/> naming the
    /// field; <paramref name="target"/> may then be partly overwritten and must be discarded. Any other exception
    /// (thrown by the game's <see cref="ISaveGameData.Serialize"/>, or a developer misuse) propagates.
    /// </summary>
    /// <remarks>
    /// The caller refuses a <see cref="DataVersion"/> newer than <see cref="ISaveGameData.LatestDataVersion"/>
    /// before calling this. JSON fields the object does not read are ignored; the binary payload must be read to
    /// its last byte, since leftover bytes mean the calls do not match the file's layout.
    /// </remarks>
    public SaveGameEnvelopeResult TryDeserialize(ISaveGameData target)
    {
        ArgumentNullException.ThrowIfNull(target);

        try
        {
            if (Format == SaveGameEnvelopeFormat.Json)
            {
                var archive = JsonSaveGameArchive.ForReading(_jsonData, DataVersion);
                target.Serialize(archive);
                archive.ThrowIfObjectOpen();
            }
            else
            {
                var archive = BinarySaveGameArchive.ForReading(_binaryBytes, _binaryPayloadStart, _binaryPayloadEnd, DataVersion);
                target.Serialize(archive);
                archive.ThrowIfObjectOpen();
                archive.ThrowIfDataLeft();
            }

            return SaveGameEnvelopeResult.Success;
        }
        catch (SaveGameDataException exception)
        {
            return SaveGameEnvelopeResult.InvalidData($"Save-game {Format} data refused: {exception.Message}");
        }
    }

    /// <summary>
    /// The exceptions a parser can raise on a hostile file: JSON syntax, depth and duplicate-name errors, invalid
    /// UTF-8, truncated or out-of-range reads, and the format's own data errors. Anything else is an engine bug and
    /// propagates.
    /// </summary>
    internal static bool IsHostileInputException(Exception exception)
    {
        return exception is SaveGameDataException
            or JsonException
            or DecoderFallbackException
            or EndOfStreamException
            or InvalidDataException
            or OverflowException
            or InvalidCastException
            or FormatException;
    }

    /// <summary>
    /// True when <paramref name="value"/> is well-formed UTF-16 (no unpaired surrogate), so strict UTF-8 encodes it.
    /// </summary>
    internal static bool IsWellFormedUtf16(string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(c))
            {
                return false;
            }
        }

        return true;
    }

    private static List<KeyValuePair<string, string>> ValidateAndSortMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        var sorted = new List<KeyValuePair<string, string>>();
        if (metadata == null)
        {
            return sorted;
        }

        foreach (KeyValuePair<string, string> pair in metadata)
        {
            if (pair.Key == null || !IsWellFormedUtf16(pair.Key))
            {
                throw new SaveGameDataException("metadata", "a metadata key is null or has an unpaired UTF-16 surrogate.");
            }

            if (pair.Value == null)
            {
                throw new SaveGameDataException("metadata." + pair.Key, "a null metadata value is refused; use an empty string.");
            }

            if (!IsWellFormedUtf16(pair.Value))
            {
                throw new SaveGameDataException("metadata." + pair.Key, "the metadata value has an unpaired UTF-16 surrogate.");
            }

            sorted.Add(pair);
        }

        // Ordinal order makes the output independent of the dictionary's enumeration order. Keys are unique within
        // one dictionary unless its comparer is not ordinal; the formats refuse a duplicate on read, so refuse it here.
        sorted.Sort(static (left, right) => string.CompareOrdinal(left.Key, right.Key));
        for (int i = 1; i < sorted.Count; i++)
        {
            if (string.Equals(sorted[i - 1].Key, sorted[i].Key, StringComparison.Ordinal))
            {
                throw new SaveGameDataException("metadata." + sorted[i].Key, "the metadata key appears twice.");
            }
        }

        return sorted;
    }

    private static bool StartsWith(byte[] bytes, byte[] prefix)
    {
        return bytes.Length >= prefix.Length && bytes.AsSpan(0, prefix.Length).SequenceEqual(prefix);
    }

    private static bool IsJsonDocumentStart(byte[] bytes, int start)
    {
        for (int i = start; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            if (b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n')
            {
                continue;
            }

            return b == (byte)'{';
        }

        return false;
    }
}
