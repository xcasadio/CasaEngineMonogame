using System;
using System.Linq;
using System.Text;
using CasaEngine.Framework.SaveGames;
using Xunit;

namespace CasaEngine.Tests.SaveGames;

/// <summary>
/// T2.2 of the save-game service plan: hostile and hand-edited JSON files. Refusals at the envelope go through
/// <see cref="SaveGameFormatAssert.RefusedAtOpen"/> (metadata-only read and full load: same result, no exception);
/// refusals of a value go through <see cref="SaveGameFormatAssert.RefusedAtData"/> (the listing still succeeds, the
/// load is <see cref="SaveGameEnvelopeStatus.InvalidData"/> naming the field).
/// </summary>
public sealed class JsonSaveGameFormatTests
{
    [Theory]
    [InlineData("{")]
    [InlineData("{\"container\": ")]
    [InlineData("{\"container\": \"casaengine-savegame\",}")]
    [InlineData("{'container' 1}")]
    [InlineData("{\"a\": [1, 2}")]
    [InlineData("{\"a\": tru}")]
    [InlineData("{} trailing")]
    [InlineData("{} {}")]
    public void MalformedDocument_IsInvalidData(string text)
    {
        SaveGameFormatAssert.RefusedAtOpen(Encoding.UTF8.GetBytes(text), SaveGameEnvelopeStatus.InvalidData);
    }

    [Fact]
    public void InvalidUtf8_IsInvalidData()
    {
        byte[] file = Encoding.UTF8.GetBytes("{\"container\":\"casaengine-savegame\",\"containerVersion\":1,\"dataVersion\":1,\"metadata\":{},\"data\":{\"text\":\"")
            .Concat(new byte[] { 0xC0, 0x80 })
            .Concat(Encoding.UTF8.GetBytes("\"}}"))
            .ToArray();

        SaveGameFormatAssert.RefusedAtOpen(file, SaveGameEnvelopeStatus.InvalidData);
    }

    [Fact]
    public void MissingField_IsInvalidDataNamingIt()
    {
        int hp = 5;
        var target = new DelegateSaveGameData(archive =>
        {
            archive.BeginObject("player");
            archive.Value("hp", ref hp);
            archive.EndObject();
        });

        string message = SaveGameFormatAssert.RefusedAtData(Utf8(Document("\"player\": { \"mp\": 3 }")), target);

        Assert.Contains("'player.hp'", message);
        Assert.Contains("missing", message);
        Assert.Equal(5, hp);
    }

    [Fact]
    public void MissingObject_IsInvalidDataNamingIt()
    {
        var target = new DelegateSaveGameData(archive =>
        {
            archive.BeginObject("player");
            archive.EndObject();
        });

        string message = SaveGameFormatAssert.RefusedAtData(Utf8(Document("\"other\": {}")), target);

        Assert.Contains("'player'", message);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("1.0")]
    [InlineData("1e2")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("\"12\"")]
    [InlineData("[12]")]
    [InlineData("{}")]
    public void NonIntegerToken_InAnIntegerField_IsInvalidDataNamingIt(string token)
    {
        int hp = 5;

        string message = SaveGameFormatAssert.RefusedAtData(
            Utf8(Document($"\"hp\": {token}")),
            new DelegateSaveGameData(archive => archive.Value("hp", ref hp)));

        Assert.Contains("'hp'", message);
        Assert.Equal(5, hp);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("\"3\"")]
    public void NonIntegerToken_InAnArrayElement_IsInvalidDataNamingTheElement(string token)
    {
        int[] values = new int[3];

        string message = SaveGameFormatAssert.RefusedAtData(
            Utf8(Document($"\"ints\": [1, 2, {token}]")),
            new DelegateSaveGameData(archive => archive.Value("ints", values)));

        Assert.Contains("'ints[2]'", message);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("\"true\"")]
    [InlineData("null")]
    public void NonBooleanToken_InABooleanField_IsInvalidDataNamingIt(string token)
    {
        bool flag = false;

        string message = SaveGameFormatAssert.RefusedAtData(
            Utf8(Document($"\"flag\": {token}")),
            new DelegateSaveGameData(archive => archive.Value("flag", ref flag)));

        Assert.Contains("'flag'", message);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("null")]
    [InlineData("false")]
    public void NonStringToken_InAStringField_IsInvalidDataNamingIt(string token)
    {
        string text = "unchanged";

        string message = SaveGameFormatAssert.RefusedAtData(
            Utf8(Document($"\"text\": {token}")),
            new DelegateSaveGameData(archive => archive.Value("text", ref text)));

        Assert.Contains("'text'", message);
        Assert.Equal("unchanged", text);
    }

