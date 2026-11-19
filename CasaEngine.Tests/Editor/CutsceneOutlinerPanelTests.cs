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
/// The action tree of the cutscene document (ADR-0136), on panels without a preview and without a graphics device: it
/// mirrors the asset, goes through the operations of the document, keeps its selection and its expanded nodes across a
/// rebuild, and releases the document it was bound to.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class CutsceneOutlinerPanelTests : IDisposable
{
    private const string FirstContextId = CasaEngine.Editor.Workspaces.EditorPanelIds.CutsceneAssetDocumentPrefix + "outliner-1";
    private const string SecondContextId = CasaEngine.Editor.Workspaces.EditorPanelIds.CutsceneAssetDocumentPrefix + "outliner-2";

    private static readonly EditorHistoryContext FirstContext = new(EditorHistoryContextKind.Cutscene, FirstContextId);
    private static readonly EditorHistoryContext SecondContext = new(EditorHistoryContextKind.Cutscene, SecondContextId);

    private readonly string _projectDirectory;
    private readonly string _previousProjectPath;
    private readonly List<CutsceneAssetInspectorPanel> _panels = [];

    public CutsceneOutlinerPanelTests()
    {
        _projectDirectory = Path.Combine(Path.GetTempPath(), "casa-cutscene-outliner-" + Guid.NewGuid().ToString("N"));
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
        foreach (var context in new[] { FirstContext, SecondContext })
        {
            EditorHistoryService.Current.Remove(context);
            EditorDirtyStateService.Current.Remove(context);
        }

        EditorHistoryService.Current.Deactivate();

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

    private static JObject SmallDocument(string name)
    {
        return new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = name,
            ["type"] = "CutsceneAsset",
            ["version"] = 1,
            ["schema_version"] = 1,
            ["root_action"] = new JObject
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
            },
        };
    }

    private (CutsceneAssetInspectorPanel Document, CutsceneEditSession Session) OpenDocument(
        ContentBrowserViewTestHarness harness, string contextId, JObject document, string fileName)
    {
        string fullPath = Path.Combine(_projectDirectory, fileName);
        File.WriteAllText(fullPath, document.ToString());

        Assert.True(CutsceneAssetInspectorPanel.TryLoadAsset(fullPath, out var session));
        Assert.NotNull(session);

        var panel = new CutsceneAssetInspectorPanel(harness.Window);
        _panels.Add(panel);
        panel.SetHistoryContextId(contextId);
        panel.LoadAsset(session, fullPath);
        return (panel, session);
    }

    private static CutsceneOutlinerPanel CreateOutliner(ContentBrowserViewTestHarness harness)
    {
        var outliner = new CutsceneOutlinerPanel(harness.Window);
        harness.Window.SetContent(outliner.CreateContent());
        return outliner;
    }

    private static int CountItems(MGUI.Core.UI.MGTreeViewItem item)
    {
        int count = 1;
        foreach (var child in item.Items)
        {
            count += CountItems(child);
        }

        return count;
    }

    private static int CountItems(CutsceneOutlinerPanel outliner)
    {
        int count = 0;
        foreach (var item in outliner.TreeView!.Items)
        {
            count += CountItems(item);
        }

        return count;
    }

    private static int CountActions(CutsceneActionData action)
    {
        int count = 1;
        foreach (var child in CutsceneActionCatalog.GetChildren(action))
        {
            count += CountActions(child);
        }

        return count;
    }

    private static void Undo(EditorHistoryContext context)
    {
        EditorHistoryService.Current.SetActiveContext(context);
        Assert.True(EditorHistoryService.Current.Undo());
    }

    private static string FindSampleCutscene()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.MonoGame.sln")))
            {
                return Path.Combine(directory.FullName, "Projects", "SpriteCutsceneSample", "Cutscenes", "loop.cutscene");
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("CasaEngine repository root was not found.");
    }

    [Fact]
    public void TheTree_ReflectsTheAssetOfTheSampleCutscene()
    {
        var harness = ContentBrowserViewTestHarness.Create(600, 600);
        var outliner = CreateOutliner(harness);

        Assert.True(CutsceneAssetInspectorPanel.TryLoadAsset(FindSampleCutscene(), out var session));
        var document = new CutsceneAssetInspectorPanel(harness.Window);
        _panels.Add(document);
        document.SetHistoryContextId(FirstContextId);
        document.LoadAsset(session!, FindSampleCutscene());
        outliner.SetEditorPanel(document);

        var root = session!.Asset.RootAction!;
        Assert.Equal(CountActions(root), CountItems(outliner));
        Assert.Single(outliner.TreeView!.Items);
        Assert.Equal(CountActions(root) + " actions", outliner.StatusText);

        var rootItem = outliner.GetItem(root);
        Assert.NotNull(rootItem);
        Assert.True(rootItem.IsExpanded);

        // Every container of the tree is expanded on the first opening.
        foreach (var child in CutsceneActionCatalog.GetChildren(root))
        {
            var item = outliner.GetItem(child);
            Assert.NotNull(item);
            if (CutsceneActionCatalog.CanHaveChildren(child) && CutsceneActionCatalog.GetChildren(child).Count > 0)
            {
                Assert.True(item.IsExpanded);
            }
        }
    }

    [Fact]
    public void AddingFromTheTree_GoesThroughTheDocument_AndTheTreeFollows()
    {
        var harness = ContentBrowserViewTestHarness.Create(600, 600);
        var outliner = CreateOutliner(harness);
        var (document, session) = OpenDocument(harness, FirstContextId, SmallDocument("Add"), "Add.cutscene");
        outliner.SetEditorPanel(document);

        var root = (SequenceCutsceneActionData)session.Asset.RootAction!;
        Assert.Equal(5, CountItems(outliner));

        Assert.True(outliner.SelectType(CutsceneActionTypes.Wait));
        outliner.AddSelectedType();

        Assert.Equal(4, root.Actions.Count);
        Assert.IsType<WaitCutsceneActionData>(root.Actions[3]);
        Assert.Equal(6, CountItems(outliner));
        Assert.Same(root.Actions[3], document.SelectedAction);
        Assert.Same(outliner.GetItem(root.Actions[3]), outliner.TreeView!.SelectedItem);
        Assert.True(document.IsDirty);

        Undo(FirstContext);

        Assert.Equal(3, root.Actions.Count);
        Assert.Equal(5, CountItems(outliner));
        Assert.Null(outliner.TreeView.SelectedItem);
    }

    [Fact]
    public void DuplicateAndDelete_GoThroughTheDocument_AndAreInactiveOnTheRoot()
    {
        var harness = ContentBrowserViewTestHarness.Create(600, 600);
        var outliner = CreateOutliner(harness);
        var (document, session) = OpenDocument(harness, FirstContextId, SmallDocument("Dup"), "Dup.cutscene");
        outliner.SetEditorPanel(document);
        var root = (SequenceCutsceneActionData)session.Asset.RootAction!;

        Assert.False(outliner.DuplicateButton!.IsEnabled);
        Assert.False(outliner.DeleteButton!.IsEnabled);

        document.SelectAction(root);
        Assert.False(outliner.DuplicateButton.IsEnabled);
        Assert.False(outliner.DeleteButton.IsEnabled);

        // A click in the tree selects the action of the document.
        var inner = root.Actions[1];
        outliner.TreeView!.SelectItem(outliner.GetItem(inner)!);
        Assert.Same(inner, document.SelectedAction);
        Assert.True(outliner.DuplicateButton.IsEnabled);
        Assert.True(outliner.DeleteButton.IsEnabled);

        outliner.DuplicateSelected();
        Assert.Equal(4, root.Actions.Count);
        Assert.Equal(CutsceneActionTypes.Sequence, root.Actions[2].Type);
        Assert.Same(root.Actions[2], document.SelectedAction);
        Assert.Equal(7, CountItems(outliner));

        outliner.DeleteSelected();
        Assert.Equal(3, root.Actions.Count);
        Assert.Equal(5, CountItems(outliner));
        Assert.Null(document.SelectedAction);
        Assert.False(outliner.DeleteButton.IsEnabled);
    }

    [Fact]
    public void AnActionTheRuleRefuses_ShowsItsMessage_AndChangesNothing()
    {
        var harness = ContentBrowserViewTestHarness.Create(600, 600);
        var outliner = CreateOutliner(harness);
        var (document, session) = OpenDocument(harness, FirstContextId, SmallDocument("Refuse"), "Refuse.cutscene");
        outliner.SetEditorPanel(document);

        document.SelectAction(session.Asset.RootAction);
        outliner.DeleteSelected();

        Assert.Equal(3, ((SequenceCutsceneActionData)session.Asset.RootAction!).Actions.Count);
        Assert.False(document.IsDirty);
        Assert.NotEqual("5 actions", outliner.StatusText);
    }

    [Fact]
    public void TheInsertionRule_AppliesToTheThreeCases()
    {
        var harness = ContentBrowserViewTestHarness.Create(600, 600);
        var outliner = CreateOutliner(harness);
        var (document, session) = OpenDocument(harness, FirstContextId, SmallDocument("Rule"), "Rule.cutscene");
        outliner.SetEditorPanel(document);
        var root = (SequenceCutsceneActionData)session.Asset.RootAction!;
        var inner = (SequenceCutsceneActionData)root.Actions[1];

        outliner.SelectType(CutsceneActionTypes.HideSprite);

        // No selection: the end of the root.
        outliner.AddSelectedType();
        Assert.IsType<HideSpriteCutsceneActionData>(root.Actions[^1]);

        // A container is selected: the new action is its last child.
        outliner.TreeView!.SelectItem(outliner.GetItem(inner)!);
        outliner.AddSelectedType();
        Assert.IsType<HideSpriteCutsceneActionData>(inner.Actions[^1]);
        Assert.Equal(2, inner.Actions.Count);

        // A leaf is selected: just after it, in its parent.
        var leaf = root.Actions[0];
        outliner.TreeView.SelectItem(outliner.GetItem(leaf)!);
        outliner.AddSelectedType();
        Assert.IsType<HideSpriteCutsceneActionData>(root.Actions[1]);
        Assert.Same(leaf, root.Actions[0]);
    }

    [Fact]
    public void SelectionAndExpandedState_SurviveARebuild_AndAnUndo()
    {
        var harness = ContentBrowserViewTestHarness.Create(600, 600);
        var outliner = CreateOutliner(harness);
        var (document, session) = OpenDocument(harness, FirstContextId, SmallDocument("State"), "State.cutscene");
        outliner.SetEditorPanel(document);
        var root = (SequenceCutsceneActionData)session.Asset.RootAction!;
        var inner = (SequenceCutsceneActionData)root.Actions[1];
        var innerChild = inner.Actions[0];

        // The user collapses the inner sequence, then selects the fade.
        outliner.GetItem(inner)!.Collapse();
        var fade = root.Actions[2];
        document.SelectAction(fade);

        // A property edit rebuilds the tree.
        var wait = (WaitCutsceneActionData)root.Actions[0];
        var property = CutsceneActionPropertyEditor.GetProperties(wait).Single(p => p.Key == "seconds");
        Assert.Null(document.SetProperty(wait, property, 4f));

        Assert.False(outliner.GetItem(inner)!.IsExpanded);
        Assert.Same(outliner.GetItem(fade), outliner.TreeView!.SelectedItem);
        Assert.Same(fade, document.SelectedAction);

        Undo(FirstContext);

        Assert.False(outliner.GetItem(inner)!.IsExpanded);
        Assert.Same(outliner.GetItem(fade), outliner.TreeView.SelectedItem);

        // Selecting a hidden child expands its ancestors.
        document.SelectAction(innerChild);
        Assert.True(outliner.GetItem(inner)!.IsExpanded);
        Assert.Same(outliner.GetItem(innerChild), outliner.TreeView.SelectedItem);
    }

    [Fact]
    public void BindingASecondDocument_ReplacesTheTree_AndLeavesNoSubscriptionOnTheFirst()
    {
        var harness = ContentBrowserViewTestHarness.Create(600, 600);
        var outliner = CreateOutliner(harness);
        var (first, firstSession) = OpenDocument(harness, FirstContextId, SmallDocument("First"), "First.cutscene");

        var secondJson = SmallDocument("Second");
        ((JArray)secondJson["root_action"]!["actions"]!).RemoveAt(2);
        var (second, secondSession) = OpenDocument(harness, SecondContextId, secondJson, "Second.cutscene");

        outliner.SetEditorPanel(first);
        Assert.Equal(CountActions(firstSession.Asset.RootAction!), CountItems(outliner));

        outliner.SetEditorPanel(second);
        Assert.Same(second, outliner.Document);
        Assert.Equal(CountActions(secondSession.Asset.RootAction!), CountItems(outliner));
        Assert.Null(outliner.GetItem(firstSession.Asset.RootAction!));

        // A change of the first document, and its selection, no longer reach the tree.
        outliner.SelectType(CutsceneActionTypes.Wait);
        first.SelectAction(((SequenceCutsceneActionData)firstSession.Asset.RootAction!).Actions[0]);
        Assert.Null(first.AddAction(CutsceneActionTypes.Wait));
        Assert.Equal(CountActions(secondSession.Asset.RootAction!), CountItems(outliner));
        Assert.Null(outliner.TreeView!.SelectedItem);

        outliner.SetEditorPanel(null);
        Assert.Null(outliner.Document);
        Assert.Empty(outliner.TreeView.Items);
        Assert.False(outliner.AddButton!.IsEnabled);

        Assert.Null(second.AddAction(CutsceneActionTypes.Wait));
        Assert.Empty(outliner.TreeView.Items);
    }

    private static JObject LongDocument()
    {
        var document = SmallDocument("Long");
        var inner = new JArray();
        for (int index = 0; index < 40; index++)
        {
            inner.Add(new JObject { ["type"] = "Wait", ["seconds"] = index });
        }

        var actions = (JArray)document["root_action"]!["actions"]!;
        actions.Clear();
        actions.Add(new JObject { ["type"] = "Wait", ["seconds"] = 1 });
        actions.Add(new JObject { ["type"] = "Sequence", ["actions"] = inner });
        return document;
    }

    private static void Settle(ContentBrowserViewTestHarness harness, CutsceneOutlinerPanel outliner)
    {
        for (int frame = 1; frame <= 40; frame++)
        {
            harness.AdvanceFrame(frame * 50);
        }
    }

    [Fact]
    public void Selecting_KeepsTheRowVisible_AndDoesNotScrollWhenTheRowIsAlreadyVisible()
    {
        var harness = ContentBrowserViewTestHarness.Create(400, 300);
        var outliner = CreateOutliner(harness);
        var (document, session) = OpenDocument(harness, FirstContextId, LongDocument(), "Long.cutscene");
        outliner.SetEditorPanel(document);
        Settle(harness, outliner);

        var scrollViewer = outliner.TreeView!.ScrollViewer;
        Assert.True(scrollViewer.MaxVerticalOffset > 0, "The test needs a tree taller than its window.");
        Assert.Equal(0f, scrollViewer.VerticalOffset);

        var root = (SequenceCutsceneActionData)session.Asset.RootAction!;
        var inner = (SequenceCutsceneActionData)root.Actions[1];

        // A row at the top that is already visible: the tree does not move.
        document.SelectAction(root.Actions[0]);
        Settle(harness, outliner);
        Assert.Equal(0f, scrollViewer.VerticalOffset);

        document.SelectAction(inner.Actions[1]);
        Settle(harness, outliner);
        Assert.Equal(0f, scrollViewer.VerticalOffset);

        // A row below the window: the tree scrolls just enough to show it, not to the bottom. (The harness does not run the
        // draw pass, so the bounds of the rows are not refreshed and only the offset can be observed.)
        document.SelectAction(inner.Actions[30]);
        Settle(harness, outliner);
        Assert.True(scrollViewer.VerticalOffset > 0);
        Assert.True(scrollViewer.VerticalOffset < scrollViewer.MaxVerticalOffset);
    }

    [Fact]
    public void WithoutSelection_TheTreeStaysAtTheTop()
    {
        var harness = ContentBrowserViewTestHarness.Create(400, 300);
        var outliner = CreateOutliner(harness);
        var (document, _) = OpenDocument(harness, FirstContextId, LongDocument(), "Long.cutscene");
        outliner.SetEditorPanel(document);
        Settle(harness, outliner);

        Assert.Null(outliner.TreeView!.SelectedItem);
        Assert.Equal(0f, outliner.TreeView.ScrollViewer.VerticalOffset);
    }

    // Frames that go by until the scroll animation of the tree is over. The time never goes back, whatever the call before.
    private static void SettleScroll(ContentBrowserViewTestHarness harness, MGUI.Core.UI.MGScrollViewer scrollViewer, ref int time)
    {
        for (int frame = 0; frame < 60; frame++)
        {
            harness.AdvanceFrame(time += 50);
        }

        Assert.Equal(scrollViewer.PendingVerticalOffset, scrollViewer.VerticalOffset);
    }

    // The row of an action against the visible area of the tree, in the space the drag code works in: the layout bounds of a
    // header do not move when the tree scrolls, the offset of the scroll viewer is taken off to get where the row is shown.
    private static void AssertRowFullyInView(CutsceneOutlinerPanel outliner, CutsceneActionData action)
    {
        var scrollViewer = outliner.TreeView!.ScrollViewer;
        var layout = outliner.GetItem(action)!.HeaderContent!.LayoutBounds;
        var viewport = scrollViewer.ContentViewport;
        float top = layout.Top - scrollViewer.VerticalOffset;
        float bottom = layout.Bottom - scrollViewer.VerticalOffset;

        Assert.True(top >= viewport.Top && bottom <= viewport.Bottom,
            $"The row spans {top}..{bottom} but the visible area spans {viewport.Top}..{viewport.Bottom} (offset {scrollViewer.VerticalOffset}).");
    }

    [Fact]
    public void Selecting_ARowNearTheTop_AfterARowFarDown_BringsItBackIntoView()
    {
        var harness = ContentBrowserViewTestHarness.Create(400, 300);
        var outliner = CreateOutliner(harness);
        var (document, session) = OpenDocument(harness, FirstContextId, LongDocument(), "Long.cutscene");
        outliner.SetEditorPanel(document);
        var scrollViewer = outliner.TreeView!.ScrollViewer;
        int time = 5000;
        SettleScroll(harness, scrollViewer, ref time);
        Assert.True(scrollViewer.MaxVerticalOffset > 0, "The test needs a tree taller than its window.");

        var inner = (SequenceCutsceneActionData)((SequenceCutsceneActionData)session.Asset.RootAction!).Actions[1];

        document.SelectAction(inner.Actions[35]);
        SettleScroll(harness, scrollViewer, ref time);
        Assert.True(scrollViewer.VerticalOffset > 100f, "The tree has to be scrolled far down for the test to mean something.");
        AssertRowFullyInView(outliner, inner.Actions[35]);

        document.SelectAction(inner.Actions[0]);
        SettleScroll(harness, scrollViewer, ref time);
        AssertRowFullyInView(outliner, inner.Actions[0]);
    }

    [Fact]
    public void Selecting_ARowFarDown_AfterARowNearTheTop_BringsItIntoView_AndAnotherOneAfterAScrollThatEnded()
    {
        var harness = ContentBrowserViewTestHarness.Create(400, 300);
        var outliner = CreateOutliner(harness);
        var (document, session) = OpenDocument(harness, FirstContextId, LongDocument(), "Long.cutscene");
        outliner.SetEditorPanel(document);
        var scrollViewer = outliner.TreeView!.ScrollViewer;
        int time = 5000;
        SettleScroll(harness, scrollViewer, ref time);

        var inner = (SequenceCutsceneActionData)((SequenceCutsceneActionData)session.Asset.RootAction!).Actions[1];

        document.SelectAction(inner.Actions[2]);
        SettleScroll(harness, scrollViewer, ref time);
        AssertRowFullyInView(outliner, inner.Actions[2]);

        document.SelectAction(inner.Actions[35]);
        SettleScroll(harness, scrollViewer, ref time);
        AssertRowFullyInView(outliner, inner.Actions[35]);

        // From a middle offset, towards a row above the visible area.
        document.SelectAction(inner.Actions[20]);
        SettleScroll(harness, scrollViewer, ref time);
        AssertRowFullyInView(outliner, inner.Actions[20]);
    }

    [Fact]
    public void Selecting_ARowThatIsAlreadyInView_FromAScrolledTree_DoesNotScroll()
    {
        var harness = ContentBrowserViewTestHarness.Create(400, 300);
        var outliner = CreateOutliner(harness);
        var (document, session) = OpenDocument(harness, FirstContextId, LongDocument(), "Long.cutscene");
        outliner.SetEditorPanel(document);
        var scrollViewer = outliner.TreeView!.ScrollViewer;
        int time = 5000;
        SettleScroll(harness, scrollViewer, ref time);

        var inner = (SequenceCutsceneActionData)((SequenceCutsceneActionData)session.Asset.RootAction!).Actions[1];

        document.SelectAction(inner.Actions[30]);
        SettleScroll(harness, scrollViewer, ref time);
        float offset = scrollViewer.VerticalOffset;
        Assert.True(offset > 0f);

        document.SelectAction(inner.Actions[29]);
        SettleScroll(harness, scrollViewer, ref time);

        Assert.Equal(offset, scrollViewer.VerticalOffset);
        AssertRowFullyInView(outliner, inner.Actions[29]);
    }

    [Fact]
    public void AContainerThatAppears_IsShownExpanded_ByAWrap_APasteAndAnUndo()
    {
        var harness = ContentBrowserViewTestHarness.Create(600, 600);
        var outliner = CreateOutliner(harness);
        var (document, session) = OpenDocument(harness, FirstContextId, SmallDocument("Appear"), "Appear.cutscene");
        outliner.SetEditorPanel(document);
        var root = (SequenceCutsceneActionData)session.Asset.RootAction!;
        var leaf = root.Actions[0];

        // A wrap: the new container is expanded, so its child is shown.
        document.SelectAction(leaf);
        Assert.Null(document.WrapSelectedAction(CutsceneActionTypes.Parallel));
        var wrapper = (ParallelCutsceneActionData)root.Actions[0];
        Assert.Same(wrapper, document.SelectedAction);
        Assert.True(outliner.GetItem(wrapper)!.IsExpanded);
        Assert.Same(outliner.GetItem(wrapper), outliner.TreeView!.SelectedItem);
        Assert.Same(outliner.GetItem(wrapper), outliner.GetItem(leaf)!.ParentItem);

        // A paste of a container copied while it was collapsed.
        var inner = (SequenceCutsceneActionData)root.Actions[1];
        outliner.GetItem(inner)!.Collapse();
        document.SelectAction(inner);
        Assert.Null(document.CopySelectedAction());
        document.SelectAction(root.Actions[2]);
        Assert.Null(document.PasteAction());
        var pasted = root.Actions[3];
        Assert.NotSame(inner, pasted);
        Assert.True(outliner.GetItem(pasted)!.IsExpanded);
        Assert.False(outliner.GetItem(inner)!.IsExpanded);

        // A container that comes back by an undo is expanded, even though the user had collapsed it before the removal.
        document.SelectAction(inner);
        Assert.Null(document.RemoveSelectedAction());
        Assert.Null(outliner.GetItem(inner));
        Undo(FirstContext);
        Assert.True(outliner.GetItem(inner)!.IsExpanded);
    }

    [Fact]
    public void SelectingAContainerThatIsAlreadyThereAndCollapsed_LeavesItCollapsed()
    {
        var harness = ContentBrowserViewTestHarness.Create(600, 600);
        var outliner = CreateOutliner(harness);
        var (document, session) = OpenDocument(harness, FirstContextId, SmallDocument("Collapsed"), "Collapsed.cutscene");
        outliner.SetEditorPanel(document);
        var root = (SequenceCutsceneActionData)session.Asset.RootAction!;
        var inner = (SequenceCutsceneActionData)root.Actions[1];

        outliner.GetItem(inner)!.Collapse();
        document.SelectAction(inner);
        Assert.False(outliner.GetItem(inner)!.IsExpanded);

        // A rebuild of the tree does not expand it either.
        document.SelectAction(root.Actions[0]);
        Assert.Null(document.AddAction(CutsceneActionTypes.Wait));
        Assert.False(outliner.GetItem(inner)!.IsExpanded);
    }

    [Fact]
    public void AnEmptyContainerThatAppears_IsExpandedOnceItReceivesAChild()
    {
        var harness = ContentBrowserViewTestHarness.Create(600, 600);
        var outliner = CreateOutliner(harness);
        var (document, session) = OpenDocument(harness, FirstContextId, SmallDocument("Empty"), "Empty.cutscene");
        outliner.SetEditorPanel(document);
        var root = (SequenceCutsceneActionData)session.Asset.RootAction!;

        document.SelectAction(null);
        Assert.Null(document.AddAction(CutsceneActionTypes.Parallel));
        var parallel = (ParallelCutsceneActionData)root.Actions[^1];
        Assert.True(outliner.GetItem(parallel)!.IsExpanded);

        Assert.Null(document.AddAction(CutsceneActionTypes.Wait));
        Assert.Single(parallel.Actions);
        Assert.True(outliner.GetItem(parallel)!.IsExpanded);
        Assert.NotNull(outliner.GetItem(parallel.Actions[0]));
    }

    private static MGUI.Core.UI.MGContextMenu? RightClick(ContentBrowserViewTestHarness harness, Microsoft.Xna.Framework.Point position, ref int time)
    {
        harness.AdvanceFrame(time += 16, position);
        harness.AdvanceFrame(time += 16, position, MGUI.Shared.Input.Mouse.MouseButton.Right);
        harness.AdvanceFrame(time += 16, position);
        return harness.Desktop.ActiveContextMenu;
    }

    [Fact]
    public void ARightClickOnARow_OpensTheMenuOfThatAction_AndOneInTheEmptyPartTheMenuOfNoAction()
    {
        var harness = ContentBrowserViewTestHarness.Create(600, 600);
        var outliner = CreateOutliner(harness);
        var (document, session) = OpenDocument(harness, FirstContextId, SmallDocument("Menu"), "Menu.cutscene");
        outliner.SetEditorPanel(document);
        Settle(harness, outliner);
        var root = (SequenceCutsceneActionData)session.Asset.RootAction!;
        var fade = root.Actions[2];
        int time = 5000;

        var onRow = outliner.GetItem(fade)!.HeaderContent!.LayoutBounds.Center;
        var rowMenu = RightClick(harness, onRow, ref time);

        Assert.NotNull(rowMenu);
        Assert.Same(fade, document.SelectedAction);
        var labels = CutsceneMenuProbe.Labels(rowMenu!);
        Assert.Contains("Move up", labels);
        Assert.Contains("Delete", labels);
        Assert.DoesNotContain("Move down", labels);

        harness.Desktop.TryCloseActiveContextMenu();
        var empty = new Microsoft.Xna.Framework.Point(300, 450);
        var emptyMenu = RightClick(harness, empty, ref time);

        Assert.NotNull(emptyMenu);
        Assert.Null(document.SelectedAction);
        Assert.Equal(["Add"], CutsceneMenuProbe.Labels(emptyMenu!));
    }
}
