using CasaEngine.Editor;
using CasaEngine.Editor.Controls;
using CasaEngine.Editor.Controls.Cutscenes;
using CasaEngine.Editor.Controls.Timeline;
using CasaEngine.Editor.Controls.Timeline.Editing;
using CasaEngine.Editor.Controls.Timeline.Menu;
using CasaEngine.Editor.History;
using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Tests.ContentBrowser;
using Microsoft.Xna.Framework;
using MGUI.Core.UI;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// The read-only timeline panel of the cutscene document (ADR-0137), on panels without a preview and without a graphics
/// device: its model mirrors the projection, every item can be picked, and the selection is shared with the document.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class CutsceneTimelinePanelTests : IDisposable
{
    private const string ContextId = CasaEngine.Editor.Workspaces.EditorPanelIds.CutsceneAssetDocumentPrefix + "timeline-1";
    private static readonly EditorHistoryContext Context = new(EditorHistoryContextKind.Cutscene, ContextId);

    private readonly string _projectDirectory;
    private readonly string _previousProjectPath;
    private readonly List<CutsceneAssetInspectorPanel> _panels = [];

    public CutsceneTimelinePanelTests()
    {
        _projectDirectory = Path.Combine(Path.GetTempPath(), "casa-cutscene-timeline-" + Guid.NewGuid().ToString("N"));
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
        EditorDirtyStateService.Current.Remove(Context);
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

    private (ContentBrowserViewTestHarness Harness, CutsceneTimelinePanel Panel, CutsceneAssetInspectorPanel Document, CutsceneEditSession Session) OpenSample()
    {
        var harness = ContentBrowserViewTestHarness.Create(900, 400);
        var panel = new CutsceneTimelinePanel(harness.Window);
        harness.Window.SetContent(panel.CreateContent());

        string path = FindSampleCutscene();
        Assert.True(CutsceneAssetInspectorPanel.TryLoadAsset(path, out var session));
        var document = new CutsceneAssetInspectorPanel(harness.Window);
        _panels.Add(document);
        document.SetHistoryContextId(ContextId);
        document.LoadAsset(session!, path);
        panel.SetEditorPanel(document);
        harness.AdvanceFrame(0);
        harness.AdvanceFrame(16);
        return (harness, panel, document, session!);
    }

    private static TimelineItem FindItem(CutsceneTimelinePanel panel, CutsceneActionData action, CutsceneTimelineItemRole role = CutsceneTimelineItemRole.Action)
    {
        foreach (var item in panel.Timeline!.Model!.Items)
        {
            var source = ((CutsceneTimelineItemInfo)item.Source!).Item;
            if (ReferenceEquals(source.Action, action) && source.Role == role)
            {
                return item;
            }
        }

        throw new InvalidOperationException("No item for the action.");
    }

    private static IEnumerable<CutsceneActionData> Flatten(CutsceneActionData action)
    {
        yield return action;
        foreach (var child in CutsceneActionCatalog.GetChildren(action))
        {
            foreach (var nested in Flatten(child))
            {
                yield return nested;
            }
        }
    }

    private static T First<T>(CutsceneEditSession session) where T : CutsceneActionData
    {
        return Flatten(session.Asset.RootAction!).OfType<T>().First();
    }

    [Fact]
    public void TheModel_ReflectsTheProjectionOfTheSample_AndIsReadOnly()
    {
        var (_, panel, _, session) = OpenSample();
        var layout = CutsceneTimelineProjection.Build(session.Asset.RootAction);
        var model = panel.Timeline!.Model!;

        int expectedTracks = 0;
        foreach (var row in layout.Rows)
        {
            expectedTracks += row.SubRowCount;
        }

        Assert.Equal(expectedTracks, model.Tracks.Count);
        Assert.Equal(layout.Items.Count, model.Items.Count);
        Assert.Equal((float)layout.DurationSeconds, model.DurationSeconds);
        Assert.All(model.Tracks, track => Assert.False(track.IsEditable));
        Assert.All(model.Items, item =>
        {
            Assert.False(item.IsEditable);
            Assert.False(item.CanMove);
            Assert.False(item.CanResizeStart);
            Assert.False(item.CanResizeEnd);
        });
        Assert.Null(panel.Timeline.Adapter);
        Assert.True(panel.Timeline.VerticalScrollEnabled);
        Assert.True(panel.Timeline.PreciseHitTestEnabled);
    }

    [Fact]
    public void EveryItemOfTheSample_CanBePickedByThePreciseHitTest()
    {
        var (_, panel, _, _) = OpenSample();
        var model = panel.Timeline!.Model!;
        var transform = new TimelineViewTransform { PixelsPerSecond = 100f };
        var trackBounds = new Rectangle(0, 0, 4000, TimelineControlMetrics.TrackRowHeight);

        foreach (var item in model.Items)
        {
            var track = model.Tracks.Single(candidate => candidate.Id == item.TrackId);
            bool picked = false;
            float end = item.Kind == TimelineItemKind.Instant ? item.StartTime : item.StartTime + item.Duration;
            for (float time = item.StartTime; time <= end + 0.0001f && !picked; time += 0.01f)
            {
                int x = (int)MathF.Round(transform.TimeToViewportX(time));
                var result = TimelineHitTest.HitTestTrackPrecise(
                    model.Items, transform, 0f, track, trackBounds, TimelineControlMetrics.ItemHitRadius,
                    TimelineControlMetrics.ResizeHandleWidth, new Point(x, trackBounds.Center.Y));
                picked = ReferenceEquals(result.Item, item);
            }

            Assert.True(picked, $"Item '{item.DisplayName}' at {item.StartTime} s cannot be picked.");
        }
    }

    [Fact]
    public void AClickOnAnItem_SelectsItsActionInTheDocument_AndTheSelectionSurvivesARebuild()
    {
        var (_, panel, document, session) = OpenSample();
        var wait = First<WaitCutsceneActionData>(session);
        var item = FindItem(panel, wait);

        // A click in the track area calls exactly this.
        panel.Timeline!.SetSelectedItemId(item.Id, true);
        Assert.Same(wait, document.SelectedAction);

        var property = CutsceneActionPropertyEditor.FindProperty(wait, "seconds")!;
        EditorHistoryService.Current.Execute(Context, new SetCutsceneActionPropertyCommand(session, wait, property, wait.Seconds + 0.5f));

        Assert.Equal(item.Id, panel.Timeline.ViewState.SelectedItemId);
        Assert.Same(wait, document.SelectedAction);
    }

    [Fact]
    public void ASelectionOfTheDocument_SelectsTheItemWithoutLooping_AndNoSelectionClearsIt()
    {
        var (_, panel, document, session) = OpenSample();
        int documentEvents = 0;
        document.SelectionChanged += _ => documentEvents++;
        var tint = First<TintSpriteCutsceneActionData>(session);

        document.SelectAction(tint);

        Assert.Equal(1, documentEvents);
        Assert.Equal(FindItem(panel, tint).Id, panel.Timeline!.ViewState.SelectedItemId);

        // A click in the empty space does not clear the selection of the document.
        panel.Timeline.SetSelectedItemId(null, true);
        Assert.Same(tint, document.SelectedAction);

        document.SelectAction(null);
        Assert.Null(panel.Timeline.ViewState.SelectedItemId);
    }

    [Fact]
    public void ARepeat_IsSelectedThroughItsLap()
    {
        var (_, panel, document, session) = OpenSample();
        var repeat = First<RepeatCutsceneActionData>(session);

        document.SelectAction(repeat);

        Assert.Equal(FindItem(panel, repeat, CutsceneTimelineItemRole.RepeatLap).Id, panel.Timeline!.ViewState.SelectedItemId);
    }

    [Fact]
    public void ADocumentChange_RebuildsTheModel_WithTheSameItemIds()
    {
        var (_, panel, _, session) = OpenSample();
        var wait = First<WaitCutsceneActionData>(session);
        var before = panel.Timeline!.Model;
        var idBefore = FindItem(panel, wait).Id;

        var property = CutsceneActionPropertyEditor.FindProperty(wait, "seconds")!;
        EditorHistoryService.Current.Execute(Context, new SetCutsceneActionPropertyCommand(session, wait, property, wait.Seconds + 1f));

        Assert.NotSame(before, panel.Timeline.Model);
        Assert.Equal(idBefore, FindItem(panel, wait).Id);
    }

    [Fact]
    public void ADetachedPanel_NoLongerFollowsTheDocument()
    {
        var (_, panel, document, session) = OpenSample();
        panel.SetEditorPanel(null);
        Assert.Null(panel.Timeline!.Model);
        Assert.Null(panel.Document);

        var wait = First<WaitCutsceneActionData>(session);
        var property = CutsceneActionPropertyEditor.FindProperty(wait, "seconds")!;
        EditorHistoryService.Current.Execute(Context, new SetCutsceneActionPropertyCommand(session, wait, property, 9f));
        document.SelectAction(wait);

        Assert.Null(panel.Timeline.Model);
    }

    [Fact]
    public void ScrubbingTheRuler_DoesNotMoveThePlayhead()
    {
        var (_, panel, _, _) = OpenSample();
        panel.Update();
        Assert.Equal(0f, panel.Timeline!.CurrentTimeSeconds);

        panel.Timeline.SetCurrentTimeSeconds(2f, false);
        panel.Timeline.NotifyTimeScrubbed(2f);

        Assert.Equal(0f, panel.Timeline.CurrentTimeSeconds);
    }

    [Fact]
    public void ScrubbingTheRuler_WithoutPreview_RecordsTheRequestAndRefusesIt()
    {
        var (_, panel, _, _) = OpenSample();
        panel.Update();
        Assert.Null(panel.LastSeekRequestSeconds);

        panel.Timeline!.NotifyTimeScrubbed(1.25f);

        Assert.Equal(1.25f, panel.LastSeekRequestSeconds);
        Assert.False(panel.LastSeekRequestAccepted);
        Assert.Equal(0f, panel.Timeline.CurrentTimeSeconds);
    }

    [Fact]
    public void ScrubbingTheRuler_BoundsTheRequestToTheProjection()
    {
        var (_, panel, _, _) = OpenSample();
        panel.Update();
        float duration = (float)panel.Layout.DurationSeconds;
        Assert.True(duration > 0f);

        panel.Timeline!.NotifyTimeScrubbed(duration + 50f);
        Assert.Equal(duration, panel.LastSeekRequestSeconds);

        panel.Timeline.NotifyTimeScrubbed(-3f);
        Assert.Equal(0f, panel.LastSeekRequestSeconds);
    }

    [Fact]
    public void TheProvider_GivesTheStructureMenuForAnItem_AndSelectsItsAction()
    {
        var (_, panel, document, session) = OpenSample();
        var wait = First<WaitCutsceneActionData>(session);
        var tint = First<TintSpriteCutsceneActionData>(session);
        document.SelectAction(tint);
        var item = FindItem(panel, wait);
        var track = panel.Timeline!.Model!.Tracks.Single(candidate => candidate.Id == item.TrackId);

        var provider = Assert.IsAssignableFrom<ITimelineContextMenuProvider>(panel.Timeline.ContextMenuProvider);
        var menu = provider.CreateContextMenu(panel.Timeline, track, item, 0f);

        Assert.NotNull(menu);
        Assert.Same(wait, document.SelectedAction);
        var labels = CutsceneMenuProbe.Labels(menu!);
        Assert.Contains("Copy", labels);
        Assert.Contains("Delete", labels);
        Assert.Contains("Wrap in", labels);
    }

    [Fact]
    public void TheMenuOfTheControl_ComesFromTheProvider_AndAnEntryDoesItsOperation()
    {
        var (_, panel, document, session) = OpenSample();
        var wait = First<WaitCutsceneActionData>(session);
        int before = CutsceneActionTree.CountActions(session.Asset.RootAction!);
        var item = FindItem(panel, wait);
        var track = panel.Timeline!.Model!.Tracks.Single(candidate => candidate.Id == item.TrackId);

        var menu = panel.Timeline.CreateContextMenu(track, item, 0f);

        Assert.NotNull(menu);
        CutsceneMenuProbe.Trigger(CutsceneMenuProbe.Button(menu!, "Delete"));
        Assert.Equal(before - 1, CutsceneActionTree.CountActions(session.Asset.RootAction!));
        Assert.False(CutsceneActionTree.Contains(session.Asset.RootAction!, wait));
    }

    [Fact]
    public void TheProvider_GivesNoMenuForTheEmptySpace_ForATrackHeader_OrWithoutDocument()
    {
        var (_, panel, document, session) = OpenSample();
        var wait = First<WaitCutsceneActionData>(session);
        var item = FindItem(panel, wait);
        var track = panel.Timeline!.Model!.Tracks.Single(candidate => candidate.Id == item.TrackId);
        var provider = (ITimelineContextMenuProvider)panel.Timeline.ContextMenuProvider!;
        document.SelectAction(null);

        Assert.Null(provider.CreateContextMenu(panel.Timeline, track, null, 0f));
        Assert.Null(provider.CreateContextMenu(panel.Timeline, null, null, 0f));
        Assert.Null(provider.CreateTrackHeaderContextMenu(panel.Timeline, track));
        Assert.Null(panel.Timeline.CreateTrackHeaderContextMenu(track));
        Assert.Null(document.SelectedAction);

        panel.SetEditorPanel(null);
        Assert.Null(provider.CreateContextMenu(panel.Timeline, track, item, 0f));
    }

    [Fact]
    public void TheAnimationTimeline_KeepsItsOwnMenu_AndTheDefaultControlHasNoProvider()
    {
        var (harness, panel, _, _) = OpenSample();
        var animation = new Animation2dTimelineControl(harness.Window);
        var plain = new TimelineControl(harness.Window);

        Assert.Same(animation, animation.ContextMenuProvider);
        Assert.Null(plain.ContextMenuProvider);
        Assert.Same(panel, panel.Timeline!.ContextMenuProvider);
    }

    private static void Settle(ContentBrowserViewTestHarness harness, CutsceneTimelinePanel panel, int frames = 6)
    {
        for (int index = 0; index < frames; index++)
        {
            panel.Update();
            harness.AdvanceFrame(16);
        }
    }

    [Fact]
    public void WhenBoundToADocument_TheZoomFitsTheProjectionInTheWidth()
    {
        var (harness, panel, _, _) = OpenSample();
        Settle(harness, panel);
        var timeline = panel.Timeline!;

        float width = timeline.VisibleTimeAreaWidth;
        Assert.True(width > 0f);
        float expected = Math.Clamp(width / ((float)panel.Layout.DurationSeconds * 1.04f), timeline.MinimumPixelsPerSecond, timeline.MaximumPixelsPerSecond);
        Assert.Equal(expected, timeline.PixelsPerSecond, 0.5f);
        Assert.True(panel.Layout.DurationSeconds * timeline.PixelsPerSecond <= width + 0.5f);
        Assert.True(panel.Layout.DurationSeconds * timeline.PixelsPerSecond >= width * 0.9f);
        Assert.Equal(0f, timeline.ViewState.ScrollX);
    }

    [Fact]
    public void ARebuildOfTheSameDocument_KeepsTheZoomOfTheUser_AndAnotherBindingFitsAgain()
    {
        var (harness, panel, document, session) = OpenSample();
        Settle(harness, panel);
        var timeline = panel.Timeline!;
        float fitted = timeline.PixelsPerSecond;

        timeline.PixelsPerSecond = fitted * 1.5f;
        Assert.NotEqual(fitted, timeline.PixelsPerSecond);
        float zoomed = timeline.PixelsPerSecond;

        var wait = First<WaitCutsceneActionData>(session);
        var property = CutsceneActionPropertyEditor.FindProperty(wait, "seconds")!;
        EditorHistoryService.Current.Execute(Context, new SetCutsceneActionPropertyCommand(session, wait, property, wait.Seconds + 0.5f));
        Settle(harness, panel);
        Assert.Equal(zoomed, timeline.PixelsPerSecond);

        panel.SetEditorPanel(null);
        panel.SetEditorPanel(document);
        Settle(harness, panel);
        float expected = Math.Clamp(timeline.VisibleTimeAreaWidth / ((float)panel.Layout.DurationSeconds * 1.04f), timeline.MinimumPixelsPerSecond, timeline.MaximumPixelsPerSecond);
        Assert.Equal(expected, timeline.PixelsPerSecond, 0.5f);
    }

    [Fact]
    public void TheSelectedTrack_IsBroughtIntoView_WhenThePanelIsAttachedAfterTheSelection()
    {
        // A window too low for all rows: the last row is out of view until the panel reveals it.
        var harness = ContentBrowserViewTestHarness.Create(900, 130);
        var panel = new CutsceneTimelinePanel(harness.Window);
        harness.Window.SetContent(panel.CreateContent());
        string path = FindSampleCutscene();
        Assert.True(CutsceneAssetInspectorPanel.TryLoadAsset(path, out var session));
        var document = new CutsceneAssetInspectorPanel(harness.Window);
        _panels.Add(document);
        document.SetHistoryContextId(ContextId);
        document.LoadAsset(session!, path);
        var tint = Flatten(session!.Asset.RootAction!).OfType<TintSpriteCutsceneActionData>().Last();
        document.SelectAction(tint);

        panel.SetEditorPanel(document);
        Settle(harness, panel, 10);

        var timeline = panel.Timeline!;
        Assert.True(timeline.TrackAreaContentHeight > timeline.TrackAreaViewportHeight, "The window must be too low for every row.");
        var item = FindItem(panel, tint);
        Assert.Equal(item.Id, timeline.ViewState.SelectedItemId);
        int trackIndex = timeline.GetTrackIndex(item.TrackId);
        float rowTop = TimelineControlMetrics.ViewportVerticalPadding + (trackIndex * timeline.TrackRowHeight);
        Assert.True(timeline.ScrollY > 0f);
        Assert.True(rowTop >= timeline.ScrollY - 0.5f);
        Assert.True(rowTop + timeline.TrackRowHeight <= timeline.ScrollY + timeline.TrackAreaViewportHeight + 0.5f);
    }

    [Fact]
    public void WithNoSelection_NoTrackIsSelected()
    {
        var (harness, panel, document, session) = OpenSample();
        Settle(harness, panel);
        Assert.Null(panel.Timeline!.ViewState.SelectedItemId);
        Assert.Null(panel.Timeline.ViewState.SelectedTrackId);

        document.SelectAction(First<TintSpriteCutsceneActionData>(session));
        Assert.NotNull(panel.Timeline.ViewState.SelectedTrackId);

        document.SelectAction(null);
        Assert.Null(panel.Timeline.ViewState.SelectedTrackId);
    }

    [Fact]
    public void TheCutsceneTimeline_UsesCompactRows_WhileTheDefaultControlKeepsTheStandardHeight()
    {
        var (harness, panel, _, _) = OpenSample();
        var bounds = new Rectangle(0, 0, 400, 400);

        Assert.Equal(23, panel.Timeline!.TrackRowHeight);
        Assert.Equal(23, panel.Timeline.GetTrackBounds(bounds, 1).Top - panel.Timeline.GetTrackBounds(bounds, 0).Top);

        var standard = new TimelineControl(harness.Window);
        var twoTracks = new TimelineModel();
        twoTracks.Tracks.Add(new TimelineTrack { Label = "a" });
        twoTracks.Tracks.Add(new TimelineTrack { Label = "b" });
        standard.SetModel(twoTracks);
        Assert.Equal(TimelineControlMetrics.TrackRowHeight, standard.TrackRowHeight);
        Assert.Equal(34, standard.GetTrackBounds(bounds, 1).Top - standard.GetTrackBounds(bounds, 0).Top);
        Assert.Equal(TimelineControlMetrics.TrackRowHeight, new TimelineControl(harness.Window, true, true).TrackRowHeight);
    }

    [Fact]
    public void AllRowsOfTheSample_FitWithoutScrolling_InAWindowOfModestHeight()
    {
        var (harness, panel, _, _) = OpenSample();
        Settle(harness, panel);
        var timeline = panel.Timeline!;

        int rowsHeight = (TimelineControlMetrics.ViewportVerticalPadding * 2) + (timeline.Model!.Tracks.Count * timeline.TrackRowHeight);
        Assert.True(timeline.TrackAreaViewportHeight >= rowsHeight, $"viewport {timeline.TrackAreaViewportHeight}, rows {rowsHeight}");
        Assert.False(timeline.IsVerticalScrollBarVisible);
    }

    // ---- Dragging in time (ADR-0141, P5 to P8, P12) -------------------------------------------------------------------

    private static JObject Node(string type, params JObject[] children)
    {
        var node = new JObject { ["type"] = type };
        if (children.Length > 0 || type is "Sequence" or "Parallel" or "At" or "Repeat")
        {
            node["actions"] = new JArray(children);
        }

        return node;
    }

    private static JObject WaitNode(double seconds) => new() { ["type"] = "Wait", ["seconds"] = seconds };

    private static JObject AtNode(double seconds, params JObject[] children)
    {
        var at = Node("At", children);
        at["seconds"] = seconds;
        return at;
    }

    // The tree of the drag tests:
    // Sequence[ Wait 1, Parallel[ At 0.5 [ a: Wait 1 ], b: Wait 2, Sequence[ c1: Wait 1, c2: Wait 1 ] ] ]: the Parallel starts at
    /// 1 s and the cutscene lasts 3 s.
    /// </summary>
    private sealed class DragRig
    {
        public required ContentBrowserViewTestHarness Harness { get; init; }

        public required CutsceneTimelinePanel Panel { get; init; }

        public required CutsceneAssetInspectorPanel Document { get; init; }

        public required CutsceneEditSession Session { get; init; }

        public TimelineControl Timeline => Panel.Timeline!;

        public SequenceCutsceneActionData Root => (SequenceCutsceneActionData)Session.Asset.RootAction!;

        public ParallelCutsceneActionData Parallel => (ParallelCutsceneActionData)Root.Actions[1];

        public AtCutsceneActionData At => (AtCutsceneActionData)Parallel.Actions[0];

        public CutsceneActionData A => At.Actions[0];

        public CutsceneActionData B => Parallel.Actions[1];

        public SequenceCutsceneActionData C => (SequenceCutsceneActionData)Parallel.Actions[2];

        public CutsceneActionData C1 => C.Actions[0];

        public CutsceneActionData C2 => C.Actions[1];

        public int Time { get; set; } = 100;
    }

    private DragRig OpenDragTree(bool historyContext = true)
    {
        var root = Node("Sequence",
            WaitNode(1),
            Node("Parallel",
                AtNode(0.5, WaitNode(1)),
                WaitNode(2),
                Node("Sequence", WaitNode(1), WaitNode(1))));
        var document = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "Drag",
            ["type"] = "CutsceneAsset",
            ["version"] = 1,
            ["schema_version"] = 1,
            ["root_action"] = root,
        };
        string path = Path.Combine(_projectDirectory, "Drag-" + Guid.NewGuid().ToString("N") + ".cutscene");
        File.WriteAllText(path, document.ToString());
        Assert.True(CutsceneAssetInspectorPanel.TryLoadAsset(path, out var session));

        var harness = ContentBrowserViewTestHarness.Create(900, 400);
        var panel = new CutsceneTimelinePanel(harness.Window);
        harness.Window.SetContent(panel.CreateContent());
        var inspector = new CutsceneAssetInspectorPanel(harness.Window);
        _panels.Add(inspector);
        if (historyContext)
        {
            inspector.SetHistoryContextId(ContextId);
            EditorHistoryService.Current.SetActiveContext(Context);
        }

        inspector.LoadAsset(session!, path);
        panel.SetEditorPanel(inspector);
        harness.AdvanceFrame(0);
        harness.AdvanceFrame(16);
        panel.Update();
        harness.AdvanceFrame(32);
        return new DragRig { Harness = harness, Panel = panel, Document = inspector, Session = session! };
    }

    private static MGElement ViewportOf(TimelineControl timeline)
    {
        var field = typeof(TimelineControl).GetField("_viewport", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return (MGElement)field!.GetValue(timeline)!;
    }

    /// <summary>The screen position of a time on the row of an item, the way the track area reads it.</summary>
    private static Point ScreenPoint(DragRig rig, TimelineItem item, float timeSeconds)
    {
        var viewport = ViewportOf(rig.Timeline);
        Rectangle layoutBounds = viewport.LayoutBounds;
        Rectangle row = rig.Timeline.GetTrackBounds(layoutBounds, rig.Timeline.GetTrackIndex(item.TrackId));
        int x = layoutBounds.Left + TimelineControlMetrics.TimeAreaPaddingLeft + (int)MathF.Round(rig.Timeline.ViewTransform.TimeToViewportX(timeSeconds));
        return viewport.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, new Point(x, row.Center.Y));
    }

    /// <summary>Presses the item at <paramref name="grabSeconds"/>, moves to <paramref name="dropSeconds"/> in steps and releases there.</summary>
    private static void Drag(DragRig rig, TimelineItem item, float grabSeconds, float dropSeconds, Microsoft.Xna.Framework.Input.Keys[]? keys = null)
    {
        Point from = ScreenPoint(rig, item, grabSeconds);
        Point to = ScreenPoint(rig, item, dropSeconds);
        var harness = rig.Harness;

        harness.AdvanceFrame(rig.Time += 16, from, MGUI.Shared.Input.Mouse.MouseButton.Left, keys);
        const int steps = 8;
        for (int step = 1; step <= steps; step++)
        {
            var position = new Point(from.X + ((to.X - from.X) * step / steps), from.Y);
            harness.AdvanceFrame(rig.Time += 16, position, MGUI.Shared.Input.Mouse.MouseButton.Left, keys);
        }

        harness.AdvanceFrame(rig.Time += 16, to, MGUI.Shared.Input.Mouse.MouseButton.Left, keys);
        harness.AdvanceFrame(rig.Time += 16, to, null, keys);
        harness.AdvanceFrame(rig.Time += 16, to);
    }

    private static CutsceneTimelineItemInfo InfoOf(TimelineItem item) => (CutsceneTimelineItemInfo)item.Source!;

    private static void Undo()
    {
        EditorHistoryService.Current.SetActiveContext(Context);
        Assert.True(EditorHistoryService.Current.Undo());
    }

    [Fact]
    public void OnlyTheItemsThePlacementRuleAllows_AreEditableAndMovable_WithNoResize_AndTheTracksStayReadOnly()
    {
        var rig = OpenDragTree();
        var model = rig.Timeline.Model!;

        // a: first child of an At; b: direct child of a Parallel; c1: first of a Sequence that is a child of a Parallel.
        foreach (var draggable in new[] { rig.A, rig.B, rig.C1 })
        {
            var item = FindItem(rig.Panel, draggable);
            Assert.True(item.IsEditable);
            Assert.True(item.CanMove);
            Assert.True(InfoOf(item).CanDrag);
        }

        // c2: second child of a Sequence; the first Wait of the root: child of the root.
        foreach (var fixedAction in new[] { rig.C2, rig.Root.Actions[0] })
        {
            var item = FindItem(rig.Panel, fixedAction);
            Assert.False(item.IsEditable);
            Assert.False(item.CanMove);
            Assert.False(InfoOf(item).CanDrag);
        }

        Assert.All(model.Items, item =>
        {
            Assert.False(item.CanResizeStart);
            Assert.False(item.CanResizeEnd);
        });
        Assert.All(model.Tracks, track => Assert.False(track.IsEditable));
        Assert.IsType<CutsceneTimelineEditPolicy>(rig.Timeline.EditPolicy);
    }

    [Fact]
    public void TheLowestInstantOfAnItem_IsWhereTheDelayOfItsAtWouldBeZero()
    {
        var rig = OpenDragTree();

        // The Parallel starts at 1 s: a is at 1.5 s through an At of 0.5 s; b and c1 are at 1 s and would be wrapped (delay 0).
        Assert.Equal(1f, InfoOf(FindItem(rig.Panel, rig.A)).LowestInstantSeconds, 4);
        Assert.Equal(1f, InfoOf(FindItem(rig.Panel, rig.B)).LowestInstantSeconds, 4);
        Assert.Equal(1f, InfoOf(FindItem(rig.Panel, rig.C1)).LowestInstantSeconds, 4);
        Assert.Equal(1.5f, FindItem(rig.Panel, rig.A).StartTime, 4);
    }

    [Fact]
    public void TheToolTipOfAnItemThatCanBeDragged_SaysSo_AndTheOthersAreUnchanged()
    {
        var rig = OpenDragTree();

        Assert.Contains("Drag in time: changes the delay of its At (now 0.5 s).", FindItem(rig.Panel, rig.A).ToolTipText);
        Assert.Contains("Drag in time: wraps it in an At.", FindItem(rig.Panel, rig.B).ToolTipText);
        Assert.DoesNotContain("Drag in time", FindItem(rig.Panel, rig.C2).ToolTipText);
        Assert.StartsWith("Wait", FindItem(rig.Panel, rig.C2).ToolTipText);
    }

    [Fact]
    public void ThePolicy_AlignsOnFrames_StopsAtTheLowestInstant_AndLeavesOtherItemsAlone()
    {
        var rig = OpenDragTree();
        var policy = new CutsceneTimelineEditPolicy();
        var model = rig.Timeline.Model!;
        TimelineSnapContext Snap(TimelineItem? item) => new() { Model = model, Item = item, SnapSettings = rig.Timeline.SnapSettings };
        var a = FindItem(rig.Panel, rig.A);
        var c2 = FindItem(rig.Panel, rig.C2);

        Assert.Equal(2f + (1f / 60f), policy.SnapTime(2.0f + (1f / 60f) + 0.004f, Snap(a)), 4);
        Assert.Equal(1f, policy.SnapTime(0.2f, Snap(a)), 4);
        Assert.Equal(1f, policy.SnapTime(1f - 0.001f, Snap(a)), 4);

        // An item that is not dragged, or no item: the time is returned as it is.
        Assert.Equal(0.1234f, policy.SnapTime(0.1234f, Snap(c2)));
        Assert.Equal(0.1234f, policy.SnapTime(0.1234f, Snap(null)));

        Assert.True(policy.CanMoveItem(a, model.Tracks[0], 2f));
        Assert.False(policy.CanMoveItem(c2, model.Tracks[0], 2f));
        Assert.False(policy.CanResizeItem(a, 1f, 2f));
        Assert.False(policy.CanInsertItem(model.Tracks[0], 1f));
        Assert.True(policy.ValidateMove(model, a, model.Tracks[0], 2f, 1f).IsValid);

        // The control only calls the policy while the snapping is enabled.
        Assert.True(rig.Timeline.SnapSettings.IsEnabled);
        Assert.Equal(1f, rig.Timeline.SnapTime(0.3f, a), 4);
    }

    [Fact]
    public void DraggingTheFirstChildOfAnAt_WritesTheDelayOfTheAt_ToAFrame_InOneEntry_AndUndoes()
    {
        var rig = OpenDragTree();
        var a = FindItem(rig.Panel, rig.A);
        Assert.Equal(0.5f, rig.At.Seconds);

        // The bar starts at 1.5 s and is grabbed 0.3 s after its start: it keeps that gap, so its start goes to 1.9 s.
        Drag(rig, a, 1.8f, 2.2f);

        Assert.Equal(0.9f, rig.At.Seconds, 1f / 60f);
        Assert.Equal(1.9f, FindItem(rig.Panel, rig.A).StartTime, 1f / 60f);
        Assert.Same(rig.A, rig.Document.SelectedAction);
        Assert.True(rig.Document.IsDirty);
        Assert.Null(rig.Panel.LastPlaceRefusal);
        Assert.NotNull(rig.Panel.LastPlaceRequestSeconds);

        Undo();
        Assert.Equal(0.5f, rig.At.Seconds);
        Assert.False(EditorHistoryService.Current.CanUndo);
        Assert.False(rig.Document.IsDirty);
    }

    [Fact]
    public void DraggingADirectChildOfAParallel_WrapsItInAnAt_InOneEntry_AndKeepsTheInstance()
    {
        var rig = OpenDragTree();
        var b = FindItem(rig.Panel, rig.B);
        var b0 = rig.B;
        int count = rig.Parallel.Actions.Count;
        Assert.Equal(1f, b.StartTime, 4);

        // The bar starts at 1 s and is grabbed 0.3 s after its start: it keeps that gap, so its start goes to 1.3 s.
        Drag(rig, b, 1.3f, 1.6f);

        Assert.Equal(count, rig.Parallel.Actions.Count);
        var wrapper = Assert.IsType<AtCutsceneActionData>(rig.Parallel.Actions[1]);
        Assert.Equal(0.3f, wrapper.Seconds, 1f / 60f);
        Assert.Same(b0, Assert.Single(wrapper.Actions));
        Assert.Equal(1.3f, FindItem(rig.Panel, b0).StartTime, 1f / 60f);

        Undo();
        Assert.Same(b0, rig.Parallel.Actions[1]);
        Assert.False(EditorHistoryService.Current.CanUndo);
    }

    [Fact]
    public void DraggingTheSecondChildOfASequence_ChangesNothing()
    {
        var rig = OpenDragTree();
        var c2 = FindItem(rig.Panel, rig.C2);
        float start = c2.StartTime;

        Drag(rig, c2, start + 0.3f, start + 1.0f);

        Assert.False(EditorHistoryService.Current.CanUndo);
        Assert.False(rig.Document.IsDirty);
        Assert.Null(rig.Panel.LastPlaceRequestSeconds);
        Assert.Equal(start, FindItem(rig.Panel, rig.C2).StartTime, 4);
    }

    [Fact]
    public void DraggingBeforeTheStartOfTheBlock_StopsAtTheLowestInstant()
    {
        var rig = OpenDragTree();
        var a = FindItem(rig.Panel, rig.A);

        Drag(rig, a, 1.8f, 0.1f);

        Assert.Equal(0f, rig.At.Seconds);
        Assert.Equal(1f, FindItem(rig.Panel, rig.A).StartTime, 1f / 60f);
    }

    [Fact]
    public void DraggingBeyondTheRealEnd_IsPossible_ThanksToTheRoomAfterIt()
    {
        var rig = OpenDragTree();
        double real = rig.Panel.Layout.DurationSeconds;
        Assert.Equal(3d, real, 4);
        Assert.Equal(real + 2d, rig.Timeline.Model!.DurationSeconds, 4);

        // Zoom out so that the displayed duration is in the window.
        rig.Timeline.PixelsPerSecond = rig.Timeline.VisibleTimeAreaWidth / 6f;
        rig.Harness.AdvanceFrame(rig.Time += 16);
        var a = FindItem(rig.Panel, rig.A);

        // The bar starts at 1.5 s and is grabbed 0.3 s after its start: its start goes to 4.1 s.
        Drag(rig, a, 1.8f, 4.4f);

        Assert.Equal(0.5f + (4.1f - 1.5f), rig.At.Seconds, 1f / 60f);
        Assert.True(FindItem(rig.Panel, rig.A).StartTime > (float)real);
    }

    [Fact]
    public void ThePanel_KeepsTheGrabOffsetOfTheBarsItDrags()
    {
        var rig = OpenDragTree();

        Assert.True(rig.Timeline.KeepGrabOffsetEnabled);
    }

    [Fact]
    public void GrabbingABarInTheMiddle_AndDraggingItByHalfASecond_MovesItByHalfASecond_NotByItsLength()
    {
        var rig = OpenDragTree();
        var a = FindItem(rig.Panel, rig.A);
        Assert.Equal(1f, a.Duration, 4);

        // The bar of a is 1 s long, starts at 1.5 s and is grabbed in its middle (2 s): the delay grows by 0.5 s, not by 1 s.
        Drag(rig, a, 2.0f, 2.5f);

        Assert.Equal(1.0f, rig.At.Seconds, 1f / 60f);
        Assert.Equal(2.0f, FindItem(rig.Panel, rig.A).StartTime, 1f / 60f);
    }

    [Fact]
    public void GrabbingABarAtItsStart_PutsItsStartUnderTheCursor_AsBefore()
    {
        var rig = OpenDragTree();
        var a = FindItem(rig.Panel, rig.A);

        Drag(rig, a, 1.5f, 2.2f);

        Assert.Equal(1.2f, rig.At.Seconds, 1f / 60f);
        Assert.Equal(2.2f, FindItem(rig.Panel, rig.A).StartTime, 1f / 60f);
    }

    [Fact]
    public void AShakyClickInTheMiddleOfABar_DoesNotJumpByHalfItsLength()
    {
        var rig = OpenDragTree();
        var a = FindItem(rig.Panel, rig.A);
        Point from = ScreenPoint(rig, a, 2.0f);
        var harness = rig.Harness;

        // Press in the middle of the bar, move by 4 px (the drag starts after 3 px), then by one more pixel, and release.
        harness.AdvanceFrame(rig.Time += 16, from, MGUI.Shared.Input.Mouse.MouseButton.Left);
        harness.AdvanceFrame(rig.Time += 16, new Point(from.X + 4, from.Y), MGUI.Shared.Input.Mouse.MouseButton.Left);
        var moved = new Point(from.X + 5, from.Y);
        harness.AdvanceFrame(rig.Time += 16, moved, MGUI.Shared.Input.Mouse.MouseButton.Left);
        harness.AdvanceFrame(rig.Time += 16, moved);
        harness.AdvanceFrame(rig.Time += 16, moved);

        // Without the grab offset the delay would be 0.5 s further (half of the bar); now it moves by the 5 px at most, to a frame.
        Assert.NotNull(rig.Panel.LastPlaceRequestSeconds);
        float movedSeconds = 5f / rig.Timeline.PixelsPerSecond;
        Assert.InRange(rig.At.Seconds, 0.5f, 0.5f + movedSeconds + (1f / 60f));
    }

    [Fact]
    public void ADragThatEndsWhereItStarted_ChangesNothing()
    {
        var rig = OpenDragTree();
        var a = FindItem(rig.Panel, rig.A);

        Drag(rig, a, 2.0f, 2.0f);

        Assert.Equal(0.5f, rig.At.Seconds);
        Assert.False(EditorHistoryService.Current.CanUndo);
    }

    [Fact]
    public void ControlDragging_DoesNothing()
    {
        var rig = OpenDragTree();
        var a = FindItem(rig.Panel, rig.A);
        int actions = CutsceneActionTree.CountActions(rig.Session.Asset.RootAction!);

        Drag(rig, a, 1.8f, 2.4f, [Microsoft.Xna.Framework.Input.Keys.LeftControl]);

        Assert.Equal(0.5f, rig.At.Seconds);
        Assert.Equal(actions, CutsceneActionTree.CountActions(rig.Session.Asset.RootAction!));
        Assert.False(EditorHistoryService.Current.CanUndo);
        Assert.Null(rig.Panel.LastPlaceRequestSeconds);
    }

    [Fact]
    public void TheGenericShortcutsOnADraggableItem_AreTakenWithoutDoingAnything()
    {
        var rig = OpenDragTree();
        var a = FindItem(rig.Panel, rig.A);
        rig.Timeline.SetSelectedItemId(a.Id, true);
        int actions = CutsceneActionTree.CountActions(rig.Session.Asset.RootAction!);

        // The control marks these as handled while a draggable item is selected, but no one listens to its requests.
        Assert.True(rig.Timeline.TryDuplicateSelectedItem(1f));
        Assert.True(rig.Timeline.TryDeleteSelectedItem());
        Assert.True(rig.Timeline.TryCopySelectedItem());
        Assert.False(rig.Timeline.TryPasteToSelectedTrack(1f));
        Assert.False(rig.Timeline.TryInsertAtSelectedTrack(1f));

        Assert.Equal(actions, CutsceneActionTree.CountActions(rig.Session.Asset.RootAction!));
        Assert.False(EditorHistoryService.Current.CanUndo);
    }

    [Fact]
    public void ARefusedDrop_ChangesNothing_AndRecordsTheMessage()
    {
        var rig = OpenDragTree(historyContext: false);
        var a = FindItem(rig.Panel, rig.A);

        // The same call the release of a drag makes.
        rig.Timeline.CommitDraggedItemTime(a.Id, 2.2f);

        Assert.Equal(0.5f, rig.At.Seconds);
        Assert.NotNull(rig.Panel.LastPlaceRefusal);
        Assert.Equal(2.2f, rig.Panel.LastPlaceRequestSeconds);
        Assert.False(rig.Document.IsDirty);
    }

    [Fact]
    public void TheModelShowsTheRealDurationPlusRoom_ButThePlayheadAndTheSeeksKeepTheRealDuration()
    {
        var rig = OpenDragTree();
        double real = rig.Panel.Layout.DurationSeconds;

        Assert.Equal(real + Math.Max(real / 4d, 2d), CutsceneTimelinePanel.GetDisplayedDuration(real, true), 6);
        Assert.Equal(2d + 2d, CutsceneTimelinePanel.GetDisplayedDuration(2d, true), 6);
        Assert.Equal(20d + 5d, CutsceneTimelinePanel.GetDisplayedDuration(20d, true), 6);
        Assert.Equal((float)CutsceneTimelinePanel.GetDisplayedDuration(real, true), rig.Timeline.Model!.DurationSeconds);

        // A request in the room after the end is bounded to the real duration, and the playhead stays there.
        rig.Timeline.NotifyTimeScrubbed((float)real + 1.5f);
        Assert.Equal((float)real, rig.Panel.LastSeekRequestSeconds);
        rig.Panel.Update();
        Assert.True(rig.Panel.PlayheadSeconds <= (float)real);
    }

    [Fact]
    public void TheZoomFit_ReadsTheDisplayedDuration_RoomIncludedWhenTheModelHasRoom()
    {
        var rig = OpenDragTree();
        Settle(rig.Harness, rig.Panel);
        var timeline = rig.Timeline;

        float width = timeline.VisibleTimeAreaWidth;
        Assert.True(timeline.Model!.DurationSeconds > (float)rig.Panel.Layout.DurationSeconds);
        float expected = Math.Clamp(width / (timeline.Model.DurationSeconds * 1.04f), timeline.MinimumPixelsPerSecond, timeline.MaximumPixelsPerSecond);
        Assert.Equal(expected, timeline.PixelsPerSecond, 0.5f);
    }

    private static float GetMaxScrollX(TimelineControl timeline)
    {
        return timeline.ViewTransform.GetMaxScrollX(timeline.GetTimelineEndSeconds(), timeline.VisibleTimeAreaWidth);
    }

    [Fact]
    public void TheSample_HasNoRoomAfterItsEnd_AndItsContentFitsTheVisibleWidthAfterTheFit()
    {
        var (harness, panel, _, _) = OpenSample();
        Settle(harness, panel);
        var timeline = panel.Timeline!;

        // The fit leaves nothing to scroll: no horizontal overflow, so the bar does not take the height of a row.
        Assert.True(timeline.VisibleTimeAreaWidth > 0f);
        Assert.True(GetMaxScrollX(timeline) <= 0.5f, $"horizontal overflow of {GetMaxScrollX(timeline)} px");
        Assert.True(timeline.ViewTransform.GetContentWidth(timeline.GetTimelineEndSeconds()) <= timeline.VisibleTimeAreaWidth + 0.5f);
        Assert.False(timeline.IsVerticalScrollBarVisible);

        // No item of loop.cutscene can be dragged: the model shows the real duration.
        Assert.All(timeline.Model!.Items, item => Assert.False(item.CanMove));
        Assert.Equal((float)panel.Layout.DurationSeconds, timeline.Model.DurationSeconds);
    }

    [Fact]
    public void ADocumentWithADraggableItem_KeepsTheRoomAfterItsEnd_AndItsContentFitsTheVisibleWidthAfterTheFit()
    {
        var rig = OpenDragTree();
        Settle(rig.Harness, rig.Panel);
        var timeline = rig.Timeline;
        double real = rig.Panel.Layout.DurationSeconds;

        Assert.Contains(timeline.Model!.Items, item => item.CanMove);
        Assert.Equal((float)CutsceneTimelinePanel.GetDisplayedDuration(real, true), timeline.Model.DurationSeconds);
        Assert.Equal((float)(real + 2d), timeline.Model.DurationSeconds);
        Assert.True(GetMaxScrollX(timeline) <= 0.5f, $"horizontal overflow of {GetMaxScrollX(timeline)} px");
        Assert.True(timeline.ViewTransform.GetContentWidth(timeline.Model.DurationSeconds) <= timeline.VisibleTimeAreaWidth + 0.5f);
    }

    [Fact]
    public void TheDisplayedDuration_HasRoomOnlyWhenAnItemCanBeDragged()
    {
        Assert.Equal(3d, CutsceneTimelinePanel.GetDisplayedDuration(3d, false), 6);
        Assert.Equal(3d + 2d, CutsceneTimelinePanel.GetDisplayedDuration(3d, true), 6);
        Assert.Equal(20d + 5d, CutsceneTimelinePanel.GetDisplayedDuration(20d, true), 6);
        Assert.Equal(0d, CutsceneTimelinePanel.GetDisplayedDuration(double.NaN, false), 6);
    }

    [Fact]
    public void AfterTheFit_ABarCanStillBeDraggedBeyondTheRealEnd_WithoutAnyZoom()
    {
        var rig = OpenDragTree();
        Settle(rig.Harness, rig.Panel);
        rig.Time += 200;
        double real = rig.Panel.Layout.DurationSeconds;
        var a = FindItem(rig.Panel, rig.A);

        // The bar starts at 1.5 s and is grabbed 0.3 s after its start: its start goes to 4.1 s, after the real end (3 s).
        Drag(rig, a, 1.8f, 4.4f);

        Assert.Equal(0.5f + (4.1f - 1.5f), rig.At.Seconds, 1f / 60f);
        Assert.True(FindItem(rig.Panel, rig.A).StartTime > (float)real);
    }

    // ---- Adding at the cursor time (ADR-0141, P9) ---------------------------------------------------------------------

    private static MGContextMenu? EmptySpaceMenu(DragRig rig, float cursorSeconds)
    {
        var track = rig.Timeline.Model!.Tracks[0];
        return rig.Timeline.CreateContextMenu(track, null, cursorSeconds);
    }

    private static MGContextMenuButton FindAddHereEntry(MGContextMenu menu, string category, string type)
    {
        var categoryButton = CutsceneMenuProbe.Button(CutsceneMenuProbe.Button(menu, "Add here").Submenu!, category);
        return CutsceneMenuProbe.Button(categoryButton.Submenu!, type);
    }

    [Fact]
    public void TheEmptySpaceWithAParallelSelected_OffersAddHere_AndAnEntryAddsAnAtAtTheCursorTime()
    {
        var rig = OpenDragTree();
        rig.Document.SelectAction(rig.Parallel);
        int count = rig.Parallel.Actions.Count;

        var menu = EmptySpaceMenu(rig, 2.5f);

        Assert.NotNull(menu);
        Assert.Equal(["Add here"], CutsceneMenuProbe.Labels(menu!));
        var categories = CutsceneMenuProbe.Labels(CutsceneMenuProbe.Button(menu!, "Add here").Submenu!);
        Assert.Contains(CutsceneActionCatalog.FlowCategory, categories);
        Assert.Contains(CutsceneActionCatalog.TimingCategory, categories);

        CutsceneMenuProbe.Trigger(FindAddHereEntry(menu!, CutsceneActionCatalog.TimingCategory, CutsceneActionTypes.Wait));

        // The Parallel starts at 1 s: the At waits 1.5 s, at the end of the Parallel, and holds a new Wait; the action is selected.
        Assert.Equal(count + 1, rig.Parallel.Actions.Count);
        var added = Assert.IsType<AtCutsceneActionData>(rig.Parallel.Actions[^1]);
        Assert.Equal(1.5f, added.Seconds, 3);
        var wait = Assert.IsType<WaitCutsceneActionData>(Assert.Single(added.Actions));
        Assert.Same(wait, rig.Document.SelectedAction);

        // One entry of the history: a single undo removes it.
        Undo();
        Assert.Equal(count, rig.Parallel.Actions.Count);
        Assert.False(EditorHistoryService.Current.CanUndo);
    }

    [Fact]
    public void TheEmptySpaceBeforeTheParallel_AddsAnAtWithNoDelay()
    {
        var rig = OpenDragTree();
        rig.Document.SelectAction(rig.Parallel);

        var menu = EmptySpaceMenu(rig, 0.25f);

        Assert.NotNull(menu);
        CutsceneMenuProbe.Trigger(FindAddHereEntry(menu!, CutsceneActionCatalog.TimingCategory, CutsceneActionTypes.Wait));
        Assert.Equal(0f, Assert.IsType<AtCutsceneActionData>(rig.Parallel.Actions[^1]).Seconds);
    }

    [Fact]
    public void TheEmptySpaceWithAnActionInsideAnAtInAParallelSelected_AddsToThatParallel()
    {
        var rig = OpenDragTree();
        rig.Document.SelectAction(rig.A);
        int count = rig.Parallel.Actions.Count;

        var menu = EmptySpaceMenu(rig, 3f);

        Assert.NotNull(menu);
        CutsceneMenuProbe.Trigger(FindAddHereEntry(menu!, CutsceneActionCatalog.TimingCategory, CutsceneActionTypes.Wait));
        Assert.Equal(count + 1, rig.Parallel.Actions.Count);
        Assert.Equal(2f, Assert.IsType<AtCutsceneActionData>(rig.Parallel.Actions[^1]).Seconds, 3);
    }

    [Fact]
    public void TheEmptySpaceWithNoParallelToReceiveTheAction_HasNoMenu_AndChangesNothing()
    {
        var rig = OpenDragTree();
        var track = rig.Timeline.Model!.Tracks[0];
        int actions = CutsceneActionTree.CountActions(rig.Root);

        // The root Sequence, an action of the root, and no selection: none is in a Parallel.
        foreach (CutsceneActionData? selection in new CutsceneActionData?[] { rig.Root, rig.Root.Actions[0], null })
        {
            rig.Document.SelectAction(selection);
            Assert.Null(rig.Timeline.CreateContextMenu(track, null, 2f));
        }

        Assert.Equal(actions, CutsceneActionTree.CountActions(rig.Root));
        Assert.False(EditorHistoryService.Current.CanUndo);
    }

    [Fact]
    public void TheEmptySpaceWithoutDocumentOrHistoryContext_HasNoMenu()
    {
        var rig = OpenDragTree(historyContext: false);
        var track = rig.Timeline.Model!.Tracks[0];
        rig.Document.SelectAction(rig.Parallel);
        Assert.Null(rig.Timeline.CreateContextMenu(track, null, 2f));

        rig.Panel.SetEditorPanel(null);
        Assert.Null(rig.Timeline.CreateContextMenu(track, null, 2f));
    }

    [Fact]
    public void TheMenuOfAnItem_IsStillTheStructureMenu_WithoutAddHere()
    {
        var rig = OpenDragTree();
        rig.Document.SelectAction(rig.Parallel);
        var item = FindItem(rig.Panel, rig.B);
        var track = rig.Timeline.Model!.Tracks.Single(candidate => candidate.Id == item.TrackId);

        var menu = rig.Timeline.CreateContextMenu(track, item, 2f);

        Assert.NotNull(menu);
        Assert.Same(rig.B, rig.Document.SelectedAction);
        var labels = CutsceneMenuProbe.Labels(menu!);
        Assert.Contains("Add", labels);
        Assert.Contains("Wrap in", labels);
        Assert.Contains("Delete", labels);
        Assert.DoesNotContain("Add here", labels);
    }

    [Fact]
    public void TheDiagnostics_SayForEachItemWhatDraggingItDoes_TheLowestInstant_AndWhereEachParallelStarts()
    {
        var rig = OpenDragTree();
        var builder = new System.Text.StringBuilder();

        rig.Panel.AppendAutomationState(builder);
        string text = builder.ToString();
        var lines = text.Split((char)10);

        var itemLines = lines.Where(line => line.StartsWith("  - Item track", StringComparison.Ordinal)).ToArray();

        // a: first child of the At at actions[0] of the Parallel (actions[1] of the root), delay 0.5 s, lowest instant 1 s.
        var changeLine = Assert.Single(itemLines, line => line.Contains("drag: changes the At root_action.actions[1].actions[0] (seconds 0.5); lowest 1 s"));
        Assert.Contains("start 1.5 s", changeLine);
        // b: direct child of the Parallel; c1: first of a Sequence in the Parallel, so the Sequence is wrapped.
        Assert.Single(itemLines, line => line.Contains("drag: wraps root_action.actions[1].actions[1] in an At (in the Parallel root_action.actions[1]); lowest 1 s"));
        Assert.Single(itemLines, line => line.Contains("drag: wraps root_action.actions[1].actions[2] in an At (in the Parallel root_action.actions[1]); lowest 1 s"));
        // The first Wait of the root and c2 (second of a Sequence): nothing.
        Assert.Equal(2, itemLines.Count(line => line.Contains("drag: none")));
        Assert.Contains("  - Parallel root_action.actions[1] starts at 1 s", text);
        Assert.Contains("  - Last drop: <none>; refusal: <none>", text);
    }
}