    [Fact]
    public void Integer70000_InAShort_IsInvalidData()
    {
        short value = 0;

        string message = SaveGameFormatAssert.RefusedAtData(
            Utf8(Document("\"hp\": 70000")),
            new DelegateSaveGameData(archive => archive.Value("hp", ref value)));

        Assert.Contains("'hp'", message);
        Assert.Contains("short range", message);
    }

    [Theory]
    [InlineData("9223372036854775808")]
    [InlineData("-9223372036854775809")]
    [InlineData("123456789012345678901234567890123456789012345678901234567890")]
    public void IntegerBeyond64Bits_InALong_IsInvalidDataNotAnException(string token)
    {
        long value = 0;

        string message = SaveGameFormatAssert.RefusedAtData(
            Utf8(Document($"\"v\": {token}")),
            new DelegateSaveGameData(archive => archive.Value("v", ref value)));

        Assert.Contains("'v'", message);
        Assert.Contains("64-bit", message);
    }

    [Fact]
    public void IntegerBeyond64Bits_InAnArrayElement_IsInvalidData()
    {
        long[] values = new long[1];

        string message = SaveGameFormatAssert.RefusedAtData(
            Utf8(Document("\"v\": [99999999999999999999]")),
            new DelegateSaveGameData(archive => archive.Value("v", values)));

        Assert.Contains("'v[0]'", message);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1e999")]
    [InlineData("-1e999")]
    [InlineData("1e39")]
    [InlineData("1000000000000000000000000000000000000000000")]
    public void NonFiniteOrOutOfRangeNumber_InAFloat_IsInvalidData(string token)
    {
        float speed = 1f;
        byte[] file = Utf8(Document($"\"speed\": {token}"));

        SaveGameEnvelopeResult result = SaveGameFormatAssert.Load(file, new DelegateSaveGameData(archive => archive.Value("speed", ref speed)));

        // Whether the JSON reader or the archive refuses it, it is invalid data and never an exception.
        Assert.Equal(SaveGameEnvelopeStatus.InvalidData, result.Status);
        Assert.Equal(1f, speed);
        SaveGameEnvelopeResult metadataOnly = SaveGameEnvelope.TryOpen(file, out _);
        Assert.True(metadataOnly.IsSuccess || metadataOnly.Status == SaveGameEnvelopeStatus.InvalidData);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("1e39")]
    public void NonFiniteFloat_ReachingTheArchive_NamesTheField(string token)
    {
        float speed = 1f;

        string message = SaveGameFormatAssert.RefusedAtData(
            Utf8(Document($"\"speed\": {token}")),
            new DelegateSaveGameData(archive => archive.Value("speed", ref speed)));

        Assert.Contains("'speed'", message);
    }

    [Theory]
    [InlineData("2", 2f)]
    [InlineData("-7", -7f)]
    [InlineData("0.5", 0.5f)]
    [InlineData("-0.0", -0.0f)]
    public void NumberToken_InAFloatField_IsAccepted(string token, float expected)
    {
        float speed = 1f;

        SaveGameEnvelopeResult result = SaveGameFormatAssert.Load(
            Utf8(Document($"\"speed\": {token}")),
            new DelegateSaveGameData(archive => archive.Value("speed", ref speed)));

        Assert.Equal(SaveGameEnvelopeStatus.Success, result.Status);
        Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(speed));
    }

    [Theory]
    [InlineData("\"1.5\"")]
    [InlineData("true")]
    [InlineData("null")]
    public void NonNumberToken_InAFloatField_IsInvalidDataNamingIt(string token)
    {
        float speed = 1f;

        string message = SaveGameFormatAssert.RefusedAtData(
            Utf8(Document($"\"speed\": {token}")),
            new DelegateSaveGameData(archive => archive.Value("speed", ref speed)));

        Assert.Contains("'speed'", message);
    }

    [Theory]
    [InlineData("{\"a\": 1, \"a\": 2}")]
    [InlineData("{\"player\": {\"hp\": 1, \"hp\": 1}}")]
    public void DuplicateKey_InTheData_IsInvalidData(string data)
    {
        string text = Envelope(data: data);

        SaveGameFormatAssert.RefusedAtOpen(Utf8(text), SaveGameEnvelopeStatus.InvalidData);
    }

    [Fact]
    public void DuplicateKey_InTheMetadata_IsInvalidData()
    {
        SaveGameFormatAssert.RefusedAtOpen(Utf8(Envelope(metadata: "{\"k\": \"a\", \"k\": \"b\"}")), SaveGameEnvelopeStatus.InvalidData);
    }

