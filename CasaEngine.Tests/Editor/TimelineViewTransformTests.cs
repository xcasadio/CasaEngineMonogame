using CasaEngine.Editor.Controls.Timeline;
using Xunit;

namespace CasaEngine.Tests.Editor;

public class TimelineViewTransformTests
{
    [Fact]
    public void TimelineModel_UsesDirectEventList()
    {
        var model = new TimelineModel
        {
            DurationSeconds = 3.5f,
        };
        model.Items.Add(new TimelineItem
        {
            StartTime = 0.5f,
            ItemType = "Hit",
        });

        Assert.Equal(3.5f, model.DurationSeconds);
        Assert.Single(model.Items);
        Assert.Equal(0.5f, model.Items[0].StartTime);
        Assert.Equal("Hit", model.Items[0].ItemType);
    }

    [Fact]
    public void TimeViewportConversions_RoundTripThroughSharedTransform()
    {
        var transform = new TimelineViewTransform
        {
            PixelsPerSecond = 120f,
            ScrollX = 30f,
        };

        float viewportX = transform.TimeToViewportX(2f);
        float roundTripTime = transform.ViewportXToTime(viewportX);

        Assert.Equal(210f, viewportX, 4);
        Assert.Equal(2f, roundTripTime, 4);
    }

    [Fact]
    public void ClampScrollX_UsesContentWidthAndViewportWidth()
    {
        var transform = new TimelineViewTransform
        {
            PixelsPerSecond = 100f,
        };

        float maxScrollX = transform.GetMaxScrollX(5f, 320f);

        Assert.Equal(180f, maxScrollX, 4);
        Assert.Equal(0f, transform.ClampScrollX(-10f, 5f, 320f), 4);
        Assert.Equal(180f, transform.ClampScrollX(999f, 5f, 320f), 4);
    }

    [Fact]
    public void AnchoredZoom_KeepsAnchorTimeAtSameViewportPosition()
    {
        var transform = new TimelineViewTransform
        {
            PixelsPerSecond = 96f,
            ScrollX = 24f,
        };

        const float anchorViewportX = 180f;
        float anchorTime = transform.ViewportXToTime(anchorViewportX);
        float newScrollX = transform.GetScrollXForAnchor(anchorTime, anchorViewportX, 192f);

        transform.PixelsPerSecond = 192f;
        transform.ScrollX = newScrollX;

        Assert.Equal(anchorTime, transform.ViewportXToTime(anchorViewportX), 4);
    }

    [Fact]
    public void MajorTickStep_AdaptsToZoom()
    {
        Assert.Equal(10f, TimelineTickCalculator.GetMajorTickStepSeconds(10f, 64f), 4);
        Assert.Equal(1f, TimelineTickCalculator.GetMajorTickStepSeconds(64f, 64f), 4);
        Assert.Equal(0.25f, TimelineTickCalculator.GetMajorTickStepSeconds(256f, 64f), 4);
        Assert.Equal(0.2f, TimelineTickCalculator.GetMinorTickStepSeconds(1f), 4);
    }

    [Fact]
    public void HitTestNearestEvent_ReturnsClosestEventWithinRadius()
    {
        var transform = new TimelineViewTransform
        {
            PixelsPerSecond = 100f,
            ScrollX = 40f,
        };
        Guid laneId = Guid.NewGuid();
        var firstEvent = new TimelineItem
        {
            TrackId = laneId,
            StartTime = 0.5f,
            ItemType = "A",
        };
        var secondEvent = new TimelineItem
        {
            TrackId = laneId,
            StartTime = 1f,
            ItemType = "B",
        };

        TimelineItem? hitEvent = TimelineHitTest.HitTestNearestEvent(
            new[] { firstEvent, secondEvent },
            transform,
            8f,
            laneId,
            32f,
            14f,
            new Microsoft.Xna.Framework.Point(18, 32));

        Assert.Same(firstEvent, hitEvent);
    }

    [Fact]
    public void HitTestNearestEvent_ReturnsNullOutsideHitRadius()
    {
        var transform = new TimelineViewTransform
        {
            PixelsPerSecond = 100f,
        };
        Guid laneId = Guid.NewGuid();
        var timelineEvent = new TimelineItem
        {
            TrackId = laneId,
            StartTime = 1f,
            ItemType = "A",
        };

        TimelineItem? hitEvent = TimelineHitTest.HitTestNearestEvent(
            new[] { timelineEvent },
            transform,
            8f,
            laneId,
            32f,
            14f,
            new Microsoft.Xna.Framework.Point(200, 80));

        Assert.Null(hitEvent);
    }
}