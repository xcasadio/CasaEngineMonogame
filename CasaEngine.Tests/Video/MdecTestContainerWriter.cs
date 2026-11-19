using System.Buffers.Binary;

namespace CasaEngine.Tests.Video;

/// <summary>
/// Writes <c>.mdecvideo</c> containers for tests, from the layout of ADR-0128 (plan choice C1): a 32-byte header, a table of
/// frame count + 1 offsets, the frames, then the optional audio section. Frames are the byte arrays given, normally built
/// by <see cref="MdecTestEncoder"/>.
/// </summary>
internal static class MdecTestContainerWriter
{
    // Byte offsets of the header fields.
    public const int MagicAt = 0;
    public const int VersionAt = 4;
    public const int FlagsAt = 6;
    public const int WidthAt = 8;
    public const int HeightAt = 10;
    public const int FrameCountAt = 12;
    public const int RateNumeratorAt = 16;
    public const int RateDenominatorAt = 20;
    public const int AudioOffsetAt = 24;
    public const int AudioLengthAt = 28;
    public const int HeaderSize = 32;

    /// <summary>A valid container. <paramref name="audio"/> is placed after the last frame; null for none.</summary>
    public static byte[] Write(
        IReadOnlyList<byte[]> frames, int width, int height, uint rateNumerator = 30, uint rateDenominator = 1,
        byte[]? audio = null)
    {
        var tableEnd = HeaderSize + (frames.Count + 1) * 4;
        var framesLength = 0;
        foreach (var frame in frames)
        {
            framesLength += frame.Length;
        }

        var audioLength = audio?.Length ?? 0;
        var file = new byte[tableEnd + framesLength + audioLength];

        "MDVC"u8.CopyTo(file);
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(VersionAt), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(FlagsAt), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(WidthAt), (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(HeightAt), (ushort)height);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(FrameCountAt), (uint)frames.Count);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(RateNumeratorAt), rateNumerator);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(RateDenominatorAt), rateDenominator);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(AudioOffsetAt), audio == null ? 0u : (uint)(tableEnd + framesLength));
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(AudioLengthAt), (uint)audioLength);

        var position = tableEnd;
        for (var i = 0; i < frames.Count; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(HeaderSize + i * 4), (uint)position);
            frames[i].CopyTo(file, position);
            position += frames[i].Length;
        }

        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(HeaderSize + frames.Count * 4), (uint)position);
        audio?.CopyTo(file, position);
        return file;
    }

    public static void SetU16(byte[] file, int at, int value)
        => BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(at), (ushort)value);

    public static void SetU32(byte[] file, int at, uint value)
        => BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(at), value);

    /// <summary>Position of the table entry <paramref name="index"/>.</summary>
    public static int TableEntryAt(int index) => HeaderSize + index * 4;
}
