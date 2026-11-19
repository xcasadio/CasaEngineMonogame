using CasaEngine.Editor;
using CasaEngine.Editor.Controls;
using CasaEngine.Editor.History;
using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Scripting.Coroutines;
using CasaEngine.Tests.ContentBrowser;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// The cutscene document (ADR-0095, ADR-0097), built on the headless MGUI desktop without a GPU. The document keeps its header,
/// its path and its status line; the properties of the selected action are edited in the typed fields of the Inspector view
/// (<see cref="CutsceneDetailsPanel"/>), which write through the document. These tests cover the commit of a field into the
/// history of the document, the dirty state, a global undo or redo, the save and the refusal of a save. The asset catalog, the
/// editor history and <see cref="EngineEnvironment.ProjectPath"/> are global state, hence the serialized collection and the
/// restore in <see cref="Dispose"/>.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class CutsceneAssetInspectorPanelTests : IDisposable
{
    private const string RelativeFile = "Intro.cutscene";
    private const string ContextId = CasaEngine.Editor.Workspaces.EditorPanelIds.CutsceneAssetDocumentPrefix + "test";

    private static readonly EditorHistoryContext Context = new(EditorHistoryContextKind.Cutscene, ContextId);

    private readonly string _projectDirectory;
    private readonly string _previousProjectPath;
    private readonly List<CutsceneAssetInspectorPanel> _panels = [];

    public CutsceneAssetInspectorPanelTests()
    {
        _projectDirectory = Path.Combine(Path.GetTempPath(), "casa-cutscene-panel-" + Guid.NewGuid().ToString("N"));
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
        public required ContentBrowserViewTestHarness Harness { get; init; }

        public required CutsceneAssetInspectorPanel Panel { get; init; }

        public required CutsceneDetailsPanel Details { get; init; }

        public required CutsceneEditSession Session { get; init; }

        /// <summary>The content of the document: header, path and status line.</summary>
        public required MGElement Content { get; init; }

        public required string FullPath { get; init; }

        public int DirtyEvents { get; set; }

        public WaitCutsceneActionData Wait => (WaitCutsceneActionData)((SequenceCutsceneActionData)Session.Asset.RootAction!).Actions[0];

        public OpenScreenCutsceneActionData OpenScreen => (OpenScreenCutsceneActionData)((SequenceCutsceneActionData)Session.Asset.RootAction!).Actions[1];

        /// <summary>The text box of the field of a property of the selected action, whatever the kind of the field.</summary>
        public MGTextBox TextBoxOf(string key)
        {
            var row = Details.Rows.Single(candidate => candidate.Key == key);
            return row switch
            {
                CutsceneDetailsPanel.NumberRow number => number.NumberField.TextBox,
                CutsceneDetailsPanel.TextRow text => text.TextBox,
                _ => throw new InvalidOperationException($"The field of '{key}' has no text box."),
            };
        }
    }

    private static JObject DefaultActions() => new()
    {
        ["type"] = "Sequence",
        ["actions"] = new JArray
        {
            new JObject { ["type"] = "Wait", ["seconds"] = 1.5 },
            new JObject { ["type"] = "OpenScreen", ["target"] = "title" },
        },
    };

    /// <summary>
    /// A project with one cutscene file on disk, a document that opens it as the editor does, and the details panel of that
    /// document on the headless window. Nothing is selected.
    /// </summary>
    private Rig Open(JObject? rootAction = null, bool setHistoryContext = true)
    {
        var document = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "Intro",
            ["type"] = "CutsceneAsset",
            ["version"] = 1,
            ["schema_version"] = 1,
            ["root_action"] = rootAction ?? DefaultActions(),
        };
        string fullPath = Path.Combine(_projectDirectory, RelativeFile);
        File.WriteAllText(fullPath, document.ToString());

        Assert.True(CutsceneAssetInspectorPanel.TryLoadAsset(fullPath, out var session));
        Assert.NotNull(session);

        var harness = ContentBrowserViewTestHarness.Create(900, 900);
        var panel = new CutsceneAssetInspectorPanel(harness.Window);
        _panels.Add(panel);
        if (setHistoryContext)
        {
            panel.SetHistoryContextId(ContextId);
        }

        panel.LoadAsset(session, fullPath);
        var content = panel.CreateContent();

        var details = new CutsceneDetailsPanel(harness.Window);
        harness.Window.SetContent(details.CreateContent());
        details.SetEditorPanel(panel);

        var rig = new Rig
        {
            Harness = harness,
            Panel = panel,
            Details = details,
            Session = session,
            Content = content,
            FullPath = fullPath,
        };
        panel.DirtyStateChanged += _ => rig.DirtyEvents++;
        return rig;
    }

    private static string StatusText(MGElement content)
        => content.TraverseVisualTree().OfType<MGTextBlock>().Select(text => text.Text ?? string.Empty)
            .First(text => text.StartsWith("Validation OK.", StringComparison.Ordinal) || text.Contains("validation issue(s).", StringComparison.Ordinal));

    /// <summary>Types in a field and presses Enter, which commits it.</summary>
    private static void TypeAndPressEnter(Rig rig, MGTextBox textBox, string text)
    {
        rig.Harness.AdvanceFrame(0);
        rig.Harness.AdvanceFrame(16);
        textBox.Focus();
        rig.Harness.AdvanceFrame(32);
        textBox.SetText(text);
        rig.Harness.AdvanceFrame(48, pressedKeys: [Keys.Enter]);
        rig.Harness.AdvanceFrame(64);
    }

    [Fact]
    public void TheDetailsPanel_ShowsOneTypedFieldPerPropertyOfTheSelectedAction_WithTheTextOfTheValue()
    {
        var rig = Open();

        rig.Panel.SelectAction(rig.Wait);
        Assert.Equal("1.5", Assert.IsType<CutsceneDetailsPanel.NumberRow>(Assert.Single(rig.Details.Rows)).NumberField.Text);

        rig.Panel.SelectAction(rig.OpenScreen);
        Assert.Equal(CutsceneActionPropertyEditor.GetProperties(rig.OpenScreen).Count, rig.Details.Rows.Count);
        Assert.Equal("title", rig.TextBoxOf("target").Text);
        Assert.False(rig.Panel.IsDirty);
    }

    [Fact]
    public void TheDocument_HoldsNoTextField_AndShowsItsHeaderItsPathAndItsStatus()
    {
        var rig = Open();

        Assert.Empty(rig.Content.TraverseVisualTree().OfType<MGTextBox>());
        var texts = rig.Content.TraverseVisualTree().OfType<MGTextBlock>().Select(text => text.Text).ToList();
        Assert.Contains("[b]Intro[/b]", texts);
        Assert.Contains(RelativeFile, texts);
        Assert.Contains("Validation OK. Not modified.", texts);
    }

    [Fact]
    public void ACommittedField_WritesTheAssetThroughTheHistory_AndMakesThePanelDirty()
    {
        var rig = Open();
        rig.Panel.SelectAction(rig.Wait);

        TypeAndPressEnter(rig, rig.TextBoxOf("seconds"), "2.5");

        Assert.Equal(2.5f, rig.Wait.Seconds);
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(1, rig.DirtyEvents);
        Assert.True(EditorHistoryService.Current.TryGet(Context, out var stack));
        Assert.Equal("Set Wait.seconds", stack.UndoDescription);
        Assert.Equal("2.5", rig.TextBoxOf("seconds").Text);
        Assert.Contains("Modified.", StatusText(rig.Content));
    }

    [Fact]
    public void AnUndo_RestoresTheValueTheFieldAndTheCleanState()
    {
        var rig = Open();
        rig.Panel.SelectAction(rig.Wait);
        TypeAndPressEnter(rig, rig.TextBoxOf("seconds"), "2.5");
        Assert.True(rig.Panel.IsDirty);

        EditorHistoryService.Current.SetActiveContext(Context);
        Assert.True(EditorHistoryService.Current.Undo());

        Assert.Equal(1.5f, rig.Wait.Seconds);
        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(2, rig.DirtyEvents);
        Assert.Equal("1.5", rig.TextBoxOf("seconds").Text);
        Assert.Contains("Not modified.", StatusText(rig.Content));

        Assert.True(EditorHistoryService.Current.Redo());

        Assert.Equal(2.5f, rig.Wait.Seconds);
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal("2.5", rig.TextBoxOf("seconds").Text);
    }

    [Fact]
    public void AnInvalidText_RestoresTheField_ShowsTheReasonUnderIt_AndCreatesNoCommand()
    {
        var rig = Open();
        rig.Panel.SelectAction(rig.Wait);

        TypeAndPressEnter(rig, rig.TextBoxOf("seconds"), "abc");

        Assert.Equal(1.5f, rig.Wait.Seconds);
        Assert.Equal("1.5", rig.TextBoxOf("seconds").Text);
        Assert.False(rig.Panel.IsDirty);
        Assert.False(EditorHistoryService.Current.TryGet(Context, out var stack) && stack.CanUndo);
        Assert.Contains("seconds must be a finite number", rig.Details.Rows.Single(row => row.Key == "seconds").ErrorText);
    }

    [Fact]
    public void ATextEqualToTheCurrentValue_CreatesNoCommand()
    {
        var rig = Open();

        rig.Panel.SelectAction(rig.Wait);
        TypeAndPressEnter(rig, rig.TextBoxOf("seconds"), "1.50");
        rig.Panel.SelectAction(rig.OpenScreen);
        TypeAndPressEnter(rig, rig.TextBoxOf("target"), "title");

        Assert.False(rig.Panel.IsDirty);
        Assert.False(EditorHistoryService.Current.TryGet(Context, out var stack) && stack.CanUndo);
        rig.Panel.SelectAction(rig.Wait);
        Assert.Equal("1.5", rig.TextBoxOf("seconds").Text);
    }

    [Fact]
    public void EnterOnAFieldWithAFormatError_CommitsItsValue_AndClearsTheError()
    {
        var rig = Open(new JObject { ["type"] = "ShowSprite", ["entity"] = "orb", ["x"] = "abc" });
        var action = Assert.IsType<ShowSpriteCutsceneActionData>(rig.Session.Asset.RootAction);
        Assert.True(action.FormatErrors.ContainsKey("x"));
        Assert.True(CutsceneSaveGuard.TryGetBlockingReason(rig.Session, out _));
        rig.Panel.SelectAction(action);

        // The field shows the value the loader kept (empty): committing it as it is clears the error.
        TypeAndPressEnter(rig, rig.TextBoxOf("x"), string.Empty);

        Assert.False(action.FormatErrors.ContainsKey("x"));
        Assert.True(rig.Panel.IsDirty);
        Assert.False(CutsceneSaveGuard.TryGetBlockingReason(rig.Session, out _));
    }

    [Fact]
    public void WithoutAHistoryContext_AChangeIsRefused()
    {
        var rig = Open(setHistoryContext: false);
        rig.Panel.SelectAction(rig.Wait);

        TypeAndPressEnter(rig, rig.TextBoxOf("seconds"), "2.5");

        Assert.Equal(1.5f, rig.Wait.Seconds);
        Assert.False(rig.Panel.IsDirty);
        Assert.Equal("1.5", rig.TextBoxOf("seconds").Text);
        Assert.Contains("no history context", rig.Details.Rows.Single(row => row.Key == "seconds").ErrorText);
    }

    [Fact]
    public void DuringAPlaySession_AChangeIsRefused()
    {
        var rig = Open();
        rig.Panel.SelectAction(rig.Wait);
        EditorHistoryService.Current.IsSuspended = true;

        TypeAndPressEnter(rig, rig.TextBoxOf("seconds"), "2.5");

        Assert.Equal(1.5f, rig.Wait.Seconds);
        Assert.False(rig.Panel.IsDirty);
        Assert.Equal("1.5", rig.TextBoxOf("seconds").Text);
        Assert.Contains("play session", rig.Details.Rows.Single(row => row.Key == "seconds").ErrorText);
    }

    [Fact]
    public void TheSave_WritesAFileThatReloadsWithTheModifiedValue_AndMarksThePanelSaved()
    {
        var rig = Open();
        rig.Panel.SelectAction(rig.Wait);
        TypeAndPressEnter(rig, rig.TextBoxOf("seconds"), "2.5");
        Assert.True(rig.Panel.IsDirty);

        Assert.True(rig.Panel.TrySaveLoadedAsset(out string? error), error);

        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(2, rig.DirtyEvents);
        Assert.True(CutsceneAssetInspectorPanel.TryLoadAsset(rig.FullPath, out var reloaded));
        var wait = (WaitCutsceneActionData)((SequenceCutsceneActionData)reloaded!.Asset.RootAction!).Actions[0];
        Assert.Equal(2.5f, wait.Seconds);
        Assert.Contains("Not modified.", StatusText(rig.Content));
    }

    [Fact]
    public void ASaveOfAnUnmodifiedDocument_WritesNothing()
    {
        var rig = Open();
        string before = File.ReadAllText(rig.FullPath);

        Assert.True(rig.Panel.TrySaveLoadedAsset(out _));

        Assert.Equal(before, File.ReadAllText(rig.FullPath));
    }

    [Fact]
    public void TheSave_IsRefusedWithTheReasonOfTheGuard_WhenTheTreeHoldsAnUnknownAction()
    {
        var rig = Open(new JObject
        {
            ["type"] = "Sequence",
            ["actions"] = new JArray
            {
                new JObject { ["type"] = "Wait", ["seconds"] = 1.5 },
                new JObject { ["type"] = "Teleport" },
            },
        });
        string before = File.ReadAllText(rig.FullPath);
        rig.Panel.SelectAction(rig.Wait);
        TypeAndPressEnter(rig, rig.TextBoxOf("seconds"), "2.5");
        Assert.True(rig.Panel.IsDirty);

        Assert.False(rig.Panel.TrySaveLoadedAsset(out string? error));

        Assert.NotNull(error);
        Assert.Contains("root_action.actions[1]", error);
        Assert.Contains("Teleport", error);
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(before, File.ReadAllText(rig.FullPath));
    }

    [Fact]
    public void AnUnreadableFile_IsNotLoaded()
    {
        string fullPath = Path.Combine(_projectDirectory, "Broken.cutscene");
        File.WriteAllText(fullPath, "{ not json");

        Assert.False(CutsceneAssetInspectorPanel.TryLoadAsset(fullPath, out var session));
        Assert.Null(session);
    }

    [Fact]
    public void TheRuntimeSnapshotOfAnIdleCutscene_KeepsTheFieldsAndTheTextBeingTyped()
    {
        var rig = Open();
        rig.Panel.SelectAction(rig.Wait);
        var textBox = rig.TextBoxOf("seconds");
        textBox.SetText("9");

        rig.Panel.RefreshRuntimeSnapshot(IdleSnapshot());

        Assert.Same(textBox, rig.TextBoxOf("seconds"));
        Assert.Equal("9", textBox.Text);
    }

    [Fact]
    public void TheRuntimeSnapshotOfARunningCutscene_ShowsItsStateInTheCutsceneViewOfTheDetails_AndKeepsTheFields()
    {
        var rig = Open();

        rig.Panel.RefreshRuntimeSnapshot(RunningSnapshot());

        Assert.Contains(
            rig.Details.Content!.TraverseVisualTree().OfType<MGTextBlock>(),
            text => text.Text == "Playing");

        rig.Panel.SelectAction(rig.Wait);
        Assert.Equal("1.5", rig.TextBoxOf("seconds").Text);
    }

    [Fact]
    public void AClosedPanel_NoLongerFollowsTheHistory()
    {
        var rig = Open();
        rig.Panel.Dispose();
        var wait = rig.Wait;
        var property = CutsceneActionPropertyEditor.FindProperty(wait, "seconds")!;

        EditorHistoryService.Current.Execute(Context, new SetCutsceneActionPropertyCommand(rig.Session, wait, property, 4f));

        Assert.Equal(0, rig.DirtyEvents);
    }

    [Fact]
    public void ReloadingTheDocument_StartsAgainACleanHistory()
    {
        var rig = Open();
        rig.Panel.SelectAction(rig.Wait);
        TypeAndPressEnter(rig, rig.TextBoxOf("seconds"), "2.5");
        Assert.True(rig.Panel.IsDirty);

        Assert.True(CutsceneAssetInspectorPanel.TryLoadAsset(rig.FullPath, out var session));
        rig.Panel.LoadAsset(session!, rig.FullPath);

        Assert.False(rig.Panel.IsDirty);
        Assert.False(EditorHistoryService.Current.TryGet(Context, out var stack) && stack.CanUndo);
        Assert.Null(rig.Panel.SelectedAction);
        var reloadedWait = (WaitCutsceneActionData)((SequenceCutsceneActionData)session!.Asset.RootAction!).Actions[0];
        rig.Panel.SelectAction(reloadedWait);
        Assert.Equal("1.5", rig.TextBoxOf("seconds").Text);
    }

    [Fact]
    public void TheTransportOperations_WithoutAPreview_ReturnFalseAndDoNothing()
    {
        var rig = Open();

        Assert.False(rig.Panel.SeekPreview(1.0));
        Assert.False(rig.Panel.SeekPreview(-1.0));
        Assert.False(rig.Panel.TogglePreviewPlayback());
        Assert.False(rig.Panel.StepPreviewFrames(1));
        Assert.False(rig.Panel.StepPreviewBack());
        Assert.False(rig.Panel.StopPreview());
        Assert.Null(rig.Panel.Preview);
    }

    [Fact]
    public void SimulatePreviewInput_WithoutAPreview_ReturnsFalseAndDoesNothing()
    {
        var rig = Open();

        Assert.False(rig.Panel.SimulatePreviewInput("exit"));
        Assert.False(rig.Panel.SimulatePreviewInput(string.Empty));
        Assert.Null(rig.Panel.Preview);

        rig.Harness.AdvanceFrame(0);
        rig.Harness.AdvanceFrame(16);
        Assert.Equal(string.Empty, rig.Panel.TransportText);
    }

    [Fact]
    public void SetPreviewSound_WithoutAPreview_ReturnsFalseAndDoesNothing()
    {
        var rig = Open();

        Assert.False(rig.Panel.SetPreviewSound(true));
        Assert.False(rig.Panel.SetPreviewSound(false));
        Assert.Null(rig.Panel.Preview);

        rig.Harness.AdvanceFrame(0);
        rig.Harness.AdvanceFrame(16);
        Assert.Equal(string.Empty, rig.Panel.TransportText);
    }

    [Fact]
    public void TheTransportText_WithoutAPreview_IsEmpty()
    {
        var rig = Open();

        rig.Harness.AdvanceFrame(0);
        rig.Harness.AdvanceFrame(16);

        Assert.Equal(string.Empty, rig.Panel.TransportText);
    }

    private static CutsceneDebugSnapshot RunningSnapshot() => new(
        CutsceneRuntimeState.Playing,
        Guid.Empty,
        "Intro",
        RelativeFile,
        CoroutineHandle.Invalid,
        [],
        [],
        "Wait",
        null,
        null,
        "Running",
        null);

    private static CutsceneDebugSnapshot IdleSnapshot() => new(
        CutsceneRuntimeState.Idle,
        Guid.Empty,
        "Intro",
        RelativeFile,
        CoroutineHandle.Invalid,
        [],
        []);
}
