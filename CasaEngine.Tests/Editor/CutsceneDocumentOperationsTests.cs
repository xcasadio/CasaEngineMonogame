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
/// The selection and the editing operations of the cutscene document (ADR-0097), on the panel without a preview: every
/// operation is one entry of the history, marks the document modified, raises <c>DocumentChanged</c> once and undoes, and
/// the selection never aims at an action that left the tree.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class CutsceneDocumentOperationsTests : IDisposable
{
    private const string ContextId = CasaEngine.Editor.Workspaces.EditorPanelIds.CutsceneAssetDocumentPrefix + "operations";

    private static readonly EditorHistoryContext Context = new(EditorHistoryContextKind.Cutscene, ContextId);

    private readonly string _projectDirectory;
    private readonly string _previousProjectPath;
    private readonly List<CutsceneAssetInspectorPanel> _panels = [];

    public CutsceneDocumentOperationsTests()
    {
        _projectDirectory = Path.Combine(Path.GetTempPath(), "casa-cutscene-ops-" + Guid.NewGuid().ToString("N"));
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
        public required CutsceneAssetInspectorPanel Panel { get; init; }

        public required CutsceneEditSession Session { get; init; }

        public int DocumentChanges { get; set; }

        public int SelectionChanges { get; set; }

        public SequenceCutsceneActionData Root => (SequenceCutsceneActionData)Session.Asset.RootAction!;

        public WaitCutsceneActionData Wait => (WaitCutsceneActionData)Root.Actions[0];

        public SequenceCutsceneActionData Inner => (SequenceCutsceneActionData)Root.Actions[1];

        public FadeScreenCutsceneActionData Fade => (FadeScreenCutsceneActionData)Root.Actions[2];
    }

    private Rig Open(bool setHistoryContext = true)
    {
        var rootAction = new JObject
        {
            ["type"] = "Sequence",
            ["actions"] = new JArray
            {
                new JObject { ["type"] = "Wait", ["seconds"] = 1.5 },
                new JObject
                {
                    ["type"] = "Sequence",
                    ["actions"] = new JArray { new JObject { ["type"] = "Wait", ["seconds"] = 2 } },
                },
                new JObject { ["type"] = "FadeScreen" },
            },
        };
        var document = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "Operations",
            ["type"] = "CutsceneAsset",
            ["version"] = 1,
            ["schema_version"] = 1,
            ["root_action"] = rootAction,
        };
        string fullPath = Path.Combine(_projectDirectory, "Operations.cutscene");
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
        harness.Window.SetContent(panel.CreateContent());

        var rig = new Rig { Panel = panel, Session = session };
        panel.DocumentChanged += _ => rig.DocumentChanges++;
        panel.SelectionChanged += _ => rig.SelectionChanges++;
        return rig;
    }

    private static CutsceneActionProperty Property(CutsceneActionData action, string key)
        => CutsceneActionPropertyEditor.GetProperties(action).Single(property => property.Key == key);

    private static void Undo()
    {
        EditorHistoryService.Current.SetActiveContext(Context);
        Assert.True(EditorHistoryService.Current.Undo());
    }

    private static void Redo()
    {
        EditorHistoryService.Current.SetActiveContext(Context);
        Assert.True(EditorHistoryService.Current.Redo());
    }

    [Fact]
    public void ANewlyLoadedDocument_HasNoSelection_AndIsClean()
    {
        var rig = Open();

        Assert.Null(rig.Panel.SelectedAction);
        Assert.False(rig.Panel.IsDirty);
    }

    [Fact]
    public void SelectAction_SelectsAnActionOfTheTree_AndRefusesAnotherOne()
    {
        var rig = Open();

        Assert.True(rig.Panel.SelectAction(rig.Wait));
        Assert.Same(rig.Wait, rig.Panel.SelectedAction);
        Assert.Equal(1, rig.SelectionChanges);

        Assert.True(rig.Panel.SelectAction(rig.Wait));
        Assert.Equal(1, rig.SelectionChanges);

        Assert.False(rig.Panel.SelectAction(new WaitCutsceneActionData()));
        Assert.Same(rig.Wait, rig.Panel.SelectedAction);

        Assert.True(rig.Panel.SelectAction(null));
        Assert.Null(rig.Panel.SelectedAction);
        Assert.Equal(2, rig.SelectionChanges);
    }

    [Fact]
    public void SetProperty_IsOneHistoryEntry_MarksDirty_RaisesDocumentChangedOnce_AndUndoes()
    {
        var rig = Open();
        var seconds = Property(rig.Wait, "seconds");

        Assert.Null(rig.Panel.SetProperty(rig.Wait, seconds, 4f));

        Assert.Equal(4f, rig.Wait.Seconds);
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(1, rig.DocumentChanges);

        Undo();

        Assert.Equal(1.5f, rig.Wait.Seconds);
        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(2, rig.DocumentChanges);
        Assert.False(EditorHistoryService.Current.CanUndo);

        Redo();
        Assert.Equal(4f, rig.Wait.Seconds);
        Assert.Equal(3, rig.DocumentChanges);
    }

    [Fact]
    public void SetProperty_WithTheCurrentValue_MakesNoEntry()
    {
        var rig = Open();

        Assert.Null(rig.Panel.SetProperty(rig.Wait, Property(rig.Wait, "seconds"), 1.5f));

        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(0, rig.DocumentChanges);
    }

    [Fact]
    public void SetProperties_WritesSeveralPropertiesInOneEntry()
    {
        var rig = Open();
        var changes = new[]
        {
            new CutscenePropertyChange(Property(rig.Fade, "r"), (byte)10),
            new CutscenePropertyChange(Property(rig.Fade, "g"), (byte)20),
            new CutscenePropertyChange(Property(rig.Fade, "b"), (byte)30),
        };

        Assert.Null(rig.Panel.SetProperties(rig.Fade, "Set FadeScreen color", changes));

        Assert.Equal((byte)10, rig.Fade.R);
        Assert.Equal((byte)20, rig.Fade.G);
        Assert.Equal((byte)30, rig.Fade.B);
        Assert.Equal(1, rig.DocumentChanges);
        Assert.True(rig.Panel.IsDirty);

        Undo();

        Assert.Equal((byte)0, rig.Fade.R);
        Assert.Equal((byte)0, rig.Fade.G);
        Assert.Equal((byte)0, rig.Fade.B);
        Assert.False(rig.Panel.IsDirty);
        Assert.False(EditorHistoryService.Current.CanUndo);
    }

    [Fact]
    public void AddAction_WithoutSelection_AppendsToTheRoot_SelectsIt_AndUndoClearsTheSelection()
    {
        var rig = Open();

        Assert.Null(rig.Panel.AddAction(CutsceneActionTypes.Wait));

        Assert.Equal(4, rig.Root.Actions.Count);
        Assert.Same(rig.Root.Actions[3], rig.Panel.SelectedAction);
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(1, rig.DocumentChanges);

        Undo();

        Assert.Equal(3, rig.Root.Actions.Count);
        Assert.Null(rig.Panel.SelectedAction);
        Assert.False(rig.Panel.IsDirty);
        Assert.False(EditorHistoryService.Current.CanUndo);

        Redo();
        Assert.Equal(4, rig.Root.Actions.Count);
        Assert.Null(rig.Panel.SelectedAction);
    }

    [Fact]
    public void AddAction_FollowsTheInsertionRule_ForAContainerAndForALeaf()
    {
        var rig = Open();

        rig.Panel.SelectAction(rig.Inner);
        Assert.Null(rig.Panel.AddAction(CutsceneActionTypes.Wait));
        Assert.Equal(2, rig.Inner.Actions.Count);
        Assert.Same(rig.Inner.Actions[1], rig.Panel.SelectedAction);

        rig.Panel.SelectAction(rig.Wait);
        Assert.Null(rig.Panel.AddAction(CutsceneActionTypes.WaitFrames));
        Assert.IsType<WaitFramesCutsceneActionData>(rig.Root.Actions[1]);
        Assert.Same(rig.Root.Actions[1], rig.Panel.SelectedAction);
    }

    [Fact]
    public void DuplicateSelectedAction_InsertsACopyAfterTheSource_AndSelectsIt()
    {
        var rig = Open();
        var source = rig.Wait;
        rig.Panel.SelectAction(source);

        Assert.Null(rig.Panel.DuplicateSelectedAction());

        Assert.Equal(4, rig.Root.Actions.Count);
        var copy = Assert.IsType<WaitCutsceneActionData>(rig.Root.Actions[1]);
        Assert.NotSame(source, copy);
        Assert.Equal(source.Seconds, copy.Seconds);
        Assert.Same(copy, rig.Panel.SelectedAction);
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(1, rig.DocumentChanges);

        Undo();
        Assert.Equal(3, rig.Root.Actions.Count);
        Assert.Null(rig.Panel.SelectedAction);
    }

    [Fact]
    public void RemoveSelectedAction_RemovesIt_LeavesNoSelection_AndStaysEmptyAfterUndoAndRedo()
    {
        var rig = Open();
        var removed = rig.Wait;
        rig.Panel.SelectAction(removed);

        Assert.Null(rig.Panel.RemoveSelectedAction());

        Assert.Equal(2, rig.Root.Actions.Count);
        Assert.Null(rig.Panel.SelectedAction);
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(1, rig.DocumentChanges);

        Undo();
        Assert.Same(removed, rig.Root.Actions[0]);
        Assert.Null(rig.Panel.SelectedAction);
        Assert.False(rig.Panel.IsDirty);

        Redo();
        Assert.Equal(2, rig.Root.Actions.Count);
        Assert.Null(rig.Panel.SelectedAction);
    }

    [Fact]
    public void RemovingAContainer_MakesItsChildrenUnselectable()
    {
        var rig = Open();
        var child = rig.Inner.Actions[0];

        rig.Panel.SelectAction(rig.Inner);
        Assert.Null(rig.Panel.RemoveSelectedAction());

        Assert.Null(rig.Panel.SelectedAction);
        Assert.False(rig.Panel.SelectAction(child));
    }

    [Fact]
    public void TheRoot_IsNeitherRemovedNorDuplicated()
    {
        var rig = Open();
        rig.Panel.SelectAction(rig.Root);

        Assert.NotNull(rig.Panel.RemoveSelectedAction());
        Assert.NotNull(rig.Panel.DuplicateSelectedAction());

        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(3, rig.Root.Actions.Count);
    }

    [Fact]
    public void WithoutASelection_DuplicateAndRemoveAreRefused()
    {
        var rig = Open();

        Assert.NotNull(rig.Panel.DuplicateSelectedAction());
        Assert.NotNull(rig.Panel.RemoveSelectedAction());
        Assert.False(rig.Panel.IsDirty);
    }

    [Fact]
    public void WithoutAHistoryContext_EveryOperationIsRefused()
    {
        var rig = Open(setHistoryContext: false);
        rig.Panel.SelectAction(rig.Wait);

        Assert.NotNull(rig.Panel.SetProperty(rig.Wait, Property(rig.Wait, "seconds"), 9f));
        Assert.NotNull(rig.Panel.AddAction(CutsceneActionTypes.Wait));
        Assert.NotNull(rig.Panel.DuplicateSelectedAction());
        Assert.NotNull(rig.Panel.RemoveSelectedAction());
        Assert.Equal(1.5f, rig.Wait.Seconds);
        Assert.Equal(3, rig.Root.Actions.Count);
    }

    [Fact]
    public void DuringAPlaySession_EveryOperationIsRefused()
    {
        var rig = Open();
        rig.Panel.SelectAction(rig.Wait);
        EditorHistoryService.Current.IsSuspended = true;

        Assert.NotNull(rig.Panel.SetProperty(rig.Wait, Property(rig.Wait, "seconds"), 9f));
        Assert.NotNull(rig.Panel.AddAction(CutsceneActionTypes.Wait));
        Assert.NotNull(rig.Panel.RemoveSelectedAction());
        Assert.Equal(1.5f, rig.Wait.Seconds);
        Assert.False(rig.Panel.IsDirty);
    }

    [Fact]
    public void SavingTheDocument_RaisesDocumentChangedOnce()
    {
        var rig = Open();
        rig.Panel.SetProperty(rig.Wait, Property(rig.Wait, "seconds"), 4f);
        int before = rig.DocumentChanges;

        Assert.True(rig.Panel.TrySaveLoadedAsset(out _));

        Assert.Equal(before + 1, rig.DocumentChanges);
        Assert.False(rig.Panel.IsDirty);
    }
}
