using CasaEngine.Editor.Controls.Timeline;
using CasaEngine.Tests.ContentBrowser;
using Microsoft.Xna.Framework;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using Xunit;

namespace CasaEngine.Tests.Editor;

public class TimelineVerticalScrollTests
{
    private const int TrackCount = 12;
    private const int RowHeight = TimelineControlMetrics.TrackRowHeight;
    private const int Padding = TimelineControlMetrics.ViewportVerticalPadding;

    [Fact]
    public void WithoutOptions_ThreeTracks_KeepsContentHeightAndHasNoVerticalScrollBar()
    {
        ContentBrowserViewTestHarness harness = ContentBrowserViewTestHarness.Create(600, 600);
        MGStackPanel stack = new(harness.Window, Orientation.Vertical);
        TimelineControl timeline = new(harness.Window);
        stack.TryAddChild(timeline);
        harness.Window.SetContent(stack);
        timeline.SetModel(CreateModel(3));
        harness.AdvanceFrame(0);
        harness.AdvanceFrame(16);

        Assert.False(timeline.VerticalScrollEnabled);
        Assert.False(timeline.PreciseHitTestEnabled);
        Assert.False(timeline.IsVerticalScrollBarVisible);
        Assert.Equal(0, timeline.TrackAreaContentHeight);
        // Ruler row + row spacing + (12 + 34 x 3): 156 was measured on the control before the options existed.
        Assert.Equal(156, timeline.LayoutBounds.Height);
    }

    [Fact]
    public void WithVerticalScroll_BoundedParent_TakesParentHeightAndScrolls()
    {
        TimelineControl timeline = CreateScrollingTimeline(out _, out _);

        Assert.Equal(200, timeline.LayoutBounds.Height);
        Assert.Equal((2 * Padding) + (TrackCount * RowHeight), timeline.TrackAreaContentHeight);
        Assert.True(timeline.TrackAreaViewportHeight > 0);
        Assert.True(timeline.TrackAreaViewportHeight < timeline.TrackAreaContentHeight);
        Assert.True(timeline.IsVerticalScrollBarVisible);
        Assert.Equal(0f, timeline.ScrollY);
    }

    [Fact]
    public void WithVerticalScroll_ContentFits_ShowsNoVerticalScrollBar()
    {
        ContentBrowserViewTestHarness harness = ContentBrowserViewTestHarness.Create(600, 400);
        TimelineControl timeline = new(harness.Window, verticalScroll: true, preciseHitTest: true);
        harness.Window.SetContent(timeline);
        timeline.SetModel(CreateModel(2));
        harness.AdvanceFrame(0);
        harness.AdvanceFrame(16);

        Assert.False(timeline.IsVerticalScrollBarVisible);
    }

    [Fact]
    public void EnsureTrackVisible_ScrollsToLastAndBackToFirst()
    {
        TimelineControl timeline = CreateScrollingTimeline(out ContentBrowserViewTestHarness harness, out TimelineModel model);

        timeline.EnsureTrackVisible(model.Tracks[TrackCount - 1].Id);
        harness.AdvanceFrame(64);
        Assert.True(timeline.ScrollY > 0f);
        Assert.True(Padding + (TrackCount * RowHeight) <= timeline.ScrollY + timeline.TrackAreaViewportHeight + 0.5f);

        timeline.EnsureTrackVisible(model.Tracks[0].Id);
        harness.AdvanceFrame(80);
        Assert.Equal(0f, timeline.ScrollY);
    }

    [Fact]
    public void GetOffsetToReveal_UsesTheGivenRowHeight()
    {
        const int compact = 24;
        float viewport = 100f;

        Assert.Equal(Padding + (6 * compact) + compact - viewport, TimelineVerticalScroll.GetOffsetToReveal(6, 0f, viewport, compact));
        Assert.Equal(Padding + (2 * compact), TimelineVerticalScroll.GetOffsetToReveal(2, 200f, viewport, compact));
        Assert.Equal(0f, TimelineVerticalScroll.GetOffsetToReveal(1, 0f, viewport, compact));
    }

