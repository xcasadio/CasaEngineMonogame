using CasaEngine.Editor.Controls;
using CasaEngine.Editor.Controls.Timeline;
using CasaEngine.Tests.ContentBrowser;
using MGUI.Core.UI;
using Xunit;

namespace CasaEngine.Tests.Editor;

public class TimelinePlayheadFollowTests
{
    private const float DurationSeconds = 30f;
    private const float PixelsPerSecond = 96f;

    [Fact]
    public void EnsureTimeVisible_TimeAfterViewport_ScrollsPlayheadBackIntoView()
    {
        Animation2dTimelineControl timeline = CreateTimeline(out ContentBrowserViewTestHarness harness);

        Assert.Equal(0f, timeline.ViewState.ScrollX);

        timeline.EnsureTimeVisible(DurationSeconds * 0.8f);

        Assert.True(timeline.ViewState.ScrollX > 0f);
        Assert.True(timeline.ViewTransform.TimeToViewportX(DurationSeconds * 0.8f) >= 0f);
        Assert.True(timeline.ViewTransform.TimeToViewportX(DurationSeconds * 0.8f) <= harness.Window.WindowWidth);
    }

    [Fact]
    public void EnsureTimeVisible_TimeAlreadyVisible_KeepsScrollUnchanged()
    {
        Animation2dTimelineControl timeline = CreateTimeline(out _);

        timeline.EnsureTimeVisible(DurationSeconds * 0.8f);
        float scrollX = timeline.ViewState.ScrollX;

        timeline.EnsureTimeVisible(DurationSeconds * 0.8f);

        Assert.Equal(scrollX, timeline.ViewState.ScrollX);
    }

    [Fact]
    public void EnsureTimeVisible_BackToStart_ScrollsBackToZero()
    {
        Animation2dTimelineControl timeline = CreateTimeline(out _);

        timeline.EnsureTimeVisible(DurationSeconds * 0.8f);
        Assert.True(timeline.ViewState.ScrollX > 0f);

        timeline.EnsureTimeVisible(0f);

        Assert.Equal(0f, timeline.ViewState.ScrollX);
    }

    private static Animation2dTimelineControl CreateTimeline(out ContentBrowserViewTestHarness harness)
    {
        harness = ContentBrowserViewTestHarness.Create(420, 200);

        Animation2dTimelineControl timeline = new(harness.Window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            MinimumPixelsPerSecond = PixelsPerSecond,
            MaximumPixelsPerSecond = PixelsPerSecond * 4f,
            PixelsPerSecond = PixelsPerSecond,
        };

        harness.Window.SetContent(timeline);

        timeline.SetTimelineData(
            new[] { new Animation2dTimelineTrackData("Track 1") },
            new Animation2dTimelineItemData[0],
            DurationSeconds);

        harness.AdvanceFrame(0);
        harness.AdvanceFrame(16);

        return timeline;
    }
}
