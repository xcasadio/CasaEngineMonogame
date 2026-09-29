using System.Buffers.Binary;
using System.Globalization;

namespace CasaEngine.Framework.SaveGames;

/// <summary>
/// The binary save-game format (ADR-0044): compact, positional, little-endian, with a final CRC-32.
/// </summary>
/// <remarks>
/// <para>
/// Layout: magic <c>"CESG"</c> (4 bytes); container version (int32); data version (int32); metadata count (int32),
/// then that many key/value pairs, each string an int32 byte length followed by strict UTF-8; the payload; the
/// CRC-32 (uint32) of every byte before it. Payload, in call order, names ignored: a boolean as one byte (0 or 1),
/// an integer at its type's width (1, 2, 4 or 8 bytes), a float as its 4 IEEE 754 bytes, a string as an int32 byte
/// length and strict UTF-8, an integer array as an int32 element count and the elements at their type's width;
/// <see cref="SaveGameArchive.BeginObject"/> and <see cref="SaveGameArchive.EndObject"/> write nothing.
/// </para>
/// <para>
/// Reading order, every step before the next: the magic; the minimum size (magic, versions, metadata count, CRC),
/// otherwise <see cref="SaveGameEnvelopeStatus.Corrupted"/>; the container version, compared to
/// <see cref="SaveGameEnvelope.CurrentContainerVersion"/> only, otherwise
/// <see cref="SaveGameEnvelopeStatus.UnsupportedContainer"/> (read before the CRC because a later container may place
/// its checksum elsewhere; it sizes nothing); the CRC-32, otherwise <see cref="SaveGameEnvelopeStatus.Corrupted"/>;
/// then, the bytes being intact but still untrusted, the data version (negative refused), the metadata count (negative
/// or more than the remaining bytes can hold refused), each length (compared as <c>length &gt; remaining</c> before
/// anything is allocated or decoded), strict UTF-8, and duplicate metadata keys. The payload is decoded only by a
/// loading archive, with the same length checks, and must be read to its last byte.
/// </para>
/// <para>
/// Field names are not stored, so a name written twice in one object cannot be detected by this format (the JSON
/// format refuses it). Never <c>BinaryReader</c>: every read is bounds-checked here.
/// </para>
/// </remarks>
internal sealed class BinarySaveGameArchive : SaveGameArchive
{
    /// <summary>Magic <c>"CESG"</c> (CasaEngine save game); never starts with '{', whitespace or a BOM.</summary>
    internal static readonly byte[] Magic = { 0x43, 0x45, 0x53, 0x47 };

    internal const int ContainerVersionOffset = 4;
    internal const int DataVersionOffset = 8;
    internal const int MetadataCountOffset = 12;
    internal const int HeaderLength = 16;
    internal const int CrcLength = 4;

    /// <summary>Magic, container version, data version, metadata count and CRC: the smallest valid file.</summary>
    internal const int MinimumFileLength = HeaderLength + CrcLength;

    /// <summary>Smallest metadata pair: two empty strings, i.e. two int32 length prefixes.</summary>
    internal const int MinimumMetadataPairLength = 8;

    private const int LengthPrefixSize = 4;

    private readonly MemoryStream _output;
    private readonly byte[] _input;
    private readonly int _end;
    private int _position;
    private int _arrayWidth;

    private BinarySaveGameArchive(int dataVersion, MemoryStream output)
        : base(false, dataVersion)
    {
        _output = output;
    }

    private BinarySaveGameArchive(int dataVersion, byte[] input, int start, int end)
        : base(true, dataVersion)
    {
        _input = input;
        _position = start;
        _end = end;
    }

    /// <summary>A loading archive over the payload <c>[start, end)</c> of <paramref name="fileBytes"/>.</summary>
    internal static BinarySaveGameArchive ForReading(byte[] fileBytes, int start, int end, int dataVersion)
    {
        ArgumentNullException.ThrowIfNull(fileBytes);
        if (start < 0 || end < start || end > fileBytes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "The payload range lies outside the file.");
        }

