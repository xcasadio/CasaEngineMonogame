using System.Collections.Generic;
using CasaEngine.Editor.Controls.Timeline;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Editor;

public class TimelinePreciseHitTestTests
{
    private const float ContentLeft = 0f;
    private const float Radius = TimelineControlMetrics.ItemHitRadius;
    private const float Handle = TimelineControlMetrics.ResizeHandleWidth;

    private static readonly TimelineViewTransform Transform = new() { PixelsPerSecond = 100f };
    private static readonly Rectangle TrackBounds = new(0, 0, 1000, 34);

    private static TimelineHitTestResult Precise(TimelineTrack track, List<TimelineItem> items, int x)
    {
        return TimelineHitTest.HitTestTrackPrecise(items, Transform, ContentLeft, track, TrackBounds, Radius, Handle, new Point(x, 17));
    }

    private static TimelineItem Item(TimelineTrack track, float start, float duration, TimelineItemKind kind)
    {
        return new TimelineItem { TrackId = track.Id, StartTime = start, Duration = duration, Kind = kind };
    }

    [Fact]
    public void MarkerOnBar_MarkerWins_WhereDefaultHitTestPicksTheBar()
    {
        TimelineTrack track = new();
        TimelineItem bar = Item(track, 0f, 4f, TimelineItemKind.Duration);
        TimelineItem marker = Item(track, 2f, 0f, TimelineItemKind.Marker);
        List<TimelineItem> items = new() { bar, marker };

        Assert.Same(marker, Precise(track, items, 200).Item);
        Assert.Same(bar, TimelineHitTest.HitTestTrack(items, Transform, ContentLeft, track, TrackBounds, Radius, Handle, new Point(200, 17)).Item);
        Assert.Same(bar, Precise(track, items, 100).Item);
    }

    [Fact]
    public void BarOnRange_BarWins()
    {
        TimelineTrack track = new();
        TimelineItem range = Item(track, 0f, 5f, TimelineItemKind.Range);
        TimelineItem bar = Item(track, 1f, 2f, TimelineItemKind.Duration);
        List<TimelineItem> items = new() { range, bar };

        Assert.Same(bar, Precise(track, items, 200).Item);
        Assert.Same(range, Precise(track, items, 400).Item);
    }

    [Fact]
    public void NestedBars_ShortestWinsWhateverTheListOrder()
    {
        TimelineTrack track = new();
        TimelineItem outer = Item(track, 0f, 6f, TimelineItemKind.Duration);
        TimelineItem inner = Item(track, 2f, 1f, TimelineItemKind.Duration);

        Assert.Same(inner, Precise(track, new List<TimelineItem> { outer, inner }, 250).Item);
        Assert.Same(inner, Precise(track, new List<TimelineItem> { inner, outer }, 250).Item);
        Assert.Same(outer, Precise(track, new List<TimelineItem> { inner, outer }, 450).Item);
    }

    [Fact]
    public void SingleBar_IsPicked_AndEmptySpaceReturnsNoItem()
    {
        TimelineTrack track = new();
        TimelineItem bar = Item(track, 1f, 1f, TimelineItemKind.Duration);
        List<TimelineItem> items = new() { bar };

        TimelineHitTestResult hit = Precise(track, items, 150);
        Assert.Same(bar, hit.Item);
        Assert.Equal(TimelineHitTestArea.ItemBody, hit.Area);

        TimelineHitTestResult miss = Precise(track, items, 500);
        Assert.Null(miss.Item);
        Assert.Equal(TimelineHitTestArea.TrackBody, miss.Area);
    }

    [Fact]
    public void ItemsOfOtherTracks_AreIgnored()
    {
        TimelineTrack track = new();
        TimelineTrack other = new();
        List<TimelineItem> items = new() { Item(other, 0f, 4f, TimelineItemKind.Duration) };

        Assert.Null(Precise(track, items, 200).Item);
    }
}
