#nullable enable

namespace CasaEngine.Editor.Controls.Timeline.Editing;

public interface ITimelineEditPolicy
{
    float SnapTime(float time, TimelineSnapContext context);

    bool CanMoveItem(TimelineItem item, TimelineTrack targetTrack, float newStartTime);

    bool CanResizeItem(TimelineItem item, float newStartTime, float newDuration);

    bool CanInsertItem(TimelineTrack track, float time);

    TimelineValidationResult ValidateMove(
        TimelineModel model,
        TimelineItem item,
        TimelineTrack targetTrack,
        float newStartTime,
        float newDuration);
}