        return new BinarySaveGameArchive(dataVersion, fileBytes, start, end);
    }

    internal static bool StartsWithMagic(byte[] fileBytes)
    {
        return fileBytes.Length >= Magic.Length && fileBytes.AsSpan(0, Magic.Length).SequenceEqual(Magic);
    }

    /// <summary>A non-empty file shorter than the magic whose bytes are the magic's start: a truncated binary file.</summary>
    internal static bool IsTruncatedMagic(byte[] fileBytes)
    {
        return fileBytes.Length > 0
            && fileBytes.Length < Magic.Length
            && fileBytes.AsSpan().SequenceEqual(Magic.AsSpan(0, fileBytes.Length));
    }

    /// <summary>
    /// Writes the whole binary file: header, <paramref name="sortedMetadata"/> in the given order, the payload
    /// <paramref name="data"/> serializes, then the CRC-32.
    /// </summary>
    internal static byte[] WriteFile(ISaveGameData data, List<KeyValuePair<string, string>> sortedMetadata)
    {
        using var output = new MemoryStream();
        var archive = new BinarySaveGameArchive(data.LatestDataVersion, output);

        output.Write(Magic);
        archive.WriteInt32(SaveGameEnvelope.CurrentContainerVersion);
        archive.WriteInt32(archive.DataVersion);
        archive.WriteInt32(sortedMetadata.Count);
        foreach (KeyValuePair<string, string> pair in sortedMetadata)
        {
            archive.WriteLengthPrefixedString(pair.Key);
            archive.WriteLengthPrefixedString(pair.Value);
        }

        data.Serialize(archive);
        archive.ThrowIfObjectOpen();

        uint crc = Crc32.Compute(output.GetBuffer().AsSpan(0, checked((int)output.Length)));
        Span<byte> crcBytes = stackalloc byte[CrcLength];
        BinaryPrimitives.WriteUInt32LittleEndian(crcBytes, crc);
        output.Write(crcBytes);

        return output.ToArray();
    }

    /// <summary>
    /// Checks the binary envelope of <paramref name="fileBytes"/> (which starts with the magic) and reads its data
    /// version and metadata; the payload is <c>[payloadStart, payloadEnd)</c>, not decoded.
    /// </summary>
    internal static SaveGameEnvelopeResult TryOpenFile(
        byte[] fileBytes,
        out int dataVersion,
        out IReadOnlyDictionary<string, string> metadata,
        out int payloadStart,
        out int payloadEnd)
    {
        dataVersion = 0;
        metadata = null;
        payloadStart = 0;
        payloadEnd = 0;

        if (fileBytes.Length < MinimumFileLength)
        {
            return SaveGameEnvelopeResult.Corrupted(Invariant(
                $"Save-game binary file truncated: {fileBytes.Length} bytes, the minimum is {MinimumFileLength}."));
        }

        int containerVersion = BinaryPrimitives.ReadInt32LittleEndian(fileBytes.AsSpan(ContainerVersionOffset));
        if (containerVersion != SaveGameEnvelope.CurrentContainerVersion)
        {
            return SaveGameEnvelopeResult.UnsupportedContainer(Invariant(
                $"Save-game binary container version {containerVersion} is not supported; this engine reads version {SaveGameEnvelope.CurrentContainerVersion}."));
        }

        int end = fileBytes.Length - CrcLength;
        uint storedCrc = BinaryPrimitives.ReadUInt32LittleEndian(fileBytes.AsSpan(end));
        uint computedCrc = Crc32.Compute(fileBytes.AsSpan(0, end));
        if (storedCrc != computedCrc)
        {
            return SaveGameEnvelopeResult.Corrupted(Invariant(
                $"Save-game binary file corrupted: CRC-32 0x{computedCrc:X8} does not match the stored 0x{storedCrc:X8}."));
        }

        // From here the bytes are intact but still untrusted: a CRC-32 is trivial to recompute.
        int version = BinaryPrimitives.ReadInt32LittleEndian(fileBytes.AsSpan(DataVersionOffset));
        if (version < 0)
        {
            return SaveGameEnvelopeResult.InvalidData(Invariant($"Save-game binary data version {version} is negative."));
        }

        int metadataCount = BinaryPrimitives.ReadInt32LittleEndian(fileBytes.AsSpan(MetadataCountOffset));
        int position = HeaderLength;
        if (metadataCount < 0 || metadataCount > (end - position) / MinimumMetadataPairLength)
        {
            return SaveGameEnvelopeResult.InvalidData(Invariant(
                $"Save-game binary metadata count {metadataCount} is negative or exceeds what the {end - position} remaining bytes can hold."));
        }

        var metadataValues = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < metadataCount; i++)
        {
            if (!TryReadMetadataString(fileBytes, ref position, end, out string key, out string error)
                || !TryReadMetadataString(fileBytes, ref position, end, out string value, out error))
            {
                return SaveGameEnvelopeResult.InvalidData(Invariant($"Save-game binary metadata pair {i}: {error}"));
            }

            if (!metadataValues.TryAdd(key, value))
            {
                return SaveGameEnvelopeResult.InvalidData(Invariant($"Save-game binary metadata pair {i}: the key appears twice."));
            }
        }

        dataVersion = version;
        metadata = metadataValues;
        payloadStart = position;
        payloadEnd = end;
        return SaveGameEnvelopeResult.Success;
    }

    /// <summary>
    /// Called after a load: bytes left in the payload mean the object's calls do not match the file's layout.
    /// </summary>
    internal void ThrowIfDataLeft()
    {
        if (_position != _end)
        {
            throw new SaveGameDataException(
                "data",
                Invariant($"{_end - _position} bytes are left after the last field; the fields do not match the file's layout."));
        }
    }

    private protected override bool ReadBooleanCore(string name)
    {
        ReadOnlySpan<byte> bytes = TakeField(name, 1);
        switch (bytes[0])
        {
            case 0:
                return false;
            case 1:
                return true;
            default:
                throw CreateDataException(name, Invariant($"the boolean byte {bytes[0]} is neither 0 nor 1."));
        }
    }

    private protected override void WriteBooleanCore(string name, bool value)
    {
        _output.WriteByte(value ? (byte)1 : (byte)0);
    }

    private protected override long ReadIntegerCore(string name, SaveGameIntegerKind kind)
    {
        return DecodeInteger(TakeField(name, GetWidth(kind)), kind);
    }

    private protected override void WriteIntegerCore(string name, long value, SaveGameIntegerKind kind)
    {
        WriteInteger(value, kind);
    }

    private protected override float ReadSingleCore(string name)
    {
        return BinaryPrimitives.ReadSingleLittleEndian(TakeField(name, sizeof(float)));
    }

    private protected override void WriteSingleCore(string name, float value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(float)];
        BinaryPrimitives.WriteSingleLittleEndian(bytes, value);
        _output.Write(bytes);
    }

    private protected override string ReadStringCore(string name)
    {
        int length = BinaryPrimitives.ReadInt32LittleEndian(TakeField(name, LengthPrefixSize));
        if (length < 0)
        {
            throw CreateDataException(name, Invariant($"the string length {length} is negative."));
        }

        if (length > _end - _position)
        {
            throw CreateDataException(name, Invariant(
                $"the string length {length} exceeds the {_end - _position} bytes left."));
        }

        string value;
        try
        {
            value = SaveGameEnvelope.StrictUtf8.GetString(_input, _position, length);
        }
        catch (System.Text.DecoderFallbackException)
        {
            throw CreateDataException(name, "the string is not valid UTF-8.");
        }

        _position += length;
        return value;
    }

    private protected override void WriteStringCore(string name, string value)
    {
        WriteLengthPrefixedString(value);
    }

    private protected override int ReadArrayStartCore(string name, SaveGameIntegerKind kind)
    {
        int count = BinaryPrimitives.ReadInt32LittleEndian(TakeField(name, LengthPrefixSize));
        int width = GetWidth(kind);
        if (count < 0 || count > (_end - _position) / width)
        {
            throw CreateDataException(name, Invariant(
                $"the array count {count} is negative or exceeds what the {_end - _position} bytes left can hold."));
        }

        _arrayWidth = width;
        return count;
    }

    private protected override void WriteArrayStartCore(string name, int length, SaveGameIntegerKind kind)
    {
        WriteInt32(length);
    }

    private protected override long ReadArrayElementCore(int index, SaveGameIntegerKind kind)
    {
        if (_arrayWidth > _end - _position)
        {
            throw CreateElementDataException(index, "the file ends before this element.");
        }

        ReadOnlySpan<byte> bytes = _input.AsSpan(_position, _arrayWidth);
        _position += _arrayWidth;
        return DecodeInteger(bytes, kind);
    }

    private protected override void WriteArrayElementCore(int index, long value, SaveGameIntegerKind kind)
    {
        WriteInteger(value, kind);
    }

    private protected override void EndArrayCore()
    {
        _arrayWidth = 0;
    }

    private protected override void BeginObjectCore(string name)
    {
    }

    private protected override void EndObjectCore()
    {
    }

    private static bool TryReadMetadataString(byte[] fileBytes, ref int position, int end, out string value, out string error)
    {
        value = null;
        if (LengthPrefixSize > end - position)
        {
            error = "the file ends before a string length.";
            return false;
        }

        int length = BinaryPrimitives.ReadInt32LittleEndian(fileBytes.AsSpan(position));
        position += LengthPrefixSize;
        if (length < 0)
        {
            error = Invariant($"the string length {length} is negative.");
            return false;
        }

        if (length > end - position)
        {
            error = Invariant($"the string length {length} exceeds the {end - position} bytes left.");
            return false;
        }

        try
        {
            value = SaveGameEnvelope.StrictUtf8.GetString(fileBytes, position, length);
        }
        catch (System.Text.DecoderFallbackException)
        {
            error = "a string is not valid UTF-8.";
            return false;
        }

        position += length;
        error = null;
        return true;
    }

    private static int GetWidth(SaveGameIntegerKind kind)
    {
        switch (kind)
        {
            case SaveGameIntegerKind.Byte:
                return 1;
            case SaveGameIntegerKind.Int16:
            case SaveGameIntegerKind.UInt16:
                return 2;
            case SaveGameIntegerKind.Int32:
            case SaveGameIntegerKind.UInt32:
                return 4;
            case SaveGameIntegerKind.Int64:
                return 8;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown save-game integer kind.");
        }
    }

    private static long DecodeInteger(ReadOnlySpan<byte> bytes, SaveGameIntegerKind kind)
    {
        switch (kind)
        {
            case SaveGameIntegerKind.Byte:
                return bytes[0];
            case SaveGameIntegerKind.Int16:
                return BinaryPrimitives.ReadInt16LittleEndian(bytes);
            case SaveGameIntegerKind.UInt16:
                return BinaryPrimitives.ReadUInt16LittleEndian(bytes);
            case SaveGameIntegerKind.Int32:
                return BinaryPrimitives.ReadInt32LittleEndian(bytes);
            case SaveGameIntegerKind.UInt32:
                return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
            case SaveGameIntegerKind.Int64:
                return BinaryPrimitives.ReadInt64LittleEndian(bytes);
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown save-game integer kind.");
        }
    }

    private static string Invariant(FormattableString text)
    {
        return text.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The next <paramref name="count"/> payload bytes of field <paramref name="name"/>, or a data error when the
    /// payload ends first.
    /// </summary>
    private ReadOnlySpan<byte> TakeField(string name, int count)
    {
        if (count > _end - _position)
        {
            throw CreateDataException(name, Invariant(
                $"the file ends before this field ({count} bytes needed, {_end - _position} left)."));
        }

        ReadOnlySpan<byte> bytes = _input.AsSpan(_position, count);
        _position += count;
        return bytes;
    }

    private void WriteInteger(long value, SaveGameIntegerKind kind)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        int width = GetWidth(kind);
        switch (kind)
        {
            case SaveGameIntegerKind.Byte:
                bytes[0] = (byte)value;
                break;
            case SaveGameIntegerKind.Int16:
                BinaryPrimitives.WriteInt16LittleEndian(bytes, (short)value);
                break;
            case SaveGameIntegerKind.UInt16:
                BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)value);
                break;
            case SaveGameIntegerKind.Int32:
                BinaryPrimitives.WriteInt32LittleEndian(bytes, (int)value);
                break;
            case SaveGameIntegerKind.UInt32:
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)value);
                break;
            case SaveGameIntegerKind.Int64:
                BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown save-game integer kind.");
        }

        _output.Write(bytes.Slice(0, width));
    }

    private void WriteInt32(int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        _output.Write(bytes);
    }

    private void WriteLengthPrefixedString(string value)
    {
        byte[] bytes = SaveGameEnvelope.StrictUtf8.GetBytes(value);
        WriteInt32(bytes.Length);
        _output.Write(bytes, 0, bytes.Length);
    }
}
