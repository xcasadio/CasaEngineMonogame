using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using CasaEngine.Framework.SaveGames;
using Xunit;

namespace CasaEngine.Tests.SaveGames;

/// <summary>A save object whose <see cref="ISaveGameData.Serialize"/> is a delegate, for single-field tests.</summary>
internal sealed class DelegateSaveGameData : ISaveGameData
{
    private readonly Action<SaveGameArchive> _serialize;

    public DelegateSaveGameData(Action<SaveGameArchive> serialize, int latestDataVersion = 1)
    {
        _serialize = serialize;
        LatestDataVersion = latestDataVersion;
    }

    public int LatestDataVersion { get; }

    public void Serialize(SaveGameArchive archive)
    {
        _serialize(archive);
    }
}

/// <summary>
/// A save object covering every supported value, nested objects, every array element type and edge values
/// (extreme integers, extreme and negative-zero floats, a non-ASCII string, an ISO date string, empty values).
/// </summary>
internal sealed class AllValuesSaveGameData : ISaveGameData
{
    public bool Flag;
    public bool OtherFlag;
    public byte Byte;
    public short Short;
    public ushort UShort;
    public int Int;
    public uint UInt;
    public long Long;
    public float Float;
    public float FloatMax;
    public float FloatTiny;
    public float FloatTenth;
    public float FloatNegativeZero;
    public string Text = string.Empty;
    public string Date = string.Empty;
    public string EmptyText = string.Empty;
    public int NestedHp;
    public string NestedName = string.Empty;
    public byte[] Bytes = new byte[3];
    public short[] Shorts = new short[2];
    public ushort[] UShorts = new ushort[2];
    public int[] Ints = new int[2];
    public uint[] UInts = new uint[2];
    public long[] Longs = new long[2];
    public int[] Empty = Array.Empty<int>();

    public int LatestDataVersion => 7;

    public static AllValuesSaveGameData CreateFilled()
    {
        return new AllValuesSaveGameData
        {
            Flag = true,
            OtherFlag = false,
            Byte = 200,
            Short = -12345,
            UShort = 54321,
            Int = -123456789,
            UInt = 4000000000,
            Long = -9000000000000000000,
            Float = -3.25e-12f,
            FloatMax = float.MaxValue,
            FloatTiny = float.Epsilon,
            FloatTenth = 0.1f,
            FloatNegativeZero = -0.0f,
            Text = "Inoa \U0001F600 \u00E9t\u00E9 \"quoted\" \\ back\nline",
            Date = "2026-09-28T12:00:00+02:00",
            NestedHp = 42,
            NestedName = "Meia",
            Bytes = new byte[] { 0, 128, 255 },
            Shorts = new short[] { short.MinValue, short.MaxValue },
            UShorts = new ushort[] { 0, ushort.MaxValue },
            Ints = new[] { int.MinValue, int.MaxValue },
            UInts = new[] { 0u, uint.MaxValue },
            Longs = new[] { long.MinValue, long.MaxValue },
        };
    }

    public void Serialize(SaveGameArchive archive)
    {
        archive.Value("flag", ref Flag);
        archive.Value("otherFlag", ref OtherFlag);
        archive.Value("byte", ref Byte);
        archive.Value("short", ref Short);
        archive.Value("ushort", ref UShort);
        archive.Value("int", ref Int);
        archive.Value("uint", ref UInt);
        archive.Value("long", ref Long);
        archive.Value("float", ref Float);
        archive.Value("floatMax", ref FloatMax);
        archive.Value("floatTiny", ref FloatTiny);
        archive.Value("floatTenth", ref FloatTenth);
        archive.Value("floatNegativeZero", ref FloatNegativeZero);
        archive.Value("text", ref Text);
        archive.Value("date", ref Date);
        archive.Value("emptyText", ref EmptyText);
        archive.BeginObject("player");
        archive.BeginObject("stats");
        archive.Value("hp", ref NestedHp);
        archive.EndObject();
        archive.Value("name", ref NestedName);
        archive.Value("bytes", Bytes);
        archive.EndObject();
        archive.Value("shorts", Shorts);
        archive.Value("ushorts", UShorts);
        archive.Value("ints", Ints);
        archive.Value("uints", UInts);
        archive.Value("longs", Longs);
        archive.Value("empty", Empty);
    }

    public void AssertEqual(AllValuesSaveGameData other)
    {
        Assert.Equal(Flag, other.Flag);
        Assert.Equal(OtherFlag, other.OtherFlag);
        Assert.Equal(Byte, other.Byte);
        Assert.Equal(Short, other.Short);
        Assert.Equal(UShort, other.UShort);
        Assert.Equal(Int, other.Int);
        Assert.Equal(UInt, other.UInt);
        Assert.Equal(Long, other.Long);
        AssertSameFloat(Float, other.Float);
        AssertSameFloat(FloatMax, other.FloatMax);
        AssertSameFloat(FloatTiny, other.FloatTiny);
        AssertSameFloat(FloatTenth, other.FloatTenth);
        AssertSameFloat(FloatNegativeZero, other.FloatNegativeZero);
        Assert.Equal(Text, other.Text);
        Assert.Equal(Date, other.Date);
        Assert.Equal(EmptyText, other.EmptyText);
        Assert.Equal(NestedHp, other.NestedHp);
        Assert.Equal(NestedName, other.NestedName);
        Assert.Equal(Bytes, other.Bytes);
        Assert.Equal(Shorts, other.Shorts);
        Assert.Equal(UShorts, other.UShorts);
        Assert.Equal(Ints, other.Ints);
        Assert.Equal(UInts, other.UInts);
        Assert.Equal(Longs, other.Longs);
        Assert.Equal(Empty, other.Empty);
    }

