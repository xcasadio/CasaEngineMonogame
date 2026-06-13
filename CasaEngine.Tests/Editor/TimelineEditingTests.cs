using CasaEngine.Editor.Controls;
using CasaEngine.Editor.Controls.Timeline;
using CasaEngine.Editor.Controls.Timeline.Editing;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Editor;

public class TimelineEditingTests
{
    private static TimelineSnapContext Context(TimelineSnapSettings settings)
    {
        return new TimelineSnapContext
        {
            Model = new TimelineModel(),
            SnapSettings = settings,
        };
    }

    [Fact]
    public void SnapTime_FrameMode_SnapsToFrameGrid()
    {
        var policy = new Animation2dTimelineEditPolicy();
        var settings = new TimelineSnapSettings { IsEnabled = true, Mode = TimelineSnapMode.Frame, FrameRate = 10f };

        Assert.Equal(0.3f, policy.SnapTime(0.32f, Context(settings)), 4);
        Assert.Equal(0.3f, policy.SnapTime(0.27f, Context(settings)), 4);
    }

    [Fact]
    public void SnapTime_StepMode_SnapsToStepGrid()
    {
        var policy = new Animation2dTimelineEditPolicy();
        var settings = new TimelineSnapSettings { IsEnabled = true, Mode = TimelineSnapMode.Step, Step = 0.25f };

        Assert.Equal(0.5f, policy.SnapTime(0.6f, Context(settings)), 4);
        Assert.Equal(0.25f, policy.SnapTime(0.2f, Context(settings)), 4);
    }

    [Fact]
    public void SnapTime_DisabledOrNone_ReturnsInput()
    {
        var policy = new Animation2dTimelineEditPolicy();
        var disabled = new TimelineSnapSettings { IsEnabled = false, Mode = TimelineSnapMode.Frame, FrameRate = 10f };
        var none = new TimelineSnapSettings { IsEnabled = true, Mode = TimelineSnapMode.None };

        Assert.Equal(0.123f, policy.SnapTime(0.123f, Context(disabled)), 4);
        Assert.Equal(0.123f, policy.SnapTime(0.123f, Context(none)), 4);
    }

    [Fact]
    public void CanMoveItem_RequiresMovableItemAndEditableTrack()
    {
        var policy = new Animation2dTimelineEditPolicy();
        var track = new TimelineTrack { IsEditable = true };
        var lockedTrack = new TimelineTrack { IsEditable = false };

        Assert.True(policy.CanMoveItem(new TimelineItem { CanMove = true }, track, 1f));
        Assert.False(policy.CanMoveItem(new TimelineItem { CanMove = false }, track, 1f));
        Assert.False(policy.CanMoveItem(new TimelineItem { CanMove = true }, lockedTrack, 1f));
    }

    [Fact]
    public void ValidationResult_ValidAndError_BehaveAsExpected()
    {
        var policy = new Animation2dTimelineEditPolicy();
        Assert.True(policy.ValidateMove(new TimelineModel(), new TimelineItem(), new TimelineTrack(), 1f, 0f).IsValid);

        Assert.True(TimelineValidationResult.Valid.IsValid);

        TimelineValidationResult error = TimelineValidationResult.Error("nope");
        Assert.False(error.IsValid);
        Assert.Equal("nope", error.Message);
    }

    [Fact]
    public void HitTestTrack_DurationItem_DetectsResizeEdgesAndBody()
    {
        var transform = new TimelineViewTransform { PixelsPerSecond = 100f };
        var track = new TimelineTrack();
        var item = new TimelineItem
        {
            TrackId = track.Id,
            StartTime = 1f,
            Duration = 2f,
            Kind = TimelineItemKind.Duration,
            CanResizeStart = true,
            CanResizeEnd = true,
        };
        var items = new[] { item };
        var trackBounds = new Rectangle(0, 0, 1000, 30);

        // startX = 100, endX = 300, centerY = 15
        TimelineHitTestResult start = TimelineHitTest.HitTestTrack(items, transform, 0f, track, trackBounds, 14f, 5f, new Point(100, 15));
        Assert.Equal(TimelineHitTestArea.ResizeStart, start.Area);
        Assert.Same(item, start.Item);

        TimelineHitTestResult end = TimelineHitTest.HitTestTrack(items, transform, 0f, track, trackBounds, 14f, 5f, new Point(300, 15));
        Assert.Equal(TimelineHitTestArea.ResizeEnd, end.Area);

        TimelineHitTestResult body = TimelineHitTest.HitTestTrack(items, transform, 0f, track, trackBounds, 14f, 5f, new Point(200, 15));
        Assert.Equal(TimelineHitTestArea.ItemBody, body.Area);

        TimelineHitTestResult outside = TimelineHitTest.HitTestTrack(items, transform, 0f, track, trackBounds, 14f, 5f, new Point(600, 15));
        Assert.Equal(TimelineHitTestArea.TrackBody, outside.Area);
        Assert.Null(outside.Item);
    }

    [Fact]
    public void HitTestTrack_InstantItem_ReturnsItemBodyWithinRadius()
    {
        var transform = new TimelineViewTransform { PixelsPerSecond = 100f };
        var track = new TimelineTrack();
        var item = new TimelineItem { TrackId = track.Id, StartTime = 1f, Kind = TimelineItemKind.Instant };
        var items = new[] { item };
        var trackBounds = new Rectangle(0, 0, 1000, 30);

        TimelineHitTestResult hit = TimelineHitTest.HitTestTrack(items, transform, 0f, track, trackBounds, 14f, 5f, new Point(104, 15));
        Assert.Equal(TimelineHitTestArea.ItemBody, hit.Area);
        Assert.Same(item, hit.Item);

        TimelineHitTestResult miss = TimelineHitTest.HitTestTrack(items, transform, 0f, track, trackBounds, 14f, 5f, new Point(200, 15));
        Assert.Null(miss.Item);
        Assert.Equal(TimelineHitTestArea.TrackBody, miss.Area);
    }

    [Fact]
    public void FormatTimeLabel_Seconds_UsesSecondsFormat()
    {
        Assert.Equal("1.5", TimelineTickCalculator.FormatTimeLabel(1.5f, TimelineTimeUnit.Seconds, 60f));
        Assert.Equal("0", TimelineTickCalculator.FormatTimeLabel(0f, TimelineTimeUnit.Seconds, 60f));
    }

    [Fact]
    public void FormatTimeLabel_Frames_UsesFrameNumber()
    {
        Assert.Equal("30", TimelineTickCalculator.FormatTimeLabel(0.5f, TimelineTimeUnit.Frames, 60f));
        Assert.Equal("10", TimelineTickCalculator.FormatTimeLabel(1f, TimelineTimeUnit.Frames, 10f));
    }
}
