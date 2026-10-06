using System.Reflection;
using CasaEngine.Editor;
using CasaEngine.Editor.Controls;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Styling;
using CasaEngine.Editor.Workspaces;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.Audio;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Effects;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Tests.Audio;
using CasaEngine.Tests.ContentBrowser;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.FillBrushes;
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

    // ───────────────────────── bus detail: effects, sends and ducking (T10.7) ─────────────────────────

    /// <summary>
    /// Sfx carrying the first <paramref name="effects"/> of a biquad (high-pass at 200 Hz), a compressor, a reverb and a ducking by
    /// Voice, and two sends (to Reverb, a return under Master, and to Ambience, under Music); Footsteps is a child of Sfx.
    /// </summary>
    private static Action<AudioMixerAsset> DetailAsset(int effects = 4)
    {
        return asset =>
        {
            asset.Buses.Add(new AudioMixerBusData { Name = "Reverb", Parent = "Master" });
            asset.Buses.Add(new AudioMixerBusData { Name = "Ambience", Parent = "Music" });
            asset.Buses.Add(new AudioMixerBusData { Name = "Footsteps", Parent = "Sfx" });

            var all = new AudioMixerEffectData[]
            {
                new AudioMixerBiquadEffectData(BiquadFilterType.HighPass, 200f, 1f, 0f),
                new AudioMixerCompressorEffectData(),
                new AudioMixerReverbEffectData(),
                new AudioMixerDuckingEffectData("Voice"),
            };

            var sfx = asset.Buses.Single(bus => bus.Name == "Sfx");
            for (int index = 0; index < effects; index++)
            {
                sfx.Effects.Add(all[index]);
            }

            sfx.Sends.Add(new AudioMixerSendData("Reverb", 0.3f));
            sfx.Sends.Add(new AudioMixerSendData("Ambience", 0.2f));
        };
    }

    private Rig OpenDetail(int effects = 4, IAudioBackend backend = null)
    {
        var rig = Open(customize: DetailAsset(effects), backend: backend);
        Assert.True(rig.Panel.SelectBus("Sfx"));
        return rig;
    }

    private static NumericField Field(MGElement content, string tag) => Tagged<NumericField>(content, tag);

    private static MGComboBox<string> Combo(MGElement content, string tag) => Tagged<MGComboBox<string>>(content, tag);

    private static MGButton DetailButton(MGElement content, string tag) => Tagged<MGButton>(content, tag);

    private static void Press(MGButton button) => Assert.True(button.TryHandleNavigationAction(UINavigationAction.Submit));

    private static MGTextBox TextBoxOf(NumericField field) => field.TraverseVisualTree().OfType<MGTextBox>().Single();

    /// <summary>Types the characters one after the other, the way the keyboard does: the text of the box changes after each one.</summary>
    private static void Type(NumericField field, string text)
    {
        var box = TextBoxOf(field);
        for (int length = 1; length <= text.Length; length++)
        {
            box.SetText(text.Substring(0, length));
        }
    }

    private static List<AudioMixerEffectData> AssetEffects(Rig rig, string bus = "Sfx")
        => rig.Document.Asset.Buses.Single(data => data.Name == bus).Effects;

    private static IReadOnlyList<AudioEffect> LiveEffects(Rig rig, string bus = "Sfx") => rig.Service.Mixer.GetBus(bus).Effects;

    private static bool HasPending(Rig rig) => rig.Panel.DetailView.HasPendingEdit;

    private static bool StatusContains(Rig rig, string text)
        => TextBlocks(rig.Content).Any(block => block.Text != null && block.Text.Contains(text, StringComparison.Ordinal));

    private static bool IsHighlighted(MGTextBlock name)
        => name.BackgroundBrush?.NormalValue is MGSolidFillBrush solid && solid.Color == EditorThemePalette.AccentSelection;

    private static int ParameterFieldCount(MGElement content, int effect)
        => content.TraverseVisualTree().OfType<NumericField>().Count(field => ((string)field.Tag).StartsWith($"effect-param:{effect}:", StringComparison.Ordinal));

    /// <summary>The controls of the detail view that edit: buttons, fields and combos, by the tags they carry.</summary>
    private static List<MGElement> EditingControls(MGElement content)
        => content.TraverseVisualTree()
            .Where(element => element.Tag is string tag
                              && (tag.StartsWith("effect-", StringComparison.Ordinal) || tag.StartsWith("send-", StringComparison.Ordinal)))
            .ToList();

    private static void SetKeyboardFocus(MGDesktop desktop, MGElement element)
    {
        var setter = typeof(MGDesktop).GetProperty(nameof(MGDesktop.FocusedKeyboardHandler), BindingFlags.Public | BindingFlags.Instance)!.GetSetMethod(true)!;
        setter.Invoke(desktop, new object[] { element });
    }

    private static void AssertEffectTypes(Rig rig, params Type[] expected)
    {
        Assert.Equal(expected, AssetEffects(rig).Select(data => data.GetType()));
        var live = LiveEffects(rig);
        Assert.Equal(expected.Length, live.Count);
        var liveTypes = live.Select(effect => effect.GetType()).ToArray();
        var wanted = expected.Select(type =>
            type == typeof(AudioMixerBiquadEffectData) ? typeof(BiquadFilterEffect)
            : type == typeof(AudioMixerCompressorEffectData) ? typeof(CompressorEffect)
            : type == typeof(AudioMixerReverbEffectData) ? typeof(ReverbEffect)
            : typeof(DuckingEffect)).ToArray();
        Assert.Equal(wanted, liveTypes);
    }

    private static readonly Type BiquadData = typeof(AudioMixerBiquadEffectData);
    private static readonly Type CompressorData = typeof(AudioMixerCompressorEffectData);
    private static readonly Type ReverbData = typeof(AudioMixerReverbEffectData);
    private static readonly Type DuckingData = typeof(AudioMixerDuckingEffectData);

    private static float WetOf(Rig rig) => ((AudioMixerReverbEffectData)AssetEffects(rig)[2]).Wet;

    // ----- selection

    [Fact]
    public void WithoutASelection_TheDetailShowsAHint_AndNoControl()
    {
        var rig = Open(customize: DetailAsset());

        Assert.Null(rig.Panel.SelectedBus);
        Assert.True(IsTagged<MGTextBlock>(rig.Content, "detail-hint"));
        Assert.Empty(EditingControls(rig.Content));
        Assert.Empty(rig.Content.TraverseVisualTree().OfType<NumericField>());
    }

    [Fact]
    public void ClickingTheNameOfABus_SelectsIt_ShowsItsDetail_AndMarksItsStrip()
    {
        var rig = Open(customize: DetailAsset());

        ClickCenterOf(rig, Tagged<MGTextBlock>(rig.Content, "bus:Sfx"));

        Assert.Equal("Sfx", rig.Panel.SelectedBus);
        Assert.Equal("[b]Effects and sends of Sfx[/b]", Tagged<MGTextBlock>(rig.Content, "detail-title").Text);
        Assert.False(IsTagged<MGTextBlock>(rig.Content, "detail-hint"));
        Assert.True(IsHighlighted(Tagged<MGTextBlock>(rig.Content, "bus:Sfx")));
        Assert.False(IsHighlighted(Tagged<MGTextBlock>(rig.Content, "bus:Music")));
        Assert.Equal(4, ParameterFieldCount(rig.Content, 3));

        // Another bus: the selection and the mark move, nothing else happens.
        ClickCenterOf(rig, Tagged<MGTextBlock>(rig.Content, "bus:Music"));

        Assert.Equal("Music", rig.Panel.SelectedBus);
        Assert.Equal("[b]Effects and sends of Music[/b]", Tagged<MGTextBlock>(rig.Content, "detail-title").Text);
        Assert.True(IsHighlighted(Tagged<MGTextBlock>(rig.Content, "bus:Music")));
        Assert.False(IsHighlighted(Tagged<MGTextBlock>(rig.Content, "bus:Sfx")));
        Assert.Equal(0, ParameterFieldCount(rig.Content, 0));
        Assert.False(CanUndo());
        Assert.False(rig.Panel.IsDirty);
    }

    [Fact]
    public void OnlyTheBusesOfTheAsset_AreSelectable()
    {
        var rig = Open(customize: DetailAsset());
        rig.Service.Mixer.CreateBus("GameBus", "Master");
        Frames(rig, 2);

        Assert.False(IsTagged<MGTextBlock>(rig.Content, "bus:Master"));
        Assert.False(IsTagged<MGTextBlock>(rig.Content, "bus:Editor"));
        Assert.False(IsTagged<MGTextBlock>(rig.Content, "bus:GameBus"));
        Assert.False(rig.Panel.SelectBus("Master"));
        Assert.False(rig.Panel.SelectBus("Editor"));
        Assert.False(rig.Panel.SelectBus("GameBus"));
        Assert.False(rig.Panel.SelectBus("Nope"));
        Assert.Null(rig.Panel.SelectedBus);

        Assert.True(rig.Panel.SelectBus("sfx"));
        Assert.Equal("Sfx", rig.Panel.SelectedBus);
    }

    [Fact]
    public void WhenTheSelectedBusIsRemoved_OrItsAdditionUndone_TheDetailGoesBackToTheHint()
    {
        var rig = Open();
        Assert.True(rig.Panel.TryAddBus("Footsteps", "Sfx"));
        Assert.True(rig.Panel.SelectBus("Footsteps"));
        Assert.True(IsTagged<MGButton>(rig.Content, "effect-add"));

        Assert.True(rig.Panel.TryRemoveBus("Footsteps"));

        Assert.Null(rig.Panel.SelectedBus);
        Assert.False(IsTagged<MGButton>(rig.Content, "effect-add"));
        Assert.True(IsTagged<MGTextBlock>(rig.Content, "detail-hint"));

        // The removal undone brings the bus back, not the selection; the addition undone while selected clears it.
        EditorHistoryService.Current.SetActiveContext(Context);
        Assert.True(EditorHistoryService.Current.Undo());
        Assert.Null(rig.Panel.SelectedBus);
        Assert.True(rig.Panel.SelectBus("Footsteps"));
        Assert.True(EditorHistoryService.Current.Undo());

        Assert.Null(rig.Panel.SelectedBus);
        Assert.False(IsTagged<MGButton>(rig.Content, "effect-add"));
        Assert.False(IsTagged<MGTextBlock>(rig.Content, "bus:Footsteps"));
    }

    // ----- rows

    [Fact]
    public void ABusWithTheFourEffectKindsAndTwoSends_ShowsOneRowPerEffectAndPerSend()
    {
        var rig = OpenDetail();

        var titles = TextBlocks(rig.Content)
            .Select(text => text.Text)
            .Where(text => text != null && text.Length > 6 && text.StartsWith("[b]", StringComparison.Ordinal) && char.IsDigit(text[3]) && text[4] == '.')
            .ToList();
        Assert.Equal(new[] { "[b]1. Biquad filter[/b]", "[b]2. Compressor[/b]", "[b]3. Reverb[/b]", "[b]4. Ducking[/b]" }, titles);
        Assert.Equal(new[] { 3, 6, 5, 4 }, Enumerable.Range(0, 4).Select(effect => ParameterFieldCount(rig.Content, effect)));
        Assert.Contains(TextBlocks(rig.Content), text => text.Text != null && text.Text.StartsWith("[b]Effects[/b] (4/4)", StringComparison.Ordinal));
        Assert.Contains(TextBlocks(rig.Content), text => text.Text != null && text.Text.StartsWith("[b]Sends[/b] (2/4)", StringComparison.Ordinal));

        for (int effect = 0; effect < 4; effect++)
        {
            Assert.True(IsTagged<MGButton>(rig.Content, $"effect-up:{effect}"), $"up {effect}");
            Assert.True(IsTagged<MGButton>(rig.Content, $"effect-down:{effect}"), $"down {effect}");
            Assert.True(IsTagged<MGButton>(rig.Content, $"effect-delete:{effect}"), $"delete {effect}");
        }

        Assert.False(IsTagged<MGButton>(rig.Content, "effect-up:4"));
        Assert.Equal(2, rig.Content.TraverseVisualTree().OfType<NumericField>().Count(field => ((string)field.Tag).StartsWith("send-level:", StringComparison.Ordinal)));
        Assert.True(IsTagged<NumericField>(rig.Content, "send-new-level"));
        Assert.True(IsTagged<MGComboBox<string>>(rig.Content, "send-new-target"));
        Assert.True(IsTagged<MGComboBox<string>>(rig.Content, "effect-add-kind"));
    }

    [Fact]
    public void TheFields_ShowTheValuesOfTheAsset_WithTheBoundsAndStepsOfTheCatalog()
    {
        var rig = OpenDetail();

        for (int effect = 0; effect < 4; effect++)
        {
            var data = AssetEffects(rig)[effect];
            var parameters = AudioMixerEffectCatalog.GetParameters(AudioMixerEffectCatalog.GetKind(data));
            for (int parameter = 0; parameter < parameters.Count; parameter++)
            {
                var field = Field(rig.Content, $"effect-param:{effect}:{parameter}");
                Assert.Equal(parameters[parameter].Min, field.Min);
                Assert.Equal(parameters[parameter].Max, field.Max);
                Assert.Equal(parameters[parameter].Step, field.Step);
                Assert.Equal(AudioMixerEffectCatalog.GetValue(data, parameter), field.Value);
            }
        }

        Assert.Equal(200f, Field(rig.Content, "effect-param:0:0").Value);
        Assert.Equal("High-pass", Combo(rig.Content, "effect-filter:0").SelectedItem);
        Assert.Equal("Voice", Combo(rig.Content, "effect-source:3").SelectedItem);
        Assert.Equal(0.3f, Field(rig.Content, "send-level:Reverb").Value);
        Assert.Equal(0.2f, Field(rig.Content, "send-level:Ambience").Value);
        Assert.Equal(0f, Field(rig.Content, "send-new-level").Value);
        Assert.Equal("Biquad filter", Combo(rig.Content, "effect-add-kind").SelectedItem);
    }

    [Fact]
    public void TheCombos_OfferTheBusesTheDocumentAccepts_NotTheBusItself()
    {
        var rig = OpenDetail();

        var sources = Combo(rig.Content, "effect-source:3").ItemsSource.ToList();
        Assert.DoesNotContain("Sfx", sources);
        Assert.All(new[] { "Music", "Voice", "Ui", "Reverb", "Ambience", "Footsteps", "Master", "Editor" }, name => Assert.Contains(name, sources));

        // A send target: the other buses, the returns included, minus the ones already sent to.
        var targets = Combo(rig.Content, "send-new-target").ItemsSource.ToList();
        Assert.DoesNotContain("Sfx", targets);
        Assert.DoesNotContain("Reverb", targets);
        Assert.DoesNotContain("Ambience", targets);
        Assert.All(new[] { "Music", "Voice", "Ui", "Footsteps", "Master", "Editor" }, name => Assert.Contains(name, targets));
        Assert.Equal(targets[0], Combo(rig.Content, "send-new-target").SelectedItem);

        Assert.Equal(
            new[] { "Low-pass", "High-pass", "Band-pass", "Peaking", "Low shelf", "High shelf" },
            Combo(rig.Content, "effect-filter:0").ItemsSource.ToArray());
        Assert.Equal(
            new[] { "Biquad filter", "Compressor", "Reverb", "Ducking" },
            Combo(rig.Content, "effect-add-kind").ItemsSource.ToArray());
    }

    [Fact]
    public void TheChoices_FollowTheBusesThatAppear_TheAssetsAndTheLiveMixers()
    {
        var rig = OpenDetail();
        Assert.True(rig.Panel.TryAddBus("Extra", "Master"));

        Assert.Contains("Extra", Combo(rig.Content, "effect-source:3").ItemsSource);
        Assert.Contains("Extra", Combo(rig.Content, "send-new-target").ItemsSource);

        rig.Service.Mixer.CreateBus("GameBus", "Master");
        Frames(rig, 2);

        Assert.Contains("GameBus", Combo(rig.Content, "effect-source:3").ItemsSource);
        Assert.Contains("GameBus", Combo(rig.Content, "send-new-target").ItemsSource);
    }

    [Fact]
    public void TheMoveButtons_AreDisabledAtTheEnds_AndTheAddButtonAtFourEffects()
    {
        var rig = OpenDetail();

        Assert.False(DetailButton(rig.Content, "effect-up:0").IsEnabled);
        Assert.True(DetailButton(rig.Content, "effect-up:1").IsEnabled);
        Assert.True(DetailButton(rig.Content, "effect-down:2").IsEnabled);
        Assert.False(DetailButton(rig.Content, "effect-down:3").IsEnabled);
        Assert.True(DetailButton(rig.Content, "effect-delete:0").IsEnabled);
        Assert.False(DetailButton(rig.Content, "effect-add").IsEnabled);

        // One effect fewer: there is room again.
        Press(DetailButton(rig.Content, "effect-delete:3"));

        Assert.True(DetailButton(rig.Content, "effect-add").IsEnabled);
        Assert.False(DetailButton(rig.Content, "effect-down:2").IsEnabled);
    }

    // ----- one history entry per user intent: the fields

    [Fact]
    public void TypingInAField_IsOneHistoryEntry_WrittenHalfASecondAfterTheLastChange()
    {
        var rig = OpenDetail();
        var field = Field(rig.Content, "effect-param:2:2"); // the wet level of the reverb
        var liveReverb = Assert.IsType<ReverbEffect>(LiveEffects(rig)[2]);
        Assert.Equal(1f, field.Value);

        Type(field, "0.25");

        // The burst is open: the document does not know it yet, but the panel counts it as unsaved.
        Assert.True(HasPending(rig));
        Assert.True(rig.Panel.IsDirty);
        Assert.False(rig.Document.IsDirty);
        Assert.False(CanUndo());
        Assert.Equal(1f, WetOf(rig));
        Assert.Equal(1f, liveReverb.Wet);

        rig.Panel.Update(0.4f);
        Assert.True(HasPending(rig));
        Assert.False(CanUndo());

        rig.Panel.Update(0.1f);

        Assert.False(HasPending(rig));
        Assert.True(CanUndo());
        Assert.True(rig.Document.IsDirty);
        Assert.Equal(1, rig.DirtyEvents);
        Assert.Equal(0.25f, WetOf(rig));
        Assert.Same(liveReverb, LiveEffects(rig)[2]);
        Assert.Equal(0.25f, liveReverb.Wet);
        Assert.Same(field, Field(rig.Content, "effect-param:2:2"));
        Assert.Equal(1, UndoEverything());
        Assert.Equal(1f, WetOf(rig));
        Assert.Equal(1f, liveReverb.Wet);
        Assert.Equal(1f, field.Value);
    }

    [Fact]
    public void EveryChangeOfTheBurst_RestartsTheHalfSecond()
    {
        var rig = OpenDetail();
        var field = Field(rig.Content, "effect-param:2:2");

        Type(field, "0.25");
        rig.Panel.Update(0.4f);
        TextBoxOf(field).SetText("0.3");
        rig.Panel.Update(0.4f);

        Assert.True(HasPending(rig));
        Assert.False(CanUndo());

        rig.Panel.Update(0.1f);

        Assert.False(HasPending(rig));
        Assert.Equal(0.3f, WetOf(rig));
        Assert.Equal(1, UndoEverything());
    }

    [Fact]
    public void AFieldThatLosesTheKeyboardFocus_WritesItsBurstAtOnce()
    {
        var rig = OpenDetail();
        var field = Field(rig.Content, "effect-param:2:2");
        SetKeyboardFocus(rig.Harness.Desktop, TextBoxOf(field));
        Type(field, "0.25");
        Assert.True(HasPending(rig));

        SetKeyboardFocus(rig.Harness.Desktop, null);

        Assert.False(HasPending(rig));
        Assert.True(CanUndo());
        Assert.Equal(0.25f, WetOf(rig));
        Assert.Equal(0.25f, ((ReverbEffect)LiveEffects(rig)[2]).Wet);
        Assert.Equal(1, UndoEverything());
    }

    [Fact]
    public void TheFocusMovingInsideTheField_KeepsTheBurstOpen()
    {
        var rig = OpenDetail();
        var field = Field(rig.Content, "effect-param:2:2");
        var plus = field.TraverseVisualTree().OfType<MGButton>().Single(button => button.TraverseVisualTree().OfType<MGTextBlock>().Any(text => text.Text == "+"));
        SetKeyboardFocus(rig.Harness.Desktop, TextBoxOf(field));
        Type(field, "0.25");

        SetKeyboardFocus(rig.Harness.Desktop, plus);

        Assert.True(HasPending(rig));
        Assert.False(CanUndo());
    }

    [Fact]
    public void AFieldThatLosesTheFocus_ShowsItsValueInTheCanonicalForm()
    {
        var rig = OpenDetail();
        var frequency = Field(rig.Content, "effect-param:0:0");
        SetKeyboardFocus(rig.Harness.Desktop, TextBoxOf(frequency));

        // More than the engine accepts: the field clamps the value, not the text.
        Type(frequency, "99999");
        SetKeyboardFocus(rig.Harness.Desktop, null);

        Assert.Equal(BiquadFilterEffect.MaxFrequencyHz, ((AudioMixerBiquadEffectData)AssetEffects(rig)[0]).FrequencyHz);
        Assert.Equal("24000", TextBoxOf(frequency).Text);

        // Not a number: nothing changes, and the text goes back to the value.
        SetKeyboardFocus(rig.Harness.Desktop, TextBoxOf(frequency));
        TextBoxOf(frequency).SetText("abc");
        Assert.False(HasPending(rig));
        SetKeyboardFocus(rig.Harness.Desktop, null);

        Assert.Equal("24000", TextBoxOf(frequency).Text);
        Assert.Equal(1, UndoEverything());
    }

    [Fact]
    public void TheStepButtonsOfAField_AreOneBurst()
    {
        var rig = OpenDetail();
        var field = Field(rig.Content, "effect-param:2:2");
        var minus = field.TraverseVisualTree().OfType<MGButton>().Single(button => button.TraverseVisualTree().OfType<MGTextBlock>().Any(text => text.Text == "−"));

        Press(minus);
        Press(minus);
        Press(minus);
        rig.Panel.Update(0.5f);

        Assert.Equal(0.85f, WetOf(rig), 0.001f);
        Assert.Equal(1, UndoEverything());
        Assert.Equal(1f, WetOf(rig));
    }

    [Fact]
    public void AnotherFieldChanging_WritesTheOpenBurstFirst_ThenOpensItsOwn()
    {
        var rig = OpenDetail();

        Type(Field(rig.Content, "effect-param:2:2"), "0.25");
        Type(Field(rig.Content, "effect-param:2:3"), "0.5"); // the dry level of the same reverb

        // The first burst is an entry already; the second is still open, and its field keeps what was typed in it.
        Assert.True(CanUndo());
        Assert.True(HasPending(rig));
        Assert.Equal(0.25f, WetOf(rig));
        Assert.Equal(0f, ((AudioMixerReverbEffectData)AssetEffects(rig)[2]).Dry);
        Assert.Equal(0.5f, Field(rig.Content, "effect-param:2:3").Value);
        Assert.Equal("0.5", TextBoxOf(Field(rig.Content, "effect-param:2:3")).Text);

        rig.Panel.Update(0.5f);

        Assert.Equal(0.5f, ((AudioMixerReverbEffectData)AssetEffects(rig)[2]).Dry);
        Assert.Equal(2, UndoEverything());
    }

    [Fact]
    public void AnotherIntent_WritesTheOpenBurstFirst_SoTheEntriesKeepTheirOrder()
    {
        var rig = OpenDetail();
        Type(Field(rig.Content, "effect-param:1:0"), "-30"); // the threshold of the compressor

        Press(DetailButton(rig.Content, "effect-down:0"));

        Assert.False(HasPending(rig));
        AssertEffectTypes(rig, CompressorData, BiquadData, ReverbData, DuckingData);
        Assert.Equal(-30f, ((AudioMixerCompressorEffectData)AssetEffects(rig)[0]).ThresholdDb);

        // Undo takes the move back first: the typed value was written before it.
        EditorHistoryService.Current.SetActiveContext(Context);
        Assert.True(EditorHistoryService.Current.Undo());
        AssertEffectTypes(rig, BiquadData, CompressorData, ReverbData, DuckingData);
        Assert.Equal(-30f, ((AudioMixerCompressorEffectData)AssetEffects(rig)[1]).ThresholdDb);

        Assert.True(EditorHistoryService.Current.Undo());
        Assert.Equal(AudioMixerEffectDefaults.CompressorThresholdDb, ((AudioMixerCompressorEffectData)AssetEffects(rig)[1]).ThresholdDb);
        Assert.False(CanUndo());
    }

    [Fact]
    public void Save_WritesTheOpenBurstFirst()
    {
        var rig = OpenDetail();
        Type(Field(rig.Content, "effect-param:2:2"), "0.25");
        Assert.True(rig.Panel.IsDirty);

        Assert.True(rig.Panel.TrySaveLoadedAsset(out string error), error);

        Assert.False(HasPending(rig));
        Assert.False(rig.Panel.IsDirty);
        Assert.True(AudioMixerPanel.TryLoadAsset(rig.FullPath, out var saved, out error), error);
        var reverb = Assert.IsType<AudioMixerReverbEffectData>(saved.Buses.Single(bus => bus.Name == "Sfx").Effects[2]);
        Assert.Equal(0.25f, reverb.Wet);
        Assert.Equal(1, UndoEverything());
    }

    [Fact]
    public void ReloadWithAnOpenBurst_IsRefusedLikeAnyUnsavedChange()
    {
        var rig = OpenDetail();
        Type(Field(rig.Content, "effect-param:2:2"), "0.25");

        Assert.False(rig.Panel.ReloadFromDisk());

        Assert.False(HasPending(rig));
        Assert.Equal(0.25f, WetOf(rig));
        Assert.True(rig.Document.IsDirty);
        Assert.True(StatusContains(rig, "Unsaved changes kept"));
    }

    [Fact]
    public void ApplyWithAnOpenBurst_WritesItFirst_ThenAppliesTheAsset()
    {
        var rig = OpenDetail();
        var liveReverb = Assert.IsType<ReverbEffect>(LiveEffects(rig)[2]);
        Type(Field(rig.Content, "effect-param:2:2"), "0.25");

        rig.Panel.ApplyToLive();

        Assert.False(HasPending(rig));
        Assert.Equal(0.25f, WetOf(rig));
        Assert.Equal(0.25f, liveReverb.Wet);
        Assert.Equal(1, UndoEverything());
    }

    [Fact]
    public void ClosingThePanelWithAnOpenBurst_DropsIt_AndLeavesTheLiveMixerAtTheSavedAsset()
    {
        var rig = OpenDetail();
        var liveReverb = Assert.IsType<ReverbEffect>(LiveEffects(rig)[2]);
        Type(Field(rig.Content, "effect-param:2:2"), "0.25");

        rig.Panel.Dispose();

        Assert.Equal(1f, liveReverb.Wet);
        Assert.Equal(1f, ((ReverbEffect)LiveEffects(rig)[2]).Wet);
        Assert.False(HasPending(rig));
        Assert.False(CanUndo());

        // Nothing is left to write: the view no longer holds the document.
        rig.Panel.Update(1f);
        Assert.False(CanUndo());
    }

    [Fact]
    public void ClosingTheDocumentOfAModifiedPanel_GivesTheSavedAssetBackToTheLiveMixer_BurstOrNot()
    {
        var rig = OpenDetail();
        var liveReverb = Assert.IsType<ReverbEffect>(LiveEffects(rig)[2]);
        Field(rig.Content, "effect-param:2:2").Value = 0.4f;
        rig.Panel.Update(0.5f);
        Assert.Equal(0.4f, liveReverb.Wet);
        Type(Field(rig.Content, "effect-param:2:3"), "0.5");

        rig.Panel.Dispose();

        Assert.Equal(1f, liveReverb.Wet);
        Assert.Equal(0f, liveReverb.Dry);
    }

    [Fact]
    public void AnUndoWhileABurstIsOpen_TakesTheBurstBack_TheFieldShowsTheDocument()
    {
        var rig = OpenDetail();
        Press(DetailButton(rig.Content, "effect-up:1")); // an entry to undo
        var field = Field(rig.Content, "effect-param:2:2");
        Type(field, "0.25");
        Assert.True(HasPending(rig));

        EditorHistoryService.Current.SetActiveContext(Context);
        Assert.True(EditorHistoryService.Current.Undo());

        Assert.False(HasPending(rig));
        Assert.Equal(1f, Field(rig.Content, "effect-param:2:2").Value);
        rig.Panel.Update(1f);
        Assert.Equal(1f, WetOf(rig));
        Assert.False(CanUndo());
    }

    // ----- one history entry per user intent: buttons and combos

    [Fact]
    public void UpAndDown_AreOneEntryEach_AndTheLiveBusFollows()
    {
        var rig = OpenDetail();
        AssertEffectTypes(rig, BiquadData, CompressorData, ReverbData, DuckingData);

        Press(DetailButton(rig.Content, "effect-up:1"));

        AssertEffectTypes(rig, CompressorData, BiquadData, ReverbData, DuckingData);
        Assert.True(CanUndo());

        Press(DetailButton(rig.Content, "effect-down:2"));

        AssertEffectTypes(rig, CompressorData, BiquadData, DuckingData, ReverbData);
        Assert.Equal(2, UndoEverything());
        AssertEffectTypes(rig, BiquadData, CompressorData, ReverbData, DuckingData);
    }

    [Fact]
    public void TheRowsFollowTheMove_AndTheValuesStayWithTheirEffect()
    {
        var rig = OpenDetail();

        Press(DetailButton(rig.Content, "effect-up:1"));

        Assert.Equal(-18f, Field(rig.Content, "effect-param:0:0").Value);
        Assert.Equal(200f, Field(rig.Content, "effect-param:1:0").Value);
        Assert.Equal("High-pass", Combo(rig.Content, "effect-filter:1").SelectedItem);
        Assert.False(DetailButton(rig.Content, "effect-up:0").IsEnabled);
        Assert.True(DetailButton(rig.Content, "effect-up:1").IsEnabled);
    }

    [Fact]
    public void Delete_IsOneEntry_AndAddAppendsTheDefaultsOfTheKind_AsOneEntry()
    {
        var rig = OpenDetail(effects: 2);

        Press(DetailButton(rig.Content, "effect-delete:0"));

        AssertEffectTypes(rig, CompressorData);
        Assert.True(CanUndo());

        Combo(rig.Content, "effect-add-kind").SelectedItem = "Reverb";
        Press(DetailButton(rig.Content, "effect-add"));

        AssertEffectTypes(rig, CompressorData, ReverbData);
        Assert.Equal(AudioMixerEffectCatalog.CreateDefault(AudioMixerEffectKind.Reverb, null), AssetEffects(rig)[1]);
        Assert.Equal(AudioMixerEffectDefaults.ReverbRoomSize, ((ReverbEffect)LiveEffects(rig)[1]).RoomSize);
        Assert.Equal("Reverb", Combo(rig.Content, "effect-add-kind").SelectedItem);
        Assert.Equal(2, UndoEverything());
        AssertEffectTypes(rig, BiquadData, CompressorData);
    }

    [Fact]
    public void AddingADucking_TakesABusThatCanDriveIt_VoiceFirst()
    {
        var rig = OpenDetail(effects: 0);
        Combo(rig.Content, "effect-add-kind").SelectedItem = "Ducking";

        Press(DetailButton(rig.Content, "effect-add"));

        var ducking = Assert.IsType<AudioMixerDuckingEffectData>(AssetEffects(rig)[0]);
        Assert.Equal("Voice", ducking.Source);
        Assert.Equal("Voice", ((DuckingEffect)LiveEffects(rig)[0]).Source.Name);
        Assert.Equal(1, UndoEverything());

        // On Ui, Voice still comes first although Music is before it; on Voice itself, the first bus that can drive it: Music.
        Assert.True(rig.Panel.SelectBus("Ui"));
        Combo(rig.Content, "effect-add-kind").SelectedItem = "Ducking";
        Press(DetailButton(rig.Content, "effect-add"));

        Assert.Equal("Voice", Assert.IsType<AudioMixerDuckingEffectData>(AssetEffects(rig, "Ui")[0]).Source);

        Assert.True(rig.Panel.SelectBus("Voice"));
        Combo(rig.Content, "effect-add-kind").SelectedItem = "Ducking";
        Press(DetailButton(rig.Content, "effect-add"));

        Assert.Equal("Music", Assert.IsType<AudioMixerDuckingEffectData>(AssetEffects(rig, "Voice")[0]).Source);
    }

    [Fact]
    public void AnAddedDuckingThatNoBusCanDrive_IsRefusedWithAMessage()
    {
        // Sfx sends to every other bus: any of them driving a ducking on Sfx would close a loop.
        var rig = Open(customize: asset =>
        {
            var sfx = asset.Buses.Single(bus => bus.Name == "Sfx");
            foreach (string target in new[] { "Music", "Voice", "Ui" })
            {
                sfx.Sends.Add(new AudioMixerSendData(target, 0.1f));
            }
        });
        Assert.True(rig.Panel.SelectBus("Sfx"));
        Combo(rig.Content, "effect-add-kind").SelectedItem = "Ducking";

        Press(DetailButton(rig.Content, "effect-add"));

        Assert.True(StatusContains(rig, "No bus can be the ducking source of 'Sfx'."));
        Assert.Empty(AssetEffects(rig));
        Assert.False(CanUndo());
        Assert.False(rig.Document.IsDirty);
    }

    [Fact]
    public void ChangingTheNewSendTarget_WritesTheLevelTypedForThePreviousTarget_AtOnce()
    {
        var rig = OpenDetail(effects: 0);
        var sfx = rig.Service.Mixer.GetBus("Sfx");
        Combo(rig.Content, "send-new-target").SelectedItem = "Voice";
        Field(rig.Content, "send-new-level").Value = 0.4f;
        Assert.True(HasPending(rig));

        Combo(rig.Content, "send-new-target").SelectedItem = "Ui";

        Assert.False(HasPending(rig));
        Assert.Equal(0.4f, sfx.GetSend(rig.Service.Mixer.GetBus("Voice")));
        Assert.Equal(0f, sfx.GetSend(rig.Service.Mixer.GetBus("Ui")));
        Assert.Equal(1, UndoEverything());
    }

    [Fact]
    public void AFifthEffect_IsRefusedWithTheMessageOfTheDocument()
    {
        var rig = OpenDetail();
        Assert.False(DetailButton(rig.Content, "effect-add").IsEnabled);

        Assert.False(rig.Panel.DetailView.TryAddEffect(AudioMixerEffectKind.Compressor));

        Assert.True(StatusContains(rig, "already holds 4 effects"));
        Assert.False(CanUndo());
        Assert.False(rig.Document.IsDirty);
        Assert.Equal(4, AssetEffects(rig).Count);
        Assert.Equal(4, LiveEffects(rig).Count);
    }

    [Fact]
    public void ChangingTheFilterType_IsOneEntry()
    {
        var rig = OpenDetail();
        var combo = Combo(rig.Content, "effect-filter:0");

        combo.SelectedItem = "Low-pass";

        var biquad = Assert.IsType<AudioMixerBiquadEffectData>(AssetEffects(rig)[0]);
        Assert.Equal(BiquadFilterType.LowPass, biquad.FilterType);
        Assert.Equal(200f, biquad.FrequencyHz);
        Assert.Equal(BiquadFilterType.LowPass, ((BiquadFilterEffect)LiveEffects(rig)[0]).Type);
        Assert.Same(combo, Combo(rig.Content, "effect-filter:0"));
        Assert.Equal(1, UndoEverything());
        Assert.Equal(BiquadFilterType.HighPass, ((BiquadFilterEffect)LiveEffects(rig)[0]).Type);
        Assert.Equal("High-pass", combo.SelectedItem);
    }

    [Fact]
    public void ChangingTheDuckingSource_ReplacesTheLiveEffect_AsOneEntry()
    {
        var rig = OpenDetail();
        var before = Assert.IsType<DuckingEffect>(LiveEffects(rig)[3]);
        Assert.Equal("Voice", before.Source.Name);

        Combo(rig.Content, "effect-source:3").SelectedItem = "Ui";

        var after = Assert.IsType<DuckingEffect>(LiveEffects(rig)[3]);
        Assert.NotSame(before, after);
        Assert.Equal("Ui", after.Source.Name);
        Assert.DoesNotContain(before, LiveEffects(rig));
        Assert.Equal("Ui", ((AudioMixerDuckingEffectData)AssetEffects(rig)[3]).Source);
        Assert.Equal(4, LiveEffects(rig).Count);
        Assert.Equal("Ui", Combo(rig.Content, "effect-source:3").SelectedItem);

        Assert.Equal(1, UndoEverything());
        Assert.Equal("Voice", ((AudioMixerDuckingEffectData)AssetEffects(rig)[3]).Source);
        Assert.Equal("Voice", ((DuckingEffect)LiveEffects(rig)[3]).Source.Name);
        Assert.Equal("Voice", Combo(rig.Content, "effect-source:3").SelectedItem);
    }

    [Fact]
    public void ADuckingSourceThatMakesACycle_IsRefusedWithTheMessage_AndTheComboGoesBack()
    {
        var rig = OpenDetail();
        var before = LiveEffects(rig)[3];

        // Sfx sends to Reverb: Reverb ducking Sfx would close the loop (the other way round, Reverb is a source of Sfx).
        Combo(rig.Content, "effect-source:3").SelectedItem = "Reverb";

        Assert.True(StatusContains(rig, "A ducking of 'Sfx' by 'Reverb' would make a cycle."));
        Assert.False(CanUndo());
        Assert.False(rig.Document.IsDirty);
        Assert.Equal("Voice", ((AudioMixerDuckingEffectData)AssetEffects(rig)[3]).Source);

        rig.Panel.Update(Frame);

        Assert.Equal("Voice", Combo(rig.Content, "effect-source:3").SelectedItem);
        Assert.Same(before, LiveEffects(rig)[3]);
    }

    // ----- sends

    [Fact]
    public void ACyclingSend_IsRefusedWithTheMessageOfTheDocument_AndTheFieldIsBackToZero()
    {
        var rig = OpenDetail(effects: 2);
        Combo(rig.Content, "send-new-target").SelectedItem = "Footsteps"; // a child of Sfx
        var level = Field(rig.Content, "send-new-level");

        level.Value = 0.5f;
        Assert.True(HasPending(rig));
        rig.Panel.Update(0.5f);

        Assert.True(StatusContains(rig, "A send from 'Sfx' to 'Footsteps' would make a cycle."));
        Assert.False(CanUndo());
        Assert.False(rig.Document.IsDirty);
        Assert.Equal(2, rig.Document.Asset.Buses.Single(bus => bus.Name == "Sfx").Sends.Count);
        Assert.Equal(0f, Field(rig.Content, "send-new-level").Value);
        Assert.Same(level, Field(rig.Content, "send-new-level"));
        Assert.Equal("Footsteps", Combo(rig.Content, "send-new-target").SelectedItem);
    }

    [Fact]
    public void ANewSend_IsOneEntry_AndALevelOfZeroRemovesIt()
    {
        var rig = OpenDetail(effects: 0);
        var sfx = rig.Service.Mixer.GetBus("Sfx");
        var voice = rig.Service.Mixer.GetBus("Voice");
        Combo(rig.Content, "send-new-target").SelectedItem = "Voice";

        Field(rig.Content, "send-new-level").Value = 0.4f;
        rig.Panel.Update(0.5f);

        Assert.Equal(0.4f, rig.Document.Asset.Buses.Single(bus => bus.Name == "Sfx").Sends.Single(send => send.Target == "Voice").Level);
        Assert.Equal(0.4f, sfx.GetSend(voice));
        Assert.Equal(0.4f, Field(rig.Content, "send-level:Voice").Value);
        Assert.Equal(0f, Field(rig.Content, "send-new-level").Value);
        Assert.DoesNotContain("Voice", Combo(rig.Content, "send-new-target").ItemsSource);
        Assert.True(CanUndo());

        Field(rig.Content, "send-level:Voice").Value = 0f;
        rig.Panel.Update(0.5f);

        Assert.DoesNotContain(rig.Document.Asset.Buses.Single(bus => bus.Name == "Sfx").Sends, send => send.Target == "Voice");
        Assert.Equal(0f, sfx.GetSend(voice));
        Assert.False(IsTagged<NumericField>(rig.Content, "send-level:Voice"));
        Assert.Contains("Voice", Combo(rig.Content, "send-new-target").ItemsSource);

        Assert.Equal(2, UndoEverything());
        Assert.Equal(2, sfx.Sends.Count);
    }

    [Fact]
    public void ChangingTheLevelOfASend_IsOneEntry_AndTheLiveSendFollows()
    {
        var rig = OpenDetail(effects: 0);
        var sfx = rig.Service.Mixer.GetBus("Sfx");
        var reverb = rig.Service.Mixer.GetBus("Reverb");

        Type(Field(rig.Content, "send-level:Reverb"), "0.75");
        rig.Panel.Update(0.5f);

        Assert.Equal(0.75f, sfx.GetSend(reverb));
        Assert.Equal(1, UndoEverything());
        Assert.Equal(0.3f, sfx.GetSend(reverb));
        Assert.Equal(0.3f, Field(rig.Content, "send-level:Reverb").Value);
    }

    [Fact]
    public void AtFourSends_TheNewSendRowIsDisabled()
    {
        var rig = OpenDetail(effects: 0);
        foreach (string target in new[] { "Voice", "Ui" })
        {
            Combo(rig.Content, "send-new-target").SelectedItem = target;
            Field(rig.Content, "send-new-level").Value = 0.5f;
            rig.Panel.Update(0.5f);
        }

        Assert.Equal(4, rig.Document.Asset.Buses.Single(bus => bus.Name == "Sfx").Sends.Count);
        Assert.False(Combo(rig.Content, "send-new-target").IsEnabled);
        Assert.False(Field(rig.Content, "send-new-level").IsEnabled);
        Assert.Equal(2, UndoEverything());
    }

    // ----- rebuild and refresh

    [Fact]
    public void TheControls_AreRebuiltOnlyWhenTheStructureChanges_NeverPerFrame()
    {
        var rig = OpenDetail();
        var up = DetailButton(rig.Content, "effect-up:1");
        var field = Field(rig.Content, "effect-param:0:0");
        Frames(rig, 60);
        Assert.Same(up, DetailButton(rig.Content, "effect-up:1"));

        // A value only: written into the control that is there.
        field.Value = 500f;
        rig.Panel.Update(0.5f);
        Assert.Same(field, Field(rig.Content, "effect-param:0:0"));
        Assert.Same(up, DetailButton(rig.Content, "effect-up:1"));

        // A mute is not a change of the structure either.
        rig.Document.SetMute("Voice", true);
        rig.Document.SetBusVolume("Music", 0.7f);
        Assert.Same(up, DetailButton(rig.Content, "effect-up:1"));

        // A move is.
        Press(up);
        Assert.NotSame(up, DetailButton(rig.Content, "effect-up:1"));
    }

    [Fact]
    public void UndoAndRedo_PutTheFieldsBack_InPlace_WithoutANewEntry()
    {
        var rig = OpenDetail();
        var field = Field(rig.Content, "effect-param:2:2");
        field.Value = 0.4f;
        rig.Panel.Update(0.5f);
        Assert.True(CanUndo());

        EditorHistoryService.Current.SetActiveContext(Context);
        Assert.True(EditorHistoryService.Current.Undo());
        Frames(rig, 40);

        Assert.Same(field, Field(rig.Content, "effect-param:2:2"));
        Assert.Equal(1f, field.Value);
        Assert.False(HasPending(rig));
        Assert.False(CanUndo());
        Assert.True(EditorHistoryService.Current.CanRedo);

        Assert.True(EditorHistoryService.Current.Redo());
        Frames(rig, 40);

        Assert.Equal(0.4f, field.Value);
        Assert.False(HasPending(rig));
        Assert.True(CanUndo());
        Assert.False(EditorHistoryService.Current.CanRedo);
        Assert.Equal(1, UndoEverything());
    }

    [Fact]
    public void AFieldValueOutsideTheBounds_IsShownClamped_AndLeftAlone()
    {
        var rig = Open(customize: asset =>
        {
            asset.Buses.Single(bus => bus.Name == "Sfx").Effects.Add(new AudioMixerBiquadEffectData(BiquadFilterType.LowPass, 50000f));
        });
        Assert.True(rig.Panel.SelectBus("Sfx"));

        Assert.Equal(BiquadFilterEffect.MaxFrequencyHz, Field(rig.Content, "effect-param:0:0").Value);
        Assert.Equal(50000f, ((AudioMixerBiquadEffectData)AssetEffects(rig)[0]).FrequencyHz);
        Frames(rig, 40);
        Assert.False(rig.Document.IsDirty);
        Assert.False(CanUndo());
    }

    // ----- the play session (P51)

    [Fact]
    public void DuringAPlaySession_EveryEditingControlOfTheDetailIsDisabled_AndTheEditsAreRefused()
    {
        var rig = OpenDetail(effects: 2);
        var controls = EditingControls(rig.Content);
        Assert.NotEmpty(controls);

        EditorHistoryService.Current.IsSuspended = true;
        rig.Panel.Update(Frame);

        Assert.All(controls, control => Assert.False(control.IsEnabled, (string)control.Tag));
        Assert.All(
            controls.OfType<NumericField>(),
            field =>
            {
                Assert.False(TextBoxOf(field).DerivedIsEnabled, (string)field.Tag);
                Assert.All(field.TraverseVisualTree().OfType<MGButton>(), button => Assert.False(button.DerivedIsEnabled, (string)field.Tag));
            });

        Assert.False(rig.Panel.DetailView.TryAddEffect(AudioMixerEffectKind.Reverb));
        Assert.True(StatusContains(rig, "Mixer edits are disabled during a play session."));
        Assert.Equal(2, AssetEffects(rig).Count);
        Assert.False(CanUndo());

        EditorHistoryService.Current.IsSuspended = false;
        rig.Panel.Update(Frame);

        Assert.All(
            controls.Where(control => (string)control.Tag != "effect-up:0" && (string)control.Tag != "effect-down:1"),
            control => Assert.True(control.IsEnabled, (string)control.Tag));
        Assert.False(DetailButton(rig.Content, "effect-up:0").IsEnabled);
        Assert.False(DetailButton(rig.Content, "effect-down:1").IsEnabled);
    }

    [Fact]
    public void ADetailOpenedDuringAPlaySession_StartsLocked()
    {
        EditorHistoryService.Current.IsSuspended = true;
        var rig = Open(customize: DetailAsset());

        Assert.True(rig.Panel.SelectBus("Sfx"));

        Assert.NotEmpty(EditingControls(rig.Content));
        Assert.All(EditingControls(rig.Content), control => Assert.False(control.IsEnabled, (string)control.Tag));
    }

    [Fact]
    public void APlaySessionStartingWithAnOpenBurst_RecordsTheBurst_AndKeepsItsEntry()
    {
        var rig = OpenDetail();
        Type(Field(rig.Content, "effect-param:2:2"), "0.25");
        Assert.True(HasPending(rig));

        EditorHistoryService.Current.IsSuspended = true;
        rig.Panel.Update(Frame);

        // The service refuses commands now, yet the change made before the session started is recorded.
        Assert.False(HasPending(rig));
        Assert.True(CanUndo());
        Assert.True(rig.Document.IsDirty);
        Assert.Equal(0.25f, WetOf(rig));

        EditorHistoryService.Current.IsSuspended = false;
        rig.Panel.Update(Frame);
        Assert.Equal(1, UndoEverything());
        Assert.Equal(1f, WetOf(rig));
    }

    [Fact]
    public void AFocusLossOnTheFirstFrameOfASession_StillRecordsTheBurst()
    {
        var rig = OpenDetail();
        var field = Field(rig.Content, "effect-param:2:2");
        SetKeyboardFocus(rig.Harness.Desktop, TextBoxOf(field));
        Type(field, "0.25");

        // The session starts, and the focus leaves the field before the panel has seen it.
        EditorHistoryService.Current.IsSuspended = true;
        SetKeyboardFocus(rig.Harness.Desktop, null);

        Assert.False(HasPending(rig));
        Assert.True(CanUndo());
        Assert.Equal(0.25f, WetOf(rig));
    }

    [Fact]
    public void AFieldChangeThatRacesTheFirstFrameOfASession_IsTakenBack()
    {
        var rig = OpenDetail();
        var field = Field(rig.Content, "effect-param:2:2");
        EditorHistoryService.Current.IsSuspended = true;

        // The panel has not seen the session yet: the field is still enabled.
        field.Value = 0.4f;

        Assert.False(HasPending(rig));
        rig.Panel.Update(Frame);
        Assert.Equal(1f, field.Value);
        Assert.Equal(1f, WetOf(rig));
        Assert.Equal(1f, ((ReverbEffect)LiveEffects(rig)[2]).Wet);
        Assert.False(rig.Panel.IsDirty);
        Assert.False(CanUndo());
    }

    // ----- allocation

    [Fact]
    public void Update_WithTheDetailShown_AllocatesNothing_EvenWithABurstOpen()
    {
        var rig = OpenDetail();
        var field = Field(rig.Content, "effect-param:2:2");
        Frames(rig, 10);

        long before = AllocationWindow.Start();
        Frames(rig, 30);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);

        field.Value = 0.4f;
        Frames(rig, 2);

        before = AllocationWindow.Start();
        Frames(rig, 10);
        allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(HasPending(rig));
        Assert.Equal(0, allocated);
    }

    // ----- rendered

    private static PcmAudioClip SineClip(double frequency, double amplitude)
    {
        var samples = new short[2 * 48000];
        for (int frame = 0; frame < 48000; frame++)
        {
            short value = (short)Math.Round(amplitude * 32767.0 * Math.Sin(2.0 * Math.PI * frequency * frame / 48000.0));
            samples[2 * frame] = value;
            samples[(2 * frame) + 1] = value;
        }

        return new PcmAudioClip(samples, 48000, 2);
    }

    /// <summary>Renders blocks of 10 ms and returns the largest peak of the last three.</summary>
    private static float PumpPeak(OfflineAudioOutput output, int blocks)
    {
        float peak = 0f;
        for (int block = 0; block < blocks; block++)
        {
            float blockPeak = output.Pump(480);
            if (block >= blocks - 3)
            {
                peak = Math.Max(peak, blockPeak);
            }
        }

        return peak;
    }

    [Fact]
    public void ALowPassAt500Hz_AddedInThePanelOnSfx_TakesAnEightKilohertzSineBelowATenthOfItsPeak_Rendered()
    {
        var output = new OfflineAudioOutput();
        var rig = Open(backend: new SoftwareAudioBackend(output, 16));
        rig.Service.MasterLimiter.IsEnabled = false;
        Assert.True(rig.Panel.SelectBus("Sfx"));
        var clip = SineClip(8000.0, 0.5);

        rig.Service.PlayClip(clip, "Sfx", AudioVoiceParameters.Default.WithLooping(true));
        rig.Service.Update(0.01f);
        float unfiltered = PumpPeak(output, 12);
        rig.Service.StopAll();
        rig.Service.Update(0.01f);
        Assert.True(PumpPeak(output, 30) < 0.001f);
        Assert.True(unfiltered > 0.1f, $"unfiltered peak {unfiltered}");

        // The panel adds a biquad (a low-pass at 1000 Hz by default) and sets its frequency to 500 Hz.
        Press(DetailButton(rig.Content, "effect-add"));
        Field(rig.Content, "effect-param:0:0").Value = 500f;
        rig.Panel.Update(0.5f);
        Assert.Equal(500f, ((BiquadFilterEffect)LiveEffects(rig)[0]).FrequencyHz);
        Assert.Equal(BiquadFilterType.LowPass, ((BiquadFilterEffect)LiveEffects(rig)[0]).Type);

        rig.Service.PlayClip(clip, "Sfx", AudioVoiceParameters.Default.WithLooping(true));
        rig.Service.Update(0.01f);
        float filtered = PumpPeak(output, 12);

        Assert.True(filtered < unfiltered / 10f, $"filtered peak {filtered}, unfiltered peak {unfiltered}");
        Assert.Equal(2, UndoEverything());
        Assert.Empty(LiveEffects(rig));
    }
}
