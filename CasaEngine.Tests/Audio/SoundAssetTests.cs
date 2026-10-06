using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Audio;

/// <summary>
/// Log-capturing tests rely on the process-global <see cref="Logs"/>, hence the serialized collection.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class SoundAssetTests
{
    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = new();

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) => Warnings.Add(msg);
        public void WriteError(string msg) { }
    }

    private static List<string> LoadWithWarnings(SoundAsset asset, JObject document)
    {
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            asset.Load(document);
            return logger.Warnings;
        }
        finally
        {
            Logs.Close();
        }
    }

    private static void AssertVariationDefaults(SoundAsset asset)
    {
        Assert.Equal(0, asset.Priority);
        Assert.Empty(asset.VariationAudioFileAssetIds);
        Assert.Equal(1f, asset.VariationVolumeMin);
        Assert.Equal(1f, asset.VariationVolumeMax);
        Assert.Equal(0f, asset.VariationPitchMin);
        Assert.Equal(0f, asset.VariationPitchMax);
    }

    private static string DemoAudioDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "CasaEngine.Demos", "Content", "Audio");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("CasaEngine.Demos/Content/Audio not found above the test output.");
    }

    public static IEnumerable<object[]> DemoSoundFiles()
    {
        foreach (var path in Directory.GetFiles(DemoAudioDirectory(), "*.sound"))
        {
            yield return new object[] { Path.GetFileName(path) };
        }
    }

    [Fact]
    public void Load_ReadsEveryVariationField()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var document = CreateDocument(Guid.NewGuid(), Guid.NewGuid());
        document["priority"] = 40;
        document["variation_audio_file_asset_ids"] = new JArray(first.ToString(), second.ToString());
        document["variation_volume_min"] = 0.5f;
        document["variation_volume_max"] = 0.9f;
        document["variation_pitch_min"] = -0.1f;
        document["variation_pitch_max"] = 0.2f;
        var asset = new SoundAsset();

        var warnings = LoadWithWarnings(asset, document);

        Assert.Empty(warnings);
        Assert.Equal(40, asset.Priority);
        Assert.Equal(new[] { first, second }, asset.VariationAudioFileAssetIds);
        Assert.Equal(0.5f, asset.VariationVolumeMin, 4);
        Assert.Equal(0.9f, asset.VariationVolumeMax, 4);
        Assert.Equal(-0.1f, asset.VariationPitchMin, 4);
        Assert.Equal(0.2f, asset.VariationPitchMax, 4);
    }

    [Fact]
    public void Load_MinimalAndSixKeyDocuments_KeepTheVariationDefaults()
    {
        var minimal = new SoundAsset();
        var minimalWarnings = LoadWithWarnings(minimal, new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "minimal",
        });

        var sixKeys = new SoundAsset();
        var sixKeyWarnings = LoadWithWarnings(sixKeys, new JObject
        {
            ["id"] = "b41f0a6c-2d58-4a19-9f73-0c5e8a91d2b4",
            ["name"] = "menu_click",
            ["audio_file_asset_id"] = "3f8c1b52-9a47-4c6e-a0d5-1e7b24c9f018",
            ["volume"] = 1.0,
            ["pitch"] = 0.0,
            ["is_looped"] = false,
            ["bus_name"] = "Sfx",
            ["is_streaming"] = false,
        });

        Assert.Empty(minimalWarnings);
        Assert.Empty(sixKeyWarnings);
        AssertVariationDefaults(minimal);
        AssertVariationDefaults(sixKeys);
    }

    [Theory]
    [InlineData("priority", JTokenType.Null)]
    [InlineData("priority", JTokenType.String)]
    [InlineData("priority", JTokenType.Object)]
    [InlineData("variation_volume_min", JTokenType.Null)]
    [InlineData("variation_volume_min", JTokenType.String)]
    [InlineData("variation_volume_min", JTokenType.Object)]
    [InlineData("variation_pitch_max", JTokenType.Null)]
    [InlineData("variation_pitch_max", JTokenType.String)]
    [InlineData("variation_pitch_max", JTokenType.Object)]
    [InlineData("variation_audio_file_asset_ids", JTokenType.Null)]
    [InlineData("variation_audio_file_asset_ids", JTokenType.String)]
    [InlineData("variation_audio_file_asset_ids", JTokenType.Object)]
    public void Load_BadVariationValue_KeepsTheDefaultAndWarnsOnce(string key, JTokenType badType)
    {
        var id = Guid.NewGuid();
        var audioFileAssetId = Guid.NewGuid();
        var document = CreateDocument(id, audioFileAssetId);
        document[key] = badType switch
        {
            JTokenType.Null => JValue.CreateNull(),
            JTokenType.String => new JValue("oops"),
            _ => new JObject(),
        };
        var asset = new SoundAsset();

        var warnings = LoadWithWarnings(asset, document);

        var warning = Assert.Single(warnings);
        Assert.Contains(key, warning);
        Assert.Contains("footstep", warning);
        AssertVariationDefaults(asset);
        Assert.Equal(id, asset.Id);
        Assert.Equal(audioFileAssetId, asset.AudioFileAssetId);
        Assert.Equal(0.75f, asset.Volume, 4);
        Assert.True(asset.IsStreaming);
    }

    [Fact]
    public void Load_InvalidGuidEntry_IsIgnoredAndTheOthersAreKept()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var document = CreateDocument(Guid.NewGuid(), Guid.NewGuid());
        document["variation_audio_file_asset_ids"] = new JArray(first.ToString(), "not-a-guid", 12, second.ToString());
        var asset = new SoundAsset();

        var warnings = LoadWithWarnings(asset, document);

        Assert.Equal(2, warnings.Count);
        Assert.Equal(new[] { first, second }, asset.VariationAudioFileAssetIds);
    }

    [Fact]
    public void Load_ClearsTheVariationListBeforeReading()
    {
        var asset = new SoundAsset();
        asset.VariationAudioFileAssetIds.Add(Guid.NewGuid());

        asset.Load(CreateDocument(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Empty(asset.VariationAudioFileAssetIds);
    }

    [Fact]
    public void Load_OutOfRangeVariationValues_AreClamped()
    {
        var document = CreateDocument(Guid.NewGuid(), Guid.NewGuid());
        document["priority"] = 500;
        document["variation_volume_min"] = -1;
        document["variation_volume_max"] = 9;
        document["variation_pitch_min"] = -3;
        document["variation_pitch_max"] = 3;
        var asset = new SoundAsset();

        asset.Load(document);

        Assert.Equal(SoundAsset.MaxPriority, asset.Priority);
        Assert.Equal(0f, asset.VariationVolumeMin);
        Assert.Equal(1f, asset.VariationVolumeMax);
        Assert.Equal(-1f, asset.VariationPitchMin);
        Assert.Equal(1f, asset.VariationPitchMax);
    }

    [Theory]
    [InlineData(-3, 0)]
    [InlineData(0, 0)]
    [InlineData(37, 37)]
    [InlineData(500, 100)]
    public void Priority_IsClamped(int value, int expected)
    {
        Assert.Equal(expected, new SoundAsset { Priority = value }.Priority);
    }

    [Theory]
    [InlineData(9f, 1f)]
    [InlineData(-1f, 0f)]
    public void VariationVolume_IsClamped(float value, float expected)
    {
        var asset = new SoundAsset { VariationVolumeMin = value, VariationVolumeMax = value };

        Assert.Equal(expected, asset.VariationVolumeMin);
        Assert.Equal(expected, asset.VariationVolumeMax);
    }

    [Theory]
    [InlineData(3f, 1f)]
    [InlineData(-3f, -1f)]
    public void VariationPitch_IsClamped(float value, float expected)
    {
        var asset = new SoundAsset { VariationPitchMin = value, VariationPitchMax = value };

        Assert.Equal(expected, asset.VariationPitchMin);
        Assert.Equal(expected, asset.VariationPitchMax);
    }

    [Fact]
    public void Variation_NaN_KeepsThePreviousValue()
    {
        var asset = new SoundAsset
        {
            VariationVolumeMin = 0.4f,
            VariationVolumeMax = 0.6f,
            VariationPitchMin = -0.2f,
            VariationPitchMax = 0.2f,
        };

        asset.VariationVolumeMin = float.NaN;
        asset.VariationVolumeMax = float.NaN;
        asset.VariationPitchMin = float.NaN;
        asset.VariationPitchMax = float.NaN;

        Assert.Equal(0.4f, asset.VariationVolumeMin);
        Assert.Equal(0.6f, asset.VariationVolumeMax);
        Assert.Equal(-0.2f, asset.VariationPitchMin);
        Assert.Equal(0.2f, asset.VariationPitchMax);
    }

    [Fact]
    public void Variation_InvertedRange_IsKeptAsIs()
    {
        var asset = new SoundAsset
        {
            VariationVolumeMin = 0.9f,
            VariationVolumeMax = 0.3f,
            VariationPitchMin = 0.5f,
            VariationPitchMax = -0.5f,
        };

        Assert.Equal(0.9f, asset.VariationVolumeMin);
        Assert.Equal(0.3f, asset.VariationVolumeMax);
        Assert.Equal(0.5f, asset.VariationPitchMin);
        Assert.Equal(-0.5f, asset.VariationPitchMax);
    }

    [Theory]
    [MemberData(nameof(DemoSoundFiles))]
    public void Loader_DemoSoundFiles_HaveTheVariationDefaults(string fileName)
    {
        var path = Path.Combine(DemoAudioDirectory(), fileName);

        var asset = Assert.IsType<SoundAsset>(new SoundAssetLoader().LoadAsset(path, null));

        AssertVariationDefaults(asset);
    }

    private static JObject CreateDocument(Guid id, Guid audioFileAssetId)
    {
        return new JObject
        {
            ["id"] = id.ToString(),
            ["name"] = "footstep",
            ["audio_file_asset_id"] = audioFileAssetId.ToString(),
            ["volume"] = 0.75f,
            ["pitch"] = -0.25f,
            ["is_looped"] = true,
            ["bus_name"] = AudioBusNames.Ui,
            ["is_streaming"] = true,
        };
    }

    [Fact]
    public void Load_ReadsEveryField()
    {
        var id = Guid.NewGuid();
        var audioFileAssetId = Guid.NewGuid();
        var asset = new SoundAsset();

        asset.Load(CreateDocument(id, audioFileAssetId));

        Assert.Equal(id, asset.Id);
        Assert.Equal("footstep", asset.Name);
        Assert.Equal(audioFileAssetId, asset.AudioFileAssetId);
        Assert.Equal(0.75f, asset.Volume, 4);
        Assert.Equal(-0.25f, asset.Pitch, 4);
        Assert.True(asset.IsLooped);
        Assert.Equal(AudioBusNames.Ui, asset.BusName);
        Assert.True(asset.IsStreaming);
    }

    [Fact]
    public void Load_AppliesTheDefaultsOnAMinimalDocument()
    {
        var asset = new SoundAsset();

        asset.Load(new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "minimal",
        });

        Assert.Equal(Guid.Empty, asset.AudioFileAssetId);
        Assert.Equal(1f, asset.Volume);
        Assert.Equal(0f, asset.Pitch);
        Assert.False(asset.IsLooped);
        Assert.Equal(AudioBusNames.Sfx, asset.BusName);
        Assert.False(asset.IsStreaming);
    }

    [Fact]
    public void NewAsset_IsPlayableAsIs()
    {
        var asset = new SoundAsset();

        Assert.Equal(1f, asset.Volume);
        Assert.Equal(AudioBusNames.Sfx, asset.BusName);
        Assert.Equal(AudioVoiceParameters.Default, asset.CreateVoiceParameters());
    }

    [Theory]
    [InlineData(4f, 1f)]
    [InlineData(-1f, 0f)]
    public void Volume_IsClamped(float value, float expected)
    {
        var asset = new SoundAsset { Volume = value };

        Assert.Equal(expected, asset.Volume);
    }

    [Theory]
    [InlineData(9f, 1f)]
    [InlineData(-9f, -1f)]
    public void Pitch_IsClamped(float value, float expected)
    {
        var asset = new SoundAsset { Pitch = value };

        Assert.Equal(expected, asset.Pitch);
    }

    [Fact]
    public void NaN_KeepsThePreviousValue()
    {
        var asset = new SoundAsset { Volume = 0.5f, Pitch = 0.25f };

        asset.Volume = float.NaN;
        asset.Pitch = float.NaN;

        Assert.Equal(0.5f, asset.Volume);
        Assert.Equal(0.25f, asset.Pitch);
    }

    [Fact]
    public void EmptyBusName_FallsBackToSfx()
    {
        var asset = new SoundAsset { BusName = "   " };

        Assert.Equal(AudioBusNames.Sfx, asset.BusName);
    }

    [Fact]
    public void CreateVoiceParameters_MirrorsTheAsset()
    {
        var asset = new SoundAsset
        {
            Volume = 0.5f,
            Pitch = 0.25f,
            IsLooped = true,
        };

        var parameters = asset.CreateVoiceParameters();

        Assert.Equal(0.5f, parameters.Volume, 4);
        Assert.Equal(0.25f, parameters.Pitch, 4);
        Assert.Equal(0f, parameters.Pan);
        Assert.True(parameters.IsLooped);
    }

    [Theory]
    [InlineData("weapon.sound", true)]
    [InlineData("weapon.SOUND", true)]
    [InlineData("weapon.wav", false)]
    [InlineData("weapon.particle", false)]
    public void Loader_OnlySupportsTheSoundExtension(string fileName, bool expected)
    {
        Assert.Equal(expected, new SoundAssetLoader().IsFileSupported(fileName));
    }

    [Fact]
    public void Loader_ReturnsNullInsteadOfThrowingOnABrokenDocument()
    {
        var path = Path.Combine(Path.GetTempPath(), $"casaengine-broken-{Guid.NewGuid():N}.sound");
        File.WriteAllText(path, "{ this is not json");

        try
        {
            Assert.Null(new SoundAssetLoader().LoadAsset(path, null));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Loader_ReadsARealDocumentFromDisk()
    {
        var id = Guid.NewGuid();
        var audioFileAssetId = Guid.NewGuid();
        var path = Path.Combine(Path.GetTempPath(), $"casaengine-{Guid.NewGuid():N}.sound");
        File.WriteAllText(path, CreateDocument(id, audioFileAssetId).ToString());

        try
        {
            var asset = Assert.IsType<SoundAsset>(new SoundAssetLoader().LoadAsset(path, null));

            Assert.Equal(id, asset.Id);
            Assert.Equal(audioFileAssetId, asset.AudioFileAssetId);
            Assert.True(asset.IsStreaming);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
