using CasaEngine.Editor.Controls.Timeline;
using CasaEngine.Tests.ContentBrowser;
using Microsoft.Xna.Framework;
using MGUI.Core.UI;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>The optional drag in the ruler of the generic timeline: off by default, a click still scrubs, a drag scrubs continuously.</summary>
public sealed class TimelineRulerDragTests
{
    private const float DurationSeconds = 10f;
    private const float PixelsPerSecond = 96f;

    private static TimelineControl CreateTimeline(bool rulerDragScrub, out ContentBrowserViewTestHarness harness, out List<float> scrubbed)
    {
        harness = ContentBrowserViewTestHarness.Create(1200, 200);
        var timeline = new TimelineControl(harness.Window, verticalScroll: false, preciseHitTest: false, rulerDragScrub: rulerDragScrub)
        {
            MinimumPixelsPerSecond = PixelsPerSecond,
            MaximumPixelsPerSecond = PixelsPerSecond * 4f,
            PixelsPerSecond = PixelsPerSecond,
        };
        harness.Window.SetContent(timeline);

        var model = new TimelineModel { DurationSeconds = DurationSeconds };
        model.Tracks.Add(new TimelineTrack { Label = "Track" });
        timeline.SetModel(model);

        var times = new List<float>();
        timeline.TimeScrubbed += times.Add;
        scrubbed = times;

        harness.AdvanceFrame(0);
        harness.AdvanceFrame(16);
        return timeline;
    }

    private static Point RulerPoint(TimelineControl timeline, float timeSeconds)
    {
        Rectangle bounds = timeline.Ruler.ActualLayoutBounds;
        Assert.False(bounds.IsEmpty);
        int x = bounds.Left + TimelineControlMetrics.TimeAreaPaddingLeft + (int)MathF.Round(timeline.ViewTransform.TimeToViewportX(timeSeconds));
        return new Point(x, bounds.Center.Y);
    }

    [Fact]
    public void TheOptionIsOffByDefault()
    {
        var harness = ContentBrowserViewTestHarness.Create(400, 200);

        Assert.False(new TimelineControl(harness.Window).RulerDragScrubEnabled);
        Assert.False(new TimelineControl(harness.Window, true, true).RulerDragScrubEnabled);
        Assert.True(new TimelineControl(harness.Window, true, true, rulerDragScrub: true).RulerDragScrubEnabled);
    }

    [Fact]
    public void WithTheOption_DraggingInTheRuler_ScrubsWithIncreasingTimesEndingAtTheReleasePoint()
    {
        var timeline = CreateTimeline(true, out var harness, out var scrubbed);
        Point from = RulerPoint(timeline, 1f);
        Point to = RulerPoint(timeline, 6f);

        harness.AdvanceFrame(32, from, MGUI.Shared.Input.Mouse.MouseButton.Left);
        int steps = 10;
        for (int step = 1; step <= steps; step++)
        {
            var position = new Point(from.X + ((to.X - from.X) * step / steps), from.Y);
            harness.AdvanceFrame(32 + (step * 16), position, MGUI.Shared.Input.Mouse.MouseButton.Left);
        }

        harness.AdvanceFrame(300, to);

        Assert.True(scrubbed.Count >= 3, $"Only {scrubbed.Count} scrub notification(s).");
        for (int index = 1; index < scrubbed.Count; index++)
        {
            Assert.True(scrubbed[index] > scrubbed[index - 1], "The scrubbed times must increase.");
        }

        float expected = timeline.ViewTransform.ViewportXToTime(to.X - timeline.Ruler.ActualLayoutBounds.Left - TimelineControlMetrics.TimeAreaPaddingLeft);
        Assert.Equal(expected, scrubbed[^1], 0.02f);
        Assert.Equal(expected, timeline.CurrentTimeSeconds, 0.02f);
        Assert.True(scrubbed[0] < scrubbed[^1] - 1f, "The drag must scrub several times before its end.");

        // The release after the drag adds nothing: the last time is the one of the last move, not repeated.
        Assert.Equal(1, scrubbed.Count(time => Math.Abs(time - scrubbed[^1]) < 0.0001f));
    }

    [Fact]
    public void WithTheOption_AClickInTheRuler_StillScrubsOnceOnRelease()
    {
        var timeline = CreateTimeline(true, out var harness, out var scrubbed);
        Point target = RulerPoint(timeline, 3f);

        harness.Click(32, target);

        Assert.Single(scrubbed);
        Assert.Equal(3f, scrubbed[0], 0.05f);
    }

    [Fact]
    public void WithTheOption_AClickAfterADrag_ScrubsAgain()
    {
        var timeline = CreateTimeline(true, out var harness, out var scrubbed);
        Point from = RulerPoint(timeline, 1f);
        Point to = RulerPoint(timeline, 2f);

        harness.AdvanceFrame(32, from, MGUI.Shared.Input.Mouse.MouseButton.Left);
        harness.AdvanceFrame(48, to, MGUI.Shared.Input.Mouse.MouseButton.Left);
        harness.AdvanceFrame(64, to);
        int afterDrag = scrubbed.Count;

        harness.Click(200, RulerPoint(timeline, 5f));

        Assert.Equal(afterDrag + 1, scrubbed.Count);
        Assert.Equal(5f, scrubbed[^1], 0.05f);
    }

    [Fact]
    public void WithoutTheOption_MovingWithTheButtonDown_DoesNotScrub_AndAClickStillDoes()
    {
        var timeline = CreateTimeline(false, out var harness, out var scrubbed);
        Point from = RulerPoint(timeline, 1f);
        Point to = RulerPoint(timeline, 6f);

        harness.AdvanceFrame(32, from, MGUI.Shared.Input.Mouse.MouseButton.Left);
        for (int step = 1; step <= 10; step++)
        {
            var position = new Point(from.X + ((to.X - from.X) * step / 10), from.Y);
            harness.AdvanceFrame(32 + (step * 16), position, MGUI.Shared.Input.Mouse.MouseButton.Left);
        }

        Assert.Empty(scrubbed);

        harness.AdvanceFrame(300, to);
        harness.Click(400, RulerPoint(timeline, 4f));

        Assert.Equal(4f, scrubbed[^1], 0.05f);
    }
}
