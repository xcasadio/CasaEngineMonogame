using CasaEngine.Editor.Controls.Timeline;
using CasaEngine.Tests.ContentBrowser;
using Microsoft.Xna.Framework;
using MGUI.Core.UI;
using MGUI.Shared.Input.Mouse;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>The optional grab offset of the generic timeline: off by default, an item dragged in time keeps the place where it was grabbed when it is on.</summary>
public sealed class TimelineGrabOffsetTests
{
    private const float PixelsPerSecond = 96f;

    private sealed class Rig
    {
        public required ContentBrowserViewTestHarness Harness { get; init; }

        public required TimelineControl Timeline { get; init; }

        public required TimelineTrack Track { get; init; }

        public required TimelineItem Bar { get; init; }

        public required TimelineItem Marker { get; init; }

        public List<(TimelineItem Item, float Time)> Commits { get; } = new();

        public int Time { get; set; } = 100;
    }

    private static Rig CreateRig(bool keepGrabOffset)
    {
        var harness = ContentBrowserViewTestHarness.Create(1200, 200);
        var timeline = new TimelineControl(harness.Window, verticalScroll: false, preciseHitTest: false, keepGrabOffset: keepGrabOffset)
        {
            MinimumPixelsPerSecond = PixelsPerSecond,
            MaximumPixelsPerSecond = PixelsPerSecond * 4f,
            PixelsPerSecond = PixelsPerSecond,
        };
        harness.Window.SetContent(timeline);

        var track = new TimelineTrack { Label = "Track" };
        var model = new TimelineModel { DurationSeconds = 10f };
        model.Tracks.Add(track);
        var bar = new TimelineItem { TrackId = track.Id, StartTime = 2f, Duration = 1f, Kind = TimelineItemKind.Duration, ItemType = "Bar" };
        var marker = new TimelineItem { TrackId = track.Id, StartTime = 4f, Kind = TimelineItemKind.Instant, ItemType = "Marker" };
        model.Items.Add(bar);
        model.Items.Add(marker);
        timeline.SetModel(model);

        var rig = new Rig { Harness = harness, Timeline = timeline, Track = track, Bar = bar, Marker = marker };
        timeline.ItemTimeEditCommitted += (item, time) => rig.Commits.Add((item, time));
        harness.AdvanceFrame(0);
        harness.AdvanceFrame(16);
        return rig;
    }

    private static MGElement ViewportOf(TimelineControl timeline)
    {
        var field = typeof(TimelineControl).GetField("_viewport", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return (MGElement)field!.GetValue(timeline)!;
    }

    private static Point ScreenPoint(Rig rig, float timeSeconds)
    {
        var viewport = ViewportOf(rig.Timeline);
        // The control without a vertical scroll reads its input bounds from the actual layout bounds, which the marker's hit radius makes visible.
        Rectangle layoutBounds = !viewport.ActualLayoutBounds.IsEmpty ? viewport.ActualLayoutBounds : viewport.LayoutBounds;
        Rectangle row = rig.Timeline.GetTrackBounds(layoutBounds, rig.Timeline.GetTrackIndex(rig.Track.Id));
        int x = layoutBounds.Left + TimelineControlMetrics.TimeAreaPaddingLeft + (int)MathF.Round(rig.Timeline.ViewTransform.TimeToViewportX(timeSeconds));
        return viewport.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, new Point(x, row.Center.Y));
    }

    private static void Drag(Rig rig, float grabSeconds, float dropSeconds)
    {
        Point from = ScreenPoint(rig, grabSeconds);
        Point to = ScreenPoint(rig, dropSeconds);
        var harness = rig.Harness;

        harness.AdvanceFrame(rig.Time += 16, from, MouseButton.Left);
        const int steps = 8;
        for (int step = 1; step <= steps; step++)
        {
            var position = new Point(from.X + ((to.X - from.X) * step / steps), from.Y);
            harness.AdvanceFrame(rig.Time += 16, position, MouseButton.Left);
        }

        harness.AdvanceFrame(rig.Time += 16, to, MouseButton.Left);
        harness.AdvanceFrame(rig.Time += 16, to);
        harness.AdvanceFrame(rig.Time += 16, to);
    }