    private static void AssertSameFloat(float expected, float actual)
    {
        // Bit comparison: distinguishes -0.0f from 0.0f.
        Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(actual));
    }
}

/// <summary>
/// Builds binary save-game files by hand, field by field, and appends a correct CRC-32 so that a test reaches the
/// check it targets instead of stopping at the checksum.
/// </summary>
internal sealed class BinarySaveFileBuilder
{
    private readonly List<byte> _bytes = new();

    public static BinarySaveFileBuilder Header(int containerVersion = 1, int dataVersion = 1, int metadataCount = 0)
    {
        return new BinarySaveFileBuilder()
            .Raw(BinarySaveGameArchive.Magic)
            .Int32(containerVersion)
            .Int32(dataVersion)
            .Int32(metadataCount);
    }

    public BinarySaveFileBuilder Raw(params byte[] bytes)
    {
        _bytes.AddRange(bytes);
        return this;
    }

    public BinarySaveFileBuilder Int32(int value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return Raw(bytes);
    }

    public BinarySaveFileBuilder String(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        return Int32(bytes.Length).Raw(bytes);
    }

    public byte[] BuildWithoutCrc()
    {
        return _bytes.ToArray();
    }

    public byte[] Build()
    {
        return WithCrc(_bytes.ToArray());
    }

    /// <summary><paramref name="content"/> followed by its CRC-32.</summary>
    public static byte[] WithCrc(byte[] content)
    {
        var file = new byte[content.Length + 4];
        content.CopyTo(file, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(content.Length), Crc32.Compute(content));
        return file;
    }

    /// <summary>Recomputes the final CRC-32 of a whole file in place, after a test modified it.</summary>
    public static byte[] RecomputeCrc(byte[] file)
    {
        return WithCrc(file.AsSpan(0, file.Length - 4).ToArray());
    }
}

internal static class SaveGameFormatAssert
{
    /// <summary>
    /// Opens then loads <paramref name="fileBytes"/> into <paramref name="target"/>, as the save-game service will:
    /// the envelope first, then the data.
    /// </summary>
    public static SaveGameEnvelopeResult Load(byte[] fileBytes, ISaveGameData target)
    {
        SaveGameEnvelopeResult openResult = SaveGameEnvelope.TryOpen(fileBytes, out SaveGameEnvelope envelope);
        if (!openResult.IsSuccess)
        {
            Assert.Null(envelope);
            return openResult;
        }

        return envelope.TryDeserialize(target);
    }

    /// <summary>
    /// The file is refused when its envelope is opened: the metadata-only read (a slot listing) and a full load both
    /// return <paramref name="expected"/>, without throwing. Returns the message.
    /// </summary>
    public static string RefusedAtOpen(byte[] fileBytes, SaveGameEnvelopeStatus expected)
    {
        SaveGameEnvelopeResult metadataOnly = SaveGameEnvelope.TryOpen(fileBytes, out SaveGameEnvelope envelope);
        Assert.Equal(expected, metadataOnly.Status);
        Assert.Null(envelope);
        Assert.False(string.IsNullOrEmpty(metadataOnly.Message));

        var target = new DelegateSaveGameData(_ => throw new InvalidOperationException("Serialize must not run."));
        SaveGameEnvelopeResult fullLoad = Load(fileBytes, target);
        Assert.Equal(expected, fullLoad.Status);
        Assert.Equal(metadataOnly.Message, fullLoad.Message);

        return metadataOnly.Message;
    }

    /// <summary>
    /// The envelope is valid, so the metadata-only read succeeds without decoding the data, and the load is refused
    /// as <see cref="SaveGameEnvelopeStatus.InvalidData"/> when <paramref name="target"/> decodes it. Returns the message.
    /// </summary>
    public static string RefusedAtData(byte[] fileBytes, ISaveGameData target)
    {
        SaveGameEnvelopeResult metadataOnly = SaveGameEnvelope.TryOpen(fileBytes, out SaveGameEnvelope envelope);
        Assert.Equal(SaveGameEnvelopeStatus.Success, metadataOnly.Status);
        Assert.NotNull(envelope);

        SaveGameEnvelopeResult load = envelope.TryDeserialize(target);
        Assert.Equal(SaveGameEnvelopeStatus.InvalidData, load.Status);

        return load.Message;
    }

    /// <summary>
    /// Bytes allocated on this thread by <paramref name="action"/>, measured on a second run so that one-time type
    /// initialization does not count. A reader that allocated a buffer for a hostile length prefix would show here.
    /// </summary>
    public static long AllocatedBytes(Action action)
    {
        action();
        long before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
