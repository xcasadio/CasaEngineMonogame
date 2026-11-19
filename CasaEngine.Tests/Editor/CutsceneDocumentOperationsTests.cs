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
/// The selection and the editing operations of the cutscene document (ADR-0136), on the panel without a preview: every
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
    private readonly List<EditorHistoryContext> _extraContexts = [];

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
        foreach (var extraContext in _extraContexts)
        {
            EditorHistoryService.Current.Remove(extraContext);
            EditorDirtyStateService.Current.Remove(extraContext);
        }

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

    // A document on a root action given by the test, with its own history context so that two documents can coexist.
    private Rig OpenRoot(JObject rootAction, string name, string? contextId = null)
    {
        var document = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = name,
            ["type"] = "CutsceneAsset",
            ["version"] = 1,
            ["schema_version"] = 1,
            ["root_action"] = rootAction,
        };
        string fullPath = Path.Combine(_projectDirectory, name + ".cutscene");
        File.WriteAllText(fullPath, document.ToString());

        Assert.True(CutsceneAssetInspectorPanel.TryLoadAsset(fullPath, out var session));
        Assert.NotNull(session);

        var harness = ContentBrowserViewTestHarness.Create(900, 900);
        var panel = new CutsceneAssetInspectorPanel(harness.Window);
        _panels.Add(panel);
        if (contextId != null)
        {
            _extraContexts.Add(new EditorHistoryContext(EditorHistoryContextKind.Cutscene, contextId));
        }

        panel.SetHistoryContextId(contextId ?? ContextId);
        panel.LoadAsset(session, fullPath);
        harness.Window.SetContent(panel.CreateContent());

        var rig = new Rig { Panel = panel, Session = session };
        panel.DocumentChanged += _ => rig.DocumentChanges++;
        panel.SelectionChanged += _ => rig.SelectionChanges++;
        return rig;
    }

    private static JObject Container(string type, params JObject[] children)
        => new() { ["type"] = type, ["actions"] = new JArray(children) };

    private static JObject WaitJson(object seconds) => new() { ["type"] = "Wait", ["seconds"] = JToken.FromObject(seconds) };

    private static void AssertOrder(SequenceCutsceneActionData root, params CutsceneActionData[] expected)
    {
        Assert.Equal(expected.Length, root.Actions.Count);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Same(expected[index], root.Actions[index]);
        }
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
    public void MoveAction_OfAnActionThatIsNotSelected_MovesItSelectsIt_AndUndoesAndRedoes()
    {
        var rig = Open();
        var wait = rig.Wait;
        var inner = rig.Inner;
        var fade = rig.Fade;
        rig.Panel.SelectAction(wait);
        int selectionChanges = rig.SelectionChanges;

        Assert.Null(rig.Panel.MoveAction(fade, rig.Root, 0));

        AssertOrder(rig.Root, fade, wait, inner);
        Assert.Same(fade, rig.Panel.SelectedAction);
        Assert.Equal(selectionChanges + 1, rig.SelectionChanges);
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(1, rig.DocumentChanges);

        Undo();
        AssertOrder(rig.Root, wait, inner, fade);
        Assert.Same(fade, rig.Panel.SelectedAction);
        Assert.False(rig.Panel.IsDirty);
        Assert.False(EditorHistoryService.Current.CanUndo);
        Assert.Equal(2, rig.DocumentChanges);

        Redo();
        AssertOrder(rig.Root, fade, wait, inner);
        Assert.Equal(3, rig.DocumentChanges);
    }

    [Fact]
    public void MoveAction_IntoAnotherContainer_PutsItAtTheIndex_AndKeepsTheInstance()
    {
        var rig = Open();
        var wait = rig.Wait;
        var inner = rig.Inner;
        var innerChild = inner.Actions[0];

        Assert.Null(rig.Panel.MoveAction(wait, inner, 1));

        Assert.Equal(2, rig.Root.Actions.Count);
        Assert.Equal(2, inner.Actions.Count);
        Assert.Same(innerChild, inner.Actions[0]);
        Assert.Same(wait, inner.Actions[1]);
        Assert.Same(wait, rig.Panel.SelectedAction);

        Undo();
        Assert.Same(wait, rig.Root.Actions[0]);
        Assert.Single(inner.Actions);
    }

    [Fact]
    public void MoveAction_Refusals_ChangeNothing()
    {
        var rig = Open();
        var innerChild = rig.Inner.Actions[0];

        Assert.NotNull(rig.Panel.MoveAction(rig.Root, rig.Inner, 0));
        Assert.NotNull(rig.Panel.MoveAction(rig.Inner, rig.Inner, 0));
        Assert.NotNull(rig.Panel.MoveAction(rig.Inner, innerChild, 0));
        Assert.NotNull(rig.Panel.MoveAction(rig.Wait, rig.Fade, 0));
        Assert.NotNull(rig.Panel.MoveAction(rig.Wait, rig.Root, 0));
        Assert.NotNull(rig.Panel.MoveAction(rig.Wait, rig.Root, 1));
        Assert.NotNull(rig.Panel.MoveAction(rig.Wait, rig.Root, 9));
        Assert.NotNull(rig.Panel.MoveAction(rig.Wait, new SequenceCutsceneActionData(), 0));
        Assert.NotNull(rig.Panel.MoveAction(new WaitCutsceneActionData(), rig.Root, 0));

        Assert.NotNull(rig.Panel.GetMoveRefusal(rig.Wait, rig.Root, 0));
        Assert.Null(rig.Panel.GetMoveRefusal(rig.Wait, rig.Root, 2));
        AssertOrder(rig.Root, rig.Wait, rig.Inner, rig.Fade);
        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(0, rig.DocumentChanges);
        Assert.Null(rig.Panel.SelectedAction);
    }

    [Fact]
    public void MoveUpAndDown_SwapWithTheNeighbour_AndAreRefusedAtBothEnds()
    {
        var rig = Open();
        var wait = rig.Wait;
        var inner = rig.Inner;
        var fade = rig.Fade;
        rig.Panel.SelectAction(wait);

        Assert.NotNull(rig.Panel.GetMoveUpRefusal());
        Assert.NotNull(rig.Panel.MoveSelectedActionUp());
        Assert.Null(rig.Panel.GetMoveDownRefusal());

        Assert.Null(rig.Panel.MoveSelectedActionDown());
        AssertOrder(rig.Root, inner, wait, fade);
        Assert.Null(rig.Panel.MoveSelectedActionDown());
        AssertOrder(rig.Root, inner, fade, wait);
        Assert.NotNull(rig.Panel.GetMoveDownRefusal());
        Assert.NotNull(rig.Panel.MoveSelectedActionDown());
        AssertOrder(rig.Root, inner, fade, wait);

        Assert.Null(rig.Panel.MoveSelectedActionUp());
        AssertOrder(rig.Root, inner, wait, fade);
        Assert.Same(wait, rig.Panel.SelectedAction);
        Assert.Equal(3, rig.DocumentChanges);

        Undo();
        AssertOrder(rig.Root, inner, fade, wait);
        Undo();
        Undo();
        AssertOrder(rig.Root, wait, inner, fade);
        Assert.False(rig.Panel.IsDirty);
        Assert.False(EditorHistoryService.Current.CanUndo);
    }

    [Fact]
    public void MoveUpAndDown_AreRefusedForTheRootAndWithoutASelection()
    {
        var rig = Open();

        Assert.NotNull(rig.Panel.GetMoveUpRefusal());
        Assert.NotNull(rig.Panel.GetMoveDownRefusal());

        rig.Panel.SelectAction(rig.Root);
        Assert.NotNull(rig.Panel.MoveSelectedActionUp());
        Assert.NotNull(rig.Panel.MoveSelectedActionDown());
        Assert.False(rig.Panel.IsDirty);
    }

    [Fact]
    public void MoveUpAndDown_NeverLeaveTheContainer_ForTheOnlyChildOfAnInnerContainer()
    {
        var rig = Open();
        rig.Panel.SelectAction(rig.Inner.Actions[0]);

        Assert.NotNull(rig.Panel.MoveSelectedActionDown());
        Assert.NotNull(rig.Panel.MoveSelectedActionUp());
        Assert.Single(rig.Inner.Actions);
    }

    [Fact]
    public void WrapSelectedAction_PutsTheActionInANewContainer_SelectsTheContainer_AndUndoesAndRedoes()
    {
        var rig = Open();
        var wait = rig.Wait;
        rig.Panel.SelectAction(wait);

        Assert.Null(rig.Panel.WrapSelectedAction(CutsceneActionTypes.Parallel));

        var container = Assert.IsType<ParallelCutsceneActionData>(rig.Root.Actions[0]);
        Assert.Same(wait, Assert.Single(container.Actions));
        Assert.Same(container, rig.Panel.SelectedAction);
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(1, rig.DocumentChanges);

        Undo();
        Assert.Same(wait, rig.Root.Actions[0]);
        Assert.False(rig.Panel.IsDirty);
        Assert.Null(rig.Panel.SelectedAction);

        Redo();
        Assert.Same(container, rig.Root.Actions[0]);
    }

    [Fact]
    public void WrapSelectedAction_OfTheRoot_MakesTheContainerTheNewRoot()
    {
        var rig = Open();
        var oldRoot = rig.Root;
        rig.Panel.SelectAction(oldRoot);

        Assert.Null(rig.Panel.WrapSelectedAction(CutsceneActionTypes.Repeat));

        var newRoot = Assert.IsType<RepeatCutsceneActionData>(rig.Session.Asset.RootAction);
        Assert.Same(oldRoot, Assert.Single(newRoot.Actions));
        Assert.Same(newRoot, rig.Panel.SelectedAction);

        Undo();
        Assert.Same(oldRoot, rig.Session.Asset.RootAction);
    }

    [Fact]
    public void WrapSelectedAction_IsRefusedForANonContainerTypeAndWithoutASelection()
    {
        var rig = Open();

        Assert.NotNull(rig.Panel.WrapSelectedAction(CutsceneActionTypes.Sequence));
        Assert.NotNull(rig.Panel.GetWrapRefusal(CutsceneActionTypes.Sequence));

        rig.Panel.SelectAction(rig.Wait);
        Assert.NotNull(rig.Panel.WrapSelectedAction(CutsceneActionTypes.Wait));
        Assert.Null(rig.Panel.GetWrapRefusal(CutsceneActionTypes.Sequence));
        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(0, rig.DocumentChanges);
    }

    [Fact]
    public void UngroupSelectedAction_PutsTheChildrenInPlace_SelectsTheFirst_AndUndoes()
    {
        var rig = Open();
        var wait = rig.Wait;
        var inner = rig.Inner;
        var innerChild = inner.Actions[0];
        var fade = rig.Fade;
        rig.Panel.SelectAction(inner);

        Assert.Null(rig.Panel.UngroupSelectedAction());

        AssertOrder(rig.Root, wait, innerChild, fade);
        Assert.Same(innerChild, rig.Panel.SelectedAction);
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(1, rig.DocumentChanges);

        Undo();
        AssertOrder(rig.Root, wait, inner, fade);
        Assert.Same(innerChild, Assert.Single(inner.Actions));
        Assert.Same(innerChild, rig.Panel.SelectedAction);
        Assert.False(rig.Panel.IsDirty);

        Redo();
        AssertOrder(rig.Root, wait, innerChild, fade);
        Assert.Same(innerChild, rig.Panel.SelectedAction);
    }

    [Fact]
    public void UngroupSelectedAction_OfAnEmptyContainer_RemovesItAndSelectsNothing()
    {
        var rig = OpenRoot(Container("Sequence", WaitJson(1), Container("Parallel")), "Empty");
        var empty = rig.Root.Actions[1];
        rig.Panel.SelectAction(empty);

        Assert.Null(rig.Panel.UngroupSelectedAction());

        Assert.Single(rig.Root.Actions);
        Assert.Null(rig.Panel.SelectedAction);

        Undo();
        Assert.Same(empty, rig.Root.Actions[1]);
    }

    [Fact]
    public void UngroupSelectedAction_IsRefusedForALeaf_TheRootAndWithoutASelection()
    {
        var rig = Open();

        Assert.NotNull(rig.Panel.UngroupSelectedAction());

        rig.Panel.SelectAction(rig.Wait);
        Assert.NotNull(rig.Panel.UngroupSelectedAction());
        rig.Panel.SelectAction(rig.Root);
        Assert.NotNull(rig.Panel.GetUngroupRefusal());
        Assert.NotNull(rig.Panel.UngroupSelectedAction());
        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(0, rig.DocumentChanges);
    }

    [Fact]
    public void CopySelectedAction_FillsTheClipboard_WithoutChangingTheDocument()
    {
        var rig = Open();
        rig.Panel.SelectAction(rig.Inner);

        Assert.False(rig.Panel.Clipboard.HasContent);
        Assert.Null(rig.Panel.CopySelectedAction());

        Assert.Equal(CutsceneActionTypes.Sequence, rig.Panel.Clipboard.ContentType);
        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(0, rig.DocumentChanges);
        Assert.False(EditorHistoryService.Current.CanUndo);

        rig.Panel.SelectAction(rig.Wait);
        Assert.Null(rig.Panel.CopySelectedAction());
        Assert.Equal(CutsceneActionTypes.Wait, rig.Panel.Clipboard.ContentType);

        rig.Panel.SelectAction(rig.Root);
        Assert.Null(rig.Panel.CopySelectedAction());
        Assert.Equal(CutsceneActionTypes.Sequence, rig.Panel.Clipboard.ContentType);
    }

    [Fact]
    public void CopySelectedAction_WithoutASelection_IsRefused()
    {
        var rig = Open();

        Assert.NotNull(rig.Panel.GetCopyRefusal());
        Assert.NotNull(rig.Panel.CopySelectedAction());
        Assert.False(rig.Panel.Clipboard.HasContent);
    }

    [Fact]
    public void PasteAction_InsertsANewCopyEachTime_SelectsIt_AndUndoes()
    {
        var rig = Open();
        rig.Panel.SelectAction(rig.Wait);
        Assert.Null(rig.Panel.CopySelectedAction());

        rig.Panel.SelectAction(rig.Fade);
        Assert.Null(rig.Panel.PasteAction());

        Assert.Equal(4, rig.Root.Actions.Count);
        var first = Assert.IsType<WaitCutsceneActionData>(rig.Root.Actions[3]);
        Assert.NotSame(rig.Wait, first);
        Assert.Equal(rig.Wait.Seconds, first.Seconds);
        Assert.Same(first, rig.Panel.SelectedAction);
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(1, rig.DocumentChanges);

        Assert.Null(rig.Panel.PasteAction());
        var second = Assert.IsType<WaitCutsceneActionData>(rig.Root.Actions[4]);
        Assert.NotSame(first, second);
        Assert.Same(second, rig.Panel.SelectedAction);

        Undo();
        Assert.Equal(4, rig.Root.Actions.Count);
        Undo();
        Assert.Equal(3, rig.Root.Actions.Count);
        Assert.False(rig.Panel.IsDirty);
    }

    [Fact]
    public void PasteAction_FollowsTheInsertionRule_ForAContainerAndWithoutASelection()
    {
        var rig = Open();
        rig.Panel.SelectAction(rig.Wait);
        Assert.Null(rig.Panel.CopySelectedAction());

        rig.Panel.SelectAction(rig.Inner);
        Assert.Null(rig.Panel.PasteAction());
        Assert.Equal(2, rig.Inner.Actions.Count);
        Assert.Same(rig.Inner.Actions[1], rig.Panel.SelectedAction);

        rig.Panel.SelectAction(null);
        Assert.Null(rig.Panel.PasteAction());
        Assert.Equal(4, rig.Root.Actions.Count);
        Assert.Same(rig.Root.Actions[3], rig.Panel.SelectedAction);
    }

    [Fact]
    public void PasteAction_WithAnEmptyClipboard_IsRefused()
    {
        var rig = Open();

        Assert.NotNull(rig.Panel.GetPasteRefusal());
        Assert.NotNull(rig.Panel.PasteAction());
        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(0, rig.DocumentChanges);
    }

    [Fact]
    public void CutSelectedAction_CopiesThenRemoves_InOneEntry_AndUndoesToTheSameInstance()
    {
        var rig = Open();
        var wait = rig.Wait;
        var inner = rig.Inner;
        var fade = rig.Fade;
        rig.Panel.SelectAction(inner);

        Assert.Null(rig.Panel.CutSelectedAction());

        AssertOrder(rig.Root, wait, fade);
        Assert.Equal(CutsceneActionTypes.Sequence, rig.Panel.Clipboard.ContentType);
        Assert.Null(rig.Panel.SelectedAction);
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(1, rig.DocumentChanges);

        Undo();
        Assert.Same(inner, rig.Root.Actions[1]);
        Assert.False(rig.Panel.IsDirty);
        Assert.False(EditorHistoryService.Current.CanUndo);
        Assert.True(rig.Panel.Clipboard.HasContent);

        Redo();
        AssertOrder(rig.Root, wait, fade);
    }

    [Fact]
    public void CutThenPaste_PutsTheSubtreeBackAsACopy()
    {
        var rig = Open();
        rig.Panel.SelectAction(rig.Inner);
        Assert.Null(rig.Panel.CutSelectedAction());

        rig.Panel.SelectAction(rig.Wait);
        Assert.Null(rig.Panel.PasteAction());

        var pasted = Assert.IsType<SequenceCutsceneActionData>(rig.Root.Actions[1]);
        Assert.IsType<WaitCutsceneActionData>(Assert.Single(pasted.Actions));
        Assert.Same(pasted, rig.Panel.SelectedAction);
    }

    [Fact]
    public void CutSelectedAction_IsRefusedForTheRoot_AndTheClipboardKeepsItsContent()
    {
        var rig = Open();
        rig.Panel.SelectAction(rig.Wait);
        Assert.Null(rig.Panel.CopySelectedAction());

        rig.Panel.SelectAction(rig.Root);
        Assert.NotNull(rig.Panel.GetCutRefusal());
        Assert.NotNull(rig.Panel.CutSelectedAction());

        Assert.Equal(CutsceneActionTypes.Wait, rig.Panel.Clipboard.ContentType);
        Assert.Equal(3, rig.Root.Actions.Count);
        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(0, rig.DocumentChanges);
    }

    [Fact]
    public void CopyAndCut_OfAnActionThatHoldsALoadLoss_AreRefused_AndChangeNeitherTheClipboardNorTheTree()
    {
        // The string "2" is read as a number and rewritten as a number: the loader notes the loss on that Wait.
        var rig = OpenRoot(Container("Sequence", WaitJson(1), Container("Parallel", WaitJson("2"))), "Lossy");
        var kept = rig.Root.Actions[0];
        var lossy = rig.Root.Actions[1];
        rig.Panel.SelectAction(kept);
        Assert.Null(rig.Panel.CopySelectedAction());
        Assert.NotEmpty(rig.Session.LoadLosses);

        rig.Panel.SelectAction(lossy);
        Assert.NotNull(rig.Panel.GetCopyRefusal());
        Assert.NotNull(rig.Panel.CopySelectedAction());
        Assert.NotNull(rig.Panel.GetCutRefusal());
        Assert.NotNull(rig.Panel.CutSelectedAction());

        Assert.Equal(CutsceneActionTypes.Wait, rig.Panel.Clipboard.ContentType);
        Assert.Equal(2, rig.Root.Actions.Count);
        Assert.Same(lossy, rig.Root.Actions[1]);
        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(0, rig.DocumentChanges);
    }

    [Fact]
    public void CopyAndCut_OfAnUnknownType_AreRefused_AndChangeNeitherTheClipboardNorTheTree()
    {
        var rig = Open();
        var unknown = new UnknownCutsceneActionData("Dance");
        rig.Root.Actions.Add(unknown);
        rig.Panel.SelectAction(rig.Wait);
        Assert.Null(rig.Panel.CopySelectedAction());

        rig.Panel.SelectAction(unknown);
        Assert.NotNull(rig.Panel.CopySelectedAction());
        Assert.NotNull(rig.Panel.CutSelectedAction());

        Assert.Equal(CutsceneActionTypes.Wait, rig.Panel.Clipboard.ContentType);
        Assert.Equal(4, rig.Root.Actions.Count);
        Assert.False(rig.Panel.IsDirty);
    }

    [Fact]
    public void ACopyInOneDocument_IsPastedInAnotherThatSharesTheClipboard()
    {
        var first = Open();
        var second = OpenRoot(Container("Sequence", WaitJson(7)), "Second", ContextId + "-second");
        var shared = new CutsceneClipboard();
        first.Panel.Clipboard = shared;
        second.Panel.Clipboard = shared;

        first.Panel.SelectAction(first.Inner);
        Assert.Null(first.Panel.CopySelectedAction());

        Assert.Null(second.Panel.PasteAction());

        Assert.Equal(2, second.Root.Actions.Count);
        var pasted = Assert.IsType<SequenceCutsceneActionData>(second.Root.Actions[1]);
        Assert.NotSame(first.Inner, pasted);
        Assert.Same(pasted, second.Panel.SelectedAction);
        Assert.Equal(3, first.Root.Actions.Count);
        Assert.False(first.Panel.IsDirty);
        Assert.Equal(0, first.DocumentChanges);
        Assert.Equal(1, second.DocumentChanges);
    }

    [Fact]
    public void ADocumentWithoutASharedClipboard_HasItsOwn()
    {
        var first = Open();
        var second = OpenRoot(Container("Sequence", WaitJson(7)), "Own", ContextId + "-own");

        Assert.NotSame(first.Panel.Clipboard, second.Panel.Clipboard);
        Assert.Throws<ArgumentNullException>(() => first.Panel.Clipboard = null!);
    }

    [Fact]
    public void WithoutAHistoryContext_TheStructureOperationsAreRefused_ButCopyStillWorks()
    {
        var rig = Open(setHistoryContext: false);
        rig.Panel.SelectAction(rig.Inner);

        Assert.NotNull(rig.Panel.MoveAction(rig.Fade, rig.Root, 0));
        Assert.NotNull(rig.Panel.MoveSelectedActionUp());
        Assert.NotNull(rig.Panel.WrapSelectedAction(CutsceneActionTypes.Parallel));
        Assert.NotNull(rig.Panel.UngroupSelectedAction());
        Assert.NotNull(rig.Panel.CutSelectedAction());
        Assert.Null(rig.Panel.CopySelectedAction());
        Assert.NotNull(rig.Panel.PasteAction());

        AssertOrder(rig.Root, rig.Wait, rig.Inner, rig.Fade);
        Assert.Equal(0, rig.DocumentChanges);
    }

    [Fact]
    public void DuringAPlaySession_TheStructureOperationsAreRefused()
    {
        var rig = Open();
        rig.Panel.SelectAction(rig.Inner);
        Assert.Null(rig.Panel.CopySelectedAction());
        EditorHistoryService.Current.IsSuspended = true;

        Assert.NotNull(rig.Panel.MoveAction(rig.Fade, rig.Root, 0));
        Assert.NotNull(rig.Panel.MoveSelectedActionDown());
        Assert.NotNull(rig.Panel.WrapSelectedAction(CutsceneActionTypes.Parallel));
        Assert.NotNull(rig.Panel.UngroupSelectedAction());
        Assert.NotNull(rig.Panel.CutSelectedAction());
        Assert.NotNull(rig.Panel.PasteAction());

        AssertOrder(rig.Root, rig.Wait, rig.Inner, rig.Fade);
        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(0, rig.DocumentChanges);
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

    private static JObject AtJson(object seconds, params JObject[] children)
    {
        var at = Container("At", children);
        at["seconds"] = JToken.FromObject(seconds);
        return at;
    }

    [Fact]
    public void PlaceActionAt_OnTheFirstChildOfAnAt_WritesTheSecondsOfTheAt_InOneEntry_AndSelectsTheAction()
    {
        var rig = OpenRoot(Container("Parallel", AtJson(0.5, WaitJson(1), WaitJson(1))), "PlaceAt");
        var at = (AtCutsceneActionData)((ParallelCutsceneActionData)rig.Session.Asset.RootAction!).Actions[0];
        var first = at.Actions[0];

        Assert.Null(rig.Panel.GetPlaceActionRefusal(first, 1.2));
        Assert.Null(rig.Panel.PlaceActionAt(first, 1.2));

        Assert.Equal(1.2f, at.Seconds);
        Assert.Same(first, rig.Panel.SelectedAction);
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(1, rig.DocumentChanges);

        Undo();
        Assert.Equal(0.5f, at.Seconds);
        Assert.False(EditorHistoryService.Current.CanUndo);
        Assert.False(rig.Panel.IsDirty);

        Redo();
        Assert.Equal(1.2f, at.Seconds);
    }

    [Fact]
    public void PlaceActionAt_AlignsOnFramesAndStopsAtZero()
    {
        var rig = OpenRoot(Container("Parallel", AtJson(0.5, WaitJson(1))), "PlaceAlign");
        var at = (AtCutsceneActionData)((ParallelCutsceneActionData)rig.Session.Asset.RootAction!).Actions[0];
        var wait = at.Actions[0];

        Assert.Null(rig.Panel.PlaceActionAt(wait, 0.5 + 0.7 + 0.004));
        Assert.Equal(1.2f, at.Seconds);

        Assert.Null(rig.Panel.PlaceActionAt(wait, -3));
        Assert.Equal(0f, at.Seconds);
    }

    [Fact]
    public void PlaceActionAt_OnADirectChildOfAParallel_WrapsItInAnAt_KeepsTheInstanceAndItsPlace_InOneEntry()
    {
        var rig = OpenRoot(Container("Parallel", WaitJson(1), WaitJson(2)), "PlaceWrap");
        var parallel = (ParallelCutsceneActionData)rig.Session.Asset.RootAction!;
        var first = parallel.Actions[0];
        var second = parallel.Actions[1];

        Assert.Null(rig.Panel.PlaceActionAt(second, 0.5));

        Assert.Equal(2, parallel.Actions.Count);
        Assert.Same(first, parallel.Actions[0]);
        var at = Assert.IsType<AtCutsceneActionData>(parallel.Actions[1]);
        Assert.Equal(0.5f, at.Seconds);
        Assert.Same(second, Assert.Single(at.Actions));
        Assert.Same(second, rig.Panel.SelectedAction);
        Assert.Equal(1, rig.DocumentChanges);

        Undo();
        Assert.Same(second, parallel.Actions[1]);
        Assert.False(EditorHistoryService.Current.CanUndo);
        Assert.False(rig.Panel.IsDirty);
        Assert.Same(second, rig.Panel.SelectedAction);

        Redo();
        Assert.Same(at, parallel.Actions[1]);
        Assert.Same(second, Assert.Single(at.Actions));
    }

    [Fact]
    public void PlaceActionAt_Refusals_ChangeNothing()
    {
        var rig = OpenRoot(
            Container("Sequence", WaitJson(1), Container("Parallel", AtJson(0.5, WaitJson(1), WaitJson(2)))),
            "PlaceRefusals");
        var root = (SequenceCutsceneActionData)rig.Session.Asset.RootAction!;
        var parallel = (ParallelCutsceneActionData)root.Actions[1];
        var at = (AtCutsceneActionData)parallel.Actions[0];

        // A later child of a Sequence, a later child of an At, and containers (no bar): refused.
        Assert.NotNull(rig.Panel.PlaceActionAt(root.Actions[0], 2));
        Assert.NotNull(rig.Panel.PlaceActionAt(at.Actions[1], 2));
        Assert.NotNull(rig.Panel.PlaceActionAt(parallel, 2));
        Assert.NotNull(rig.Panel.PlaceActionAt(at, 2));
        Assert.NotNull(rig.Panel.PlaceActionAt(root, 2));

        // The same time as now (the first child of the At starts at 1.5 s), a time that is not a number, an action of another cutscene.
        Assert.NotNull(rig.Panel.PlaceActionAt(at.Actions[0], 1.5));
        Assert.NotNull(rig.Panel.PlaceActionAt(at.Actions[0], double.NaN));
        Assert.NotNull(rig.Panel.PlaceActionAt(new WaitCutsceneActionData(), 2));

        Assert.Equal(0.5f, at.Seconds);
        Assert.Equal(2, root.Actions.Count);
        Assert.False(rig.Panel.IsDirty);
        Assert.False(EditorHistoryService.Current.CanUndo);
        Assert.Equal(0, rig.DocumentChanges);
        Assert.Null(rig.Panel.SelectedAction);
    }

    [Fact]
    public void PlaceActionAt_IsRefusedWithoutAHistoryContext_AndDuringAPlaySession()
    {
        var rig = OpenRoot(Container("Parallel", WaitJson(1)), "PlaceSession");
        var wait = ((ParallelCutsceneActionData)rig.Session.Asset.RootAction!).Actions[0];
        EditorHistoryService.Current.IsSuspended = true;

        Assert.NotNull(rig.Panel.PlaceActionAt(wait, 1));
        Assert.NotNull(rig.Panel.GetPlaceActionRefusal(wait, 1));
        Assert.NotNull(rig.Panel.AddActionAt(CutsceneActionTypes.Wait, 1));

        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(0, rig.DocumentChanges);

        EditorHistoryService.Current.IsSuspended = false;
        var withoutContext = Open(setHistoryContext: false);
        Assert.NotNull(withoutContext.Panel.PlaceActionAt(withoutContext.Wait, 1));
        Assert.NotNull(withoutContext.Panel.GetAddActionAtRefusal(1));
    }

    [Fact]
    public void PlaceActionAt_UsesTheCurrentStartOfTheItem_ForABlockThatDoesNotStartAtZero()
    {
        // The Parallel starts at 1 s: dragging by difference moves the At by the same amount whatever the start of the block.
        var rig = OpenRoot(
            Container("Sequence", WaitJson(1), Container("Parallel", AtJson(0.5, WaitJson(1)))),
            "PlaceDiff");
        var root = (SequenceCutsceneActionData)rig.Session.Asset.RootAction!;
        var at = (AtCutsceneActionData)((ParallelCutsceneActionData)root.Actions[1]).Actions[0];

        // The child starts at 1.5 s; dropping it at 2 s adds 0.5 s.
        Assert.Null(rig.Panel.PlaceActionAt(at.Actions[0], 2));

        Assert.Equal(1f, at.Seconds);
    }

    [Fact]
    public void AddActionAt_PutsAnAtAtTheEndOfTheSelectedParallel_CountedFromItsStart_AndSelectsTheAction()
    {
        var rig = OpenRoot(Container("Sequence", WaitJson(1), Container("Parallel", WaitJson(1))), "AddAt");
        var root = (SequenceCutsceneActionData)rig.Session.Asset.RootAction!;
        var parallel = (ParallelCutsceneActionData)root.Actions[1];
        rig.Panel.SelectAction(parallel);

        Assert.Null(rig.Panel.GetAddActionAtRefusal(2.5));
        Assert.Null(rig.Panel.GetAddActionAtRefusal(CutsceneActionTypes.Wait, 2.5));
        Assert.Null(rig.Panel.AddActionAt(CutsceneActionTypes.Wait, 2.5));

        Assert.Equal(2, parallel.Actions.Count);
        var at = Assert.IsType<AtCutsceneActionData>(parallel.Actions[1]);
        Assert.Equal(1.5f, at.Seconds);
        var created = Assert.IsType<WaitCutsceneActionData>(Assert.Single(at.Actions));
        Assert.Same(created, rig.Panel.SelectedAction);
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(1, rig.DocumentChanges);

        Undo();
        Assert.Single(parallel.Actions);
        Assert.False(EditorHistoryService.Current.CanUndo);
        Assert.False(rig.Panel.IsDirty);
        Assert.Null(rig.Panel.SelectedAction);

        Redo();
        Assert.Same(at, parallel.Actions[1]);
    }

    [Fact]
    public void AddActionAt_FromAnActionInsideAnAt_TargetsTheNearestParallel_AndStopsAtZero()
    {
        var rig = OpenRoot(Container("Sequence", WaitJson(1), Container("Parallel", AtJson(0.5, WaitJson(1)))), "AddAtNested");
        var root = (SequenceCutsceneActionData)rig.Session.Asset.RootAction!;
        var parallel = (ParallelCutsceneActionData)root.Actions[1];
        var inner = ((AtCutsceneActionData)parallel.Actions[0]).Actions[0];
        rig.Panel.SelectAction(inner);

        // The Parallel starts at 1 s: a time before it gives 0.
        Assert.Null(rig.Panel.AddActionAt(CutsceneActionTypes.FadeScreen, 0.2));

        Assert.Equal(2, parallel.Actions.Count);
        var at = Assert.IsType<AtCutsceneActionData>(parallel.Actions[1]);
        Assert.Equal(0f, at.Seconds);
        Assert.IsType<FadeScreenCutsceneActionData>(Assert.Single(at.Actions));
    }

    [Fact]
    public void AddActionAt_WithoutATargetParallel_IsRefused_AndChangesNothing()
    {
        var rig = OpenRoot(Container("Sequence", WaitJson(1), Container("Parallel", WaitJson(1))), "AddAtNoTarget");
        var root = (SequenceCutsceneActionData)rig.Session.Asset.RootAction!;

        // No selection, then an action of the root Sequence.
        Assert.NotNull(rig.Panel.GetAddActionAtRefusal(1));
        Assert.NotNull(rig.Panel.AddActionAt(CutsceneActionTypes.Wait, 1));
        rig.Panel.SelectAction(root.Actions[0]);
        Assert.NotNull(rig.Panel.GetAddActionAtRefusal(1));
        Assert.NotNull(rig.Panel.AddActionAt(CutsceneActionTypes.Wait, 1));

        Assert.Equal(2, root.Actions.Count);
        Assert.Single(((ParallelCutsceneActionData)root.Actions[1]).Actions);
        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(0, rig.DocumentChanges);
        Assert.Same(root.Actions[0], rig.Panel.SelectedAction);
    }

    [Fact]
    public void AddActionAt_RefusesAnUnknownTypeAndATimeThatIsNotANumber()
    {
        var rig = OpenRoot(Container("Parallel", WaitJson(1)), "AddAtBad");
        rig.Panel.SelectAction(rig.Session.Asset.RootAction);

        Assert.NotNull(rig.Panel.AddActionAt("NoSuchType", 1));
        Assert.NotNull(rig.Panel.AddActionAt(CutsceneActionTypes.Wait, double.NaN));
        Assert.NotNull(rig.Panel.GetAddActionAtRefusal(double.PositiveInfinity));
        Assert.NotNull(rig.Panel.GetAddActionAtRefusal("NoSuchType", 1));

        Assert.Single(((ParallelCutsceneActionData)rig.Session.Asset.RootAction!).Actions);
        Assert.False(rig.Panel.IsDirty);
    }
}