    [Fact]
    public void TheOptionIsOffByDefault()
    {
        var harness = ContentBrowserViewTestHarness.Create(400, 200);

        Assert.False(new TimelineControl(harness.Window).KeepGrabOffsetEnabled);
        Assert.False(new TimelineControl(harness.Window, true, true).KeepGrabOffsetEnabled);
        Assert.False(new TimelineControl(harness.Window, true, true, rulerDragScrub: true).KeepGrabOffsetEnabled);
        Assert.True(new TimelineControl(harness.Window, true, true, keepGrabOffset: true).KeepGrabOffsetEnabled);
    }

    [Fact]
    public void WithTheOption_ABarGrabbedInTheMiddle_MovesByTheDistanceOfTheCursor_NotByHalfItsLength()
    {
        var rig = CreateRig(keepGrabOffset: true);

        Drag(rig, 2.5f, 3.0f);

        var commit = Assert.Single(rig.Commits);
        Assert.Same(rig.Bar, commit.Item);
        Assert.Equal(2.5f, commit.Time, 0.03f);
    }

    [Fact]
    public void WithoutTheOption_TheStartOfTheBarGoesUnderTheCursor_AsBefore()
    {
        var rig = CreateRig(keepGrabOffset: false);

        Drag(rig, 2.5f, 3.0f);

        var commit = Assert.Single(rig.Commits);
        Assert.Same(rig.Bar, commit.Item);
        Assert.Equal(3.0f, commit.Time, 0.03f);
    }

    [Fact]
    public void WithTheOption_ABarGrabbedAtItsStart_HasItsStartUnderTheCursor()
    {
        var rig = CreateRig(keepGrabOffset: true);

        Drag(rig, 2.0f, 3.0f);

        Assert.Equal(3.0f, Assert.Single(rig.Commits).Time, 0.03f);
    }

    [Fact]
    public void WithTheOption_ABarGrabbedNearItsEnd_KeepsTheGap_AndFollowsTheCursorBackwards()
    {
        var rig = CreateRig(keepGrabOffset: true);

        // Grabbed 0.9 s after its start, dragged back by 0.4 s: it moves by 0.4 s, not to the cursor.
        Drag(rig, 2.9f, 2.5f);

        Assert.Equal(1.6f, Assert.Single(rig.Commits).Time, 0.03f);
    }

    [Fact]
    public void WithTheOption_AMarkerKeepsItsBehaviour_ItsTimeGoesUnderTheCursor()
    {
        var rig = CreateRig(keepGrabOffset: true);

        Drag(rig, 4.0f, 5.0f);

        var commit = Assert.Single(rig.Commits);
        Assert.Same(rig.Marker, commit.Item);
        Assert.Equal(5.0f, commit.Time, 0.03f);
    }

    [Fact]
    public void WithTheOption_APressInTheMiddleOfABarFollowedByASmallMove_DoesNotJumpByHalfItsLength()
    {
        var rig = CreateRig(keepGrabOffset: true);
        Point from = ScreenPoint(rig, 2.5f);
        var harness = rig.Harness;

        harness.AdvanceFrame(rig.Time += 16, from, MouseButton.Left);
        harness.AdvanceFrame(rig.Time += 16, new Point(from.X + 4, from.Y), MouseButton.Left);
        var moved = new Point(from.X + 5, from.Y);
        harness.AdvanceFrame(rig.Time += 16, moved, MouseButton.Left);
        harness.AdvanceFrame(rig.Time += 16, moved);
        harness.AdvanceFrame(rig.Time += 16, moved);

        // The bar follows the 5 px of the cursor (about 0.05 s): it stays near its place, not half its length (0.5 s) away.
        var commit = Assert.Single(rig.Commits);
        Assert.InRange(commit.Time, 2f, 2f + (5f / PixelsPerSecond) + 0.02f);
    }
}
