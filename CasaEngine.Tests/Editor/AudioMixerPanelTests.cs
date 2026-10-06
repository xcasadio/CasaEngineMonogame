using System.Reflection;
using CasaEngine.Editor;
using CasaEngine.Editor.Controls;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Workspaces;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.Audio;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Tests.Audio;
using CasaEngine.Tests.ContentBrowser;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// The shell of the mixing document panel (plan T10.5, decisions P49 and P50), built on the headless MGUI desktop without a GPU:
/// the loader, the banners (live, not heard, software backend only), Save, Reload and Apply, the problem list, the dirty state,
/// the history of the context and the live binding of a project change. The asset catalog, the editor history and
/// <see cref="EngineEnvironment.ProjectPath"/> are global state, hence the serialized collection and the restore in
/// <see cref="Dispose"/>.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class AudioMixerPanelTests : IDisposable
{
    private const string RelativeFile = "Main.audioMixer";
    private const string ContextId = EditorPanelIds.AudioMixerAssetDocumentPrefix + "test";

    private static readonly EditorHistoryContext Context = new(EditorHistoryContextKind.AudioMixer, ContextId);

    private readonly string _projectDirectory;
    private readonly string _previousProjectPath;
    private readonly List<IDisposable> _disposables = new();

    public AudioMixerPanelTests()
    {
        _projectDirectory = Path.Combine(Path.GetTempPath(), "casa-audio-mixer-panel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectDirectory);

        _previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = _projectDirectory;
        AssetCatalog.ClearInternal();
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        EditorHistoryService.Current.Remove(Context);
        EditorHistoryService.Current.Deactivate();
        EditorDirtyStateService.Current.Remove(Context);

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

    private sealed class Rig
    {
        public required ContentBrowserViewTestHarness Harness { get; init; }

        public required AudioService Service { get; init; }

        public required AudioMixerAssetApplier Applier { get; init; }

        public required AudioMixerPanel Panel { get; init; }

        public required MGElement Content { get; init; }

        public required string FullPath { get; init; }

        public required Guid AssetId { get; init; }

        public int DirtyEvents { get; set; }

        public AudioMixerDocument Document => Panel.Document!;

        public float LiveVolume(string bus)
        {
            Assert.True(Service.Mixer.TryGetBus(bus, out var liveBus));
            return liveBus.Volume;
        }

        public void SetLiveVolume(string bus, float volume)
        {
            Assert.True(Service.Mixer.TryGetBus(bus, out var liveBus));
            liveBus.Volume = volume;
        }
    }

    /// <summary>
    /// A project with one mixer file on disk (default buses, Sfx at 0.5) in the catalogue, an audio service whose live mixer
    /// holds that asset (what the project mixer does at startup), and a panel on a headless window.
    /// </summary>
    private Rig Open(
        bool live = true,
        bool buildContentFirst = false,
        IAudioBackend backend = null,
        Action<AudioMixerAsset> customize = null)
    {
        var asset = AudioMixerAsset.CreateDefault("Main");
        asset.Buses.Find(bus => bus.Name == "Sfx")!.Volume = 0.5f;
        customize?.Invoke(asset);
        asset.FileName = RelativeFile;

        AssetCatalog.AddInternal(new AssetInfo(asset.Id) { Name = "Main", FileName = RelativeFile });
        EditorAssetWriterService.SaveAsset(RelativeFile, asset, EditorAssetSaveSource.AudioMixerEditorPanel);

        string fullPath = Path.Combine(_projectDirectory, RelativeFile);
        var service = new AudioService(backend ?? new FakeAudioBackend());
        _disposables.Add(service);
        var applier = new AudioMixerAssetApplier(service);

        Assert.True(AudioMixerPanel.TryLoadAsset(fullPath, out var projectCopy, out string error), error);
        applier.Apply(projectCopy, logProblems: false);

        Assert.True(AudioMixerPanel.TryLoadAsset(fullPath, out var loaded, out error), error);

        // Big enough for every control to be laid out inside the viewport, so the buttons can be clicked.
        var harness = ContentBrowserViewTestHarness.Create(900, 900);
        var panel = new AudioMixerPanel(harness.Window, () => service);
        panel.SetHistoryContextId(ContextId);

        MGElement content;
        if (buildContentFirst)
        {
            content = panel.CreateContent();
            panel.LoadAsset(loaded, fullPath, live ? applier : null);
        }
        else
        {
            panel.LoadAsset(loaded, fullPath, live ? applier : null);
            content = panel.CreateContent();
        }

        harness.Window.SetContent(content);

        var rig = new Rig
        {
            Harness = harness,
            Service = service,
            Applier = applier,
            Panel = panel,
            Content = content,
            FullPath = fullPath,
            AssetId = asset.Id,
        };
        panel.DirtyStateChanged += _ => rig.DirtyEvents++;
        return rig;
    }

    private static List<MGTextBlock> TextBlocks(MGElement content)
        => content.TraverseVisualTree().OfType<MGTextBlock>().ToList();

    private static MGTextBlock BannerStartingWith(MGElement content, string prefix)
        => TextBlocks(content).Single(text => text.Text != null && text.Text.StartsWith(prefix, StringComparison.Ordinal));

    private static bool IsShown(MGElement element) => element.Visibility == Visibility.Visible;

    private static MGTextBlock LiveBanner(MGElement content) => BannerStartingWith(content, "Live:");

    private static MGTextBlock NotHeardBanner(MGElement content) => BannerStartingWith(content, "This asset is not the project mixer");

    private static MGTextBlock SoftwareOnlyBanner(MGElement content) => BannerStartingWith(content, "Effects, sends and ducking");

    private static MGButton Button(MGElement content, string label)
        => content.TraverseVisualTree()
            .OfType<MGButton>()
            .Single(button => button.TraverseVisualTree().OfType<MGTextBlock>().Any(text => text.Text == label));

    private static void ClickCenterOf(Rig rig, MGElement element)
    {
        // A frame to lay out the tree, one to hover the element, then press and release on it.
        rig.Harness.AdvanceFrame(0);
        rig.Harness.AdvanceFrame(16);
        var bounds = element.ActualLayoutBounds;
        Assert.False(bounds.IsEmpty);
        rig.Harness.AdvanceFrame(32, bounds.Center);
        rig.Harness.Click(48, bounds.Center);
    }

    private static JObject ReadFile(string fullPath) => JObject.Parse(File.ReadAllText(fullPath));

    private static float FileVolume(string fullPath, string bus)
        => (float)ReadFile(fullPath)["buses"]!.Single(node => (string)node["name"] == bus)["volume"]!;

    /// <summary>Changes a volume in the file on disk, the way an external editor would.</summary>
    private static void EditFileVolume(string fullPath, string bus, float volume)
    {
        var document = ReadFile(fullPath);
        document["buses"]!.Single(node => (string)node["name"] == bus)["volume"] = volume;
        File.WriteAllText(fullPath, document.ToString());
    }

    // ───────────────────────── plumbing: identifiers, mapping ─────────────────────────

    [Fact]
    public void TheMixingDocument_HasItsOwnHistoryContext()
    {
        var document = new EditorDocumentContext(EditorDocumentKind.AudioMixer, ContextId, "Main");

        var context = EditorHistoryContext.FromDocument(document);

        Assert.Equal(Context, context);
        Assert.False(context.IsEmpty);
        // New members come last: the numeric values of the existing ones do not move.
        Assert.Equal(EditorDocumentKind.TileMap + 1, EditorDocumentKind.AudioMixer);
        Assert.Equal(EditorHistoryContextKind.ContentBrowser + 1, EditorHistoryContextKind.AudioMixer);
    }

    [Fact]
    public void TheMixingPanelPrefix_IsNotThePrefixOfAnotherDocumentPanel_NorPrefixedByOne()
    {
        var prefixes = typeof(EditorPanelIds)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.Name.EndsWith("DocumentPrefix", StringComparison.Ordinal))
            .ToDictionary(field => field.Name, field => (string)field.GetRawConstantValue()!);

        string mixerPrefix = EditorPanelIds.AudioMixerAssetDocumentPrefix;
        Assert.Equal("panel_audio_mixer_asset_", mixerPrefix);
        Assert.All(
            prefixes.Where(pair => pair.Value != mixerPrefix),
            pair =>
            {
                Assert.False(pair.Value.StartsWith(mixerPrefix, StringComparison.Ordinal), pair.Key);
                Assert.False(mixerPrefix.StartsWith(pair.Value, StringComparison.Ordinal), pair.Key);
            });
    }

    // ───────────────────────── the loader ─────────────────────────

    [Fact]
    public void TryLoadAsset_OfAValidFile_ReadsTheAsset_AndTheCatalogId()
    {
        var rig = Open();

        Assert.True(AudioMixerPanel.TryLoadAsset(rig.FullPath, out var asset, out string error));

        Assert.Equal(string.Empty, error);
        Assert.Equal("Main", asset.Name);
        Assert.Equal(rig.AssetId, asset.AssetId);
        Assert.Equal(RelativeFile, asset.FileName);
        Assert.Equal(new[] { "Music", "Sfx", "Voice", "Ui" }, asset.Buses.Select(bus => bus.Name));
        Assert.Equal(0.5f, asset.Buses.Single(bus => bus.Name == "Sfx").Volume);
    }

    [Fact]
    public void TryLoadAsset_OfAFileOutsideTheCatalog_HasNoAssetId()
    {
        var asset = AudioMixerAsset.CreateDefault("Loose");
        EditorAssetWriterService.SaveAsset("Loose.audioMixer", asset, EditorAssetSaveSource.AudioMixerEditorPanel);

        Assert.True(AudioMixerPanel.TryLoadAsset(Path.Combine(_projectDirectory, "Loose.audioMixer"), out var loaded, out _));

        Assert.Equal(Guid.Empty, loaded.AssetId);
        Assert.Equal(asset.Id, loaded.Id);
    }

    [Fact]
    public void TryLoadAsset_OfANewerVersion_IsRefusedWithAMessage()
    {
        var rig = Open();
        var document = ReadFile(rig.FullPath);
        document["version"] = AudioMixerAsset.CurrentVersion + 1;
        File.WriteAllText(rig.FullPath, document.ToString());

        Assert.False(AudioMixerPanel.TryLoadAsset(rig.FullPath, out var asset, out string error));

        Assert.Contains("newer", error, StringComparison.Ordinal);
        Assert.Contains(RelativeFile, error, StringComparison.Ordinal);
        Assert.NotNull(asset);
        Assert.Empty(asset.Buses);
    }

    [Fact]
    public void TryLoadAsset_OfAMissingFileOrAnotherExtensionOrBrokenJson_IsRefusedWithAMessage()
    {
        File.WriteAllText(Path.Combine(_projectDirectory, "Broken.audioMixer"), "{ not json");
        File.WriteAllText(Path.Combine(_projectDirectory, "Other.sound"), "{}");

        foreach (string name in new[] { "Missing.audioMixer", "Broken.audioMixer", "Other.sound" })
        {
            Assert.False(AudioMixerPanel.TryLoadAsset(Path.Combine(_projectDirectory, name), out var asset, out string error), name);
            Assert.False(string.IsNullOrWhiteSpace(error), name);
            Assert.NotNull(asset);
        }
    }

    // ───────────────────────── building the content ─────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildingTheContent_ShowsTheAsset_WithoutMarkingItDirty(bool buildContentFirst)
    {
        var rig = Open(buildContentFirst: buildContentFirst);

        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(0, rig.DirtyEvents);
        Assert.Equal(RelativeFile, rig.Panel.LoadedRelativePath);

        var texts = TextBlocks(rig.Content).Select(text => text.Text).ToList();
        Assert.Contains("[b]Main[/b]", texts);
        Assert.Contains(texts, text => text != null && text.Contains($"Source: {RelativeFile}", StringComparison.Ordinal));
        Assert.Contains(texts, text => text != null && text.Contains($"Id: {rig.AssetId}", StringComparison.Ordinal));
        Assert.Contains($"Asset: {RelativeFile}", texts);

        Assert.True(Button(rig.Content, "Save").IsEnabled);
        Assert.True(Button(rig.Content, "Reload").IsEnabled);
        Assert.True(Button(rig.Content, "Apply to live mixer").IsEnabled);
    }

    [Fact]
    public void ThePanelWithoutAnAsset_SaysSoAndDisablesItsButtons()
    {
        var harness = ContentBrowserViewTestHarness.Create(900, 900);
        var panel = new AudioMixerPanel(harness.Window, () => null);

        var content = panel.CreateContent();
        harness.Window.SetContent(content);

        Assert.Same(content, panel.CreateContent());
        Assert.Contains(TextBlocks(content), text => text.Text == "No audio mixer loaded.");
        Assert.False(IsShown(LiveBanner(content)));
        Assert.DoesNotContain(
            TextBlocks(content),
            text => IsShown(text) && text.Text != null && text.Text.StartsWith("This asset is not the project mixer", StringComparison.Ordinal));
        Assert.False(Button(content, "Save").IsEnabled);
        Assert.False(Button(content, "Reload").IsEnabled);
        Assert.False(Button(content, "Apply to live mixer").IsEnabled);
        Assert.False(panel.IsDirty);
        Assert.Null(panel.Document);
        Assert.False(panel.ReloadFromDisk());
        Assert.False(panel.TrySaveLoadedAsset(out string error));
        Assert.False(string.IsNullOrWhiteSpace(error));
        panel.Update(0.016f);
        panel.ResetMeters();
    }

    // ───────────────────────── banners ─────────────────────────

    [Fact]
    public void ALiveDocument_ShowsTheLiveBanner_AndNotTheNotHeardOne()
    {
        var rig = Open(live: true);

        Assert.True(rig.Document.IsLive);
        Assert.True(IsShown(LiveBanner(rig.Content)));
        Assert.Equal("Live: changes are applied to the mixer", LiveBanner(rig.Content).Text);
        Assert.False(IsShown(NotHeardBanner(rig.Content)));
    }

    [Fact]
    public void ADocumentThatIsNotTheProjectMixer_SaysItsChangesAreNotHeard_AndNamesTheSetting()
    {
        var rig = Open(live: false);

        Assert.False(rig.Document.IsLive);
        Assert.False(IsShown(LiveBanner(rig.Content)));
        var banner = NotHeardBanner(rig.Content);
        Assert.True(IsShown(banner));
        Assert.Equal(
            $"This asset is not the project mixer: changes are saved but not heard. Set AudioMixerAsset = {rig.AssetId} in the project file.",
            banner.Text);
        Assert.False(Button(rig.Content, "Apply to live mixer").IsEnabled);
    }

    [Fact]
    public void TheSoftwareOnlyBanner_ShowsUnderABackendWithoutBuses()
    {
        var rig = Open(backend: new FakeAudioBackend());

        Assert.False(rig.Service.Backend is IAudioBusBackend);
        Assert.True(IsShown(SoftwareOnlyBanner(rig.Content)));
        Assert.Equal("Effects, sends and ducking: software backend only", SoftwareOnlyBanner(rig.Content).Text);
    }

    [Fact]
    public void TheSoftwareOnlyBanner_IsHiddenUnderTheSoftwareBackend()
    {
        var rig = Open(backend: new SoftwareAudioBackend(new OfflineAudioOutput(), 16));

        Assert.IsAssignableFrom<IAudioBusBackend>(rig.Service.Backend);
        Assert.False(IsShown(SoftwareOnlyBanner(rig.Content)));
    }

    [Fact]
    public void WithoutAnAudioService_TheSoftwareOnlyBannerShows()
    {
        var harness = ContentBrowserViewTestHarness.Create(900, 900);
        var rig = Open();
        var panel = new AudioMixerPanel(harness.Window, () => null);
        Assert.True(AudioMixerPanel.TryLoadAsset(rig.FullPath, out var asset, out _));
        panel.LoadAsset(asset, rig.FullPath, null);

        var content = panel.CreateContent();

        Assert.True(IsShown(SoftwareOnlyBanner(content)));
    }

    [Fact]
    public void TheBanners_FollowTheLiveBindingOfTheDocument_AfterItsChangedEvent()
    {
        var rig = Open(live: true);
        Assert.True(IsShown(LiveBanner(rig.Content)));

        // The document is detached directly (not through the panel): the panel only listens to Changed.
        rig.Document.DetachLive();

        Assert.False(IsShown(LiveBanner(rig.Content)));
        Assert.True(IsShown(NotHeardBanner(rig.Content)));
        Assert.False(Button(rig.Content, "Apply to live mixer").IsEnabled);

        rig.Document.UpdateLiveBinding(rig.AssetId, rig.Applier);

        Assert.True(IsShown(LiveBanner(rig.Content)));
        Assert.False(IsShown(NotHeardBanner(rig.Content)));
        Assert.True(Button(rig.Content, "Apply to live mixer").IsEnabled);

        rig.Document.UpdateLiveBinding(Guid.NewGuid(), rig.Applier);

        Assert.False(rig.Document.IsLive);
        Assert.True(IsShown(NotHeardBanner(rig.Content)));
    }

    [Fact]
    public void ProjectChange_DetachesTheDocument_ThenAttachesItOnlyWhenTheNewProjectNamesItsAsset()
    {
        var rig = Open(live: true);
        rig.Panel.SetHistoryContextId(ContextId);
        rig.Panel.Document!.SetMute("Sfx", true);
        Assert.True(rig.Service.Mixer.TryGetBus("Sfx", out var sfx));
        Assert.True(sfx.IsMuted);

        // Project closed: the mute the document set is given back, nothing drives the mixer.
        rig.Panel.DetachLive();
        Assert.False(rig.Document.IsLive);
        Assert.False(sfx.IsMuted);

        // The next project applies another asset: the document stays out, and an edit leaves the mixer alone.
        rig.Panel.UpdateLiveBinding(Guid.NewGuid(), rig.Applier);
        Assert.False(rig.Document.IsLive);
        rig.SetLiveVolume("Sfx", 0.1f);
        rig.Document.SetBusVolume("Sfx", 0.9f);
        Assert.Equal(0.1f, rig.LiveVolume("Sfx"));

        // A project whose setting names this asset attaches it again and applies its asset to the live mixer.
        rig.Panel.UpdateLiveBinding(rig.AssetId, rig.Applier);
        Assert.True(rig.Document.IsLive);
        Assert.Equal(0.9f, rig.LiveVolume("Sfx"));
        Assert.True(IsShown(LiveBanner(rig.Content)));
    }

    // ───────────────────────── problems ─────────────────────────

    [Fact]
    public void TheProblemList_ShowsWhatTheValidatorFound_AndGoesWhenItIsFixed()
    {
        var rig = Open(customize: asset => asset.Buses.Add(new AudioMixerBusData { Name = "Orphan", Parent = "Nowhere" }));

        Assert.NotEmpty(rig.Document.Problems);
        var texts = TextBlocks(rig.Content).Select(text => text.Text).ToList();
        Assert.Contains($"[b]Problems ({rig.Document.Problems.Count})[/b]", texts);
        Assert.Contains(texts, text => text != null && text.StartsWith("- ", StringComparison.Ordinal)
                                                   && text.Contains("Orphan", StringComparison.Ordinal));

        Assert.True(rig.Document.TryRemoveBus("Orphan", out string error), error);

        Assert.Empty(rig.Document.Problems);
        Assert.DoesNotContain(TextBlocks(rig.Content), text => text.Text != null && text.Text.StartsWith("[b]Problems", StringComparison.Ordinal));
    }

    [Fact]
    public void ACleanAsset_ShowsNoProblemLine()
    {
        var rig = Open();

        Assert.Empty(rig.Document.Problems);
        Assert.DoesNotContain(TextBlocks(rig.Content), text => text.Text != null && text.Text.StartsWith("[b]Problems", StringComparison.Ordinal));
    }

    // ───────────────────────── dirty state and history ─────────────────────────

    [Fact]
    public void AnEdit_MarksThePanelDirty_RecordsOneHistoryEntry_AndDisablesReload()
    {
        var rig = Open();

        Assert.True(rig.Document.SetBusVolume("Sfx", 0.4f));

        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(1, rig.DirtyEvents);
        Assert.True(EditorHistoryService.Current.TryGet(Context, out var stack));
        Assert.True(stack.CanUndo);
        Assert.Equal("Set volume of 'Sfx'", stack.UndoDescription);
        Assert.False(Button(rig.Content, "Reload").IsEnabled);
        Assert.Contains(TextBlocks(rig.Content), text => text.Text == $"Modified {RelativeFile}");

        // A second edit stays dirty: no second event.
        rig.Document.SetBusVolume("Sfx", 0.3f);
        Assert.Equal(1, rig.DirtyEvents);
    }

    [Fact]
    public void UndoThroughTheEditorHistory_GivesBackTheAsset_AndTheCleanState()
    {
        var rig = Open();
        rig.Document.SetBusVolume("Sfx", 0.4f);
        Assert.Equal(0.4f, rig.LiveVolume("Sfx"));
        Assert.True(EditorDirtyStateService.Current.IsDirty(Context));

        EditorHistoryService.Current.SetActiveContext(Context);
        Assert.True(EditorHistoryService.Current.Undo());

        Assert.Equal(0.5f, rig.Document.Asset.Buses.Single(bus => bus.Name == "Sfx").Volume);
        Assert.Equal(0.5f, rig.LiveVolume("Sfx"));
        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(2, rig.DirtyEvents);
        Assert.False(EditorDirtyStateService.Current.IsDirty(Context));
        Assert.True(Button(rig.Content, "Reload").IsEnabled);

        Assert.True(EditorHistoryService.Current.Redo());
        Assert.Equal(0.4f, rig.LiveVolume("Sfx"));
        Assert.True(rig.Panel.IsDirty);
        Assert.True(EditorDirtyStateService.Current.IsDirty(Context));
    }

    [Fact]
    public void TheHistoryContextId_MustNotBeBlank()
    {
        var harness = ContentBrowserViewTestHarness.Create();
        var panel = new AudioMixerPanel(harness.Window, () => null);

        Assert.Throws<ArgumentException>(() => panel.SetHistoryContextId(" "));
        Assert.Throws<ArgumentNullException>(() => new AudioMixerPanel(harness.Window, null!));
    }

    // ───────────────────────── save ─────────────────────────

    [Fact]
    public void Save_WritesTheFile_ClearsTheDirtyState_AndMarksTheContextSaved()
    {
        var rig = Open();
        rig.Document.SetBusVolume("Sfx", 0.4f);
        rig.Document.TryAddBus("Footsteps", "Sfx", out _);
        Assert.Equal(0.5f, FileVolume(rig.FullPath, "Sfx"));

        Assert.True(rig.Panel.TrySaveLoadedAsset(out string error), error);

        Assert.Null(error);
        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(2, rig.DirtyEvents);
        Assert.False(EditorDirtyStateService.Current.IsDirty(Context));
        Assert.Equal(0.4f, FileVolume(rig.FullPath, "Sfx"));
        Assert.Contains(ReadFile(rig.FullPath)["buses"]!, node => (string)node["name"] == "Footsteps");
        Assert.Contains(TextBlocks(rig.Content), text => text.Text == $"Saved {RelativeFile}");
        Assert.True(Button(rig.Content, "Reload").IsEnabled);

        // The saved state is the new reference: undo makes the document dirty again, redo clean.
        EditorHistoryService.Current.SetActiveContext(Context);
        Assert.True(EditorHistoryService.Current.Undo());
        Assert.True(rig.Panel.IsDirty);
        Assert.True(EditorDirtyStateService.Current.IsDirty(Context));
        Assert.True(EditorHistoryService.Current.Redo());
        Assert.False(rig.Panel.IsDirty);
        Assert.False(EditorDirtyStateService.Current.IsDirty(Context));
    }

    [Fact]
    public void TheSaveButton_SavesTheDirtyAsset()
    {
        var rig = Open();
        rig.Document.SetBusVolume("Sfx", 0.35f);

        ClickCenterOf(rig, Button(rig.Content, "Save"));

        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(0.35f, FileVolume(rig.FullPath, "Sfx"));
    }

    [Fact]
    public void Save_ToAFolderThatDoesNotExist_ReportsTheError_AndStaysDirty()
    {
        var rig = Open();
        var asset = AudioMixerAsset.CreateDefault("Elsewhere");
        var panel = new AudioMixerPanel(rig.Harness.Window, () => rig.Service);
        panel.SetHistoryContextId(ContextId);
        panel.LoadAsset(asset, Path.Combine(_projectDirectory, "MissingFolder", "Elsewhere.audioMixer"), null);
        panel.Document!.SetBusVolume("Sfx", 0.2f);

        Assert.False(panel.TrySaveLoadedAsset(out string error));

        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.True(panel.IsDirty);
        Assert.True(EditorDirtyStateService.Current.IsDirty(Context));
    }

    // ───────────────────────── reload ─────────────────────────

    [Fact]
    public void Reload_IsRefusedWhileTheDocumentIsModified_AndKeepsTheEdits()
    {
        var rig = Open();
        rig.Document.SetBusVolume("Sfx", 0.4f);
        var document = rig.Document;

        Assert.False(rig.Panel.ReloadFromDisk());

        Assert.Same(document, rig.Document);
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(0.4f, rig.Document.Asset.Buses.Single(bus => bus.Name == "Sfx").Volume);
        Assert.Contains(TextBlocks(rig.Content), text => text.Text != null && text.Text.StartsWith("Unsaved changes kept", StringComparison.Ordinal));
    }

    [Fact]
    public void Reload_OfALiveDocument_ReadsTheFileAgain_AppliesItToTheLiveMixer_AndStartsTheHistoryAgain()
    {
        var rig = Open(live: true);
        rig.Document.SetBusVolume("Sfx", 0.4f);
        Assert.True(rig.Panel.TrySaveLoadedAsset(out _));
        Assert.True(EditorHistoryService.Current.TryGet(Context, out var stack));
        Assert.True(stack.CanUndo);

        // Another tool changed the file.
        EditFileVolume(rig.FullPath, "Sfx", 0.25f);
        Assert.Equal(0.4f, rig.LiveVolume("Sfx"));
        var previousDocument = rig.Document;

        Assert.True(rig.Panel.ReloadFromDisk());

        Assert.NotSame(previousDocument, rig.Document);
        Assert.Equal(0.25f, rig.Document.Asset.Buses.Single(bus => bus.Name == "Sfx").Volume);
        Assert.True(rig.Document.IsLive);
        Assert.Equal(0.25f, rig.LiveVolume("Sfx"));
        Assert.False(rig.Panel.IsDirty);
        Assert.False(stack.CanUndo);
        Assert.False(EditorDirtyStateService.Current.IsDirty(Context));
        Assert.True(IsShown(LiveBanner(rig.Content)));
        Assert.Contains(TextBlocks(rig.Content), text => text.Text == $"Reloaded {RelativeFile}");

        // The new document reports its changes to the panel.
        rig.Document.SetBusVolume("Sfx", 0.6f);
        Assert.True(rig.Panel.IsDirty);
    }

    [Fact]
    public void Reload_GivesBackTheMutesTheOldDocumentSet()
    {
        var rig = Open(live: true);
        rig.Document.SetMute("Voice", true);
        Assert.True(rig.Service.Mixer.TryGetBus("Voice", out var voice));
        Assert.True(voice.IsMuted);

        Assert.True(rig.Panel.ReloadFromDisk());

        Assert.False(voice.IsMuted);
    }

    [Fact]
    public void Reload_OfAMissingFile_LeavesTheDocumentAlone_AndSaysSo()
    {
        var rig = Open();
        var document = rig.Document;
        File.Delete(rig.FullPath);

        Assert.False(rig.Panel.ReloadFromDisk());

        Assert.Same(document, rig.Document);
        Assert.Contains(TextBlocks(rig.Content), text => text.Text != null && text.Text.Contains("does not exist", StringComparison.Ordinal));
    }

    [Fact]
    public void LoadingAssetOverAModifiedDocument_TakesBackItsLiveEdits_AndReportsTheCleanState()
    {
        var rig = Open(live: true);
        rig.Document.SetBusVolume("Sfx", 0.9f);
        Assert.Equal(1, rig.DirtyEvents);
        Assert.Equal(0.9f, rig.LiveVolume("Sfx"));
        Assert.True(AudioMixerPanel.TryLoadAsset(rig.FullPath, out var again, out _));

        rig.Panel.LoadAsset(again, rig.FullPath, rig.Applier);

        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(2, rig.DirtyEvents);
        Assert.Equal(0.5f, rig.LiveVolume("Sfx"));
        Assert.False(EditorDirtyStateService.Current.IsDirty(Context));
        Assert.Contains(TextBlocks(rig.Content), text => text.Text == $"Asset: {RelativeFile}");
    }

    [Fact]
    public void TheReloadButton_ReloadsTheCleanDocument()
    {
        var rig = Open();
        EditFileVolume(rig.FullPath, "Voice", 0.2f);

        ClickCenterOf(rig, Button(rig.Content, "Reload"));

        Assert.Equal(0.2f, rig.Document.Asset.Buses.Single(bus => bus.Name == "Voice").Volume);
    }

    // ───────────────────────── apply, and a panel that is not live ─────────────────────────

    [Fact]
    public void Apply_PutsTheAssetOnTheLiveMixer_NeverTheOtherWay()
    {
        var rig = Open(live: true);
        rig.SetLiveVolume("Sfx", 0.1f);
        Assert.Equal(0.5f, rig.Document.Asset.Buses.Single(bus => bus.Name == "Sfx").Volume);

        rig.Panel.ApplyToLive();

        Assert.Equal(0.5f, rig.LiveVolume("Sfx"));
        Assert.Equal(0.5f, rig.Document.Asset.Buses.Single(bus => bus.Name == "Sfx").Volume);
        Assert.False(rig.Panel.IsDirty);
    }

    [Fact]
    public void TheApplyButton_AppliesTheAsset()
    {
        var rig = Open(live: true);
        rig.SetLiveVolume("Sfx", 0.1f);

        ClickCenterOf(rig, Button(rig.Content, "Apply to live mixer"));

        Assert.Equal(0.5f, rig.LiveVolume("Sfx"));
    }

    [Fact]
    public void APanelWithoutAnApplier_LeavesTheLiveMixerUntouched()
    {
        var rig = Open(live: false);
        rig.SetLiveVolume("Sfx", 0.1f);
        int busCount = rig.Service.Mixer.Buses.Count;

        rig.Document.SetBusVolume("Sfx", 0.9f);
        rig.Document.TryAddBus("Footsteps", "Sfx", out _);
        rig.Document.SetMute("Voice", true);
        rig.Panel.ApplyToLive();
        Assert.True(rig.Panel.TrySaveLoadedAsset(out _));
        Assert.True(rig.Panel.ReloadFromDisk());

        Assert.False(rig.Document.IsLive);
        Assert.Equal(0.1f, rig.LiveVolume("Sfx"));
        Assert.Equal(busCount, rig.Service.Mixer.Buses.Count);
        Assert.False(rig.Service.Mixer.TryGetBus("Footsteps", out _));
        Assert.True(rig.Service.Mixer.TryGetBus("Voice", out var voice));
        Assert.False(voice.IsMuted);
        Assert.Contains(TextBlocks(rig.Content), text => text.Text == $"Reloaded {RelativeFile}");
    }

    [Fact]
    public void ApplyOnANonLivePanel_SaysThereIsNothingToApply()
    {
        var rig = Open(live: false);

        rig.Panel.ApplyToLive();

        Assert.Contains(TextBlocks(rig.Content), text => text.Text != null && text.Text.Contains("nothing to apply", StringComparison.Ordinal));
    }

    // ───────────────────────── closing ─────────────────────────

    [Fact]
    public void Dispose_OfAModifiedLiveDocument_PutsTheSavedAssetBackOnTheLiveMixer_AndGivesBackTheMutes()
    {
        var rig = Open(live: true);
        rig.Document.SetBusVolume("Sfx", 0.9f);
        rig.Document.SetSolo("Music", true);
        Assert.Equal(0.9f, rig.LiveVolume("Sfx"));
        Assert.True(rig.Service.Mixer.TryGetBus("Voice", out var voice));
        Assert.True(voice.IsMuted);

        rig.Panel.Dispose();

        Assert.Equal(0.5f, rig.LiveVolume("Sfx"));
        Assert.False(voice.IsMuted);
        Assert.Null(rig.Panel.Document);
    }

    [Fact]
    public void Dispose_EndsAnOpenFaderGesture_BeforeRestoringTheSavedAsset()
    {
        var rig = Open(live: true);
        rig.Document.BeginGesture("Fader");
        rig.Document.UpdateBusVolume("Sfx", 0.8f);
        Assert.Equal(0.8f, rig.LiveVolume("Sfx"));
        var document = rig.Document;

        rig.Panel.Dispose();

        Assert.False(document.IsGestureOpen);
        Assert.Equal(0.5f, rig.LiveVolume("Sfx"));
        Assert.True(EditorHistoryService.Current.TryGet(Context, out var stack));
        Assert.True(stack.CanUndo);
    }

    [Fact]
    public void Dispose_OfASavedDocument_LeavesTheLiveMixerAsItIs_AndIsIdempotent()
    {
        var rig = Open(live: true);
        rig.Document.SetBusVolume("Sfx", 0.4f);
        Assert.True(rig.Panel.TrySaveLoadedAsset(out _));

        rig.Panel.Dispose();
        rig.Panel.Dispose();

        // The saved asset is the one the live mixer already holds.
        Assert.Equal(0.4f, rig.LiveVolume("Sfx"));
    }

    [Fact]
    public void AfterDispose_TheDocumentNoLongerTouchesThePanel()
    {
        var rig = Open(live: true);
        var document = rig.Document;
        rig.Panel.Dispose();
        int dirtyEvents = rig.DirtyEvents;

        document.SetBusVolume("Sfx", 0.2f);

        Assert.Equal(dirtyEvents, rig.DirtyEvents);
    }

    // ───────────────────────── the frame ─────────────────────────

    [Fact]
    public void Update_ChangesNothingInTheContent()
    {
        var rig = Open();
        var before = TextBlocks(rig.Content).Select(text => text.Text).ToList();

        rig.Panel.Update(0.016f);
        rig.Panel.ResetMeters();

        Assert.Equal(before, TextBlocks(rig.Content).Select(text => text.Text).ToList());
    }

    [Fact]
    public void AFaderGesture_DoesNotRefreshTheShell_UntilItEnds()
    {
        var rig = Open();
        rig.Document.BeginGesture("Fader");
        rig.Document.UpdateBusVolume("Sfx", 0.8f);

        // The shell texts are not rebuilt while the fader moves: still the clean state.
        Assert.Contains(TextBlocks(rig.Content), text => text.Text == $"Asset: {RelativeFile}");

        rig.Document.EndGesture();

        Assert.Contains(TextBlocks(rig.Content), text => text.Text == $"Modified {RelativeFile}");
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(1, rig.DirtyEvents);
    }
}
