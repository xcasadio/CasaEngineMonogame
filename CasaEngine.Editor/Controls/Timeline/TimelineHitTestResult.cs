#nullable enable

namespace CasaEngine.Editor.Controls.Timeline;

public enum TimelineHitTestArea
{
    None,
    TrackHeader,
    TrackBody,
    ItemBody,
    ResizeStart,
    ResizeEnd,
    Ruler,
    Playhead
}

public sealed class TimelineHitTestResult
{
    public TimelineTrack? Track { get; init; }

    public TimelineItem? Item { get; init; }

    public TimelineHitTestArea Area { get; init; }

    public float Time { get; init; }
}
