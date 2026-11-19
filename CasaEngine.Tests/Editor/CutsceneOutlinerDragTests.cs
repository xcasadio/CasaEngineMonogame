using CasaEngine.Editor;
using CasaEngine.Editor.Controls;
using CasaEngine.Editor.History;
using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Tests.ContentBrowser;
using MGUI.Core.UI;
using MGUI.Shared.Input.Mouse;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// The drag and drop of an action in the Hierarchy tree (ADR-0140, P5 to P7), with the mouse of the headless harness: the
/// tree is never drawn, so the marker is covered by the drop calculation tests and by the state the tree exposes.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class CutsceneOutlinerDragTests : IDisposable
{
    private const string FirstContextId = CasaEngine.Editor.Workspaces.EditorPanelIds.CutsceneAssetDocumentPrefix + "drag-1";
    private const string SecondContextId = CasaEngine.Editor.Workspaces.EditorPanelIds.CutsceneAssetDocumentPrefix + "drag-2";

    private static readonly EditorHistoryContext FirstContext = new(EditorHistoryContextKind.Cutscene, FirstContextId);
    private static readonly EditorHistoryContext SecondContext = new(EditorHistoryContextKind.Cutscene, SecondContextId);

    private readonly string _projectDirectory;
    private readonly string _previousProjectPath;
    private readonly List<CutsceneAssetInspectorPanel> _panels = [];

    public CutsceneOutlinerDragTests()
    {
        _projectDirectory = Path.Combine(Path.GetTempPath(), "casa-cutscene-drag-" + Guid.NewGuid().ToString("N"));
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

    // Root: a Wait, a Sequence that holds a Wait, a FadeScreen.
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

    // Root: a Wait, then a Sequence of 40 Waits: taller than a small window.
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

    private sealed class Scene
    {
        public required ContentBrowserViewTestHarness Harness { get; init; }
        public required CutsceneOutlinerPanel Outliner { get; init; }
        public required CutsceneAssetInspectorPanel Document { get; init; }
        public required SequenceCutsceneActionData Root { get; init; }
        public int Time { get; set; } = 5000;

        public MGTreeView Tree => Outliner.TreeView!;

        public void Frame(Point position, MouseButton? button = null, Keys[]? keys = null)
            => Harness.AdvanceFrame(Time += 16, position, button, keys);

        public void Settle()
        {
            for (int frame = 0; frame < 40; frame++)
            {
                Harness.AdvanceFrame(Time += 50);
            }
        }

        /// <summary>Frames that go by with the left button held at a point, as in the middle of a drag.</summary>
        public void SettleHolding(Point position)
        {
            for (int frame = 0; frame < 40; frame++)
            {
                Harness.AdvanceFrame(Time += 50, position, MouseButton.Left);
            }
        }

        /// <summary>A point inside the row of an action: in its header, <paramref name="fraction"/> of the way down.</summary>
        public Point PointOn(CutsceneActionData action, float fraction)
        {
            var bounds = ScreenBounds(action);
            return new Point(bounds.Left + 40, bounds.Top + (int)(bounds.Height * fraction));
        }

        /// <summary>The row of an action on the screen, by the origin the row itself got at its last update (so it follows the scroll).</summary>
        public Rectangle ScreenBounds(CutsceneActionData action)
        {
            var header = Outliner.GetItem(action)!.HeaderContent!;
            return header.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, header.LayoutBounds);
        }

        /// <summary>Presses on a point, then moves a little and to the target, with the button down; the drag is then in progress.</summary>
        public void StartDrag(Point from, Point to)
        {
            Frame(from);
            Frame(from, MouseButton.Left);
            Frame(new Point(from.X, from.Y + 4), MouseButton.Left);
            Frame(new Point(from.X, from.Y + 6), MouseButton.Left);
            MoveTo(to);
        }

        public void MoveTo(Point to)
        {
            Frame(to, MouseButton.Left);
            Frame(to, MouseButton.Left);
        }

        public void Release(Point at)
        {
            Frame(at);
            Frame(at);
        }
    }

    private Scene Open(JObject document, int width = 600, int height = 600)
    {
        var harness = ContentBrowserViewTestHarness.Create(width, height);
        var outliner = new CutsceneOutlinerPanel(harness.Window);
        harness.Window.SetContent(outliner.CreateContent());

        var panel = OpenDocument(harness, FirstContextId, document, "Drag.cutscene", out var session);
        outliner.SetEditorPanel(panel);

        var scene = new Scene
        {
            Harness = harness,
            Outliner = outliner,
            Document = panel,
            Root = (SequenceCutsceneActionData)session.Asset.RootAction!,
        };
        scene.Settle();
        return scene;
    }

    private CutsceneAssetInspectorPanel OpenDocument(ContentBrowserViewTestHarness harness, string contextId, JObject document, string fileName, out CutsceneEditSession session)
    {
        string fullPath = Path.Combine(_projectDirectory, fileName);
        File.WriteAllText(fullPath, document.ToString());

        Assert.True(CutsceneAssetInspectorPanel.TryLoadAsset(fullPath, out var loaded));
        Assert.NotNull(loaded);
        session = loaded;

        var panel = new CutsceneAssetInspectorPanel(harness.Window);
        _panels.Add(panel);
        panel.SetHistoryContextId(contextId);
        panel.LoadAsset(session, fullPath);
        return panel;
    }

    private static void Undo(EditorHistoryContext context)
    {
        EditorHistoryService.Current.SetActiveContext(context);
        Assert.True(EditorHistoryService.Current.Undo());
    }

    private static void AssertOrder(IReadOnlyList<CutsceneActionData> actual, params CutsceneActionData[] expected)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.True(ReferenceEquals(expected[index], actual[index]), $"Child {index} is {actual[index].Type}, not {expected[index].Type}.");
        }
    }

    [Fact]
    public void DraggingAnUnselectedAction_OntoTheLowerHalfOfALeaf_MovesItAfterThatLeaf_SelectsIt_AndUndoPutsItBack()
    {
        var scene = Open(SmallDocument("After"));
        var wait = scene.Root.Actions[0];
        var inner = scene.Root.Actions[1];
        var fade = scene.Root.Actions[2];
        scene.Document.SelectAction(inner);
        scene.Settle();

        scene.StartDrag(scene.PointOn(wait, 0.5f), scene.PointOn(fade, 0.8f));

        // The press reaches the tree and starts the drag; neither it nor the moves change the selection.
        Assert.Same(wait, scene.Outliner.DragAction);
        Assert.Same(inner, scene.Document.SelectedAction);
        Assert.Same(scene.Outliner.GetItem(inner), scene.Tree.SelectedItem);
        Assert.True(scene.Outliner.DragTarget.Accepted);
        Assert.Equal(CutsceneDropKind.After, scene.Outliner.DragTarget.Kind);
        Assert.Same(scene.Root, scene.Outliner.DragTarget.Container);
        Assert.Equal(3, scene.Outliner.DragTarget.Index);

        scene.Release(scene.PointOn(fade, 0.8f));

        AssertOrder(scene.Root.Actions, inner, fade, wait);
        Assert.Same(wait, scene.Document.SelectedAction);
        Assert.Same(scene.Outliner.GetItem(wait), scene.Tree.SelectedItem);
        Assert.Null(scene.Outliner.DragAction);
        Assert.Equal(CutsceneDropKind.None, scene.Outliner.DragTarget.Kind);

        Undo(FirstContext);
        AssertOrder(scene.Root.Actions, wait, inner, fade);
    }

    [Fact]
    public void DraggingAnAction_IntoAContainer_AddsItAtTheEndOfThatContainer()
    {
        var scene = Open(SmallDocument("Into"));
        var wait = scene.Root.Actions[0];
        var container = (SequenceCutsceneActionData)scene.Root.Actions[1];
        var fade = scene.Root.Actions[2];
        var innerWait = container.Actions[0];

        scene.StartDrag(scene.PointOn(wait, 0.5f), scene.PointOn(container, 0.5f));
        Assert.Equal(CutsceneDropKind.Into, scene.Outliner.DragTarget.Kind);
        scene.Release(scene.PointOn(container, 0.5f));

        AssertOrder(scene.Root.Actions, container, fade);
        AssertOrder(container.Actions, innerWait, wait);
        Assert.Same(wait, scene.Document.SelectedAction);
    }

    [Fact]
    public void TheStateOfTheDrag_FollowsTheZoneUnderThePointer()
    {
        var scene = Open(SmallDocument("Zones"));
        var wait = scene.Root.Actions[0];
        var container = (SequenceCutsceneActionData)scene.Root.Actions[1];
        var fade = scene.Root.Actions[2];

        scene.StartDrag(scene.PointOn(wait, 0.5f), scene.PointOn(fade, 0.2f));
        var target = scene.Outliner.DragTarget;
        Assert.Equal((CutsceneDropKind.Before, 2, true), (target.Kind, target.Index, target.Accepted));
        Assert.Same(scene.Root, target.Container);

        scene.MoveTo(scene.PointOn(fade, 0.8f));
        target = scene.Outliner.DragTarget;
        Assert.Equal((CutsceneDropKind.After, 3, true), (target.Kind, target.Index, target.Accepted));

        // The container is expanded and has a child: the lower quarter aims at its first position.
        scene.MoveTo(scene.PointOn(container, 0.9f));
        target = scene.Outliner.DragTarget;
        Assert.Equal((CutsceneDropKind.After, 0, true), (target.Kind, target.Index, target.Accepted));
        Assert.Same(container, target.Container);

        scene.MoveTo(scene.PointOn(container, 0.5f));
        target = scene.Outliner.DragTarget;
        Assert.Equal((CutsceneDropKind.Into, 1, true), (target.Kind, target.Index, target.Accepted));
        Assert.Same(container, target.Container);

        // Just before the container, which is where the dragged action already is next to it: a move that changes nothing.
        scene.MoveTo(scene.PointOn(container, 0.1f));
        target = scene.Outliner.DragTarget;
        Assert.Equal((CutsceneDropKind.Before, 1, false), (target.Kind, target.Index, target.Accepted));
        Assert.Same(scene.Root, target.Container);

        // The sides of the root row give a refused destination; its middle is the end of the root.
        scene.MoveTo(scene.PointOn(scene.Root, 0.1f));
        target = scene.Outliner.DragTarget;
        Assert.Equal(CutsceneDropKind.Before, target.Kind);
        Assert.False(target.Accepted);

        scene.MoveTo(scene.PointOn(scene.Root, 0.5f));
        target = scene.Outliner.DragTarget;
        Assert.Equal((CutsceneDropKind.Into, 3, true), (target.Kind, target.Index, target.Accepted));

        scene.Release(new Point(100, 590));
        AssertOrder(scene.Root.Actions, wait, container, fade);
    }

    [Fact]
    public void DraggingAContainerOntoOneOfItsChildren_ChangesNothing()
    {
        var scene = Open(SmallDocument("Descendant"));
        var wait = scene.Root.Actions[0];
        var container = (SequenceCutsceneActionData)scene.Root.Actions[1];
        var fade = scene.Root.Actions[2];
        var innerWait = container.Actions[0];

        scene.StartDrag(scene.PointOn(container, 0.5f), scene.PointOn(innerWait, 0.8f));
        Assert.Same(container, scene.Outliner.DragAction);
        Assert.False(scene.Outliner.DragTarget.Accepted);
        Assert.NotNull(scene.Outliner.DragTarget.Refusal);

        scene.Release(scene.PointOn(innerWait, 0.8f));

        AssertOrder(scene.Root.Actions, wait, container, fade);
        AssertOrder(container.Actions, innerWait);
        Assert.Null(scene.Outliner.DragAction);
    }

    [Fact]
    public void ReleasingBelowTheTree_OrToItsRight_ChangesNothing()
    {
        var scene = Open(SmallDocument("Outside"));
        var wait = scene.Root.Actions[0];
        var container = scene.Root.Actions[1];
        var fade = scene.Root.Actions[2];

        // Under the tree: the status line.
        scene.StartDrag(scene.PointOn(wait, 0.5f), scene.PointOn(fade, 0.8f));
        Assert.True(scene.Outliner.DragTarget.Accepted);
        scene.MoveTo(new Point(100, 590));
        Assert.Equal(CutsceneDropKind.None, scene.Outliner.DragTarget.Kind);
        scene.Release(new Point(100, 590));
        AssertOrder(scene.Root.Actions, wait, container, fade);
        Assert.Null(scene.Outliner.DragAction);

        // To the right of the tree, on the height of a row.
        int rowY = scene.PointOn(fade, 0.8f).Y;
        scene.StartDrag(scene.PointOn(wait, 0.5f), scene.PointOn(fade, 0.8f));
        Assert.True(scene.Outliner.DragTarget.Accepted);
        scene.MoveTo(new Point(700, rowY));
        Assert.Equal(CutsceneDropKind.None, scene.Outliner.DragTarget.Kind);
        scene.Release(new Point(700, rowY));
        AssertOrder(scene.Root.Actions, wait, container, fade);
    }

    [Fact]
    public void ReleasingUnderTheLastRowButInTheTree_MovesTheActionToTheEndOfTheRoot()
    {
        var scene = Open(SmallDocument("End"));
        var wait = scene.Root.Actions[0];
        var container = scene.Root.Actions[1];
        var fade = scene.Root.Actions[2];

        var empty = new Point(300, 450);
        scene.StartDrag(scene.PointOn(wait, 0.5f), empty);
        Assert.Equal((CutsceneDropKind.After, 3), (scene.Outliner.DragTarget.Kind, scene.Outliner.DragTarget.Index));
        scene.Release(empty);

        AssertOrder(scene.Root.Actions, container, fade, wait);
    }

    [Fact]
    public void AnUndoInTheMiddleOfADrag_CancelsIt_AndTheReleaseMovesNothing()
    {
        var scene = Open(SmallDocument("Undo"));
        var wait = scene.Root.Actions[0];
        var container = scene.Root.Actions[1];
        var fade = scene.Root.Actions[2];

        scene.Document.SelectAction(null);
        Assert.Null(scene.Document.AddAction(CutsceneActionTypes.Wait));
        Assert.Equal(4, scene.Root.Actions.Count);
        scene.Settle();

        scene.StartDrag(scene.PointOn(wait, 0.5f), scene.PointOn(fade, 0.8f));
        Assert.True(scene.Outliner.DragTarget.Accepted);

        Undo(FirstContext);
        Assert.Equal(3, scene.Root.Actions.Count);
        Assert.Null(scene.Outliner.DragAction);
        Assert.Equal(CutsceneDropKind.None, scene.Outliner.DragTarget.Kind);

        // The release at the same place, with the pointer still: nothing moves, no marker.
        scene.Release(scene.PointOn(fade, 0.8f));
        AssertOrder(scene.Root.Actions, wait, container, fade);
        Assert.Null(scene.Outliner.DragAction);
        Assert.Equal(CutsceneDropKind.None, scene.Outliner.DragTarget.Kind);
    }

    [Fact]
    public void ChangingTheDocumentInTheMiddleOfADrag_CancelsIt()
    {
        var scene = Open(SmallDocument("First"));
        var wait = scene.Root.Actions[0];
        var container = scene.Root.Actions[1];
        var fade = scene.Root.Actions[2];

        var second = OpenDocument(scene.Harness, SecondContextId, SmallDocument("Second"), "Second.cutscene", out var secondSession);
        var secondRoot = (SequenceCutsceneActionData)secondSession.Asset.RootAction!;
        var secondOrder = secondRoot.Actions.ToArray();

        scene.StartDrag(scene.PointOn(wait, 0.5f), scene.PointOn(fade, 0.8f));
        Assert.True(scene.Outliner.DragTarget.Accepted);

        scene.Outliner.SetEditorPanel(second);
        Assert.Null(scene.Outliner.DragAction);
        Assert.Equal(CutsceneDropKind.None, scene.Outliner.DragTarget.Kind);

        scene.Release(new Point(300, 450));
        AssertOrder(scene.Root.Actions, wait, container, fade);
        AssertOrder(secondRoot.Actions, secondOrder);
        Assert.Null(scene.Outliner.DragAction);
    }

    [Fact]
    public void Escape_CancelsTheDrag_AndTheReleaseMovesNothing()
    {
        var scene = Open(SmallDocument("Escape"));
        var wait = scene.Root.Actions[0];
        var container = scene.Root.Actions[1];
        var fade = scene.Root.Actions[2];
        var target = scene.PointOn(fade, 0.8f);

        scene.StartDrag(scene.PointOn(wait, 0.5f), target);
        Assert.True(scene.Outliner.DragTarget.Accepted);

        scene.Frame(target, MouseButton.Left, [Keys.Escape]);
        Assert.Null(scene.Outliner.DragAction);
        Assert.Equal(CutsceneDropKind.None, scene.Outliner.DragTarget.Kind);

        scene.Frame(target, MouseButton.Left);
        scene.Release(target);
        AssertOrder(scene.Root.Actions, wait, container, fade);
        Assert.Null(scene.Outliner.DragAction);
    }

    [Fact]
    public void ASimpleClick_StillSelectsTheRow_AndTheRootAndTheExpanderAreNotHandles()
    {
        var scene = Open(SmallDocument("Click"));
        var wait = scene.Root.Actions[0];
        var container = scene.Root.Actions[1];
        var fade = scene.Root.Actions[2];

        scene.Harness.Click(scene.Time += 100, scene.PointOn(fade, 0.5f));
        Assert.Same(fade, scene.Document.SelectedAction);
        Assert.Null(scene.Outliner.DragAction);

        // The root does not move.
        scene.StartDrag(scene.PointOn(scene.Root, 0.5f), scene.PointOn(fade, 0.8f));
        Assert.Null(scene.Outliner.DragAction);
        scene.Release(scene.PointOn(fade, 0.8f));

        // The expander of a container is not a handle either.
        var header = scene.ScreenBounds(container);
        var expander = new Point(header.Left - 6, header.Top + (header.Height / 2));
        scene.StartDrag(expander, scene.PointOn(fade, 0.8f));
        Assert.Null(scene.Outliner.DragAction);
        scene.Release(scene.PointOn(fade, 0.8f));

        AssertOrder(scene.Root.Actions, wait, container, fade);
    }

    [Fact]
    public void TheRightButton_DoesNotDrag()
    {
        var scene = Open(SmallDocument("Right"));
        var wait = scene.Root.Actions[0];
        var container = scene.Root.Actions[1];
        var fade = scene.Root.Actions[2];

        var from = scene.PointOn(wait, 0.5f);
        scene.Frame(from);
        scene.Frame(from, MouseButton.Right);
        scene.Frame(new Point(from.X, from.Y + 6), MouseButton.Right);
        scene.Frame(scene.PointOn(fade, 0.8f), MouseButton.Right);
        Assert.Null(scene.Outliner.DragAction);
        scene.Frame(scene.PointOn(fade, 0.8f));
        scene.Harness.Desktop.TryCloseActiveContextMenu();

        AssertOrder(scene.Root.Actions, wait, container, fade);
    }

    [Fact]
    public void WhenTheTreeScrollsDuringTheDrag_TheDestinationFollowsTheRowsWhereTheyAreNow()
    {
        var scene = Open(LongDocument(), 400, 300);
        var scrollViewer = scene.Tree.ScrollViewer;
        Assert.True(scrollViewer.MaxVerticalOffset > 60, "The test needs a tree taller than its window.");
        var first = scene.Root.Actions[0];
        var inner = (SequenceCutsceneActionData)scene.Root.Actions[1];

        // The drag starts, the rows are listed; then the tree scrolls without being rebuilt.
        scene.StartDrag(scene.PointOn(first, 0.5f), scene.PointOn(inner.Actions[2], 0.8f));
        Assert.Same(inner, scene.Outliner.DragTarget.Container);
        Assert.Equal(3, scene.Outliner.DragTarget.Index);

        int before = scene.ScreenBounds(inner.Actions[10]).Top;
        scrollViewer.ScrollTo(null, 60f, TimeSpan.Zero, null);
        scene.SettleHolding(scene.PointOn(inner.Actions[2], 0.8f));
        int after = scene.ScreenBounds(inner.Actions[10]).Top;
        Assert.True(after < before - 30, "The tree has to scroll for the test to mean something.");

        var target = scene.PointOn(inner.Actions[10], 0.8f);
        scene.Frame(target, MouseButton.Left);
        scene.MoveTo(target);
        Assert.Same(inner, scene.Outliner.DragTarget.Container);
        Assert.Equal(11, scene.Outliner.DragTarget.Index);
        Assert.Equal(CutsceneDropKind.After, scene.Outliner.DragTarget.Kind);

        scene.Release(target);

        Assert.Same(first, inner.Actions[11]);
        Assert.Single(scene.Root.Actions);
        Assert.Equal(41, inner.Actions.Count);
    }

    [Fact]
    public void FindingTheDestination_AllocatesNothing()
    {
        var scene = Open(SmallDocument("Allocation"));
        var wait = scene.Root.Actions[0];
        var fade = scene.Root.Actions[2];

        scene.StartDrag(scene.PointOn(wait, 0.5f), scene.PointOn(fade, 0.8f));
        Assert.True(scene.Outliner.DragTarget.Accepted);

        for (int warmUp = 0; warmUp < 10; warmUp++)
        {
            scene.Outliner.RefreshDragTarget();
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int pass = 0; pass < 100; pass++)
        {
            scene.Outliner.RefreshDragTarget();
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);

        scene.Release(new Point(100, 590));
    }

    [Fact]
    public void Delete_OnTheFocusedTree_RemovesTheSelectedAction_AndUndoPutsItBack()
    {
        var scene = Open(SmallDocument("Delete"));
        var wait = scene.Root.Actions[0];
        var container = scene.Root.Actions[1];
        var fade = scene.Root.Actions[2];

        // A click selects the row and gives the tree the keyboard focus.
        scene.Harness.Click(scene.Time += 100, scene.PointOn(fade, 0.5f));
        Assert.Same(fade, scene.Document.SelectedAction);
        Assert.Same(scene.Tree, scene.Harness.Desktop.FocusedKeyboardHandler);

        scene.Frame(scene.PointOn(fade, 0.5f), keys: [Keys.Delete]);
        scene.Settle();

        AssertOrder(scene.Root.Actions, wait, container);
        Assert.Null(scene.Document.SelectedAction);

        Undo(FirstContext);
        AssertOrder(scene.Root.Actions, wait, container, fade);
    }

    [Fact]
    public void Delete_WhenTheTreeDoesNotHaveTheFocus_RemovesNothing()
    {
        var scene = Open(SmallDocument("DeleteUnfocused"));
        var wait = scene.Root.Actions[0];
        var container = scene.Root.Actions[1];
        var fade = scene.Root.Actions[2];

        scene.Harness.Click(scene.Time += 100, scene.PointOn(fade, 0.5f));
        Assert.Same(fade, scene.Document.SelectedAction);

        // The focus is somewhere else, as when the user works in another panel.
        scene.Harness.Desktop.ClearFocusedKeyboardHandler();
        scene.Frame(new Point(300, 590), keys: [Keys.Delete]);
        scene.Settle();

        AssertOrder(scene.Root.Actions, wait, container, fade);
        Assert.Same(fade, scene.Document.SelectedAction);
    }

    [Fact]
    public void Delete_OnTheRoot_ShowsTheRefusal_AndChangesNothing()
    {
        var scene = Open(SmallDocument("DeleteRoot"));
        var wait = scene.Root.Actions[0];
        var container = scene.Root.Actions[1];
        var fade = scene.Root.Actions[2];

        scene.Harness.Click(scene.Time += 100, scene.PointOn(scene.Root, 0.5f));
        Assert.Same(scene.Root, scene.Document.SelectedAction);

        scene.Frame(scene.PointOn(scene.Root, 0.5f), keys: [Keys.Delete]);

        AssertOrder(scene.Root.Actions, wait, container, fade);
        Assert.Equal(RemoveCutsceneActionCommand.GetRefusal(scene.Document.Session!, scene.Root), scene.Outliner.StatusText);
    }

    [Fact]
    public void TheAutomationDragHover_PutsTheTreeInTheStateOfADrag_AndKeepsIt()
    {
        var scene = Open(SmallDocument("Hover"));
        var wait = scene.Root.Actions[0];
        var container = (SequenceCutsceneActionData)scene.Root.Actions[1];
        var fade = scene.Root.Actions[2];

        var outcome = scene.Outliner.TrySetDragHoverForAutomation(wait, fade, CutsceneDropKind.After, out string message);

        Assert.Equal(CutsceneOutlinerPanel.AutomationOutcome.Done, outcome);
        Assert.Contains("accepted", message);
        Assert.Same(wait, scene.Outliner.DragAction);
        var target = scene.Outliner.DragTarget;
        Assert.Equal((CutsceneDropKind.After, 3, true), (target.Kind, target.Index, target.Accepted));
        Assert.Same(scene.Root, target.Container);

        // Nothing moved, and the state stays while frames go by.
        scene.Settle();
        Assert.Same(wait, scene.Outliner.DragAction);
        Assert.Equal(CutsceneDropKind.After, scene.Outliner.DragTarget.Kind);
        AssertOrder(scene.Root.Actions, wait, container, fade);

        outcome = scene.Outliner.TrySetDragHoverForAutomation(wait, container, CutsceneDropKind.Into, out message);
        Assert.Equal(CutsceneOutlinerPanel.AutomationOutcome.Done, outcome);
        Assert.Equal((CutsceneDropKind.Into, true), (scene.Outliner.DragTarget.Kind, scene.Outliner.DragTarget.Accepted));
        Assert.Same(container, scene.Outliner.DragTarget.Container);

        outcome = scene.Outliner.TrySetDragHoverForAutomation(wait, fade, CutsceneDropKind.Before, out message);
        Assert.Equal(CutsceneOutlinerPanel.AutomationOutcome.Done, outcome);
        Assert.Equal((CutsceneDropKind.Before, 2), (scene.Outliner.DragTarget.Kind, scene.Outliner.DragTarget.Index));
    }

    [Fact]
    public void TheAutomationDragHover_OntoARefusedDestination_IsDoneButNotAccepted_AndOntoALeafIntoIsRefused()
    {
        var scene = Open(SmallDocument("HoverRefused"));
        var wait = scene.Root.Actions[0];
        var container = scene.Root.Actions[1];
        var fade = scene.Root.Actions[2];

        // Just before the container, next to which the action already is: a move that changes nothing.
        var outcome = scene.Outliner.TrySetDragHoverForAutomation(wait, container, CutsceneDropKind.Before, out string message);
        Assert.Equal(CutsceneOutlinerPanel.AutomationOutcome.Done, outcome);
        Assert.Contains("refused", message);
        Assert.False(scene.Outliner.DragTarget.Accepted);

        outcome = scene.Outliner.TrySetDragHoverForAutomation(wait, fade, CutsceneDropKind.Into, out message);
        Assert.Equal(CutsceneOutlinerPanel.AutomationOutcome.Refused, outcome);
        Assert.Contains("not a container", message);
    }

    [Fact]
    public void TheAutomationDragHover_IsClearedWhenTheTreeIsRebuilt()
    {
        var scene = Open(SmallDocument("HoverRebuild"));
        var wait = scene.Root.Actions[0];
        var fade = scene.Root.Actions[2];

        Assert.Equal(CutsceneOutlinerPanel.AutomationOutcome.Done,
            scene.Outliner.TrySetDragHoverForAutomation(wait, fade, CutsceneDropKind.After, out _));
        Assert.True(scene.Outliner.IsDragging);

        // The tree changes: the rows the state aimed at are gone.
        scene.Document.SelectAction(fade);
        Assert.Null(scene.Document.AddAction("Wait"));
        Assert.False(scene.Outliner.IsDragging);
    }

    [Fact]
    public void TheAutomationMenu_OpensAtTheRowOfTheSelection_AndStaysOpenWhenTheMouseIsFarAway()
    {
        var scene = Open(SmallDocument("Menu"));
        var fade = scene.Root.Actions[2];
        scene.Document.SelectAction(fade);
        scene.Settle();

        var outcome = scene.Outliner.TryOpenMenuForAutomation(out string message);

        Assert.Equal(CutsceneOutlinerPanel.AutomationOutcome.Done, outcome);
        var menu = scene.Harness.Desktop.ActiveContextMenu;
        Assert.NotNull(menu);
        Assert.Null(menu!.AutoCloseThreshold);
        Assert.Contains("opened at", message);

        for (int frame = 0; frame < 20; frame++)
        {
            scene.Frame(new Point(599, 0));
        }

        Assert.Same(menu, scene.Harness.Desktop.ActiveContextMenu);
    }

    [Fact]
    public void TheAutomationMenu_WithoutADocument_IsRefused()
    {
        var harness = ContentBrowserViewTestHarness.Create(600, 600);
        var outliner = new CutsceneOutlinerPanel(harness.Window);
        harness.Window.SetContent(outliner.CreateContent());

        var outcome = outliner.TryOpenMenuForAutomation(out string message);

        Assert.Equal(CutsceneOutlinerPanel.AutomationOutcome.Refused, outcome);
        Assert.NotEmpty(message);
    }
}
