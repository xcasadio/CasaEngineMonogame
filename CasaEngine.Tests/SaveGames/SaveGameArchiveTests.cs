using System;
using System.Collections.Generic;
using CasaEngine.Framework.SaveGames;
using Xunit;

namespace CasaEngine.Tests.SaveGames;

/// <summary>
/// T2.1 of the save-game service plan: the checks <see cref="SaveGameArchive"/> shares between the formats
/// (integer ranges, finite floats, strings, fixed array lengths, nesting, field paths), exercised through
/// <see cref="TapeArchive"/>, a minimal in-memory positional archive. The JSON and binary formats of T2.2 get
/// their own round-trip and hostile-file tests.
/// </summary>
public sealed class SaveGameArchiveTests
{
    [Fact]
    public void RoundTrip_AllTypes_RestoresEveryField()
    {
        var saved = AllTypesData.CreateFilled();
        var writer = TapeArchive.ForWriting(saved.LatestDataVersion);
        saved.Serialize(writer);
        writer.ThrowIfObjectOpen();

        var loaded = new AllTypesData();
        var reader = TapeArchive.ForReading(writer.Entries, 3);
        loaded.Serialize(reader);
        reader.ThrowIfObjectOpen();

        Assert.False(writer.IsLoading);
        Assert.Equal(5, writer.DataVersion);
        Assert.True(reader.IsLoading);
        Assert.Equal(3, reader.DataVersion);
        Assert.True(reader.IsAtEnd);
        saved.AssertEqual(loaded);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Write_NonFiniteFloat_IsRefusedWithNestedPath(float speed)
    {
        var writer = TapeArchive.ForWriting(1);
        writer.BeginObject("player");

        var exception = Assert.Throws<SaveGameDataException>(() => writer.Value("speed", ref speed));

        Assert.Equal("player.speed", exception.FieldPath);
        Assert.Contains("not finite", exception.Reason);
        Assert.Empty(writer.EntriesOfTag(TapeArchive.FloatTag));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Read_NonFiniteFloat_IsRefusedAndLeavesTheFieldUnchanged(float stored)
    {
        var reader = TapeArchive.ForReading(new List<TapeEntry> { new(TapeArchive.FloatTag, "speed", stored) }, 1);
        float speed = 1.5f;

        var exception = Assert.Throws<SaveGameDataException>(() => reader.Value("speed", ref speed));

        Assert.Equal("speed", exception.FieldPath);
        Assert.Equal(1.5f, speed);
    }

    [Fact]
    public void Read_ExtremeFiniteFloats_AreAccepted()
    {
        var reader = TapeArchive.ForReading(
            new List<TapeEntry>
            {
                new(TapeArchive.FloatTag, "max", float.MaxValue),
                new(TapeArchive.FloatTag, "tiny", float.Epsilon),
                new(TapeArchive.FloatTag, "negativeZero", -0.0f),
            },
            1);
        float max = 0, tiny = 0, negativeZero = 0;

        reader.Value("max", ref max);
        reader.Value("tiny", ref tiny);
        reader.Value("negativeZero", ref negativeZero);

        Assert.Equal(float.MaxValue, max);
        Assert.Equal(float.Epsilon, tiny);
        Assert.True(float.IsNegative(negativeZero));
    }

    [Fact]
    public void Read_ShortOutOfRange_IsRefusedNamingTheFieldAndType()
    {
        var reader = TapeArchive.ForReading(new List<TapeEntry> { Integer("hp", 70000) }, 1);
        short hp = 12;

        var exception = Assert.Throws<SaveGameDataException>(() => reader.Value("hp", ref hp));

        Assert.Equal("hp", exception.FieldPath);
        Assert.Equal("70000 is outside the short range [-32768, 32767].", exception.Reason);
        Assert.Equal("Invalid save-game data at 'hp': 70000 is outside the short range [-32768, 32767].", exception.Message);
        Assert.Equal(12, hp);
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(256L)]
    public void Read_ByteOutOfRange_IsRefused(long stored)
    {
        byte value = 0;
        Assert.Throws<SaveGameDataException>(() => TapeArchive.ForReading(new List<TapeEntry> { Integer("v", stored) }, 1).Value("v", ref value));
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(65536L)]
    public void Read_UInt16OutOfRange_IsRefused(long stored)
    {
        ushort value = 0;
        Assert.Throws<SaveGameDataException>(() => TapeArchive.ForReading(new List<TapeEntry> { Integer("v", stored) }, 1).Value("v", ref value));
    }

    [Theory]
    [InlineData(-32769L)]
    [InlineData(32768L)]
    public void Read_Int16OutOfRange_IsRefused(long stored)
    {
        short value = 0;
        Assert.Throws<SaveGameDataException>(() => TapeArchive.ForReading(new List<TapeEntry> { Integer("v", stored) }, 1).Value("v", ref value));
    }

    [Theory]
    [InlineData(-2147483649L)]
    [InlineData(2147483648L)]
    public void Read_Int32OutOfRange_IsRefused(long stored)
    {
        int value = 0;
        Assert.Throws<SaveGameDataException>(() => TapeArchive.ForReading(new List<TapeEntry> { Integer("v", stored) }, 1).Value("v", ref value));
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(4294967296L)]
    public void Read_UInt32OutOfRange_IsRefused(long stored)
    {
        uint value = 0;
        Assert.Throws<SaveGameDataException>(() => TapeArchive.ForReading(new List<TapeEntry> { Integer("v", stored) }, 1).Value("v", ref value));
    }

    [Fact]
    public void Read_IntegerRangeBoundaries_AreAccepted()
    {
        var reader = TapeArchive.ForReading(
            new List<TapeEntry>
            {
                Integer("b0", 0), Integer("b1", 255),
                Integer("s0", short.MinValue), Integer("s1", short.MaxValue),
                Integer("u0", 0), Integer("u1", ushort.MaxValue),
                Integer("i0", int.MinValue), Integer("i1", int.MaxValue),
                Integer("w0", 0), Integer("w1", uint.MaxValue),
                Integer("l0", long.MinValue), Integer("l1", long.MaxValue),
            },
            1);
        byte b0 = 1, b1 = 0;
        short s0 = 0, s1 = 0;
        ushort u0 = 1, u1 = 0;
        int i0 = 0, i1 = 0;
        uint w0 = 1, w1 = 0;
        long l0 = 0, l1 = 0;

        reader.Value("b0", ref b0);
        reader.Value("b1", ref b1);
        reader.Value("s0", ref s0);
        reader.Value("s1", ref s1);
        reader.Value("u0", ref u0);
        reader.Value("u1", ref u1);
        reader.Value("i0", ref i0);
        reader.Value("i1", ref i1);
        reader.Value("w0", ref w0);
        reader.Value("w1", ref w1);
        reader.Value("l0", ref l0);
        reader.Value("l1", ref l1);

        Assert.Equal((byte)0, b0);
        Assert.Equal(byte.MaxValue, b1);
        Assert.Equal(short.MinValue, s0);
        Assert.Equal(short.MaxValue, s1);
        Assert.Equal((ushort)0, u0);
        Assert.Equal(ushort.MaxValue, u1);
        Assert.Equal(int.MinValue, i0);
        Assert.Equal(int.MaxValue, i1);
        Assert.Equal(0u, w0);
        Assert.Equal(uint.MaxValue, w1);
        Assert.Equal(long.MinValue, l0);
        Assert.Equal(long.MaxValue, l1);
    }

    [Fact]
    public void Write_Integers_ReachTheFormatWithTheirKind()
    {
        var writer = TapeArchive.ForWriting(1);
        uint big = uint.MaxValue;
        short negative = -5;

        writer.Value("big", ref big);
        writer.Value("negative", ref negative);

        Assert.Equal(new TapeEntry(TapeArchive.IntegerTag, "big", (long)uint.MaxValue, SaveGameIntegerKind.UInt32), writer.Entries[0]);
        Assert.Equal(new TapeEntry(TapeArchive.IntegerTag, "negative", -5L, SaveGameIntegerKind.Int16), writer.Entries[1]);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void Read_ArrayOfAnotherLength_IsRefusedBeforeAnyElement(int storedLength)
    {
        var entries = new List<TapeEntry>
        {
            new(TapeArchive.ArrayStartTag, "flags", (long)storedLength, SaveGameIntegerKind.Byte),
            new(TapeArchive.ElementTag, null, 9L, SaveGameIntegerKind.Byte),
        };
        var reader = TapeArchive.ForReading(entries, 1);
        reader.BeginObjectWithoutTape("world");
        byte[] flags = { 1, 2, 3 };

        var exception = Assert.Throws<SaveGameDataException>(() => reader.Value("flags", flags));

        Assert.Equal("world.flags", exception.FieldPath);
        Assert.Contains("expected an array of 3 elements", exception.Reason);
        Assert.Equal(new byte[] { 1, 2, 3 }, flags);
        Assert.Equal(0, reader.ElementsRead);
    }

    [Fact]
    public void Read_ArrayElementOutOfRange_IsRefusedWithItsIndex()
    {
        var entries = new List<TapeEntry>
        {
            new(TapeArchive.ArrayStartTag, "counts", 3L, SaveGameIntegerKind.UInt16),
            new(TapeArchive.ElementTag, null, 1L, SaveGameIntegerKind.UInt16),
            new(TapeArchive.ElementTag, null, 2L, SaveGameIntegerKind.UInt16),
            new(TapeArchive.ElementTag, null, 70000L, SaveGameIntegerKind.UInt16),
            new(TapeArchive.ArrayEndTag, null, null),
        };
        var reader = TapeArchive.ForReading(entries, 1);
        reader.BeginObjectWithoutTape("stats");
        var counts = new ushort[3];

        var exception = Assert.Throws<SaveGameDataException>(() => reader.Value("counts", counts));

        Assert.Equal("stats.counts[2]", exception.FieldPath);
        Assert.Contains("ushort", exception.Reason);
    }

    [Fact]
    public void FormatElementError_NamesTheArrayElement()
    {
        var entries = new List<TapeEntry>
        {
            new(TapeArchive.ArrayStartTag, "ids", 2L, SaveGameIntegerKind.Int32),
            new(TapeArchive.ElementTag, null, 1L, SaveGameIntegerKind.Int32),
            new(TapeArchive.FloatTag, null, 1.0f),
        };
        var reader = TapeArchive.ForReading(entries, 1);
        var ids = new int[2];

        var exception = Assert.Throws<SaveGameDataException>(() => reader.Value("ids", ids));

        Assert.Equal("ids[1]", exception.FieldPath);
    }

    [Fact]
    public void FormatFieldError_NamesTheFieldInsideNestedObjects()
    {
        var entries = new List<TapeEntry>
        {
            new(TapeArchive.ObjectStartTag, "player", null),
            new(TapeArchive.ObjectStartTag, "stats", null),
            new(TapeArchive.StringTag, "hp", "oops"),
        };
        var reader = TapeArchive.ForReading(entries, 1);
        int hp = 0;
        reader.BeginObject("player");
        reader.BeginObject("stats");

        var exception = Assert.Throws<SaveGameDataException>(() => reader.Value("hp", ref hp));

        Assert.Equal("player.stats.hp", exception.FieldPath);
    }

    [Fact]
    public void FormatObjectError_NamesTheObjectOnce()
    {
        var reader = TapeArchive.ForReading(new List<TapeEntry> { new(TapeArchive.StringTag, "x", "y") }, 1);
        reader.BeginObjectWithoutTape("player");

        var exception = Assert.Throws<SaveGameDataException>(() => reader.BeginObject("inventory"));

        Assert.Equal("player.inventory", exception.FieldPath);
    }

    [Fact]
    public void Write_NullString_IsRefused()
    {
        var writer = TapeArchive.ForWriting(1);
        string name = null;

        var exception = Assert.Throws<SaveGameDataException>(() => writer.Value("name", ref name));

        Assert.Equal("name", exception.FieldPath);
        Assert.Empty(writer.Entries);
    }

    [Fact]
    public void Read_NullString_IsRefusedAndLeavesTheFieldUnchanged()
    {
        var reader = TapeArchive.ForReading(new List<TapeEntry> { new(TapeArchive.StringTag, "name", null) }, 1);
        string name = "kept";

        Assert.Throws<SaveGameDataException>(() => reader.Value("name", ref name));

        Assert.Equal("kept", name);
    }

    // Built in the test from an index: attribute arguments (UTF-8 in metadata) and xUnit's theory-data
    // serialization both turn a lone surrogate into U+FFFD.
    private static readonly string[] UnpairedSurrogateStrings =
    {
        "abc" + (char)0xD800,
        (char)0xDC00 + "abc",
        "a" + (char)0xDC00 + (char)0xD800 + "b",
        new string(new[] { (char)0xD800, (char)0xD800, (char)0xDC00 }),
    };

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void UnpairedSurrogate_IsRefusedInBothDirections(int caseIndex)
    {
        string value = UnpairedSurrogateStrings[caseIndex];
        Assert.Contains(value, c => char.IsSurrogate(c));

        var writer = TapeArchive.ForWriting(1);
        string written = value;
        Assert.Throws<SaveGameDataException>(() => writer.Value("text", ref written));

        var reader = TapeArchive.ForReading(new List<TapeEntry> { new(TapeArchive.StringTag, "text", value) }, 1);
        string read = "kept";
        Assert.Throws<SaveGameDataException>(() => reader.Value("text", ref read));
        Assert.Equal("kept", read);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Alundra")]
    [InlineData("\U0001F600 surrogate pair")]
    [InlineData("2026-09-28T12:00:00Z")]
    [InlineData("line\nbreak\0nul")]
    public void WellFormedStrings_RoundTrip(string value)
    {
        var writer = TapeArchive.ForWriting(1);
        string written = value;
        writer.Value("text", ref written);

        var reader = TapeArchive.ForReading(writer.Entries, 1);
        string read = null;
        reader.Value("text", ref read);

        Assert.Equal(value, read);
    }

    [Fact]
    public void InvalidName_IsDeveloperMisuse()
    {
        var writer = TapeArchive.ForWriting(1);
        int value = 0;

        Assert.Throws<ArgumentNullException>(() => writer.Value(null, ref value));
        Assert.Throws<ArgumentException>(() => writer.Value(string.Empty, ref value));
        Assert.Throws<ArgumentException>(() => writer.Value(string.Empty, new int[1]));
        Assert.Throws<ArgumentException>(() => writer.BeginObject(string.Empty));
        Assert.Empty(writer.Entries);
    }

    [Fact]
    public void NullArray_IsDeveloperMisuse()
    {
        var reader = TapeArchive.ForReading(new List<TapeEntry>(), 1);

        Assert.Throws<ArgumentNullException>(() => reader.Value("flags", (byte[])null));
        Assert.Throws<ArgumentNullException>(() => reader.Value("flags", (long[])null));
    }

    [Fact]
    public void UnbalancedObjects_AreDeveloperMisuse()
    {
        var writer = TapeArchive.ForWriting(1);

        Assert.Throws<InvalidOperationException>(() => writer.EndObject());

        writer.BeginObject("player");
        writer.BeginObject("stats");
        var exception = Assert.Throws<InvalidOperationException>(() => writer.ThrowIfObjectOpen());
        Assert.Contains("player.stats", exception.Message);

        writer.EndObject();
        writer.EndObject();
        writer.ThrowIfObjectOpen();
        Assert.Throws<InvalidOperationException>(() => writer.EndObject());
    }

    [Fact]
    public void Nesting_IsLimitedToMaxObjectDepth()
    {
        var writer = TapeArchive.ForWriting(1);
        for (int i = 0; i < SaveGameArchive.MaxObjectDepth; i++)
        {
            writer.BeginObject("o" + i);
        }

        Assert.Throws<InvalidOperationException>(() => writer.BeginObject("tooDeep"));
        Assert.Equal(SaveGameArchive.MaxObjectDepth, writer.EntriesOfTag(TapeArchive.ObjectStartTag).Count);
    }

    [Fact]
    public void NegativeDataVersion_IsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TapeArchive.ForWriting(-1));
    }

    private static TapeEntry Integer(string name, long value)
    {
        return new TapeEntry(TapeArchive.IntegerTag, name, value);
    }

    /// <summary>
    /// A game-side save object covering every supported value, nested objects and every array element type.
    /// </summary>
    private sealed class AllTypesData : ISaveGameData
    {
        public bool Flag;
        public byte Byte;
        public short Short;
        public ushort UShort;
        public int Int;
        public uint UInt;
        public long Long;
        public float Float;
        public string Text = string.Empty;
        public string Date = string.Empty;
        public int NestedHp;
        public byte[] Bytes = new byte[3];
        public short[] Shorts = new short[2];
        public ushort[] UShorts = new ushort[2];
        public int[] Ints = new int[2];
        public uint[] UInts = new uint[2];
        public long[] Longs = new long[2];
        public int[] Empty = Array.Empty<int>();

        public int LatestDataVersion => 5;

        public static AllTypesData CreateFilled()
        {
            return new AllTypesData
            {
                Flag = true,
                Byte = 200,
                Short = -12345,
                UShort = 54321,
                Int = -123456789,
                UInt = 4000000000,
                Long = -9000000000000000000,
                Float = -3.25e-12f,
                Text = "Inoa \U0001F600",
                Date = "2026-09-28T12:00:00+02:00",
                NestedHp = 42,
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
            archive.Value("byte", ref Byte);
            archive.Value("short", ref Short);
            archive.Value("ushort", ref UShort);
            archive.Value("int", ref Int);
            archive.Value("uint", ref UInt);
            archive.Value("long", ref Long);
            archive.Value("float", ref Float);
            archive.Value("text", ref Text);
            archive.Value("date", ref Date);
            archive.BeginObject("player");
            archive.BeginObject("stats");
            archive.Value("hp", ref NestedHp);
            archive.EndObject();
            archive.Value("bytes", Bytes);
            archive.EndObject();
            archive.Value("shorts", Shorts);
            archive.Value("ushorts", UShorts);
            archive.Value("ints", Ints);
            archive.Value("uints", UInts);
            archive.Value("longs", Longs);
            archive.Value("empty", Empty);
        }

        public void AssertEqual(AllTypesData other)
        {
            Assert.Equal(Flag, other.Flag);
            Assert.Equal(Byte, other.Byte);
            Assert.Equal(Short, other.Short);
            Assert.Equal(UShort, other.UShort);
            Assert.Equal(Int, other.Int);
            Assert.Equal(UInt, other.UInt);
            Assert.Equal(Long, other.Long);
            Assert.Equal(Float, other.Float);
            Assert.Equal(Text, other.Text);
            Assert.Equal(Date, other.Date);
            Assert.Equal(NestedHp, other.NestedHp);
            Assert.Equal(Bytes, other.Bytes);
            Assert.Equal(Shorts, other.Shorts);
            Assert.Equal(UShorts, other.UShorts);
            Assert.Equal(Ints, other.Ints);
            Assert.Equal(UInts, other.UInts);
            Assert.Equal(Longs, other.Longs);
            Assert.Equal(Empty, other.Empty);
        }
    }
}

/// <summary>One recorded call of <see cref="TapeArchive"/>.</summary>
internal readonly record struct TapeEntry(string Tag, string Name, object Value, SaveGameIntegerKind Kind = SaveGameIntegerKind.Int64);

/// <summary>
/// Minimal positional archive for tests: writing records every format primitive on a tape, reading replays it
/// and reports a tag or name mismatch as a data error, the way a real format reports a missing or mistyped field.
/// It performs no check of its own on values, so every value check observed through it is the archive's.
/// </summary>
internal sealed class TapeArchive : SaveGameArchive
{
    public const string BooleanTag = "bool";
    public const string IntegerTag = "integer";
    public const string FloatTag = "float";
    public const string StringTag = "string";
    public const string ArrayStartTag = "array";
    public const string ElementTag = "element";
    public const string ArrayEndTag = "array-end";
    public const string ObjectStartTag = "object";
    public const string ObjectEndTag = "object-end";

    private int _position;
    private bool _skipNextObjectEntry;

    private TapeArchive(bool isLoading, int dataVersion, List<TapeEntry> entries)
        : base(isLoading, dataVersion)
    {
        Entries = entries;
    }

    public List<TapeEntry> Entries { get; }

    public int ElementsRead { get; private set; }

    public bool IsAtEnd => _position == Entries.Count;

    public static TapeArchive ForWriting(int dataVersion)
    {
        return new TapeArchive(false, dataVersion, new List<TapeEntry>());
    }

    public static TapeArchive ForReading(List<TapeEntry> entries, int dataVersion)
    {
        return new TapeArchive(true, dataVersion, entries);
    }

    public List<TapeEntry> EntriesOfTag(string tag)
    {
        return Entries.FindAll(entry => entry.Tag == tag);
    }

    /// <summary>Opens an object on the archive's path without consuming a tape entry, to set up a nesting path.</summary>
    public void BeginObjectWithoutTape(string name)
    {
        _skipNextObjectEntry = true;
        BeginObject(name);
    }

    private protected override bool ReadBooleanCore(string name)
    {
        return (bool)Next(BooleanTag, name).Value;
    }

    private protected override void WriteBooleanCore(string name, bool value)
    {
        Entries.Add(new TapeEntry(BooleanTag, name, value));
    }

    private protected override long ReadIntegerCore(string name, SaveGameIntegerKind kind)
    {
        return (long)Next(IntegerTag, name).Value;
    }

    private protected override void WriteIntegerCore(string name, long value, SaveGameIntegerKind kind)
    {
        Entries.Add(new TapeEntry(IntegerTag, name, value, kind));
    }

    private protected override float ReadSingleCore(string name)
    {
        return (float)Next(FloatTag, name).Value;
    }

    private protected override void WriteSingleCore(string name, float value)
    {
        Entries.Add(new TapeEntry(FloatTag, name, value));
    }

    private protected override string ReadStringCore(string name)
    {
        return (string)Next(StringTag, name).Value;
    }

    private protected override void WriteStringCore(string name, string value)
    {
        Entries.Add(new TapeEntry(StringTag, name, value));
    }

    private protected override int ReadArrayStartCore(string name, SaveGameIntegerKind kind)
    {
        return checked((int)(long)Next(ArrayStartTag, name).Value);
    }

    private protected override void WriteArrayStartCore(string name, int length, SaveGameIntegerKind kind)
    {
        Entries.Add(new TapeEntry(ArrayStartTag, name, (long)length, kind));
    }

    private protected override long ReadArrayElementCore(int index, SaveGameIntegerKind kind)
    {
        if (_position >= Entries.Count || Entries[_position].Tag != ElementTag)
        {
            throw CreateElementDataException(index, "missing element.");
        }

        ElementsRead++;
        return (long)Entries[_position++].Value;
    }

    private protected override void WriteArrayElementCore(int index, long value, SaveGameIntegerKind kind)
    {
        Entries.Add(new TapeEntry(ElementTag, null, value, kind));
    }

    private protected override void EndArrayCore()
    {
        if (IsLoading)
        {
            Next(ArrayEndTag, null);
        }
        else
        {
            Entries.Add(new TapeEntry(ArrayEndTag, null, null));
        }
    }

    private protected override void BeginObjectCore(string name)
    {
        if (_skipNextObjectEntry)
        {
            _skipNextObjectEntry = false;
            return;
        }

        if (IsLoading)
        {
            Next(ObjectStartTag, name);
        }
        else
        {
            Entries.Add(new TapeEntry(ObjectStartTag, name, null));
        }
    }

    private protected override void EndObjectCore()
    {
        if (IsLoading)
        {
            Next(ObjectEndTag, null);
        }
        else
        {
            Entries.Add(new TapeEntry(ObjectEndTag, null, null));
        }
    }

    private TapeEntry Next(string tag, string name)
    {
        if (_position >= Entries.Count || Entries[_position].Tag != tag || Entries[_position].Name != name)
        {
            throw CreateDataException(name ?? tag, $"expected a {tag} entry.");
        }

        return Entries[_position++];
    }
}