    [Fact]
    public void DuplicateKey_InTheEnvelope_IsInvalidData()
    {
        string text = "{\"container\":\"casaengine-savegame\",\"containerVersion\":1,\"containerVersion\":1,\"dataVersion\":1,\"metadata\":{},\"data\":{}}";

        SaveGameFormatAssert.RefusedAtOpen(Utf8(text), SaveGameEnvelopeStatus.InvalidData);
    }

    [Theory]
    [InlineData("{\"a\":", "}")]
    [InlineData("[", "]")]
    public void TenThousandNestingLevels_IsInvalidData(string open, string close)
    {
        string nested = string.Concat(Enumerable.Repeat(open, 10000)) + "1" + string.Concat(Enumerable.Repeat(close, 10000));
        string text = Envelope(data: "{\"deep\": " + nested + "}");

        string message = SaveGameFormatAssert.RefusedAtOpen(Utf8(text), SaveGameEnvelopeStatus.InvalidData);

        Assert.Contains("MaxDepth", message);
    }

    [Fact]
    public void DeepestObjectsTheArchiveWrites_StayReadable()
    {
        int value = 3;
        var data = new DelegateSaveGameData(archive =>
        {
            for (int i = 0; i < SaveGameArchive.MaxObjectDepth; i++)
            {
                archive.BeginObject("o");
            }

            archive.Value("v", new[] { value });
            for (int i = 0; i < SaveGameArchive.MaxObjectDepth; i++)
            {
                archive.EndObject();
            }
        });
        SaveGameEnvelope.TryWrite(SaveGameEnvelopeFormat.Json, data, null, out byte[] file);

        SaveGameEnvelopeResult result = SaveGameFormatAssert.Load(file, data);

        Assert.Equal(SaveGameEnvelopeStatus.Success, result.Status);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("2147483648")]
    [InlineData("99999999999999999999999")]
    public void ContainerVersion_OtherThanOne_IsUnsupportedContainer(string version)
    {
        SaveGameFormatAssert.RefusedAtOpen(Utf8(Envelope(containerVersion: version)), SaveGameEnvelopeStatus.UnsupportedContainer);
    }

    [Theory]
    [InlineData("\"1\"")]
    [InlineData("1.0")]
    [InlineData("null")]
    public void ContainerVersion_NotAnInteger_IsInvalidData(string version)
    {
        SaveGameFormatAssert.RefusedAtOpen(Utf8(Envelope(containerVersion: version)), SaveGameEnvelopeStatus.InvalidData);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("2147483648")]
    [InlineData("1.5")]
    [InlineData("\"1\"")]
    [InlineData("null")]
    public void DataVersion_NegativeOrNotAnInt_IsInvalidData(string version)
    {
        SaveGameFormatAssert.RefusedAtOpen(Utf8(Envelope(dataVersion: version)), SaveGameEnvelopeStatus.InvalidData);
    }

    [Theory]
    [InlineData("{\"containerVersion\":1,\"dataVersion\":1,\"metadata\":{},\"data\":{}}")]
    [InlineData("{\"container\":\"other-engine\",\"containerVersion\":1,\"dataVersion\":1,\"metadata\":{},\"data\":{}}")]
    [InlineData("{\"container\":\"casaengine-savegame\",\"dataVersion\":1,\"metadata\":{},\"data\":{}}")]
    [InlineData("{\"container\":\"casaengine-savegame\",\"containerVersion\":1,\"metadata\":{},\"data\":{}}")]
    [InlineData("{\"container\":\"casaengine-savegame\",\"containerVersion\":1,\"dataVersion\":1,\"data\":{}}")]
    [InlineData("{\"container\":\"casaengine-savegame\",\"containerVersion\":1,\"dataVersion\":1,\"metadata\":{}}")]
    [InlineData("{\"container\":\"casaengine-savegame\",\"containerVersion\":1,\"dataVersion\":1,\"metadata\":[],\"data\":{}}")]
    [InlineData("{\"container\":\"casaengine-savegame\",\"containerVersion\":1,\"dataVersion\":1,\"metadata\":{},\"data\":[]}")]
    [InlineData("{\"container\":\"casaengine-savegame\",\"containerVersion\":1,\"dataVersion\":1,\"metadata\":{\"k\":1},\"data\":{}}")]
    [InlineData("{\"container\":\"casaengine-savegame\",\"containerVersion\":1,\"dataVersion\":1,\"metadata\":{\"k\":null},\"data\":{}}")]
    [InlineData("{}")]
    public void EnvelopeFieldMissingOrMistyped_IsInvalidData(string text)
    {
        SaveGameFormatAssert.RefusedAtOpen(Utf8(text), SaveGameEnvelopeStatus.InvalidData);
    }

