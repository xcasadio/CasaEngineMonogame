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
using MGUI.Core.UI.Containers.Grids;
using MGUI.Shared.Input.Mouse;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// The mixing document panel (plan T10.5 and T10.6, decisions P49 to P54), built on the headless MGUI desktop without a GPU: the
/// loader, the banners (live, not heard, software backend only), Save, Reload and Apply, the problem list, the dirty state, the
/// history of the context and the live binding of a project change; then the bus strips (editable, read-only, Master and Editor),
/// the fader gestures, mute and solo, the meters and the play session. The asset catalog, the editor history (its suspension
/// included) and <see cref="EngineEnvironment.ProjectPath"/> are global state, hence the serialized collection and the restore
/// in <see cref="Dispose"/>.
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

        EditorHistoryService.Current.IsSuspended = false;
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
        Action<AudioMixerAsset> customize = null,
        int windowWidth = 900)
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

        // Big enough for every control to be laid out inside the viewport, so the buttons can be clicked. The harness desktop is only
        // 640 pixels wide: a button at the right end of a wide window cannot be hovered, hence the width parameter.
        var harness = ContentBrowserViewTestHarness.Create(windowWidth, 900);
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

    // ───────────────────────── bus strips: helpers ─────────────────────────

    private const float Frame = 0.016f;

    private static readonly string[] DefaultBuses = { "Music", "Sfx", "Voice", "Ui" };

    private static T Tagged<T>(MGElement content, string tag) where T : MGElement
        => content.TraverseVisualTree().OfType<T>().Single(element => element.Tag as string == tag);

    private static bool IsTagged<T>(MGElement content, string tag) where T : MGElement
        => content.TraverseVisualTree().OfType<T>().Any(element => element.Tag as string == tag);

    private static MGSlider Fader(MGElement content, string bus) => Tagged<MGSlider>(content, "fader:" + bus);

    private static MGToggleButton MuteToggle(MGElement content, string bus) => Tagged<MGToggleButton>(content, "mute:" + bus);

    private static MGToggleButton SoloToggle(MGElement content, string bus) => Tagged<MGToggleButton>(content, "solo:" + bus);

    private static MGTextBlock Readout(MGElement content, string bus) => Tagged<MGTextBlock>(content, "db:" + bus);

    private static MGButton RemoveButton(MGElement content, string bus) => Tagged<MGButton>(content, "remove:" + bus);

    private static MGGrid StripGrid(MGElement content) => content.TraverseVisualTree().OfType<MGGrid>().Single();

    /// <summary>The names of the strips, in the order they are shown.</summary>
    private static List<string> StripNames(MGElement content)
    {
        var notNames = new HashSet<string> { "Bus", "Volume", "Level (dBFS)", "M", "S", "X", "n/a" };
        return StripGrid(content)
            .TraverseVisualTree(includeComponents: false)
            .OfType<MGTextBlock>()
            .Select(text => text.Text)
            .Where(text => !string.IsNullOrEmpty(text) && !notNames.Contains(text) && !text.EndsWith(" dB", StringComparison.Ordinal))
            .ToList();
    }

    private static MGTextBlock NameBlock(MGElement content, string bus)
        => StripGrid(content).TraverseVisualTree(includeComponents: false).OfType<MGTextBlock>().Single(text => text.Text == bus);

    private static int MeterCount(MGElement content) => content.TraverseVisualTree().OfType<AudioMeterControl>().Count();

    private static bool CanUndo() => EditorHistoryService.Current.TryGet(Context, out var stack) && stack.CanUndo;

    /// <summary>Undoes every entry of the context and returns how many there were.</summary>
    private static int UndoEverything()
    {
        EditorHistoryService.Current.SetActiveContext(Context);
        int count = 0;
        while (EditorHistoryService.Current.Undo())
        {
            count++;
        }

        return count;
    }

    private static void Frames(Rig rig, int count)
    {
        for (int frame = 0; frame < count; frame++)
        {
            rig.Panel.Update(Frame);
        }
    }

    private static PcmAudioClip ConstantStereoClip(short value, int frames)
    {
        var samples = new short[2 * frames];
        Array.Fill(samples, value);
        return new PcmAudioClip(samples, 48000, 2);
    }

    private static float AssetVolume(Rig rig, string bus) => rig.Document.Asset.Buses.Single(data => data.Name == bus).Volume;

    // ───────────────────────── bus strips: rows ─────────────────────────

    [Fact]
    public void TheStrips_ListMaster_TheBusesOfTheAsset_ThenEditor_WithTheirControls()
    {
        var rig = Open();

        Assert.Equal(new[] { "Master", "Music", "Sfx", "Voice", "Ui", "Editor" }, StripNames(rig.Content));
        Assert.Equal(6, MeterCount(rig.Content));

        foreach (string bus in DefaultBuses)
        {
            Assert.True(Fader(rig.Content, bus).IsEnabled, bus);
            Assert.Equal(0f, Fader(rig.Content, bus).Minimum);
            Assert.Equal(1f, Fader(rig.Content, bus).Maximum);
            Assert.True(MuteToggle(rig.Content, bus).IsEnabled, bus);
            Assert.True(SoloToggle(rig.Content, bus).IsEnabled, bus);
            Assert.False(IsTagged<MGButton>(rig.Content, "remove:" + bus), "a default bus cannot be deleted: " + bus);
        }

        Assert.Equal(0.5f, Fader(rig.Content, "Sfx").Value);
        Assert.Equal(1f, Fader(rig.Content, "Music").Value);
        Assert.Equal("-6.0 dB", Readout(rig.Content, "Sfx").Text);
        Assert.Equal("0.0 dB", Readout(rig.Content, "Music").Text);
    }

    [Fact]
    public void MasterAndEditor_AreReadOnly_AndShowTheLiveVolumeOfTheirBus()
    {
        var rig = Open();

        foreach (string bus in new[] { "Master", "Editor" })
        {
            Assert.False(IsTagged<MGSlider>(rig.Content, "fader:" + bus), bus);
            Assert.False(IsTagged<MGToggleButton>(rig.Content, "mute:" + bus), bus);
            Assert.False(IsTagged<MGToggleButton>(rig.Content, "solo:" + bus), bus);
            Assert.False(IsTagged<MGButton>(rig.Content, "remove:" + bus), bus);
            Assert.Equal("0.0 dB", Readout(rig.Content, bus).Text);
            Assert.True(NameBlock(rig.Content, bus).Opacity < 1f, bus);
        }

        rig.Service.Mixer.Root.Volume = 0.5f;
        rig.Service.Mixer.GetBus("Editor").Volume = 0.25f;
        rig.Panel.Update(Frame);

        Assert.Equal("-6.0 dB", Readout(rig.Content, "Master").Text);
        Assert.Equal("-12.0 dB", Readout(rig.Content, "Editor").Text);
        Assert.False(rig.Panel.IsDirty);
        Assert.False(CanUndo());
    }

    [Fact]
    public void MasterAndEditor_WithoutALiveMixer_ShowNotAvailable()
    {
        var rig = Open();
        var harness = ContentBrowserViewTestHarness.Create(900, 900);
        var panel = new AudioMixerPanel(harness.Window, () => null);
        Assert.True(AudioMixerPanel.TryLoadAsset(rig.FullPath, out var asset, out _));
        panel.LoadAsset(asset, rig.FullPath, null);

        var content = panel.CreateContent();

        Assert.Equal("n/a", Readout(content, "Master").Text);
        Assert.Equal("n/a", Readout(content, "Editor").Text);
        Assert.Equal(new[] { "Master", "Music", "Sfx", "Voice", "Ui", "Editor" }, StripNames(content));
    }

    [Fact]
    public void TheStrips_AreIndentedByDepth()
    {
        var rig = Open(customize: asset =>
        {
            asset.Buses.Add(new AudioMixerBusData { Name = "Footsteps", Parent = "Sfx" });
            asset.Buses.Add(new AudioMixerBusData { Name = "Gravel", Parent = "Footsteps" });
        });

        // A bus comes right after its parent, whatever the order of the file.
        Assert.Equal(new[] { "Master", "Music", "Sfx", "Footsteps", "Gravel", "Voice", "Ui", "Editor" }, StripNames(rig.Content));
        Assert.Equal(0f, (float)NameBlock(rig.Content, "Master").Margin.Left);
        Assert.Equal(12f, (float)NameBlock(rig.Content, "Sfx").Margin.Left);
        Assert.Equal(24f, (float)NameBlock(rig.Content, "Footsteps").Margin.Left);
        Assert.Equal(36f, (float)NameBlock(rig.Content, "Gravel").Margin.Left);
        Assert.True(IsTagged<MGButton>(rig.Content, "remove:Footsteps"));
        Assert.True(IsTagged<MGButton>(rig.Content, "remove:Gravel"));
    }

    [Fact]
    public void ABusWhoseParentTheAssetDoesNotName_StillGetsAStrip_AndAnotherWithACycleToo()
    {
        var rig = Open(customize: asset =>
        {
            asset.Buses.Add(new AudioMixerBusData { Name = "Orphan", Parent = "Nowhere" });
            asset.Buses.Add(new AudioMixerBusData { Name = "A", Parent = "B" });
            asset.Buses.Add(new AudioMixerBusData { Name = "B", Parent = "A" });
        });

        var names = StripNames(rig.Content);

        Assert.Equal(new[] { "Master", "Music", "Sfx", "Voice", "Ui", "Orphan", "A", "B", "Editor" }, names);
        Assert.Equal(9, MeterCount(rig.Content));
        Assert.True(IsTagged<MGSlider>(rig.Content, "fader:Orphan"));
        Assert.NotEmpty(rig.Document.Problems);
    }

    [Fact]
    public void ALiveBusTheAssetDoesNotName_IsAReadOnlyStrip_AppearingWhenTheMixerGrows()
    {
        var rig = Open();
        var gridBefore = StripGrid(rig.Content);

        rig.Service.Mixer.CreateBus("GameBus", "Sfx");
        rig.Panel.Update(Frame);

        Assert.NotSame(gridBefore, StripGrid(rig.Content));
        Assert.Equal(new[] { "Master", "Music", "Sfx", "GameBus", "Voice", "Ui", "Editor" }, StripNames(rig.Content));
        Assert.Equal(7, MeterCount(rig.Content));
        Assert.Equal(24f, (float)NameBlock(rig.Content, "GameBus").Margin.Left);
        Assert.True(NameBlock(rig.Content, "GameBus").Opacity < NameBlock(rig.Content, "Sfx").Opacity);
        Assert.False(IsTagged<MGSlider>(rig.Content, "fader:GameBus"));
        Assert.False(IsTagged<MGToggleButton>(rig.Content, "mute:GameBus"));
        Assert.False(IsTagged<MGToggleButton>(rig.Content, "solo:GameBus"));
        Assert.False(IsTagged<MGButton>(rig.Content, "remove:GameBus"));
        Assert.False(IsTagged<MGTextBlock>(rig.Content, "db:GameBus"));

        // The asset knows nothing of it.
        Assert.DoesNotContain(rig.Document.Asset.Buses, bus => bus.Name == "GameBus");
        Assert.False(rig.Panel.IsDirty);

        // Once nothing changes the strips are not rebuilt again.
        var gridAfter = StripGrid(rig.Content);
        Frames(rig, 5);
        Assert.Same(gridAfter, StripGrid(rig.Content));
    }

    [Fact]
    public void TheFadersShowTheAsset_NeverTheLiveMixer()
    {
        var rig = Open();

        rig.SetLiveVolume("Sfx", 0.1f);
        rig.Panel.Update(Frame);

        Assert.Equal(0.5f, Fader(rig.Content, "Sfx").Value);
        Assert.Equal("-6.0 dB", Readout(rig.Content, "Sfx").Text);
    }

    [Theory]
    [InlineData(1f, "0.0 dB")]
    [InlineData(0.5f, "-6.0 dB")]
    [InlineData(0.25f, "-12.0 dB")]
    [InlineData(0.1f, "-20.0 dB")]
    [InlineData(0f, "-inf dB")]
    public void TheReadout_IsInDecibelsWithATenthOfPrecision(float volume, string expected)
    {
        Assert.Equal(expected, AudioMixerPanel.FormatDb(volume));
    }

    [Fact]
    public void AFreshPanel_ShowsNoStrips_AndNoAddRow_UntilAnAssetIsLoaded()
    {
        var harness = ContentBrowserViewTestHarness.Create(900, 900);
        var rig = Open();
        var panel = new AudioMixerPanel(harness.Window, () => rig.Service);
        panel.SetHistoryContextId(ContextId);
        var content = panel.CreateContent();
        harness.Window.SetContent(content);

        Assert.Empty(content.TraverseVisualTree().OfType<MGGrid>());
        Assert.Empty(content.TraverseVisualTree().OfType<AudioMeterControl>());
        Assert.False(IsShown(AddNameBox(content).Parent!));
        Assert.False(Button(content, "Add").IsEnabled);
        panel.Update(Frame);

        Assert.True(AudioMixerPanel.TryLoadAsset(rig.FullPath, out var asset, out _));
        panel.LoadAsset(asset, rig.FullPath, null);

        Assert.Equal(6, StripNames(content).Count);
        Assert.True(IsShown(AddNameBox(content).Parent!));
        Assert.True(Button(content, "Add").IsEnabled);
    }

    // ───────────────────────── add and remove a bus ─────────────────────────

    [Fact]
    public void AddingABus_IsOneHistoryEntry_ShowsAStrip_AndCreatesTheLiveBus()
    {
        var rig = Open();
        var gridBefore = StripGrid(rig.Content);
        AddNameBox(rig.Content).SetText("typed");

        Assert.True(rig.Panel.TryAddBus("Footsteps", "Sfx"));

        Assert.NotSame(gridBefore, StripGrid(rig.Content));
        Assert.Equal(new[] { "Master", "Music", "Sfx", "Footsteps", "Voice", "Ui", "Editor" }, StripNames(rig.Content));
        Assert.Equal("Sfx", rig.Document.Asset.Buses.Single(bus => bus.Name == "Footsteps").Parent);
        Assert.True(rig.Service.Mixer.TryGetBus("Footsteps", out var live));
        Assert.Equal("Sfx", live.Parent!.Name);
        Assert.True(IsTagged<MGSlider>(rig.Content, "fader:Footsteps"));
        Assert.True(IsTagged<MGButton>(rig.Content, "remove:Footsteps"));
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(string.Empty, AddNameBox(rig.Content).Text);
        Assert.Contains(TextBlocks(rig.Content), text => text.Text == "Added bus 'Footsteps'");
        Assert.Equal(1, UndoEverything());
    }

    [Fact]
    public void UndoingTheAddition_RemovesItFromTheAsset_AndTheLiveBusStaysAsAReadOnlyStrip()
    {
        var rig = Open();
        Assert.True(rig.Panel.TryAddBus("Footsteps", "Sfx"));

        EditorHistoryService.Current.SetActiveContext(Context);
        Assert.True(EditorHistoryService.Current.Undo());

        Assert.DoesNotContain(rig.Document.Asset.Buses, bus => bus.Name == "Footsteps");
        Assert.False(rig.Panel.IsDirty);
        // The live mixer never loses a bus: it is now a bus the asset does not name.
        Assert.True(rig.Service.Mixer.TryGetBus("Footsteps", out _));
        Assert.Equal(new[] { "Master", "Music", "Sfx", "Footsteps", "Voice", "Ui", "Editor" }, StripNames(rig.Content));
        Assert.False(IsTagged<MGSlider>(rig.Content, "fader:Footsteps"));
        Assert.False(IsTagged<MGButton>(rig.Content, "remove:Footsteps"));
    }

    [Fact]
    public void TheParentChoices_AreMasterThenTheBusesOfTheAsset_AndFollowTheAsset()
    {
        var rig = Open();
        var combo = rig.Content.TraverseVisualTree().OfType<MGComboBox<string>>().Single();

        Assert.Equal(new[] { "Master", "Music", "Sfx", "Voice", "Ui" }, combo.ItemsSource);
        Assert.Equal("Master", combo.SelectedItem);

        combo.SelectedItem = "Sfx";
        Assert.True(rig.Panel.TryAddBus("Footsteps", "Sfx"));

        Assert.Equal(new[] { "Master", "Music", "Sfx", "Footsteps", "Voice", "Ui" }, combo.ItemsSource);
        Assert.Equal("Sfx", combo.SelectedItem);
    }

    [Fact]
    public void AddingABus_WithABadNameOrParent_ShowsTheReasonInTheStatus_AndRecordsNothing()
    {
        var rig = Open();
        var gridBefore = StripGrid(rig.Content);

        foreach ((string name, string parent, string expected) in new[]
                 {
                     ("Sfx", "Master", "already exists"),
                     ("", "Master", "needs a name"),
                     ("Master", "Master", "reserved"),
                     ("Editor", "Master", "reserved"),
                     ("Footsteps", "Nowhere", "does not exist"),
                     ("Footsteps", "Editor", "cannot be a parent"),
                 })
        {
            Assert.False(rig.Panel.TryAddBus(name, parent), $"{name} under {parent}");
            Assert.Contains(TextBlocks(rig.Content), text => text.Text != null && text.Text.Contains(expected, StringComparison.Ordinal));
        }

        Assert.False(rig.Panel.IsDirty);
        Assert.False(CanUndo());
        Assert.Same(gridBefore, StripGrid(rig.Content));
    }

    [Fact]
    public void RemovingACustomBus_IsOneHistoryEntry_AndTheLiveBusStaysUntilTheNextStart()
    {
        var rig = Open();
        Assert.True(rig.Panel.TryAddBus("Footsteps", "Sfx"));

        Assert.True(rig.Panel.TryRemoveBus("Footsteps"));

        Assert.DoesNotContain(rig.Document.Asset.Buses, bus => bus.Name == "Footsteps");
        Assert.True(rig.Service.Mixer.TryGetBus("Footsteps", out _));
        Assert.False(IsTagged<MGSlider>(rig.Content, "fader:Footsteps"));
        Assert.Contains("Footsteps", StripNames(rig.Content));
        Assert.Contains(TextBlocks(rig.Content), text => text.Text == "Removed bus 'Footsteps' from the asset");
        Assert.Equal(2, UndoEverything());
    }

    [Fact]
    public void RemovingADefaultBus_OrABusWithAChild_IsRefusedWithTheReason()
    {
        var rig = Open();
        Assert.True(rig.Panel.TryAddBus("Footsteps", "Sfx"));
        Assert.True(rig.Panel.TryAddBus("Gravel", "Footsteps"));

        Assert.False(rig.Panel.TryRemoveBus("Sfx"));
        Assert.Contains(TextBlocks(rig.Content), text => text.Text != null && text.Text.Contains("default bus", StringComparison.Ordinal));
        Assert.False(rig.Panel.TryRemoveBus("Footsteps"));
        Assert.Contains(TextBlocks(rig.Content), text => text.Text != null && text.Text.Contains("child bus", StringComparison.Ordinal));
        Assert.False(rig.Panel.TryRemoveBus("Nope"));

        Assert.Contains(rig.Document.Asset.Buses, bus => bus.Name == "Footsteps");
        Assert.Contains(rig.Document.Asset.Buses, bus => bus.Name == "Gravel");
    }

    [Fact]
    public void TheAddButton_AddsTheBusTypedUnderTheChosenParent()
    {
        var rig = Open();
        AddNameBox(rig.Content).SetText("FromUi");
        rig.Content.TraverseVisualTree().OfType<MGComboBox<string>>().Single().SelectedItem = "Sfx";

        ClickCenterOf(rig, Button(rig.Content, "Add"));

        Assert.Equal("Sfx", rig.Document.Asset.Buses.Single(bus => bus.Name == "FromUi").Parent);
        Assert.Equal(string.Empty, AddNameBox(rig.Content).Text);
        Assert.Equal(1, UndoEverything());
    }

    [Fact]
    public void TheDeleteButtonOfACustomBus_RemovesIt()
    {
        var rig = Open(windowWidth: 600);
        Assert.True(rig.Panel.TryAddBus("FromUi", "Master"));

        ClickCenterOf(rig, RemoveButton(rig.Content, "FromUi"));

        Assert.DoesNotContain(rig.Document.Asset.Buses, bus => bus.Name == "FromUi");
    }

    [Fact]
    public void TheAddButton_IsDisabledAtTheBusCapacity_AndTheHintSaysWhy()
    {
        var rig = Open(backend: new SoftwareAudioBackend(new OfflineAudioOutput(), 16));
        Assert.Equal(32, rig.Document.BusCapacity);

        // Master, Editor and the four default buses make six: 26 more reach the capacity of 32.
        for (int index = 0; index < 25; index++)
        {
            Assert.True(rig.Panel.TryAddBus($"Bus{index}", "Master"), $"Bus{index}");
        }

        Assert.True(Button(rig.Content, "Add").IsEnabled);
        Assert.DoesNotContain(TextBlocks(rig.Content), text => text.Text != null && text.Text.StartsWith("Bus capacity reached", StringComparison.Ordinal) && IsShown(text));

        Assert.True(rig.Panel.TryAddBus("Bus25", "Master"));

        Assert.False(Button(rig.Content, "Add").IsEnabled);
        var hint = TextBlocks(rig.Content).Single(text => text.Text == "Bus capacity reached (32)");
        Assert.True(IsShown(hint));
        Assert.False(rig.Panel.TryAddBus("OneTooMany", "Master"));
        Assert.Contains(TextBlocks(rig.Content), text => text.Text != null && text.Text.Contains("capacity of 32", StringComparison.Ordinal));
    }

    private static MGTextBox AddNameBox(MGElement content) => content.TraverseVisualTree().OfType<MGTextBox>().Single();

    [Fact]
    public void TheStrips_AreRebuiltOnlyWhenTheBusSetOrItsStructureChanges()
    {
        var rig = Open();
        var grid = StripGrid(rig.Content);

        rig.Document.SetBusVolume("Sfx", 0.3f);
        rig.Document.SetMute("Voice", true);
        rig.Document.SetSolo("Music", true);
        rig.Document.SetSolo("Music", false);
        Assert.True(rig.Panel.TrySaveLoadedAsset(out _));
        rig.Panel.ApplyToLive();
        Frames(rig, 10);

        Assert.Same(grid, StripGrid(rig.Content));
        Assert.Equal(0.3f, Fader(rig.Content, "Sfx").Value);

        Assert.True(rig.Panel.TryAddBus("Footsteps", "Sfx"));
        var gridAfterAdd = StripGrid(rig.Content);
        Assert.NotSame(grid, gridAfterAdd);

        rig.Document.SetBusVolume("Footsteps", 0.9f);
        Frames(rig, 3);
        Assert.Same(gridAfterAdd, StripGrid(rig.Content));
    }

    // ───────────────────────── faders and gestures ─────────────────────────

    [Fact]
    public void ASimulatedDrag_IsOneHistoryEntry_ClosedByTheFirstIdleFrame()
    {
        var rig = Open();
        string jsonBefore = ReadFile(rig.FullPath).ToString();
        var fader = Fader(rig.Content, "Sfx");

        // Several values in one burst, like the frames of a drag.
        foreach (float value in new[] { 0.45f, 0.4f, 0.35f, 0.3f, 0.25f })
        {
            fader.Value = value;
            rig.Panel.Update(Frame);
        }

        Assert.True(rig.Document.IsGestureOpen);
        Assert.False(CanUndo());
        Assert.Equal(0.25f, AssetVolume(rig, "Sfx"));
        Assert.Equal(0.25f, rig.LiveVolume("Sfx"));
        Assert.Equal("-12.0 dB", Readout(rig.Content, "Sfx").Text);

        // The first frame without change closes it: one entry.
        rig.Panel.Update(Frame);

        Assert.False(rig.Document.IsGestureOpen);
        Assert.True(CanUndo());
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(1, rig.DirtyEvents);
        Assert.Contains(TextBlocks(rig.Content), text => text.Text == $"Modified {RelativeFile}");
        Assert.Equal(1, UndoEverything());
        Assert.Equal(0.5f, AssetVolume(rig, "Sfx"));
        Assert.Equal(0.5f, rig.LiveVolume("Sfx"));
        Assert.Equal(jsonBefore, ReadFile(rig.FullPath).ToString());
    }

    [Fact]
    public void ADragWhoseMouseButtonIsStillPressedOnTheFader_StaysOneGesture_UntilTheButtonIsReleased()
    {
        var rig = Open();
        var fader = Fader(rig.Content, "Sfx");
        rig.Harness.AdvanceFrame(0);
        rig.Harness.AdvanceFrame(16);
        var center = fader.ActualLayoutBounds.Center;
        rig.Harness.AdvanceFrame(32, center, MouseButton.Left);
        Assert.True(fader.IsLMBPressed);

        fader.Value = 0.3f;
        Frames(rig, 1);
        // No change any more, but the button is still down on the fader: still the same gesture.
        for (int frame = 0; frame < 5; frame++)
        {
            rig.Harness.AdvanceFrame(48 + (frame * 16), center, MouseButton.Left);
            Frames(rig, 1);
        }

        Assert.True(rig.Document.IsGestureOpen);
        Assert.False(CanUndo());

        rig.Harness.AdvanceFrame(200, center);
        Assert.False(fader.IsLMBPressed);
        Frames(rig, 1);

        Assert.False(rig.Document.IsGestureOpen);
        Assert.Equal(1, UndoEverything());
    }

    [Fact]
    public void AKeyboardBurst_OnAFader_IsOneEntry()
    {
        var rig = Open();
        var fader = Fader(rig.Content, "Sfx");

        Assert.True(fader.TryHandleNavigationAction(UINavigationAction.MoveLeft));
        Assert.True(fader.TryHandleNavigationAction(UINavigationAction.MoveLeft));
        Assert.True(fader.TryHandleNavigationAction(UINavigationAction.MoveLeft));
        Frames(rig, 3);

        Assert.False(rig.Document.IsGestureOpen);
        Assert.Equal(0.2f, AssetVolume(rig, "Sfx"), 0.001f);
        Assert.Equal(1, UndoEverything());
        Assert.Equal(0.5f, AssetVolume(rig, "Sfx"));
    }

    [Fact]
    public void ADragThatComesBackToItsStart_AddsNoEntry_AndLeavesTheDocumentClean()
    {
        var rig = Open();
        var fader = Fader(rig.Content, "Sfx");

        fader.Value = 0.1f;
        fader.Value = 0.5f;
        Frames(rig, 3);

        Assert.False(CanUndo());
        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(0.5f, rig.LiveVolume("Sfx"));
    }

    [Fact]
    public void UndoAfterADrag_PutsTheFaderBack_WithoutANewEntry_AndRedoMovesItAgain()
    {
        var rig = Open();
        var fader = Fader(rig.Content, "Sfx");
        fader.Value = 0.25f;
        Frames(rig, 2);
        Assert.True(CanUndo());

        EditorHistoryService.Current.SetActiveContext(Context);
        Assert.True(EditorHistoryService.Current.Undo());

        // Writing the restored value into the fader is not a user change: no gesture opens, not even for a frame.
        Assert.False(rig.Document.IsGestureOpen);
        Frames(rig, 5);

        Assert.Equal(0.5f, fader.Value);
        Assert.Equal("-6.0 dB", Readout(rig.Content, "Sfx").Text);
        Assert.Equal(0.5f, rig.LiveVolume("Sfx"));
        Assert.False(CanUndo());
        Assert.True(EditorHistoryService.Current.CanRedo);
        Assert.False(rig.Document.IsGestureOpen);
        Assert.False(rig.Panel.IsDirty);

        Assert.True(EditorHistoryService.Current.Redo());
        Assert.False(rig.Document.IsGestureOpen);
        Frames(rig, 5);

        Assert.Equal(0.25f, fader.Value);
        Assert.Equal("-12.0 dB", Readout(rig.Content, "Sfx").Text);
        Assert.Equal(0.25f, rig.LiveVolume("Sfx"));
        Assert.True(CanUndo());
        Assert.False(EditorHistoryService.Current.CanRedo);
        Assert.Equal(1, UndoEverything());
    }

    [Fact]
    public void WhileAFaderMoves_OnlyItsReadoutChanges_NothingIsRebuilt_AndTheShellWaits()
    {
        var rig = Open();
        var fader = Fader(rig.Content, "Sfx");
        var grid = StripGrid(rig.Content);

        fader.Value = 0.25f;

        Assert.Same(fader, Fader(rig.Content, "Sfx"));
        Assert.Same(grid, StripGrid(rig.Content));
        Assert.Equal("-12.0 dB", Readout(rig.Content, "Sfx").Text);
        Assert.Equal("0.0 dB", Readout(rig.Content, "Music").Text);
        Assert.Contains(TextBlocks(rig.Content), text => text.Text == $"Asset: {RelativeFile}");

        Frames(rig, 2);

        Assert.Same(fader, Fader(rig.Content, "Sfx"));
        Assert.Same(grid, StripGrid(rig.Content));
        Assert.Contains(TextBlocks(rig.Content), text => text.Text == $"Modified {RelativeFile}");
    }

    [Fact]
    public void ADocumentGestureOpenedByTheDocument_MovesTheFaderAndItsReadout()
    {
        var rig = Open();
        var fader = Fader(rig.Content, "Sfx");

        rig.Document.BeginGesture("Fader");
        rig.Document.UpdateBusVolume("Sfx", 0.8f);

        Assert.Equal(0.8f, fader.Value);
        Assert.Equal(AudioMixerPanel.FormatDb(0.8f), Readout(rig.Content, "Sfx").Text);
        Assert.Same(fader, Fader(rig.Content, "Sfx"));

        rig.Document.EndGesture();
        Assert.Equal(0.8f, fader.Value);
    }

    [Fact]
    public void ALiveBusCreatedDuringADrag_IsShownAfterTheGesture_NotUnderTheMouse()
    {
        var rig = Open();
        var fader = Fader(rig.Content, "Sfx");
        fader.Value = 0.3f;

        rig.Service.Mixer.CreateBus("GameBus", "Sfx");
        rig.Panel.Update(Frame);

        Assert.Same(fader, Fader(rig.Content, "Sfx"));
        Assert.DoesNotContain("GameBus", StripNames(rig.Content));

        Frames(rig, 3);

        Assert.Contains("GameBus", StripNames(rig.Content));
        Assert.Equal(0.3f, Fader(rig.Content, "Sfx").Value);
        Assert.Equal(1, UndoEverything());
    }

    [Fact]
    public void ADragOnADocumentThatIsNotLive_ChangesTheAssetOnly()
    {
        var rig = Open(live: false);
        rig.SetLiveVolume("Sfx", 0.1f);

        Fader(rig.Content, "Sfx").Value = 0.9f;
        Frames(rig, 3);

        Assert.Equal(0.9f, AssetVolume(rig, "Sfx"));
        Assert.Equal(0.1f, rig.LiveVolume("Sfx"));
        Assert.Equal(1, UndoEverything());
    }

    [Fact]
    public void DisposingThePanel_MidDrag_ClosesTheGesture_AndGivesTheSavedAssetBackToTheLiveMixer()
    {
        var rig = Open();
        var document = rig.Document;
        Fader(rig.Content, "Sfx").Value = 0.2f;
        Assert.True(document.IsGestureOpen);
        Assert.Equal(0.2f, rig.LiveVolume("Sfx"));

        rig.Panel.Dispose();

        Assert.False(document.IsGestureOpen);
        Assert.True(CanUndo());
        Assert.Equal(0.5f, rig.LiveVolume("Sfx"));
    }

    [Fact]
    public void ReloadingThePanel_GivesTheStripsTheNewDocument_AndTheFadersEditIt()
    {
        var rig = Open();
        EditFileVolume(rig.FullPath, "Sfx", 0.25f);
        var previous = rig.Document;

        Assert.True(rig.Panel.ReloadFromDisk());

        Assert.NotSame(previous, rig.Document);
        Assert.Equal(0.25f, Fader(rig.Content, "Sfx").Value);
        Assert.Equal("-12.0 dB", Readout(rig.Content, "Sfx").Text);

        Fader(rig.Content, "Sfx").Value = 0.75f;
        Frames(rig, 2);

        Assert.Equal(0.75f, AssetVolume(rig, "Sfx"));
        Assert.Equal(0.75f, rig.LiveVolume("Sfx"));
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(1, UndoEverything());
    }

    // ───────────────────────── mute and solo ─────────────────────────

    [Fact]
    public void TheMuteToggle_MutesTheLiveBus_WithoutTouchingTheAssetOrTheHistory()
    {
        var rig = Open();
        Assert.True(rig.Service.Mixer.TryGetBus("Voice", out var voice));

        MuteToggle(rig.Content, "Voice").IsChecked = true;

        Assert.True(voice.IsMuted);
        Assert.True(rig.Document.IsMuted("Voice"));
        Assert.False(rig.Panel.IsDirty);
        Assert.False(CanUndo());

        MuteToggle(rig.Content, "Voice").IsChecked = false;

        Assert.False(voice.IsMuted);
        Assert.False(rig.Document.IsMuted("Voice"));
    }

    [Fact]
    public void TheSoloToggle_MutesTheOtherBusesOfTheAsset_AndGivesThemBackWhenItIsReleased()
    {
        var rig = Open();
        Assert.True(rig.Service.Mixer.TryGetBus("Music", out var music));
        Assert.True(rig.Service.Mixer.TryGetBus("Sfx", out var sfx));
        Assert.True(rig.Service.Mixer.TryGetBus("Voice", out var voice));
        Assert.True(rig.Service.Mixer.TryGetBus("Ui", out var ui));
        Assert.True(rig.Service.Mixer.TryGetBus("Editor", out var editor));

        SoloToggle(rig.Content, "Music").IsChecked = true;

        Assert.False(music.IsMuted);
        Assert.True(sfx.IsMuted);
        Assert.True(voice.IsMuted);
        Assert.True(ui.IsMuted);
        Assert.False(editor.IsMuted);
        Assert.True(rig.Document.IsSoloed("Music"));
        Assert.False(rig.Panel.IsDirty);
        Assert.False(CanUndo());

        // A second solo adds to the first.
        SoloToggle(rig.Content, "Voice").IsChecked = true;
        Assert.False(voice.IsMuted);
        Assert.False(music.IsMuted);
        Assert.True(sfx.IsMuted);

        SoloToggle(rig.Content, "Music").IsChecked = false;
        SoloToggle(rig.Content, "Voice").IsChecked = false;

        Assert.False(music.IsMuted);
        Assert.False(sfx.IsMuted);
        Assert.False(voice.IsMuted);
        Assert.False(ui.IsMuted);
    }

    [Fact]
    public void TheToggles_FollowTheTransientStateOfTheDocument()
    {
        var rig = Open();

        rig.Document.SetMute("Sfx", true);
        rig.Document.SetSolo("Ui", true);

        Assert.True(MuteToggle(rig.Content, "Sfx").IsChecked);
        Assert.False(MuteToggle(rig.Content, "Ui").IsChecked);
        Assert.True(SoloToggle(rig.Content, "Ui").IsChecked);
        Assert.False(SoloToggle(rig.Content, "Sfx").IsChecked);

        rig.Document.ClearTransientState();

        Assert.False(MuteToggle(rig.Content, "Sfx").IsChecked);
        Assert.False(SoloToggle(rig.Content, "Ui").IsChecked);
        Assert.True(rig.Service.Mixer.TryGetBus("Sfx", out var sfx));
        Assert.False(sfx.IsMuted);
    }

    [Fact]
    public void TheMuteAndSoloToggles_OfADocumentThatIsNotLive_LeaveTheLiveMixerAlone()
    {
        var rig = Open(live: false);
        Assert.True(rig.Service.Mixer.TryGetBus("Voice", out var voice));

        MuteToggle(rig.Content, "Voice").IsChecked = true;
        SoloToggle(rig.Content, "Music").IsChecked = true;

        Assert.False(voice.IsMuted);
        Assert.True(rig.Document.IsMuted("Voice"));
    }

    [Fact]
    public void ClosingThePanel_GivesBackTheMutesAndSolosItSet()
    {
        var rig = Open();
        Assert.True(rig.Service.Mixer.TryGetBus("Sfx", out var sfx));
        MuteToggle(rig.Content, "Voice").IsChecked = true;
        SoloToggle(rig.Content, "Music").IsChecked = true;
        Assert.True(sfx.IsMuted);

        rig.Panel.Dispose();

        Assert.All(rig.Service.Mixer.Buses, bus => Assert.False(bus.IsMuted, bus.Name));
    }

    // ───────────────────────── the play session (P51) ─────────────────────────

    [Fact]
    public void DuringAPlaySession_TheFadersAndTheEditsAreDisabled_MuteSoloAndMetersStayActive()
    {
        var rig = Open();
        Assert.True(rig.Panel.TryAddBus("Footsteps", "Sfx"));
        Assert.True(rig.Panel.TrySaveLoadedAsset(out _));
        rig.Document.SetBusVolume("Sfx", 0.4f);
        Assert.False(IsShown(PlayLockBanner(rig.Content)));

        EditorHistoryService.Current.IsSuspended = true;
        rig.Panel.Update(Frame);

        Assert.Equal("Stop play mode to edit the mixer", PlayLockBanner(rig.Content).Text);
        Assert.True(IsShown(PlayLockBanner(rig.Content)));
        foreach (string bus in DefaultBuses.Append("Footsteps"))
        {
            Assert.False(Fader(rig.Content, bus).IsEnabled, bus);
            Assert.True(MuteToggle(rig.Content, bus).IsEnabled, bus);
            Assert.True(SoloToggle(rig.Content, bus).IsEnabled, bus);
        }

        Assert.False(RemoveButton(rig.Content, "Footsteps").IsEnabled);
        Assert.False(Button(rig.Content, "Add").IsEnabled);
        Assert.False(Button(rig.Content, "Save").IsEnabled);
        Assert.False(Button(rig.Content, "Apply to live mixer").IsEnabled);

        // The operations are refused too, not only their controls.
        Assert.False(rig.Panel.TryAddBus("More", "Master"));
        Assert.False(rig.Panel.TryRemoveBus("Footsteps"));
        Assert.Contains(TextBlocks(rig.Content), text => text.Text == "Mixer edits are disabled during a play session.");
        Assert.DoesNotContain(rig.Document.Asset.Buses, bus => bus.Name == "More");
        Assert.Contains(rig.Document.Asset.Buses, bus => bus.Name == "Footsteps");

        // Mute and solo keep working, and reach the live mixer.
        Assert.True(rig.Service.Mixer.TryGetBus("Voice", out var voice));
        MuteToggle(rig.Content, "Voice").IsChecked = true;
        Assert.True(voice.IsMuted);
        SoloToggle(rig.Content, "Music").IsChecked = true;
        Assert.True(rig.Service.Mixer.GetBus("Sfx").IsMuted);

        // The session ends: everything is back.
        EditorHistoryService.Current.IsSuspended = false;
        rig.Panel.Update(Frame);

        Assert.False(IsShown(PlayLockBanner(rig.Content)));
        foreach (string bus in DefaultBuses.Append("Footsteps"))
        {
            Assert.True(Fader(rig.Content, bus).IsEnabled, bus);
        }

        Assert.True(RemoveButton(rig.Content, "Footsteps").IsEnabled);
        Assert.True(Button(rig.Content, "Add").IsEnabled);
        Assert.True(Button(rig.Content, "Save").IsEnabled);
        Assert.True(Button(rig.Content, "Apply to live mixer").IsEnabled);
    }

    [Fact]
    public void APanelOpenedDuringAPlaySession_StartsLocked()
    {
        EditorHistoryService.Current.IsSuspended = true;
        var rig = Open();

        Assert.All(DefaultBuses, bus => Assert.False(Fader(rig.Content, bus).IsEnabled, bus));
        Assert.False(Button(rig.Content, "Add").IsEnabled);
        Assert.False(Button(rig.Content, "Save").IsEnabled);
        Assert.True(IsShown(PlayLockBanner(rig.Content)));
        Assert.True(MuteToggle(rig.Content, "Sfx").IsEnabled);
    }

    [Fact]
    public void APlaySessionStartingMidDrag_ClosesTheGesture_AndKeepsItsEntry()
    {
        var rig = Open();
        Fader(rig.Content, "Sfx").Value = 0.25f;
        Assert.True(rig.Document.IsGestureOpen);

        EditorHistoryService.Current.IsSuspended = true;
        rig.Panel.Update(Frame);

        // The service refuses commands now, yet the change made before the session started is recorded.
        Assert.False(rig.Document.IsGestureOpen);
        Assert.True(CanUndo());
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(0.25f, AssetVolume(rig, "Sfx"));
        Assert.False(Fader(rig.Content, "Sfx").IsEnabled);

        EditorHistoryService.Current.IsSuspended = false;
        rig.Panel.Update(Frame);
        Assert.Equal(1, UndoEverything());
        Assert.Equal(0.5f, AssetVolume(rig, "Sfx"));
    }

    [Fact]
    public void AFaderChangeThatRacesTheFirstFrameOfASession_IsTakenBack()
    {
        var rig = Open();
        EditorHistoryService.Current.IsSuspended = true;

        // The panel has not seen the session yet: its fader is still enabled.
        Fader(rig.Content, "Sfx").Value = 0.2f;

        Assert.Equal(0.5f, Fader(rig.Content, "Sfx").Value);
        Assert.Equal(0.5f, AssetVolume(rig, "Sfx"));
        Assert.Equal(0.5f, rig.LiveVolume("Sfx"));
        Assert.False(rig.Document.IsGestureOpen);
        Assert.False(rig.Panel.IsDirty);
    }

    private static MGTextBlock PlayLockBanner(MGElement content) => BannerStartingWith(content, "Stop play mode");

    // ───────────────────────── meters ─────────────────────────

    private sealed class MeteredRig
    {
        public MeteredRig(AudioMixerPanelTests tests)
        {
            Output = new OfflineAudioOutput();
            Rig = tests.Open(backend: new SoftwareAudioBackend(Output, 16));
            Rig.Service.MasterLimiter.IsEnabled = false;
        }

        public OfflineAudioOutput Output { get; }

        public Rig Rig { get; }

        public void PlayConstant(string bus, short value)
        {
            Rig.Service.PlayClip(ConstantStereoClip(value, 480 * 700), bus, AudioVoiceParameters.Default);
            Rig.Service.Update(0.01f);
        }

        public void Pump(int blocks)
        {
            for (int block = 0; block < blocks; block++)
            {
                Output.Pump(480);
            }
        }
    }

    [Fact]
    public void TheMeters_ReadTheLiveMixer_ThroughTheModelOfThePanel_NotThroughAnotherCursor()
    {
        var metered = new MeteredRig(this);
        var rig = metered.Rig;
        metered.PlayConstant("Sfx", 16384);

        // Another reader (the Audio panel) takes its own blocks: the panel's model keeps its own cursor.
        var other = new AudioProfilerModel(() => rig.Service);
        metered.Pump(3);
        other.ReadMeters(Frame);
        rig.Panel.Update(Frame);

        var sfx = rig.Panel.MeterModel.Buses.Single(track => track.Name == "Sfx");
        var music = rig.Panel.MeterModel.Buses.Single(track => track.Name == "Music");
        Assert.True(sfx.PeakDb > AudioMeterScale.FloorDb);
        Assert.Equal(AudioMeterScale.FloorDb, music.PeakDb);
        Assert.NotSame(other.Buses.Single(track => track.Name == "Sfx"), sfx);
        // The strips draw the model's tracks: one meter each, Master and Editor included.
        Assert.Equal(6, MeterCount(rig.Content));

        rig.Panel.ResetMeters();
        Assert.Equal(AudioMeterScale.FloorDb, sfx.PeakDb);
    }

    [Fact]
    public void WithoutAMeteringBackend_TheStripsStillShow_WithSilentMeters()
    {
        var rig = Open(backend: new FakeAudioBackend());

        rig.Panel.Update(Frame);

        Assert.False(rig.Panel.MeterModel.IsMeteringAvailable);
        Assert.Equal(6, MeterCount(rig.Content));
        Assert.Equal(6, StripNames(rig.Content).Count);
    }

    [Fact]
    public void Update_ReadingTheMeters_AllocatesNothing()
    {
        var metered = new MeteredRig(this);
        var rig = metered.Rig;
        metered.PlayConstant("Sfx", 16384);
        metered.PlayConstant("Music", 8192);

        // Warm up: the model builds its meters, the strips are rebuilt for them, the code compiles.
        for (int frame = 0; frame < 10; frame++)
        {
            metered.Pump(1);
            rig.Panel.Update(Frame);
        }

        long allocated = 0;
        for (int frame = 0; frame < 30; frame++)
        {
            metered.Pump(1);

            long before = AllocationWindow.Start();
            rig.Panel.Update(Frame);
            allocated += GC.GetAllocatedBytesForCurrentThread() - before;
        }

        Assert.Equal(0, allocated);
        Assert.True(rig.Panel.MeterModel.Buses.Single(track => track.Name == "Sfx").PeakDb > AudioMeterScale.FloorDb);
    }

    [Fact]
    public void Update_WhileAGestureIsOpen_AllocatesNothing()
    {
        var rig = Open();
        var fader = Fader(rig.Content, "Sfx");
        rig.Harness.AdvanceFrame(0);
        rig.Harness.AdvanceFrame(16);
        var center = fader.ActualLayoutBounds.Center;
        rig.Harness.AdvanceFrame(32, center, MouseButton.Left);
        fader.Value = 0.3f;
        for (int frame = 0; frame < 10; frame++)
        {
            rig.Panel.Update(Frame);
        }

        Assert.True(rig.Document.IsGestureOpen);

        long before = AllocationWindow.Start();
        rig.Panel.Update(Frame);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(rig.Document.IsGestureOpen);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void DuringAPlaySession_ReloadIsDisabledAndRefused_EvenForACleanDocument()
    {
        var rig = Open();
        Assert.False(rig.Document.IsDirty);
        Assert.True(Button(rig.Content, "Reload").IsEnabled);

        EditorHistoryService.Current.IsSuspended = true;
        rig.Panel.Update(Frame);

        Assert.False(Button(rig.Content, "Reload").IsEnabled);
        Assert.False(rig.Panel.ReloadFromDisk());

        EditorHistoryService.Current.IsSuspended = false;
        rig.Panel.Update(Frame);

        Assert.True(Button(rig.Content, "Reload").IsEnabled);
    }
}
