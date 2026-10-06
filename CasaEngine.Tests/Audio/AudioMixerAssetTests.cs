using CasaEngine.Core.Logging;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Audio.Effects;
using CasaEngine.Framework.Audio.Mixing;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Audio;

/// <summary>Log-capturing tests rely on the process-global <see cref="Logs"/>, hence the serialized collection.</summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class AudioMixerAssetTests
{
    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) => Warnings.Add(msg);
        public void WriteError(string msg) => Errors.Add(msg);
    }

    private static List<string> LoadWithWarnings(AudioMixerAsset asset, JObject document)
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

    private static JObject Document(params JObject[] buses)
        => new()
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "Mixer",
            ["buses"] = new JArray(buses.Cast<object>().ToArray()),
        };

    [Fact]
    public void IsFileSupported_MatchesTheExtensionOnly()
    {
        var loader = new AudioMixerAssetLoader();

        Assert.True(loader.IsFileSupported("project.audioMixer"));
        Assert.True(loader.IsFileSupported("PROJECT.AUDIOMIXER"));
        Assert.False(loader.IsFileSupported("project.sound"));
    }

    [Fact]
    public void CreateDefault_HoldsFourBusesUnderMasterAtFullVolume()
    {
        var asset = AudioMixerAsset.CreateDefault("Project mixer");

        Assert.Equal("Project mixer", asset.Name);
        Assert.Equal(AudioMixerAsset.CurrentVersion, asset.Version);
        Assert.Equal(
            new[] { AudioBusNames.Music, AudioBusNames.Sfx, AudioBusNames.Voice, AudioBusNames.Ui },
            asset.Buses.Select(bus => bus.Name).ToArray());
        Assert.All(asset.Buses, bus =>
        {
            Assert.Equal(AudioBusNames.Master, bus.Parent);
            Assert.Equal(1f, bus.Volume);
            Assert.Empty(bus.Effects);
            Assert.Empty(bus.Sends);
        });
    }

    [Fact]
    public void New_HasADefaultNameAndNoBus()
    {
        var asset = new AudioMixerAsset();

        Assert.Equal($"Audio mixer {asset.Id}", asset.Name);
        Assert.Empty(asset.Buses);
    }

    [Fact]
    public void BusVolume_IsClampedAndNanIsIgnored()
    {
        var bus = new AudioMixerBusData { Volume = 0.5f };

        bus.Volume = 3f;
        Assert.Equal(1f, bus.Volume);
        bus.Volume = -1f;
        Assert.Equal(0f, bus.Volume);
        bus.Volume = 0.25f;
        bus.Volume = float.NaN;
        Assert.Equal(0.25f, bus.Volume);
    }

    [Fact]
    public void EffectDefaults_MatchTheEngineEffectConstructors()
    {
        var biquad = new AudioMixerBiquadEffectData();
        Assert.Equal(BiquadFilterType.LowPass, biquad.FilterType);
        Assert.Equal(1000f, biquad.FrequencyHz);
        Assert.Equal(0.70710678f, biquad.Q);
        Assert.Equal(0f, biquad.GainDb);

        var compressor = new AudioMixerCompressorEffectData();
        Assert.Equal((-18f, 4f, 6f, 0.01f, 0.1f, 0f),
            (compressor.ThresholdDb, compressor.Ratio, compressor.KneeDb, compressor.AttackSeconds, compressor.ReleaseSeconds, compressor.MakeupGainDb));

        var reverb = new AudioMixerReverbEffectData();
        Assert.Equal((0.5f, 0.5f, 1f, 0f, 1f),
            (reverb.RoomSize, reverb.Damping, reverb.Wet, reverb.Dry, reverb.StereoSeparation));

        var ducking = new AudioMixerDuckingEffectData("Voice");
        Assert.Equal(("Voice", 12f, -40f, 0.02f, 0.4f),
            (ducking.Source, ducking.DepthDb, ducking.ThresholdDb, ducking.AttackSeconds, ducking.ReleaseSeconds));
    }

    [Fact]
    public void Load_MinimalDocument_HasNoBus()
    {
        var id = Guid.NewGuid();
        var asset = new AudioMixerAsset();

        var warnings = LoadWithWarnings(asset, new JObject { ["id"] = id.ToString(), ["name"] = "Empty" });

        Assert.Empty(warnings);
        Assert.Equal(id, asset.Id);
        Assert.Equal("Empty", asset.Name);
        Assert.Empty(asset.Buses);
        Assert.Equal(AudioMixerAsset.CurrentVersion, asset.Version);
    }

    [Fact]
    public void Load_BusWithOnlyAName_UsesTheDefaults()
    {
        var asset = new AudioMixerAsset();

        var warnings = LoadWithWarnings(asset, Document(new JObject { ["name"] = "Ambience" }));

        Assert.Empty(warnings);
        var bus = Assert.Single(asset.Buses);
        Assert.Equal("Ambience", bus.Name);
        Assert.Equal(AudioBusNames.Master, bus.Parent);
        Assert.Equal(1f, bus.Volume);
    }

    [Fact]
    public void Load_ReplacesThePreviousBuses()
    {
        var asset = AudioMixerAsset.CreateDefault("x");

        LoadWithWarnings(asset, Document(new JObject { ["name"] = "Only" }));

        Assert.Equal("Only", Assert.Single(asset.Buses).Name);
    }

    [Fact]
    public void Load_SkipsBadEntriesAndDefaultsBadValues_WithOneWarningEach()
    {
        var document = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "Tolerant",
            ["buses"] = new JArray(
                42,
                new JObject { ["parent"] = "Master" },
                new JObject
                {
                    ["name"] = "Fx",
                    ["volume"] = "loud",
                    ["effects"] = new JArray(
                        "nope",
                        new JObject { ["type"] = "flanger" },
                        new JObject { ["type"] = "biquad", ["filter"] = "Wobble", ["frequency_hz"] = "high", ["q"] = 2.0 },
                        new JObject { ["type"] = "ducking" }),
                    ["sends"] = new JArray(7, new JObject { ["level"] = 0.5 }, new JObject { ["target"] = "Sfx", ["level"] = 0.5 }),
                }),
        };
        var asset = new AudioMixerAsset();

        var warnings = LoadWithWarnings(asset, document);

        var bus = Assert.Single(asset.Buses);
        Assert.Equal("Fx", bus.Name);
        Assert.Equal(1f, bus.Volume);
        var biquad = Assert.IsType<AudioMixerBiquadEffectData>(Assert.Single(bus.Effects));
        Assert.Equal(BiquadFilterType.LowPass, biquad.FilterType);
        Assert.Equal(1000f, biquad.FrequencyHz);
        Assert.Equal(2f, biquad.Q);
        Assert.Equal(new AudioMixerSendData("Sfx", 0.5f), Assert.Single(bus.Sends));

        // non-object bus, nameless bus, bad volume, non-object effect, unknown type, unknown filter,
        // bad frequency, source-less ducking, non-object send, target-less send.
        Assert.Equal(10, warnings.Count);
        Assert.All(warnings, warning => Assert.Contains("Tolerant", warning));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Load_RefusesAnUnsupportedVersion(int version)
    {
        var document = Document();
        document["version"] = version;
        var asset = new AudioMixerAsset();

        Assert.Throws<InvalidOperationException>(() => asset.Load(document));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(0)]
    public void Loader_ReturnsNullAndLogsAnUnsupportedVersion(int version)
    {
        string directory = CreateTempDirectory();
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            string path = Path.Combine(directory, "future.audioMixer");
            var document = Document();
            document["version"] = version;
            File.WriteAllText(path, document.ToString());

            var result = new AudioMixerAssetLoader().LoadAsset(path, new AssetContentManager());

            Assert.Null(result);
            Assert.NotEmpty(logger.Errors);
            Assert.Contains(logger.Errors, error => error.Contains("future.audioMixer"));
        }
        finally
        {
            Logs.Close();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void MigrateToCurrent_SetsTheVersionKeys()
    {
        var document = new JObject();

        AudioMixerAssetJsonSerializer.MigrateToCurrent(document);

        Assert.Equal(1, (int)document["version"]);
        Assert.Equal(1, (int)document["schema_version"]);
        Assert.True(AudioMixerAssetJsonSerializer.CanMigrate(1));
        Assert.False(AudioMixerAssetJsonSerializer.CanMigrate(2));
        Assert.False(AudioMixerAssetJsonSerializer.CanMigrate(0));
    }

    [Fact]
    public void RegisterLoaders_LoadsAnAudioMixerFileThroughTheContentManager()
    {
        string directory = CreateTempDirectory();
        string previousProjectPath = EngineEnvironment.ProjectPath;
        try
        {
            EngineEnvironment.ProjectPath = directory;
            var document = Document(new JObject { ["name"] = "Ambience", ["parent"] = "Sfx", ["volume"] = 0.5 });
            File.WriteAllText(Path.Combine(directory, "project.audioMixer"), document.ToString());
            var manager = new AssetContentManager();
            AssetLoaderRegistry.RegisterLoaders(manager);

            var asset = manager.LoadFromFile<AudioMixerAsset>("project.audioMixer");

            var bus = Assert.Single(asset.Buses);
            Assert.Equal(("Ambience", "Sfx", 0.5f), (bus.Name, bus.Parent, bus.Volume));
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DemoMixer_LoadsThroughTheRealLoaderAndIsWrittenAsTheEditorWritesIt()
    {
        string path = Path.Combine(DemoAudioDirectory(), "demo_mixer.audioMixer");
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        object loaded;
        try
        {
            loaded = new AudioMixerAssetLoader().LoadAsset(path, new AssetContentManager());
        }
        finally
        {
            Logs.Close();
        }

        Assert.Empty(logger.Warnings);
        Assert.Empty(logger.Errors);
        var asset = Assert.IsType<AudioMixerAsset>(loaded);
        Assert.Equal(Guid.Parse("23ddf15f-ea55-4c8b-9077-9b033f10b0f9"), asset.Id);
        Assert.Equal("demo_mixer", asset.Name);
        Assert.Equal(AudioMixerAsset.CurrentVersion, asset.Version);

        Assert.Equal(
            new[] { AudioBusNames.Music, AudioBusNames.Sfx, AudioBusNames.Voice, AudioBusNames.Ui, "DemoMixerReverb" },
            asset.Buses.Select(bus => bus.Name).ToArray());
        Assert.All(asset.Buses, bus =>
        {
            Assert.Equal(AudioBusNames.Master, bus.Parent);
            Assert.Equal(1f, bus.Volume);
        });

        var reverbBus = asset.Buses[4];
        var reverb = Assert.IsType<AudioMixerReverbEffectData>(Assert.Single(reverbBus.Effects));
        Assert.Equal(new AudioMixerReverbEffectData(0.5f, 0.5f, 1f, 0f, 1f), reverb);
        Assert.Equal(new AudioMixerReverbEffectData(), reverb);
        Assert.Empty(reverbBus.Sends);

        Assert.All(asset.Buses.Take(4), bus => Assert.Empty(bus.Effects));
        Assert.Equal(new AudioMixerSendData("DemoMixerReverb", 0.3f), Assert.Single(asset.Buses[1].Sends));
        Assert.All(asset.Buses.Where(bus => bus.Name != AudioBusNames.Sfx), bus => Assert.Empty(bus.Sends));

        Assert.Empty(AudioMixerAssetValidator.Validate(asset, null, 32).Problems);

        // The editor writer produces the same document, key order and number format included
        // (JToken.DeepEquals ignores the order of the properties, hence the text comparison).
        Assert.True(EditorAssetJsonSerializer.TrySerialize(asset, out var written));
        string fileText = File.ReadAllText(path);
        Assert.True(JToken.DeepEquals(JObject.Parse(fileText), JObject.Parse(written.ToString())));
        Assert.Equal(NormalizeJsonText(fileText), NormalizeJsonText(written.ToString()));
    }

    private static string NormalizeJsonText(string text) => text.Replace("\r\n", "\n").TrimEnd();

    private static string DemoAudioDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "CasaEngine.Demos", "Content", "Audio");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("CasaEngine.Demos/Content/Audio not found above the test output.");
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
