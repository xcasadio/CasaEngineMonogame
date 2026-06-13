#nullable enable

namespace CasaEngine.Editor.Controls.Timeline.Editing;

public sealed class TimelineSnapContext
{
    public required TimelineModel Model { get; init; }

    public TimelineTrack? Track { get; init; }

    public TimelineItem? Item { get; init; }

    public required TimelineSnapSettings SnapSettings { get; init; }
}