    [Fact]
    public void Utf8BomAndWhitespace_BeforeTheDocument_IsReadNormally()
    {
        int hp = 0;
        byte[] file = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(Encoding.ASCII.GetBytes(" \t\r\n  \n"))
            .Concat(Utf8(Document("\"hp\": 12")))
            .ToArray();

        SaveGameEnvelopeResult result = SaveGameFormatAssert.Load(file, new DelegateSaveGameData(archive => archive.Value("hp", ref hp)));

        Assert.Equal(SaveGameEnvelopeStatus.Success, result.Status);
        Assert.Equal(12, hp);
    }

    [Fact]
    public void WhitespaceWithoutBom_BeforeTheDocument_IsReadNormally()
    {
        byte[] file = Utf8("\n\n   " + Document("\"hp\": 12"));

        SaveGameEnvelopeResult result = SaveGameEnvelope.TryOpen(file, out SaveGameEnvelope envelope);

        Assert.Equal(SaveGameEnvelopeStatus.Success, result.Status);
        Assert.Equal(SaveGameEnvelopeFormat.Json, envelope.Format);
    }

    [Fact]
    public void HandEditedFile_WithCommentsAndUnknownFields_IsRead()
    {
        string text = "{ /* edited */ \"container\":\"casaengine-savegame\",\"containerVersion\":1,\"dataVersion\":1,"
            + "\"metadata\":{},\"extra\":true,\"data\":{\"hp\": 12, // lives\n \"unread\": [1,2]}} // end";
        int hp = 0;

        SaveGameEnvelopeResult result = SaveGameFormatAssert.Load(Utf8(text), new DelegateSaveGameData(archive => archive.Value("hp", ref hp)));

        Assert.Equal(SaveGameEnvelopeStatus.Success, result.Status);
        Assert.Equal(12, hp);
    }

    [Fact]
    public void CommentBeforeTheDocument_IsNotDetectedAsJson()
    {
        // Detection only skips a BOM and whitespace before '{'.
        SaveGameFormatAssert.RefusedAtOpen(Utf8("/* edited */ " + Document("\"hp\": 12")), SaveGameEnvelopeStatus.InvalidData);
    }

    [Fact]
    public void ArrayOfAnotherLength_IsInvalidDataNamingIt()
    {
        int[] values = new int[2];

        string message = SaveGameFormatAssert.RefusedAtData(
            Utf8(Document("\"ints\": [1, 2, 3]")),
            new DelegateSaveGameData(archive => archive.Value("ints", values)));

        Assert.Contains("'ints'", message);
        Assert.Equal(new[] { 0, 0 }, values);
    }

    [Fact]
    public void EscapedUnpairedSurrogate_IsReplacedByTheReader_SoTheStringIsWellFormed()
    {
        // Newtonsoft's reader turns an unpaired "\uD800" escape into U+FFFD: the archive never sees a malformed
        // string from JSON, and the envelope's own well-formedness check on metadata stays a second guard.
        string text = string.Empty;
        byte[] file = Utf8(Envelope(metadata: "{\"k\": \"x\\uD800\"}", data: "{\"text\": \"a\\uD800b\"}"));

        SaveGameEnvelopeResult result = SaveGameFormatAssert.Load(file, new DelegateSaveGameData(archive => archive.Value("text", ref text)));
        SaveGameEnvelope.TryOpen(file, out SaveGameEnvelope envelope);

        Assert.Equal(SaveGameEnvelopeStatus.Success, result.Status);
        Assert.Equal("a\uFFFDb", text);
        Assert.Equal("x\uFFFD", envelope.Metadata["k"]);
    }

    private static byte[] Utf8(string text)
    {
        return Encoding.UTF8.GetBytes(text);
    }

    /// <summary>A valid version-1 envelope around the data object's members <paramref name="dataMembers"/>.</summary>
    private static string Document(string dataMembers)
    {
        return Envelope(data: "{" + dataMembers + "}");
    }

    private static string Envelope(string containerVersion = "1", string dataVersion = "1", string metadata = "{}", string data = "{}")
    {
        return "{\"container\":\"casaengine-savegame\",\"containerVersion\":" + containerVersion
            + ",\"dataVersion\":" + dataVersion + ",\"metadata\":" + metadata + ",\"data\":" + data + "}";
    }
}
