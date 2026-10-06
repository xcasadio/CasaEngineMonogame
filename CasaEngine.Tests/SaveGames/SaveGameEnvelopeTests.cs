using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CasaEngine.Framework.SaveGames;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.SaveGames;

/// <summary>
/// T2.2 of the save-game service plan: what both formats share (round trip, determinism, metadata, format
/// detection, exception boundary) and the CRC-32. Hostile files are in <see cref="BinarySaveGameFormatTests"/> and
/// <see cref="JsonSaveGameFormatTests"/>.
/// </summary>
public sealed class SaveGameEnvelopeTests
{
    private static readonly Dictionary<string, string> SampleMetadata = new()
    {
        ["location"] = "Inoa village",
        ["playTime"] = "01:23:45",
        ["chapter"] = "\u00C9t\u00E9 \U0001F600",
        [string.Empty] = string.Empty,
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RoundTrip_AllValues_RestoresEveryFieldAndMetadata(bool binary)
    {
        SaveGameEnvelopeFormat format = FormatOf(binary);
        AllValuesSaveGameData saved = AllValuesSaveGameData.CreateFilled();

        SaveGameEnvelopeResult writeResult = SaveGameEnvelope.TryWrite(format, saved, SampleMetadata, out byte[] fileBytes);
        SaveGameEnvelopeResult openResult = SaveGameEnvelope.TryOpen(fileBytes, out SaveGameEnvelope envelope);
        var loaded = new AllValuesSaveGameData();
        SaveGameEnvelopeResult loadResult = envelope.TryDeserialize(loaded);

        Assert.Equal(SaveGameEnvelopeStatus.Success, writeResult.Status);
        Assert.Equal(SaveGameEnvelopeStatus.Success, openResult.Status);
        Assert.Equal(SaveGameEnvelopeStatus.Success, loadResult.Status);
        Assert.Equal(format, envelope.Format);
        Assert.Equal(7, envelope.DataVersion);
        Assert.Equal(SampleMetadata.OrderBy(pair => pair.Key, StringComparer.Ordinal), envelope.Metadata.OrderBy(pair => pair.Key, StringComparer.Ordinal));
        saved.AssertEqual(loaded);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RoundTrip_TwiceThroughTheFile_GivesIdenticalBytes(bool binary)
    {
        SaveGameEnvelopeFormat format = FormatOf(binary);
        SaveGameEnvelope.TryWrite(format, AllValuesSaveGameData.CreateFilled(), SampleMetadata, out byte[] first);
        var loaded = new AllValuesSaveGameData();
        SaveGameEnvelope.TryOpen(first, out SaveGameEnvelope envelope);
        envelope.TryDeserialize(loaded);

        SaveGameEnvelope.TryWrite(format, loaded, envelope.Metadata, out byte[] second);

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Write_SameObjectTwice_GivesIdenticalBytes(bool binary)
    {
        SaveGameEnvelopeFormat format = FormatOf(binary);
        AllValuesSaveGameData data = AllValuesSaveGameData.CreateFilled();

        SaveGameEnvelope.TryWrite(format, data, SampleMetadata, out byte[] first);
        SaveGameEnvelope.TryWrite(format, data, SampleMetadata, out byte[] second);

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Write_MetadataInAnyEnumerationOrder_GivesIdenticalBytes(bool binary)
    {
        SaveGameEnvelopeFormat format = FormatOf(binary);
        var forward = new Dictionary<string, string> { ["a"] = "1", ["b"] = "2", ["c"] = "3" };
        var backward = new SortedDictionary<string, string>(Comparer<string>.Create((x, y) => string.CompareOrdinal(y, x)))
        {
            ["a"] = "1",
            ["b"] = "2",
            ["c"] = "3",
        };
        var data = new DelegateSaveGameData(_ => { });

        SaveGameEnvelope.TryWrite(format, data, forward, out byte[] first);
        SaveGameEnvelope.TryWrite(format, data, backward, out byte[] second);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Json_Output_IsTheDocumentedEnvelopeWithIntegersAndLfLineEnds()
    {
        SaveGameEnvelope.TryWrite(SaveGameEnvelopeFormat.Json, AllValuesSaveGameData.CreateFilled(), SampleMetadata, out byte[] fileBytes);
        string text = new UTF8Encoding(false, true).GetString(fileBytes);
        var document = JObject.Parse(text);

        Assert.False(fileBytes.Length >= 3 && fileBytes[0] == 0xEF && fileBytes[1] == 0xBB && fileBytes[2] == 0xBF);
        Assert.DoesNotContain('\r', text);
        Assert.Equal(new[] { "container", "containerVersion", "dataVersion", "metadata", "data" }, document.Properties().Select(property => property.Name));
        Assert.Equal("casaengine-savegame", (string)document["container"]);
        Assert.Equal(JTokenType.Integer, document["containerVersion"].Type);
        Assert.Equal(1L, (long)document["containerVersion"]);
        Assert.Equal(JTokenType.Integer, document["dataVersion"].Type);
        Assert.Equal(7L, (long)document["dataVersion"]);
        Assert.Equal(JTokenType.Integer, document["data"]["uint"].Type);
        Assert.Equal(4000000000L, (long)document["data"]["uint"]);
        Assert.Equal(JTokenType.Integer, document["data"]["longs"][0].Type);
        Assert.Equal(JTokenType.Boolean, document["data"]["flag"].Type);
        Assert.Equal(JTokenType.Float, document["data"]["float"].Type);
        Assert.Equal(42L, (long)document["data"]["player"]["stats"]["hp"]);
        Assert.Contains("\"containerVersion\": 1,", text);
        Assert.Equal(new[] { string.Empty, "chapter", "location", "playTime" }, ((JObject)document["metadata"]).Properties().Select(property => property.Name));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsoDateString_RoundTripsUnchanged(bool binary)
    {
        SaveGameEnvelopeFormat format = FormatOf(binary);
        string[] dates = { "2026-09-28T12:00:00+02:00", "2026-09-28T10:00:00Z", "2026-09-28T12:00:00.1234567", "2026-09-28" };
        foreach (string date in dates)
        {
            string written = date;
            SaveGameEnvelope.TryWrite(format, new DelegateSaveGameData(archive => archive.Value("when", ref written)), null, out byte[] fileBytes);
            string read = null;

            SaveGameEnvelopeResult result = SaveGameFormatAssert.Load(fileBytes, new DelegateSaveGameData(archive => archive.Value("when", ref read)));

            Assert.Equal(SaveGameEnvelopeStatus.Success, result.Status);
            Assert.Equal(date, read);
        }
    }

    [Fact]
    public void Json_HandWrittenIsoDate_IsReadAsTheExactString()
    {
        byte[] fileBytes = Encoding.UTF8.GetBytes(
            "{\"container\":\"casaengine-savegame\",\"containerVersion\":1,\"dataVersion\":1," +
            "\"metadata\":{\"savedAt\":\"2026-09-28T12:00:00.000+02:00\"},\"data\":{\"when\":\"2026-09-28T12:00:00.000+02:00\"}}");
        string read = null;

        SaveGameEnvelopeResult result = SaveGameFormatAssert.Load(fileBytes, new DelegateSaveGameData(archive => archive.Value("when", ref read)));
        SaveGameEnvelope.TryOpen(fileBytes, out SaveGameEnvelope envelope);

        Assert.Equal(SaveGameEnvelopeStatus.Success, result.Status);
        Assert.Equal("2026-09-28T12:00:00.000+02:00", read);
        Assert.Equal("2026-09-28T12:00:00.000+02:00", envelope.Metadata["savedAt"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Open_ReadsMetadataAndDataVersionWithoutDecodingTheData(bool binary)
    {
        SaveGameEnvelopeFormat format = FormatOf(binary);
        SaveGameEnvelope.TryWrite(format, AllValuesSaveGameData.CreateFilled(), SampleMetadata, out byte[] fileBytes);

        SaveGameEnvelopeResult result = SaveGameEnvelope.TryOpen(fileBytes, out SaveGameEnvelope envelope);

        Assert.Equal(SaveGameEnvelopeStatus.Success, result.Status);
        Assert.Equal(7, envelope.DataVersion);
        Assert.Equal("Inoa village", envelope.Metadata["location"]);
        Assert.Equal(SampleMetadata.Count, envelope.Metadata.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Open_DataThatNoObjectCouldRead_StillListsTheMetadata(bool binary)
    {
        SaveGameEnvelopeFormat format = FormatOf(binary);
        // The data holds a string where the reader below expects an int: only decoding would notice.
        string text = "not a number";
        SaveGameEnvelope.TryWrite(format, new DelegateSaveGameData(archive => archive.Value("hp", ref text)), SampleMetadata, out byte[] fileBytes);
        int hp = 0;

        string message = SaveGameFormatAssert.RefusedAtData(fileBytes, new DelegateSaveGameData(archive => archive.Value("hp", ref hp)));
        SaveGameEnvelope.TryOpen(fileBytes, out SaveGameEnvelope envelope);

        Assert.Equal("Inoa village", envelope.Metadata["location"]);
        Assert.NotEmpty(message);
    }

    [Fact]
    public void Open_BinaryPayloadOfGarbage_StillListsTheMetadata()
    {
        byte[] fileBytes = BinarySaveFileBuilder.Header(metadataCount: 1)
            .String("location").String("Inoa")
            .Raw(0xFF, 0xFF, 0xFF, 0xFF, 0x80, 0x00, 0x7F)
            .Build();

        SaveGameEnvelopeResult result = SaveGameEnvelope.TryOpen(fileBytes, out SaveGameEnvelope envelope);

        Assert.Equal(SaveGameEnvelopeStatus.Success, result.Status);
        Assert.Equal(SaveGameEnvelopeFormat.Binary, envelope.Format);
        Assert.Equal("Inoa", envelope.Metadata["location"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Write_NullMetadata_WritesNone(bool binary)
    {
        SaveGameEnvelopeFormat format = FormatOf(binary);
        SaveGameEnvelope.TryWrite(format, new DelegateSaveGameData(_ => { }), null, out byte[] fileBytes);

        SaveGameEnvelope.TryOpen(fileBytes, out SaveGameEnvelope envelope);

        Assert.Empty(envelope.Metadata);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Write_NonFiniteFloat_IsInvalidDataAndNoBytes(bool binary)
    {
        SaveGameEnvelopeFormat format = FormatOf(binary);
        float speed = float.NaN;

        SaveGameEnvelopeResult result = SaveGameEnvelope.TryWrite(format, new DelegateSaveGameData(archive => archive.Value("speed", ref speed)), null, out byte[] fileBytes);

        Assert.Equal(SaveGameEnvelopeStatus.InvalidData, result.Status);
        Assert.Contains("'speed'", result.Message);
        Assert.Null(fileBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Write_NullMetadataValue_IsInvalidData(bool binary)
    {
        SaveGameEnvelopeFormat format = FormatOf(binary);
        var metadata = new Dictionary<string, string> { ["location"] = null };

        SaveGameEnvelopeResult result = SaveGameEnvelope.TryWrite(format, new DelegateSaveGameData(_ => { }), metadata, out byte[] fileBytes);

        Assert.Equal(SaveGameEnvelopeStatus.InvalidData, result.Status);
        Assert.Contains("metadata.location", result.Message);
        Assert.Null(fileBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Write_MetadataWithUnpairedSurrogate_IsInvalidData(bool binary, bool inKey)
    {
        SaveGameEnvelopeFormat format = FormatOf(binary);
        var metadata = inKey
            ? new Dictionary<string, string> { ["key\uDC00"] = "v" }
            : new Dictionary<string, string> { ["k"] = "\uD800" };

        SaveGameEnvelopeResult result = SaveGameEnvelope.TryWrite(format, new DelegateSaveGameData(_ => { }), metadata, out byte[] fileBytes);

        Assert.Equal(SaveGameEnvelopeStatus.InvalidData, result.Status);
        Assert.Null(fileBytes);
    }

    [Fact]
    public void Write_JsonFieldNameTwiceInOneObject_IsADeveloperErrorThatPropagates()
    {
        int a = 1, b = 2;
        var data = new DelegateSaveGameData(archive =>
        {
            archive.BeginObject("player");
            archive.Value("hp", ref a);
            archive.Value("hp", ref b);
            archive.EndObject();
        });

        var exception = Assert.Throws<ArgumentException>(() => SaveGameEnvelope.TryWrite(SaveGameEnvelopeFormat.Json, data, null, out _));

        Assert.Contains("'player.hp'", exception.Message);
    }

    [Fact]
    public void Write_JsonObjectAndFieldWithTheSameName_IsADeveloperErrorThatPropagates()
    {
        int a = 1;
        var data = new DelegateSaveGameData(archive =>
        {
            archive.Value("player", ref a);
            archive.BeginObject("player");
            archive.EndObject();
        });

        Assert.Throws<ArgumentException>(() => SaveGameEnvelope.TryWrite(SaveGameEnvelopeFormat.Json, data, null, out _));
    }

    [Fact]
    public void Write_BinaryFieldNameTwice_IsNotDetectedBecauseNamesAreNotStored()
    {
        int a = 1, b = 2;
        var data = new DelegateSaveGameData(archive =>
        {
            archive.Value("hp", ref a);
            archive.Value("hp", ref b);
        });

        SaveGameEnvelopeResult result = SaveGameEnvelope.TryWrite(SaveGameEnvelopeFormat.Binary, data, null, out byte[] fileBytes);

        Assert.Equal(SaveGameEnvelopeStatus.Success, result.Status);
        Assert.NotNull(fileBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Write_ExceptionFromTheGameSerialize_Propagates(bool binary)
    {
        SaveGameEnvelopeFormat format = FormatOf(binary);
        var data = new DelegateSaveGameData(_ => throw new InvalidOperationException("game bug"));

        var exception = Assert.Throws<InvalidOperationException>(() => SaveGameEnvelope.TryWrite(format, data, null, out _));

        Assert.Equal("game bug", exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Write_ObjectLeftOpen_IsADeveloperErrorThatPropagates(bool binary)
    {
        SaveGameEnvelopeFormat format = FormatOf(binary);
        var data = new DelegateSaveGameData(archive => archive.BeginObject("player"));

        Assert.Throws<InvalidOperationException>(() => SaveGameEnvelope.TryWrite(format, data, null, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Load_ExceptionFromTheGameSerialize_Propagates(bool binary)
    {
        SaveGameEnvelopeFormat format = FormatOf(binary);
        SaveGameEnvelope.TryWrite(format, AllValuesSaveGameData.CreateFilled(), null, out byte[] fileBytes);
        SaveGameEnvelope.TryOpen(fileBytes, out SaveGameEnvelope envelope);
        var target = new DelegateSaveGameData(_ => throw new InvalidOperationException("game bug"));

        var exception = Assert.Throws<InvalidOperationException>(() => envelope.TryDeserialize(target));

        Assert.Equal("game bug", exception.Message);
    }

    [Fact]
    public void Write_NegativeLatestDataVersion_IsADeveloperError()
    {
        var data = new DelegateSaveGameData(_ => { }, latestDataVersion: -1);

        Assert.Throws<ArgumentOutOfRangeException>(() => SaveGameEnvelope.TryWrite(SaveGameEnvelopeFormat.Binary, data, null, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaveGameEnvelope.TryWrite(SaveGameEnvelopeFormat.Json, data, null, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Load_OlderDataVersion_IsVisibleToTheObject(bool binary)
    {
        SaveGameEnvelopeFormat format = FormatOf(binary);
        SaveGameEnvelope.TryWrite(format, new DelegateSaveGameData(_ => { }, latestDataVersion: 2), null, out byte[] fileBytes);
        int seenVersion = -1;
        bool seenLoading = false;

        SaveGameEnvelopeResult result = SaveGameFormatAssert.Load(fileBytes, new DelegateSaveGameData(
            archive =>
            {
                seenVersion = archive.DataVersion;
                seenLoading = archive.IsLoading;
            },
            latestDataVersion: 5));

        Assert.Equal(SaveGameEnvelopeStatus.Success, result.Status);
        Assert.Equal(2, seenVersion);
        Assert.True(seenLoading);
    }

    [Fact]
    public void Open_EmptyFile_IsInvalidData()
    {
        string message = SaveGameFormatAssert.RefusedAtOpen(Array.Empty<byte>(), SaveGameEnvelopeStatus.InvalidData);

        Assert.Contains("empty", message);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("[1, 2]")]
    [InlineData("   ")]
    [InlineData("\uFEFF\uFEFF{}")]
    [InlineData("PK\u0003\u0004")]
    public void Open_NeitherMagicNorBrace_IsInvalidData(string content)
    {
        SaveGameFormatAssert.RefusedAtOpen(Encoding.UTF8.GetBytes(content), SaveGameEnvelopeStatus.InvalidData);
    }

    [Fact]
    public void Open_Utf8BomThenWhitespace_BeforeTheBinaryMagic_IsInvalidData()
    {
        SaveGameEnvelope.TryWrite(SaveGameEnvelopeFormat.Binary, new DelegateSaveGameData(_ => { }), null, out byte[] fileBytes);
        byte[] prefixed = new byte[] { 0xEF, 0xBB, 0xBF, (byte)' ' }.Concat(fileBytes).ToArray();

        SaveGameFormatAssert.RefusedAtOpen(prefixed, SaveGameEnvelopeStatus.InvalidData);
    }

    [Fact]
    public void Crc32_MatchesTheIeeeCheckValue()
    {
        Assert.Equal(0xCBF43926u, Crc32.Compute(Encoding.ASCII.GetBytes("123456789")));
        Assert.Equal(0u, Crc32.Compute(ReadOnlySpan<byte>.Empty));
        Assert.Equal(0xD202EF8Du, Crc32.Compute(new byte[] { 0 }));
    }

    [Fact]
    public void SaveGameSources_UseNoTypeDrivenSerializerNorUnboundedReader()
    {
        string folder = Path.Combine(FindRepositoryRoot(), "CasaEngine", "Framework", "SaveGames");
        string[] files = Directory.GetFiles(folder, "*.cs");
        string[] forbidden = { "TypeNameHandling", "JsonConvert.", "ToObject", "JsonSerializer", "BinaryFormatter", "ReadString(" };

        Assert.Contains(files, file => Path.GetFileName(file) == "JsonSaveGameArchive.cs");
        Assert.Contains(files, file => Path.GetFileName(file) == "BinarySaveGameArchive.cs");
        foreach (string file in files)
        {
            string source = File.ReadAllText(file);
            foreach (string token in forbidden)
            {
                Assert.False(source.Contains(token, StringComparison.Ordinal), $"{Path.GetFileName(file)} contains '{token}'.");
            }
        }
    }

    /// <summary>Theories take a bool: the format enum is internal and cannot appear in a public test signature.</summary>
    private static SaveGameEnvelopeFormat FormatOf(bool binary)
    {
        return binary ? SaveGameEnvelopeFormat.Binary : SaveGameEnvelopeFormat.Json;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.MonoGame.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("CasaEngine repository root was not found.");
    }
}
