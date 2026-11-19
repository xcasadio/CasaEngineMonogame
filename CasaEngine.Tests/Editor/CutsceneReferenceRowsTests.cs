using CasaEngine.Editor;
using CasaEngine.Editor.Controls;
using CasaEngine.Editor.History;
using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Tests.ContentBrowser;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// The reference fields of the Inspector view of a cutscene document (ADR-0136): an entity of the preview world or an asset of
/// the catalogue chosen in a list, the value written in the form the runtime reads, and the stored value kept and marked
/// "(missing)" when it is not in the list.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class CutsceneReferenceRowsTests : IDisposable
{
    private const string ContextId = CasaEngine.Editor.Workspaces.EditorPanelIds.CutsceneAssetDocumentPrefix + "references";

    private static readonly EditorHistoryContext Context = new(EditorHistoryContextKind.Cutscene, ContextId);
    private static readonly Guid ThemeId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly string _projectDirectory;
    private readonly string _previousProjectPath;
    private readonly List<CutsceneAssetInspectorPanel> _panels = [];

    public CutsceneReferenceRowsTests()
    {
        _projectDirectory = Path.Combine(Path.GetTempPath(), "casa-cutscene-references-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectDirectory);

        _previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = _projectDirectory;
        AssetCatalog.ClearInternal();
        AssetCatalog.AddInternal(new AssetInfo(ThemeId) { Name = "theme", FileName = "Sounds/theme.sound", AssetType = "sound" });
        AssetCatalog.AddInternal(new AssetInfo(Guid.NewGuid()) { Name = "alarm", FileName = "Sounds/alarm.sound", AssetType = "sound" });
        AssetCatalog.AddInternal(new AssetInfo(Guid.NewGuid()) { Name = "orb-left", FileName = "Sprites/orb-left.sprite", AssetType = "sprite" });
        AssetCatalog.AddInternal(new AssetInfo(Guid.NewGuid()) { Name = "Cave", FileName = "Worlds/cave.world", AssetType = "world" });
        AssetCatalog.AddInternal(new AssetInfo(Guid.NewGuid()) { Name = "Title", FileName = "Screens/title.uiscreen", AssetType = "uiscreen" });
    }

    public void Dispose()
    {
        foreach (var panel in _panels)
        {
            panel.Dispose();
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
        public required CutsceneAssetInspectorPanel Document { get; init; }

        public required CutsceneDetailsPanel Details { get; init; }

        public required CutsceneEditSession Session { get; init; }

        public SequenceCutsceneActionData Root => (SequenceCutsceneActionData)Session.Asset.RootAction!;

        public T Add<T>(string type) where T : CutsceneActionData
        {
            Assert.Null(Document.AddAction(type));
            return (T)Document.SelectedAction!;
        }

        public CutsceneDetailsPanel.ChoiceRow Choice(string key)
            => Details.Rows.OfType<CutsceneDetailsPanel.ChoiceRow>().Single(row => row.Key == key);

        /// <summary>A list of entity names for the property, as the details panel builds it when the preview world has entities.</summary>
        public CutsceneDetailsPanel.ChoiceRow EntityChoice(CutsceneActionData action, params string[] names)
            => new(Details, action, CutsceneActionPropertyEditor.FindProperty(action, "entity")!, null, [.. names]);
    }

    private Rig Open()
    {
        var document = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "References",
            ["type"] = "CutsceneAsset",
            ["version"] = 1,
            ["schema_version"] = 1,
            ["root_action"] = new JObject { ["type"] = "Sequence", ["actions"] = new JArray() },
        };
        string fullPath = Path.Combine(_projectDirectory, "References.cutscene");
        File.WriteAllText(fullPath, document.ToString());

        Assert.True(CutsceneAssetInspectorPanel.TryLoadAsset(fullPath, out var session));
        Assert.NotNull(session);

        var harness = ContentBrowserViewTestHarness.Create(900, 900);
        var panel = new CutsceneAssetInspectorPanel(harness.Window);
        _panels.Add(panel);
        panel.SetHistoryContextId(ContextId);
        panel.LoadAsset(session, fullPath);

        var details = new CutsceneDetailsPanel(harness.Window);
        harness.Window.SetContent(details.CreateContent());
        details.SetEditorPanel(panel);
        return new Rig { Document = panel, Details = details, Session = session };
    }

    [Fact]
    public void ASoundIsChosenInAListAndWrittenAsItsGuid()
    {
        var rig = Open();
        var action = rig.Add<PlayMusicCutsceneActionData>(CutsceneActionTypes.PlayMusic);

        var row = rig.Choice("sound_asset_id");
        Assert.Equal(["(none)", "alarm", "theme"], row.Labels);
        Assert.Equal("(none)", row.Combo.SelectedItem);

        row.Combo.SelectedItem = "theme";

        Assert.Equal(ThemeId, action.SoundAssetId);
        Assert.Equal("theme", row.Combo.SelectedItem);
        Assert.True(rig.Document.IsDirty);
    }

    [Fact]
    public void AWorldIsWrittenAsItsFileName_AndASpriteAsItsCatalogueName()
    {
        var rig = Open();
        var world = rig.Add<ChangeWorldCutsceneActionData>(CutsceneActionTypes.ChangeWorld);
        rig.Choice("target").Combo.SelectedItem = "Cave";
        Assert.Equal("Worlds/cave.world", world.Target);

        var sprite = rig.Add<ShowSpriteCutsceneActionData>(CutsceneActionTypes.ShowSprite);
        rig.Choice("sprite").Combo.SelectedItem = "orb-left";
        Assert.Equal("orb-left", sprite.Sprite);
    }

    [Fact]
    public void AScreenStoredAsAGuid_IsShownAsItsCatalogueEntry()
    {
        var rig = Open();
        var screenId = AssetCatalog.AssetInfos.Single(info => info.AssetType == "uiscreen").Id;
        var action = rig.Add<OpenScreenCutsceneActionData>(CutsceneActionTypes.OpenScreen);
        action.Target = screenId.ToString();
        rig.Document.SelectAction(null);
        rig.Document.SelectAction(action);

        Assert.Equal("Title", rig.Choice("target").Combo.SelectedItem);
        Assert.DoesNotContain(rig.Choice("target").Labels, label => label.EndsWith("(missing)", StringComparison.Ordinal));
    }

    [Fact]
    public void AValueOfTheCatalogueThatIsNotInTheList_IsKeptAndMarkedMissing()
    {
        var rig = Open();
        var action = rig.Add<ShowSpriteCutsceneActionData>(CutsceneActionTypes.ShowSprite);
        action.Sprite = "deleted-sprite";
        rig.Document.SelectAction(null);
        rig.Document.SelectAction(action);

        var row = rig.Choice("sprite");

        Assert.Equal("deleted-sprite (missing)", row.Combo.SelectedItem);
        Assert.Equal("deleted-sprite", action.Sprite);

        // Choosing the missing entry again, or leaving the field alone, writes nothing.
        rig.Document.SelectAction(null);
        rig.Document.SelectAction(action);
        Assert.Equal("deleted-sprite", action.Sprite);
    }

    [Fact]
    public void ANoneChoice_ClearsAReference()
    {
        var rig = Open();
        var action = rig.Add<ShowSpriteCutsceneActionData>(CutsceneActionTypes.ShowSprite);
        action.Sprite = "orb-left";
        rig.Document.SelectAction(null);
        rig.Document.SelectAction(action);

        rig.Choice("sprite").Combo.SelectedItem = "(none)";

        Assert.Equal(string.Empty, action.Sprite);
    }

    [Fact]
    public void WithoutAnAssetOfTheType_TheReferenceStaysATextField()
    {
        AssetCatalog.ClearInternal();
        var rig = Open();

        rig.Add<ShowSpriteCutsceneActionData>(CutsceneActionTypes.ShowSprite);

        Assert.IsType<CutsceneDetailsPanel.TextRow>(rig.Details.Rows.Single(row => row.Key == "sprite"));
    }

    [Fact]
    public void AnEntityIsChosenInTheListOfTheNamesOfTheWorld_AndAMissingNameIsKept()
    {
        var rig = Open();
        var action = rig.Add<MoveSpriteCutsceneActionData>(CutsceneActionTypes.MoveSprite);
        action.EntityName = "ghost";

        var row = rig.EntityChoice(action, "orb-left", "orb-right");

        Assert.Equal(["(none)", "orb-left", "orb-right", "ghost (missing)"], row.Labels);
        Assert.Equal("ghost (missing)", row.Combo.SelectedItem);

        row.Combo.SelectedItem = "orb-right";

        Assert.Equal("orb-right", action.EntityName);
        Assert.Equal("orb-right", row.Combo.SelectedItem);
    }

    [Fact]
    public void AnEntityListStaysATextFieldWithoutAPreviewWorld()
    {
        var rig = Open();

        rig.Add<MoveSpriteCutsceneActionData>(CutsceneActionTypes.MoveSprite);

        Assert.Empty(rig.Document.GetPreviewEntityNames());
        Assert.IsType<CutsceneDetailsPanel.TextRow>(rig.Details.Rows.Single(row => row.Key == "entity"));
    }
}
