using CasaEngine.Core.Logging;
using CasaEngine.EditorServices.Audio;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio.Mixing;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

/// <summary>
/// The bus list of the sound inspector (plan decisions D21 and P57): <see cref="SoundBusChoices.Resolve"/> is a pure function of
/// the project's mixer asset and the bus of the sound, <see cref="SoundBusChoices.TryLoadProjectMixer"/> reads that asset from a
/// temporary project folder. The catalog, the project path, the project setting and the log are global state, hence the
/// serialized collection and the restore in <see cref="Dispose"/>.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class SoundBusChoicesTests : IDisposable
{
    private sealed class CapturingLogger : ILogger
    {
        public List<string> Lines { get; } = new();

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) => Lines.Add("warning: " + msg);
        public void WriteError(string msg) => Lines.Add("error: " + msg);
    }

    private readonly string _projectDirectory;
    private readonly string _previousProjectPath = EngineEnvironment.ProjectPath;
    private readonly string _previousSetting = GameSettings.ProjectSettings.AudioMixerAsset;
    private readonly CapturingLogger _logger = new();

    public SoundBusChoicesTests()
    {
        _projectDirectory = Path.Combine(Path.GetTempPath(), "casa-sound-bus-choices-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectDirectory);
        EngineEnvironment.ProjectPath = _projectDirectory;
        AssetCatalog.ClearInternal();
        Logs.AddLogger(_logger);
    }

    public void Dispose()
    {
        Logs.Close();
        GameSettings.ProjectSettings.AudioMixerAsset = _previousSetting;
        EngineEnvironment.ProjectPath = _previousProjectPath;
        AssetCatalog.ClearInternal();

        try
        {
            Directory.Delete(_projectDirectory, recursive: true);
        }
        catch (IOException)
        {
            // A temporary folder left behind is harmless.
        }
    }

    private static AudioMixerAsset Asset(params string[] busNames)
    {
        var asset = new AudioMixerAsset { Name = "Project mixer" };
        foreach (string name in busNames)
        {
            asset.Buses.Add(new AudioMixerBusData { Name = name, Parent = AudioBusNames.Master });
        }

        return asset;
    }

    private static string[] Names(IEnumerable<SoundBusChoice> choices) => choices.Select(choice => choice.Name).ToArray();

    private Guid AddMixerFile(string name, AudioMixerAsset asset)
    {
        var node = new JObject();
        EditorAudioMixerAssetJsonWriter.Save(asset, node);
        return AddMixerFile(name, node.ToString());
    }

    private Guid AddMixerFile(string name, string content)
    {
        var info = new AssetInfo { Name = name, FileName = name + ".audioMixer" };
        File.WriteAllText(Path.Combine(_projectDirectory, info.FileName), content);
        AssetCatalog.AddInternal(info);
        return info.Id;
    }

    // ----- Resolve -----

    [Fact]
    public void WithoutAnAsset_TheListIsTheFourBusesOfTheEngine_InTheCurrentOrder()
    {
        List<SoundBusChoice> choices = SoundBusChoices.Resolve(null, AudioBusNames.Sfx);

        Assert.Equal(new[] { "Sfx", "Music", "Voice", "Ui" }, Names(choices));
        Assert.All(choices, choice => Assert.False(choice.IsUnknown));
    }

    [Fact]
    public void WithAnAsset_TheEngineBusesComeFirst_ThenTheOtherBusesInFileOrder_NeverMasterNorEditor()
    {
        AudioMixerAsset asset = Asset("Zeta", AudioBusNames.Music, "Master", "Alpha", "Editor", "ALPHA", "  ", AudioBusNames.Sfx, "Beta");

        List<SoundBusChoice> choices = SoundBusChoices.Resolve(asset, AudioBusNames.Sfx);

        // File order for the extra buses (Zeta before Alpha), no engine bus twice, no duplicate, no reserved bus, no blank name.
        Assert.Equal(new[] { "Sfx", "Music", "Voice", "Ui", "Zeta", "Alpha", "Beta" }, Names(choices));
        Assert.All(choices, choice => Assert.False(choice.IsUnknown));
    }

    [Fact]
    public void AnAssetWithoutMusic_AndASoundOnMusic_IsNotMarkedUnknown()
    {
        List<SoundBusChoice> choices = SoundBusChoices.Resolve(Asset("Ambience"), AudioBusNames.Music);

        Assert.Equal(new[] { "Sfx", "Music", "Voice", "Ui", "Ambience" }, Names(choices));
        Assert.DoesNotContain(choices, choice => choice.IsUnknown);
    }

    [Fact]
    public void ABusInNeitherPart_IsOneMoreEntryAtTheEnd_MarkedUnknown()
    {
        List<SoundBusChoice> choices = SoundBusChoices.Resolve(Asset("Ambience"), "Gone");

        Assert.Equal(new[] { "Sfx", "Music", "Voice", "Ui", "Ambience", "Gone" }, Names(choices));
        Assert.Equal(new SoundBusChoice("Gone", true), choices[^1]);
        Assert.Single(choices, choice => choice.IsUnknown);
    }

    [Fact]
    public void ABusInNeitherPart_WithoutAnAsset_IsMarkedUnknown()
    {
        List<SoundBusChoice> choices = SoundBusChoices.Resolve(null, "Gone");

        Assert.Equal(new[] { "Sfx", "Music", "Voice", "Ui", "Gone" }, Names(choices));
        Assert.True(choices[^1].IsUnknown);
    }

    [Theory]
    [InlineData("sfx")]
    [InlineData("MUSIC")]
    [InlineData("ambience")]
    [InlineData("AMBIENCE")]
    public void AKnownBusWithADifferentCase_IsNotMarkedUnknown(string currentBus)
    {
        List<SoundBusChoice> choices = SoundBusChoices.Resolve(Asset("Ambience"), currentBus);

        Assert.Equal(5, choices.Count);
        Assert.DoesNotContain(choices, choice => choice.IsUnknown);
    }

    [Theory]
    [InlineData("Master")]
    [InlineData("Editor")]
    public void AReservedBus_IsNotListed_SoASoundOnItIsMarkedUnknown(string currentBus)
    {
        List<SoundBusChoice> choices = SoundBusChoices.Resolve(Asset("Master", "Editor"), currentBus);

        Assert.Equal(new[] { "Sfx", "Music", "Voice", "Ui", currentBus }, Names(choices));
        Assert.True(choices[^1].IsUnknown);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ABlankCurrentBus_AddsNoEntry(string currentBus)
    {
        List<SoundBusChoice> choices = SoundBusChoices.Resolve(Asset("Ambience"), currentBus);

        Assert.Equal(new[] { "Sfx", "Music", "Voice", "Ui", "Ambience" }, Names(choices));
    }

    [Fact]
    public void EachCall_GivesAFreshList()
    {
        List<SoundBusChoice> first = SoundBusChoices.Resolve(null, "Sfx");
        first.Add(new SoundBusChoice("Added", false));

        Assert.Equal(4, SoundBusChoices.Resolve(null, "Sfx").Count);
    }

    // ----- TryLoadProjectMixer -----

    [Fact]
    public void ABlankSetting_IsNoAsset_AndLogsNothing()
    {
        GameSettings.ProjectSettings.AudioMixerAsset = string.Empty;

        Assert.False(SoundBusChoices.TryLoadProjectMixer(out AudioMixerAsset asset));
        Assert.Null(asset);
        Assert.Empty(_logger.Lines);
    }

    [Fact]
    public void TheSetting_ByNameOrById_LoadsTheAssetFromTheProjectFolder()
    {
        Guid id = AddMixerFile("Project", Asset("Ambience", "Cinematics"));

        GameSettings.ProjectSettings.AudioMixerAsset = "Project";
        Assert.True(SoundBusChoices.TryLoadProjectMixer(out AudioMixerAsset byName));
        Assert.Equal(new[] { "Ambience", "Cinematics" }, byName.Buses.Select(bus => bus.Name).ToArray());

        GameSettings.ProjectSettings.AudioMixerAsset = "  " + id.ToString() + " ";
        Assert.True(SoundBusChoices.TryLoadProjectMixer(out AudioMixerAsset byId));
        Assert.Equal(new[] { "Ambience", "Cinematics" }, byId.Buses.Select(bus => bus.Name).ToArray());

        Assert.Empty(_logger.Lines);

        // The loaded asset feeds Resolve.
        Assert.Equal(
            new[] { "Sfx", "Music", "Voice", "Ui", "Ambience", "Cinematics" },
            Names(SoundBusChoices.Resolve(byName, AudioBusNames.Sfx)));
    }

    [Fact]
    public void TheFileIsReadAgainOnEachCall()
    {
        AddMixerFile("Project", Asset("Ambience"));
        GameSettings.ProjectSettings.AudioMixerAsset = "Project";
        Assert.True(SoundBusChoices.TryLoadProjectMixer(out AudioMixerAsset before));

        AddMixerFileContent("Project", Asset("Ambience", "Cinematics"));

        Assert.True(SoundBusChoices.TryLoadProjectMixer(out AudioMixerAsset after));
        Assert.Single(before.Buses);
        Assert.Equal(2, after.Buses.Count);
    }

    private void AddMixerFileContent(string name, AudioMixerAsset asset)
    {
        var node = new JObject();
        EditorAudioMixerAssetJsonWriter.Save(asset, node);
        File.WriteAllText(Path.Combine(_projectDirectory, name + ".audioMixer"), node.ToString());
    }

    [Fact]
    public void ASettingThatNamesNothing_IsNoAsset_WithOneWarning()
    {
        AddMixerFile("Project", Asset("Ambience"));

        GameSettings.ProjectSettings.AudioMixerAsset = "Missing";
        Assert.False(SoundBusChoices.TryLoadProjectMixer(out AudioMixerAsset byName));
        Assert.Null(byName);
        Assert.Single(_logger.Lines);
        Assert.StartsWith("warning: ", _logger.Lines[0], StringComparison.Ordinal);
        Assert.Contains("Missing", _logger.Lines[0], StringComparison.Ordinal);

        _logger.Lines.Clear();
        GameSettings.ProjectSettings.AudioMixerAsset = Guid.NewGuid().ToString();
        Assert.False(SoundBusChoices.TryLoadProjectMixer(out AudioMixerAsset byId));
        Assert.Null(byId);
        Assert.Single(_logger.Lines);
        Assert.StartsWith("warning: ", _logger.Lines[0], StringComparison.Ordinal);

        // The list the inspector then shows is the default one.
        Assert.Equal(new[] { "Sfx", "Music", "Voice", "Ui" }, Names(SoundBusChoices.Resolve(byId, AudioBusNames.Sfx)));
    }

    [Fact]
    public void AMissingFile_IsNoAsset_WithOneWarning()
    {
        Guid id = AddMixerFile("Project", Asset("Ambience"));
        File.Delete(Path.Combine(_projectDirectory, "Project.audioMixer"));

        GameSettings.ProjectSettings.AudioMixerAsset = id.ToString();

        Assert.False(SoundBusChoices.TryLoadProjectMixer(out AudioMixerAsset asset));
        Assert.Null(asset);
        Assert.Single(_logger.Lines);
        Assert.StartsWith("warning: ", _logger.Lines[0], StringComparison.Ordinal);
        Assert.Contains("Project.audioMixer", _logger.Lines[0], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("{\"name\":\"x\",\"version\":2,\"buses\":[{\"name\":\"A\"}]}")]
    public void ACorruptOrFutureFile_IsNoAsset_WithOneLogLine_AndNeverThrows(string content)
    {
        Guid id = AddMixerFile("Project", content);
        GameSettings.ProjectSettings.AudioMixerAsset = id.ToString();

        bool loaded = SoundBusChoices.TryLoadProjectMixer(out AudioMixerAsset asset);

        Assert.False(loaded);
        Assert.Null(asset);
        Assert.Single(_logger.Lines);
    }
}
