using CasaEngine.Editor;
using CasaEngine.Editor.Controls;
using CasaEngine.Editor.History;
using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Tests.ContentBrowser;
using Microsoft.Xna.Framework;
using MGUI.Core.UI;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// The typed fields of the Inspector view of a cutscene document (ADR-0136), on panels without a preview and without a
/// graphics device: the field of each kind of property, the writes through the document as single history entries, the
/// refusals with their reason, and the text typed and not validated that is written before another action is shown.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class CutsceneDetailsPanelTests : IDisposable
{
    private const string ContextId = CasaEngine.Editor.Workspaces.EditorPanelIds.CutsceneAssetDocumentPrefix + "details";

    private static readonly EditorHistoryContext Context = new(EditorHistoryContextKind.Cutscene, ContextId);

    private readonly string _projectDirectory;
    private readonly string _previousProjectPath;
    private readonly List<CutsceneAssetInspectorPanel> _panels = [];

    public CutsceneDetailsPanelTests()
    {
        _projectDirectory = Path.Combine(Path.GetTempPath(), "casa-cutscene-details-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectDirectory);

        _previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = _projectDirectory;
        AssetCatalog.ClearInternal();
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

        public T Row<T>(string key) where T : CutsceneDetailsPanel.DetailRow
            => Details.Rows.OfType<T>().Single(row => row.Key == key);
    }

    /// <summary>A cutscene with one Wait of 1.5 s, bound to a details panel; the history starts empty.</summary>
    private Rig Open()
    {
        var document = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "Details",
            ["type"] = "CutsceneAsset",
            ["version"] = 1,
            ["schema_version"] = 1,
            ["root_action"] = new JObject
            {
                ["type"] = "Sequence",
                ["actions"] = new JArray { new JObject { ["type"] = "Wait", ["seconds"] = 1.5 } },
            },
        };
        string fullPath = Path.Combine(_projectDirectory, "Details.cutscene");
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

    private static void ClearHistory()
    {
        EditorHistoryService.Current.Clear(Context);
        EditorHistoryService.Current.SetActiveContext(Context);
    }

    private static bool CanUndo()
    {
        EditorHistoryService.Current.SetActiveContext(Context);
        return EditorHistoryService.Current.CanUndo;
    }

    private static void Undo()
    {
        EditorHistoryService.Current.SetActiveContext(Context);
        Assert.True(EditorHistoryService.Current.Undo());
    }

    /// <summary>Asserts that exactly one entry was made since <see cref="ClearHistory"/>, by undoing it.</summary>
    private static void UndoTheOnlyEntry()
    {
        Undo();
        Assert.False(CanUndo());
    }

    private static IEnumerable<string> Texts(MGElement? element)
        => element == null ? [] : element.TraverseVisualTree().OfType<MGTextBlock>().Select(text => text.Text ?? string.Empty);

    [Fact]
    public void EveryKindOfPropertyGetsItsField()
    {
        var rig = Open();

        var wait = (WaitCutsceneActionData)rig.Root.Actions[0];
        rig.Document.SelectAction(wait);
        Assert.IsType<CutsceneDetailsPanel.NumberRow>(Assert.Single(rig.Details.Rows));

        rig.Add<MoveToCutsceneActionData>(CutsceneActionTypes.MoveTo);
        Assert.IsType<CutsceneDetailsPanel.TextRow>(rig.Row<CutsceneDetailsPanel.TextRow>("entity"));
        Assert.Equal(3, rig.Row<CutsceneDetailsPanel.VectorRow>("destination").Fields.Count);
        rig.Row<CutsceneDetailsPanel.NumberRow>("stopping_distance");

        rig.Add<PlayMusicCutsceneActionData>(CutsceneActionTypes.PlayMusic);
        rig.Row<CutsceneDetailsPanel.TextRow>("sound_asset_id");
        rig.Row<CutsceneDetailsPanel.NumberRow>("fade_in_seconds");
        rig.Row<CutsceneDetailsPanel.BooleanRow>("crossfade");

        rig.Add<FadeScreenCutsceneActionData>(CutsceneActionTypes.FadeScreen);
        rig.Row<CutsceneDetailsPanel.RgbColorRow>("r,g,b");
        rig.Row<CutsceneDetailsPanel.NumberRow>("duration_seconds");
        Assert.DoesNotContain("(none)", rig.Row<CutsceneDetailsPanel.EnumRow>("blend_mode").Combo.ItemsSource);
        Assert.Equal(3, rig.Details.Rows.Count);

        rig.Add<ShowSpriteCutsceneActionData>(CutsceneActionTypes.ShowSprite);
        var color = rig.Row<CutsceneDetailsPanel.ColorRow>("color");
        Assert.NotNull(color.ClearButton);
        var blend = rig.Row<CutsceneDetailsPanel.EnumRow>("blend_mode");
        Assert.Equal("(none)", blend.Combo.SelectedItem);
        Assert.NotNull(rig.Row<CutsceneDetailsPanel.NumberRow>("x").ClearButton);
        Assert.Null(rig.Row<CutsceneDetailsPanel.TextRow>("sprite").ClearButton);
    }

    [Fact]
    public void TypingANumberAndValidatingItMakesOneHistoryEntry()
    {
        var rig = Open();
        var wait = (WaitCutsceneActionData)rig.Root.Actions[0];
        rig.Document.SelectAction(wait);
        ClearHistory();

        var row = rig.Row<CutsceneDetailsPanel.NumberRow>("seconds");
        Assert.Equal("1.5", row.NumberField.Text);

        row.NumberField.TextBox.SetText("1.25");
        row.NumberField.Flush();

        Assert.Equal(1.25f, wait.Seconds);
        Assert.True(rig.Document.IsDirty);
        Assert.Equal("1.25", row.NumberField.Text);
        UndoTheOnlyEntry();
        Assert.Equal(1.5f, wait.Seconds);
        Assert.Equal("1.5", row.NumberField.Text);
    }

    [Fact]
    public void ThePlusButtonWritesAtOnceAndTheMinusButtonStopsAtTheLimit()
    {
        var rig = Open();
        var wait = (WaitCutsceneActionData)rig.Root.Actions[0];
        rig.Document.SelectAction(wait);
        ClearHistory();

        var field = rig.Row<CutsceneDetailsPanel.NumberRow>("seconds").NumberField;
        Assert.NotNull(field.PlusButton);
        field.Step(1);
        Assert.Equal(2.5f, wait.Seconds);
        UndoTheOnlyEntry();

        field.TextBox.SetText("0.5");
        field.Flush();
        ClearHistory();
        field.Step(-1);
        Assert.Equal(0.5f, wait.Seconds);
        Assert.False(CanUndo());
    }

    [Fact]
    public void TheExclusiveLimitOfTheSpeedIsRespected()
    {
        var rig = Open();
        var move = rig.Add<MoveSpriteCutsceneActionData>(CutsceneActionTypes.MoveSprite);
        ClearHistory();

        var field = rig.Row<CutsceneDetailsPanel.NumberRow>("speed").NumberField;
        field.Step(1);
        Assert.Equal(1f, move.Speed);
        ClearHistory();

        field.Step(-1);
        Assert.Equal(1f, move.Speed);
        Assert.False(CanUndo());
    }

    [Fact]
    public void ARefusedTextIsExplainedAndTheFieldShowsTheValueAgain()
    {
        var rig = Open();
        var wait = (WaitCutsceneActionData)rig.Root.Actions[0];
        rig.Document.SelectAction(wait);
        ClearHistory();

        var row = rig.Row<CutsceneDetailsPanel.NumberRow>("seconds");

        row.NumberField.TextBox.SetText("abc");
        row.NumberField.Flush();
        Assert.Contains("finite number", row.ErrorText);
        Assert.Equal("1.5", row.NumberField.Text);

        row.NumberField.TextBox.SetText("-1");
        row.NumberField.Flush();
        Assert.Contains("at least 0", row.ErrorText);
        Assert.Equal("1.5", row.NumberField.Text);

        row.NumberField.TextBox.SetText(string.Empty);
        row.NumberField.Flush();
        Assert.Contains("cannot be empty", row.ErrorText);
        Assert.Equal("1.5", row.NumberField.Text);

        Assert.Equal(1.5f, wait.Seconds);
        Assert.False(CanUndo());
    }

    [Fact]
    public void AnEmptyTextIsNoValueForANullableProperty()
    {
        var rig = Open();
        var move = rig.Add<MoveSpriteCutsceneActionData>(CutsceneActionTypes.MoveSprite);
        var row = rig.Row<CutsceneDetailsPanel.NumberRow>("x");

        row.NumberField.TextBox.SetText("5");
        row.NumberField.Flush();
        Assert.Equal(5f, move.X);

        ClearHistory();
        row.NumberField.TextBox.SetText(string.Empty);
        row.NumberField.Flush();
        Assert.Null(move.X);
        UndoTheOnlyEntry();
        Assert.Equal(5f, move.X);

        // The clear button does the same.
        ClearHistory();
        row.Clear();
        Assert.Null(move.X);
        UndoTheOnlyEntry();
        Assert.Equal(5f, move.X);
    }

    [Fact]
    public void TheEnumerationAndTheFlagWriteThroughTheDocument()
    {
        var rig = Open();
        var fade = rig.Add<FadeScreenCutsceneActionData>(CutsceneActionTypes.FadeScreen);
        ClearHistory();

        rig.Row<CutsceneDetailsPanel.EnumRow>("blend_mode").Combo.SelectedItem = "Additive";
        Assert.Equal(SpriteBlendMode.Additive, fade.BlendMode);
        UndoTheOnlyEntry();
        Assert.Equal(SpriteBlendMode.Opaque, fade.BlendMode);
        Assert.Equal("Opaque", rig.Row<CutsceneDetailsPanel.EnumRow>("blend_mode").Combo.SelectedItem);

        var music = rig.Add<PlayMusicCutsceneActionData>(CutsceneActionTypes.PlayMusic);
        ClearHistory();
        bool before = music.Crossfade;
        rig.Row<CutsceneDetailsPanel.BooleanRow>("crossfade").CheckBox.IsChecked = !before;
        Assert.Equal(!before, music.Crossfade);
        UndoTheOnlyEntry();
        Assert.Equal(before, music.Crossfade);
        Assert.Equal(before, rig.Row<CutsceneDetailsPanel.BooleanRow>("crossfade").CheckBox.IsChecked);
    }

    [Fact]
    public void TheFadeColorIsOneFieldAndOneEntryThatWritesTheThreeChannels()
    {
        var rig = Open();
        var fade = rig.Add<FadeScreenCutsceneActionData>(CutsceneActionTypes.FadeScreen);
        ClearHistory();

        var row = rig.Row<CutsceneDetailsPanel.RgbColorRow>("r,g,b");
        row.Editor.Value = new Color(10, 20, 30);

        Assert.Equal((byte)10, fade.R);
        Assert.Equal((byte)20, fade.G);
        Assert.Equal((byte)30, fade.B);
        UndoTheOnlyEntry();
        Assert.Equal((byte)0, fade.R);
        Assert.Equal((byte)0, fade.G);
        Assert.Equal((byte)0, fade.B);
    }

    [Fact]
    public void TheVectorIsValidatedAsAWhole()
    {
        var rig = Open();
        var move = rig.Add<MoveToCutsceneActionData>(CutsceneActionTypes.MoveTo);
        ClearHistory();

        var row = rig.Row<CutsceneDetailsPanel.VectorRow>("destination");
        row.Fields[0].TextBox.SetText("3");
        row.Fields[2].TextBox.SetText("-4.5");
        row.Flush();

        Assert.Equal(new Vector3(3, 0, -4.5f), move.Destination);
        UndoTheOnlyEntry();
        Assert.Equal(Vector3.Zero, move.Destination);
        Assert.Equal("0", row.Fields[0].Text);
    }

    [Fact]
    public void TextTypedAndNotValidatedIsWrittenBeforeAnotherActionIsShown()
    {
        var rig = Open();
        var wait = (WaitCutsceneActionData)rig.Root.Actions[0];
        var move = rig.Add<MoveToCutsceneActionData>(CutsceneActionTypes.MoveTo);
        rig.Document.SelectAction(wait);
        ClearHistory();

        rig.Row<CutsceneDetailsPanel.NumberRow>("seconds").NumberField.TextBox.SetText("9");
        Assert.False(CanUndo());

        rig.Document.SelectAction(move);

        Assert.Equal(9f, wait.Seconds);
        Assert.Same(move, rig.Details.BuiltFor);
        UndoTheOnlyEntry();
        Assert.Equal(1.5f, wait.Seconds);
    }

    [Fact]
    public void TextTypedAndNotValidatedIsCommittedBeforeTheDocumentIsAskedToClose()
    {
        var rig = Open();
        var wait = (WaitCutsceneActionData)rig.Root.Actions[0];
        rig.Document.SelectAction(wait);
        ClearHistory();

        rig.Row<CutsceneDetailsPanel.NumberRow>("seconds").NumberField.TextBox.SetText("9");
        Assert.False(CanUndo());

        rig.Details.CommitPendingText();

        // The close question reads the dirty state right after: the typed value now counts as a change.
        Assert.Equal(9f, wait.Seconds);
        Assert.True(rig.Document.IsDirty);
        Assert.Same(wait, rig.Details.BuiltFor);
        Assert.Equal("9", rig.Row<CutsceneDetailsPanel.NumberRow>("seconds").NumberField.Text);
    }

    [Fact]
    public void TheMessagesOfTheSelectedActionAreListedAndFollowTheEdits()
    {
        var rig = Open();
        rig.Add<MoveToCutsceneActionData>(CutsceneActionTypes.MoveTo);

        Assert.Contains(Texts(rig.Details.MessagesStack), text => text.Contains("MoveTo.entity", StringComparison.Ordinal));

        var row = rig.Row<CutsceneDetailsPanel.TextRow>("entity");
        row.TextBox.SetText("hero");
        row.Flush();

        Assert.DoesNotContain(Texts(rig.Details.MessagesStack), text => text.Contains("MoveTo.entity", StringComparison.Ordinal));
    }

    [Fact]
    public void WithoutASelectionTheCutsceneIsShownAndTheDocumentIsReleasedByNull()
    {
        var rig = Open();
        Assert.Null(rig.Document.SelectedAction);
        Assert.Empty(rig.Details.Rows);
        Assert.Contains(Texts(rig.Details.Content), text => text.Contains("Editor play mode", StringComparison.Ordinal));
        Assert.Contains(Texts(rig.Details.Content), text => text.Contains("Validation", StringComparison.Ordinal));

        rig.Add<WaitCutsceneActionData>(CutsceneActionTypes.Wait);
        Assert.Single(rig.Details.Rows);

        rig.Details.SetEditorPanel(null);
        Assert.Empty(rig.Details.Rows);
        Assert.Null(rig.Details.Document);

        // The panel left the document: a later selection does not rebuild it.
        rig.Document.SelectAction(null);
        Assert.Empty(rig.Details.Rows);
        Assert.Contains(Texts(rig.Details.Content), text => text.Contains("No cutscene is open", StringComparison.Ordinal));
    }

    [Fact]
    public void TheFieldsKeepTheirControlsWhenTheDocumentChangesInPlace()
    {
        var rig = Open();
        var wait = (WaitCutsceneActionData)rig.Root.Actions[0];
        rig.Document.SelectAction(wait);

        var row = rig.Row<CutsceneDetailsPanel.NumberRow>("seconds");
        row.NumberField.TextBox.SetText("2");
        row.NumberField.Flush();

        // Same row object: an edit refills the fields, it does not replace them.
        Assert.Same(row, rig.Row<CutsceneDetailsPanel.NumberRow>("seconds"));
    }
}
