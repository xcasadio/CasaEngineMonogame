using System.Buffers.Binary;
using CasaEngine.Framework.Configuration;

namespace CasaEngine.Framework.Video;

/// <summary>
/// A <c>.mdecvideo</c> container (ADR-0128), loaded by its header and its offset table only. The frames are not read
/// here: a player opens <see cref="FileName"/> and reads frame <c>i</c> between <see cref="GetFrameOffset"/> of
/// <c>i</c> and of <c>i + 1</c>. The optional audio section is validated as a byte range and nothing else.
/// <para/>
/// Layout (little-endian): magic <c>MDVC</c>, <c>u16</c> format version (1), <c>u16</c> flags (0), <c>u16</c> width,
/// <c>u16</c> height (1 to 4096), <c>u32</c> frame count (at least 1), <c>u32</c> frame rate numerator and denominator
/// (non-zero), <c>u32</c> audio offset and length ((0, 0) when absent), then frame count + 1 <c>u32</c> offsets from the
/// start of the file, strictly increasing. Every invalid field is reported by an <see cref="InvalidDataException"/> that
/// names the file and the field.
/// </summary>
public sealed class MdecVideoAsset
{
    public const int HeaderSize = 32;
    public const int MaxDimension = 4096;
    public const ushort FormatVersion = 1;

    private static ReadOnlySpan<byte> Magic => "MDVC"u8;

    private readonly uint[] _frameOffsets;

    private MdecVideoAsset(
        string fileName, int width, int height, int frameCount, uint rateNumerator, uint rateDenominator,
        uint audioOffset, uint audioLength, uint[] frameOffsets)
    {
        FileName = fileName;
        Width = width;
        Height = height;
        FrameCount = frameCount;
        FrameRateNumerator = rateNumerator;
        FrameRateDenominator = rateDenominator;
        AudioOffset = audioOffset;
        AudioLength = audioLength;
        _frameOffsets = frameOffsets;
    }

    /// <summary>Full path of the container file, as resolved by the asset content manager.</summary>
    public string FileName { get; }

    /// <summary>Displayed width in pixels.</summary>
    public int Width { get; }

    /// <summary>Displayed height in pixels.</summary>
    public int Height { get; }

    public int FrameCount { get; }

    /// <summary>Frames per second, numerator.</summary>
    public uint FrameRateNumerator { get; }

    /// <summary>Frames per second, denominator.</summary>
    public uint FrameRateDenominator { get; }

    /// <summary>Offset of the audio section from the start of the file, 0 when there is none.</summary>
    public uint AudioOffset { get; }

    /// <summary>Length of the audio section in bytes, 0 when there is none. Not played by this version.</summary>
    public uint AudioLength { get; }

    public bool HasAudio => AudioLength != 0;

    /// <summary>Offset from the start of the file of frame <paramref name="index"/> (0 to <see cref="FrameCount"/>; the last is the end of the last frame).</summary>
    public uint GetFrameOffset(int index) => _frameOffsets[index];

    /// <summary>Length in bytes of frame <paramref name="index"/>.</summary>
    public int GetFrameLength(int index) => (int)(_frameOffsets[index + 1] - _frameOffsets[index]);

    /// <summary>Length in bytes of the longest frame, the size of the read buffer a player needs.</summary>
    public int MaxFrameLength
    {
        get
        {
            var max = 0;
            for (var i = 0; i < FrameCount; i++)
            {
                max = Math.Max(max, GetFrameLength(i));
            }

            return max;
        }
    }

    /// <summary>Reads the header and the table of the container file <paramref name="fileName"/>.</summary>
    public static MdecVideoAsset Load(string fileName)
    {
        using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Read(stream, fileName);
    }