    [Fact]
    public void GetOffsetToReveal_MovesOnlyWhenTrackIsOutOfView()
    {
        float viewport = 100f;

        Assert.Equal(0f, TimelineVerticalScroll.GetOffsetToReveal(0, 0f, viewport));
        Assert.Equal(0f, TimelineVerticalScroll.GetOffsetToReveal(1, 0f, viewport));
        Assert.Equal(Padding + (5 * RowHeight) + RowHeight - viewport, TimelineVerticalScroll.GetOffsetToReveal(5, 0f, viewport));
        Assert.Equal(0f, TimelineVerticalScroll.GetOffsetToReveal(0, 200f, viewport));
        Assert.Equal(Padding + (2 * RowHeight), TimelineVerticalScroll.GetOffsetToReveal(2, 200f, viewport));
        Assert.Equal(60f, TimelineVerticalScroll.GetOffsetToReveal(3, 60f, viewport));
    }

    [Fact]
    public void WheelOverHeaders_Scrolls_WheelOverTimeArea_ZoomsWithoutScrolling()
    {
        TimelineControl timeline = CreateScrollingTimeline(out ContentBrowserViewTestHarness harness, out _);
        Point headers = new(40, 120);
        Point timeArea = new(300, 120);
        float pixelsPerSecond = timeline.PixelsPerSecond;

        harness.AdvanceFrame(100, timeArea, null, null, 0);
        harness.AdvanceFrame(116, timeArea, null, null, -120);
        harness.AdvanceFrame(132, timeArea, null, null, -240);
        Assert.Equal(0f, timeline.ScrollY);
        Assert.True(timeline.PixelsPerSecond < pixelsPerSecond);

        harness.AdvanceFrame(148, headers, null, null, -240);
        harness.AdvanceFrame(164, headers, null, null, -360);
        harness.AdvanceFrame(180, headers, null, null, -480);
        Assert.True(timeline.ScrollY > 0f);
    }

    [Fact]
    public void ClickOnItem_AfterVerticalScroll_SelectsThatItem()
    {
        TimelineControl timeline = CreateScrollingTimeline(out ContentBrowserViewTestHarness harness, out TimelineModel model);
        TimelineTrack lastTrack = model.Tracks[TrackCount - 1];
        TimelineItem item = new()
        {
            TrackId = lastTrack.Id,
            StartTime = 1f,
            Duration = 2f,
            Kind = TimelineItemKind.Duration,
            IsEditable = false,
            CanMove = false,
        };
        model.Items.Add(item);
        timeline.SetModel(model);
        timeline.EnsureTrackVisible(lastTrack.Id);
        harness.AdvanceFrame(64);
        harness.AdvanceFrame(80);

        Assert.True(timeline.ScrollY > 0f);
        TimelineItem? selected = null;
        timeline.SelectedItemChanged += picked => selected = picked;

        // The whole timeline fits horizontally, so no horizontal scroll bar takes height below the track area.
        // The last track is the only one holding an item and it is scrolled into the bottom of the view: every click that
        // selects it must land inside the screen rows where that track is drawn, not where it would be without the scroll.
        int viewTop = timeline.LayoutBounds.Height - timeline.TrackAreaViewportHeight;
        float trackTop = viewTop + Padding + ((TrackCount - 1) * RowHeight) - timeline.ScrollY;
        int hits = 0;
        int time = 200;
        for (int y = viewTop + 2; y < timeline.LayoutBounds.Height; y += 3)
        {
            selected = null;
            timeline.SetSelectedItemId(null, notify: false);
            Point click = new(120 + 1 + 8 + 130, y);
            harness.AdvanceFrame(time, click);
            // Far apart in time so that two clicks never count as a double click.
            time = harness.Click(time + 1000, click);
            if (selected != null)
            {
                hits++;
                Assert.Equal(item.Id, selected.Id);
                Assert.InRange(y, trackTop - 1f, trackTop + RowHeight + 1f);
            }
        }

        Assert.True(hits >= 5);
    }

    private static TimelineControl CreateScrollingTimeline(out ContentBrowserViewTestHarness harness, out TimelineModel model)
    {
        harness = ContentBrowserViewTestHarness.Create(600, 200);
        TimelineControl timeline = new(harness.Window, verticalScroll: true, preciseHitTest: true);
        harness.Window.SetContent(timeline);
        model = CreateModel(TrackCount);
        timeline.SetModel(model);
        harness.AdvanceFrame(0);
        harness.AdvanceFrame(16);
        harness.AdvanceFrame(32);
        return timeline;
    }

    private static TimelineModel CreateModel(int trackCount)
    {
        TimelineModel model = new() { DurationSeconds = 3f };
        for (int index = 0; index < trackCount; index++)
        {
            model.Tracks.Add(new TimelineTrack { Label = "Track " + index, IsEditable = false });
        }

        return model;
    }
}
