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
/// The action tree of the cutscene document (ADR-0097), on panels without a preview and without a graphics device: it
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
}
