using CasaEngine.Editor;
using CasaEngine.Editor.Controls;
using CasaEngine.Editor.Controls.Cutscenes;
using CasaEngine.Editor.Controls.Timeline;
using CasaEngine.Editor.History;
using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Tests.ContentBrowser;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// The read-only timeline panel of the cutscene document (ADR-0098), on panels without a preview and without a graphics
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
}
