using CasaEngine.Core.Logging;
using CasaEngine.EditorServices.Audio;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Effects;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Configuration.Project;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Audio;

/// <summary>Application of the project's mixer asset (plan decision P48). Touches the global catalog, project path and logs.</summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class ProjectAudioMixerTests
{
    private sealed class CapturingLogger : ILogger
    {
        public List<string> Infos { get; } = new();
        public List<string> Warnings { get; } = new();

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) => Infos.Add(msg);
        public void WriteWarning(string msg) => Warnings.Add(msg);
        public void WriteError(string msg) { }
    }

    /// <summary>A temporary project folder with its catalog and a content manager reading it.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly string _previousProjectPath = EngineEnvironment.ProjectPath;
        private readonly List<(Guid Id, string Name, string File)> _entries = new();
        private readonly CapturingLogger _logger = new();

        public Fixture()
        {
            Directory = CreateDirectory();
            Service = new AudioService(new FakeAudioBackend());
            Context = new EngineRuntimeContext(new ProjectSettings(), Directory, AssetCatalog.Get);
            Assets = new AssetContentManager { RuntimeContext = Context };
            AssetLoaderRegistry.RegisterLoaders(Assets);
            Mixer = new ProjectAudioMixer(Service, Assets);
            Logs.AddLogger(_logger);
        }

        public string Directory { get; }
        public AudioService Service { get; }
        public EngineRuntimeContext Context { get; }
        public AssetContentManager Assets { get; }
        public ProjectAudioMixer Mixer { get; }
        public List<string> Warnings => _logger.Warnings;
        public List<string> Infos => _logger.Infos;

        public static string CreateDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame", Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            return directory;
        }

        public Guid AddAsset(string name, string content)
        {
            var id = Guid.NewGuid();
            string file = name + ".audioMixer";
            File.WriteAllText(Path.Combine(Directory, file), content);
            _entries.Add((id, name, file));
            WriteCatalog();
            return id;
        }

        public Guid AddAsset(string name, AudioMixerAsset asset)
        {
            return AddAsset(name, Serialize(name, asset));
        }

        public static string Serialize(string name, AudioMixerAsset asset)
        {
            var node = new JObject();
            EditorAudioMixerAssetJsonWriter.Save(asset, node);
            return node.ToString();
        }

        private void WriteCatalog()
        {
            var infos = new JArray();
            foreach (var entry in _entries)
            {
                infos.Add(new JObject
                {
                    ["id"] = entry.Id.ToString(),
                    ["name"] = entry.Name,
                    ["file_name"] = entry.File,
                    ["asset_type"] = "audioMixer",
                });
            }

            string catalogFile = Path.Combine(Directory, "AssetInfos.json");
            File.WriteAllText(catalogFile, new JObject { ["asset_infos"] = infos }.ToString());
            AssetCatalog.Load(catalogFile);
        }

        public void Dispose()
        {
            Logs.Close();
            AssetCatalog.ClearInternal();
            EngineEnvironment.ProjectPath = _previousProjectPath;
            Service.Dispose();
            try
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static ProjectSettings Setting(string value) => new() { AudioMixerAsset = value };

    private static AudioMixerAsset Asset(params AudioMixerBusData[] buses)
    {
        var asset = new AudioMixerAsset { Name = "Project mixer" };
        asset.Buses.AddRange(buses);
        return asset;
    }

    private static AudioMixerBusData Bus(string name, string parent, float volume)
        => new() { Name = name, Parent = parent, Volume = volume };

    private static AudioMixerAsset SfxHalfWithFilter()
    {
        var sfx = Bus("Sfx", "Master", 0.5f);
        sfx.Effects.Add(new AudioMixerBiquadEffectData(BiquadFilterType.HighPass, 300f));
        return Asset(sfx, Bus("Ambience", "Music", 0.25f));
    }

    private static AudioMixerAsset MusicQuarter() => Asset(Bus("Music", "Master", 0.25f));

    [Fact]
    public void Apply_ABlankSetting_ChangesNothing()
    {
        using var fixture = new Fixture();
        int version = fixture.Service.Mixer.Version;

        fixture.Mixer.Apply(Setting(""));
        fixture.Mixer.Apply(Setting("   "));
        fixture.Mixer.Apply(null);

        Assert.False(fixture.Mixer.Applier.IsApplied);
        Assert.Equal(Guid.Empty, fixture.Mixer.AppliedAssetId);
        Assert.Equal(version, fixture.Service.Mixer.Version);
        Assert.Empty(fixture.Warnings);
        Assert.Empty(fixture.Infos);
    }

    [Fact]
    public void Apply_ById_AppliesTheAsset_AndLogsOneInfo()
    {
        using var fixture = new Fixture();
        Guid id = fixture.AddAsset("Project", SfxHalfWithFilter());

        fixture.Mixer.Apply(Setting(id.ToString()));

        Assert.Equal(id, fixture.Mixer.AppliedAssetId);
        Assert.True(fixture.Mixer.Applier.IsApplied);
        Assert.Equal(0.5f, fixture.Service.Mixer.GetBus("Sfx").Volume);
        Assert.Equal(0.25f, fixture.Service.Mixer.GetBus("Ambience").Volume);
        Assert.Single(fixture.Service.Mixer.GetBus("Sfx").Effects);
        // The fake backend has no bus support, which the applier says once; nothing says the setting failed.
        Assert.DoesNotContain(fixture.Warnings, warning => warning.Contains("could not be applied"));
        Assert.Contains(fixture.Infos, info => info.Contains("Project") && info.Contains("2 buses") && info.Contains("0 problems"));
    }

    [Fact]
    public void Apply_ByName_AppliesTheAsset()
    {
        using var fixture = new Fixture();
        Guid id = fixture.AddAsset("Project", SfxHalfWithFilter());

        fixture.Mixer.Apply(Setting("Project"));

        Assert.Equal(id, fixture.Mixer.AppliedAssetId);
        Assert.Equal(0.5f, fixture.Service.Mixer.GetBus("Sfx").Volume);
    }

    [Theory]
    [InlineData("3f2a5f0e-0000-4000-8000-000000000001")]
    [InlineData("No such asset")]
    public void Apply_AnUnknownAsset_WarnsAndKeepsTheDefaultMixer(string setting)
    {
        using var fixture = new Fixture();
        fixture.AddAsset("Project", SfxHalfWithFilter());
        int busCount = fixture.Service.Mixer.Buses.Count;

        fixture.Mixer.Apply(Setting(setting));

        Assert.Equal(Guid.Empty, fixture.Mixer.AppliedAssetId);
        Assert.False(fixture.Mixer.Applier.IsApplied);
        Assert.Equal(busCount, fixture.Service.Mixer.Buses.Count);
        Assert.Equal(1f, fixture.Service.Mixer.GetBus("Sfx").Volume);
        Assert.Contains(fixture.Warnings, warning => warning.Contains(setting));
    }

    [Theory]
    [InlineData("truncated", "{\"name\":\"x\",\"buses\":[{\"name\":\"A\"")]
    [InlineData("invalid", "this is not json")]
    [InlineData("future", "{\"name\":\"x\",\"version\":2,\"buses\":[{\"name\":\"A\"}]}")]
    public void Apply_AnUnreadableFile_DoesNotThrow_WarnsAndKeepsTheDefaultMixer(string name, string content)
    {
        using var fixture = new Fixture();
        fixture.AddAsset(name, content);

        fixture.Mixer.Apply(Setting(name));

        Assert.Equal(Guid.Empty, fixture.Mixer.AppliedAssetId);
        Assert.False(fixture.Mixer.Applier.IsApplied);
        Assert.False(fixture.Service.Mixer.TryGetBus("A", out _));
        Assert.Equal(1f, fixture.Service.Mixer.GetBus("Sfx").Volume);
        Assert.Contains(fixture.Warnings, warning => warning.Contains(name));
    }

    [Fact]
    public void Apply_AFailureAfterASuccess_ReleasesTheFirstAsset()
    {
        using var fixture = new Fixture();
        Guid id = fixture.AddAsset("Project", SfxHalfWithFilter());
        fixture.Mixer.Apply(Setting(id.ToString()));

        fixture.Mixer.Apply(Setting("No such asset"));

        Assert.Equal(Guid.Empty, fixture.Mixer.AppliedAssetId);
        Assert.Equal(1f, fixture.Service.Mixer.GetBus("Sfx").Volume);
        Assert.Empty(fixture.Service.Mixer.GetBus("Sfx").Effects);
    }

    [Fact]
    public void Apply_AThenBThenBlank_RestoresTheVolumesAndEffects()
    {
        using var fixture = new Fixture();
        fixture.AddAsset("A", SfxHalfWithFilter());
        Guid b = fixture.AddAsset("B", MusicQuarter());

        fixture.Mixer.Apply(Setting("A"));
        Assert.Equal(0.5f, fixture.Service.Mixer.GetBus("Sfx").Volume);

        fixture.Mixer.Apply(Setting("B"));
        Assert.Equal(b, fixture.Mixer.AppliedAssetId);
        Assert.Equal(1f, fixture.Service.Mixer.GetBus("Sfx").Volume);
        Assert.Empty(fixture.Service.Mixer.GetBus("Sfx").Effects);
        Assert.Equal(0.25f, fixture.Service.Mixer.GetBus("Music").Volume);

        fixture.Mixer.Apply(Setting(""));
        Assert.Equal(Guid.Empty, fixture.Mixer.AppliedAssetId);
        Assert.False(fixture.Mixer.Applier.IsApplied);
        Assert.Equal(1f, fixture.Service.Mixer.GetBus("Music").Volume);
        Assert.Equal(1f, fixture.Service.Mixer.GetBus("Sfx").Volume);
    }

    [Fact]
    public void Apply_RereadsTheFile_OnEveryCall()
    {
        using var fixture = new Fixture();
        fixture.AddAsset("A", Asset(Bus("Sfx", "Master", 0.5f)));
        fixture.Mixer.Apply(Setting("A"));
        Assert.Equal(0.5f, fixture.Service.Mixer.GetBus("Sfx").Volume);

        File.WriteAllText(Path.Combine(fixture.Directory, "A.audioMixer"), Fixture.Serialize("A", Asset(Bus("Sfx", "Master", 0.75f))));
        fixture.Mixer.Apply(Setting("A"));

        Assert.Equal(0.75f, fixture.Service.Mixer.GetBus("Sfx").Volume);
    }

    [Fact]
    public void Apply_TwoProjectFolders_AreReadByTheSameManagerAfterItsProjectPathChanges()
    {
        using var fixture = new Fixture();
        string second = Fixture.CreateDirectory();
        try
        {
            Guid firstId = fixture.AddAsset("Project", Asset(Bus("Sfx", "Master", 0.5f)));
            fixture.Mixer.Apply(Setting("Project"));
            Assert.Equal(firstId, fixture.Mixer.AppliedAssetId);
            Assert.Equal(0.5f, fixture.Service.Mixer.GetBus("Sfx").Volume);

            // Second project: its own catalog and its own file under the same name.
            File.WriteAllText(Path.Combine(second, "Project.audioMixer"), Fixture.Serialize("Project", Asset(Bus("Sfx", "Master", 0.125f))));
            Guid secondId = Guid.NewGuid();
            string catalogFile = Path.Combine(second, "AssetInfos.json");
            File.WriteAllText(
                catalogFile,
                new JObject
                {
                    ["asset_infos"] = new JArray(new JObject
                    {
                        ["id"] = secondId.ToString(),
                        ["name"] = "Project",
                        ["file_name"] = "Project.audioMixer",
                        ["asset_type"] = "audioMixer",
                    }),
                }.ToString());
            AssetCatalog.Load(catalogFile);
            fixture.Context.ProjectPath = second;

            fixture.Mixer.Apply(Setting("Project"));

            Assert.Equal(secondId, fixture.Mixer.AppliedAssetId);
            Assert.Equal(0.125f, fixture.Service.Mixer.GetBus("Sfx").Volume);
        }
        finally
        {
            Directory.Delete(second, recursive: true);
        }
    }

    [Fact]
    public void TryResolveAssetId_AcceptsAnIdOrACatalogName()
    {
        using var fixture = new Fixture();
        Guid id = fixture.AddAsset("Project", MusicQuarter());
        var other = Guid.NewGuid();

        Assert.True(ProjectAudioMixer.TryResolveAssetId(other.ToString(), out Guid parsed));
        Assert.Equal(other, parsed);
        Assert.True(ProjectAudioMixer.TryResolveAssetId("Project", out Guid byName));
        Assert.Equal(id, byName);
        Assert.False(ProjectAudioMixer.TryResolveAssetId("Missing", out Guid missing));
        Assert.Equal(Guid.Empty, missing);
        Assert.False(ProjectAudioMixer.TryResolveAssetId("", out _));
    }
}