    /// <summary>Reads the header and the table from <paramref name="stream"/>, positioned at the start of the container. <paramref name="fileName"/> names it in errors.</summary>
    public static MdecVideoAsset Read(Stream stream, string fileName)
    {
        var fileLength = stream.Length;
        Span<byte> header = stackalloc byte[HeaderSize];
        if (stream.Read(header) < HeaderSize)
        {
            throw Error(fileName, "header", $"the file is {fileLength} bytes long, the header needs {HeaderSize}");
        }

        if (!header[..4].SequenceEqual(Magic))
        {
            throw Error(fileName, "magic", "expected 'MDVC'");
        }

        var version = BinaryPrimitives.ReadUInt16LittleEndian(header[4..]);
        if (version != FormatVersion)
        {
            throw Error(fileName, "version", $"expected {FormatVersion}, found {version}");
        }

        var flags = BinaryPrimitives.ReadUInt16LittleEndian(header[6..]);
        if (flags != 0)
        {
            throw Error(fileName, "flags", $"reserved, expected 0, found 0x{flags:X4}");
        }

        var width = BinaryPrimitives.ReadUInt16LittleEndian(header[8..]);
        if (width < 1 || width > MaxDimension)
        {
            throw Error(fileName, "width", $"expected 1 to {MaxDimension}, found {width}");
        }

        var height = BinaryPrimitives.ReadUInt16LittleEndian(header[10..]);
        if (height < 1 || height > MaxDimension)
        {
            throw Error(fileName, "height", $"expected 1 to {MaxDimension}, found {height}");
        }

        var frameCount = BinaryPrimitives.ReadUInt32LittleEndian(header[12..]);
        if (frameCount < 1)
        {
            throw Error(fileName, "frame count", "expected at least 1, found 0");
        }

        var rateNumerator = BinaryPrimitives.ReadUInt32LittleEndian(header[16..]);
        if (rateNumerator == 0)
        {
            throw Error(fileName, "frame rate numerator", "expected a non-zero value, found 0");
        }

        var rateDenominator = BinaryPrimitives.ReadUInt32LittleEndian(header[20..]);
        if (rateDenominator == 0)
        {
            throw Error(fileName, "frame rate denominator", "expected a non-zero value, found 0");
        }

        var audioOffset = BinaryPrimitives.ReadUInt32LittleEndian(header[24..]);
        var audioLength = BinaryPrimitives.ReadUInt32LittleEndian(header[28..]);
        if (audioLength == 0 && audioOffset != 0)
        {
            throw Error(fileName, "audio section", $"offset {audioOffset} with a length of 0 (no audio is (0, 0))");
        }

        if (audioLength != 0 && (long)audioOffset + audioLength > fileLength)
        {
            throw Error(
                fileName, "audio section",
                $"bytes {audioOffset} to {(long)audioOffset + audioLength} are outside the file ({fileLength} bytes)");
        }

        var tableBytes = ((long)frameCount + 1) * sizeof(uint);
        if (HeaderSize + tableBytes > fileLength)
        {
            throw Error(
                fileName, "offset table",
                $"{frameCount + 1} offsets need {tableBytes} bytes after the header, the file has {fileLength - HeaderSize}");
        }

        var tableData = new byte[tableBytes];
        stream.ReadExactly(tableData);
        var offsets = new uint[frameCount + 1];
        for (var i = 0; i < offsets.Length; i++)
        {
            offsets[i] = BinaryPrimitives.ReadUInt32LittleEndian(tableData.AsSpan(i * sizeof(uint)));
        }

        var tableEnd = HeaderSize + tableBytes;
        if (offsets[0] < tableEnd)
        {
            throw Error(fileName, "offset table", $"offset 0 is {offsets[0]}, inside the header and table (the frames start at {tableEnd} or later)");
        }

        for (var i = 1; i < offsets.Length; i++)
        {
            if (offsets[i] <= offsets[i - 1])
            {
                throw Error(
                    fileName, "offset table",
                    $"offset {i} ({offsets[i]}) is not greater than offset {i - 1} ({offsets[i - 1]})");
            }
        }

        var lastOffset = offsets[^1];
        if (lastOffset > fileLength)
        {
            throw Error(fileName, "offset table", $"the last offset {lastOffset} is outside the file ({fileLength} bytes): the last frame overflows");
        }

        if (audioLength != 0 && lastOffset > audioOffset)
        {
            throw Error(fileName, "audio section", $"it starts at {audioOffset}, before the end of the last frame ({lastOffset})");
        }

        return new MdecVideoAsset(
            fileName, width, height, (int)frameCount, rateNumerator, rateDenominator, audioOffset, audioLength, offsets);
    }

    private static InvalidDataException Error(string fileName, string field, string detail)
        => new($"MDEC video '{fileName}': invalid {field}: {detail}.");
}
