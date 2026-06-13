#nullable enable

using System;
using CasaEngine.Editor.Controls.Timeline;
using CasaEngine.Editor.Controls.Timeline.Editing;

namespace CasaEngine.Editor.Controls;

internal sealed class Animation2dTimelineEditPolicy : ITimelineEditPolicy
{
    public float SnapTime(float time, TimelineSnapContext context)
    {
        TimelineSnapSettings settings = context.SnapSettings;
        if (!settings.IsEnabled)
        {
            return time;
        }

        switch (settings.Mode)
        {
            case TimelineSnapMode.Frame when settings.FrameRate > 0f:
                return MathF.Round(time * settings.FrameRate) / settings.FrameRate;

            case TimelineSnapMode.Step when settings.Step > 0f:
                return MathF.Round(time / settings.Step) * settings.Step;

            default:
                return time;
        }
    }

    public bool CanMoveItem(TimelineItem item, TimelineTrack targetTrack, float newStartTime)
    {
        return item.CanMove && targetTrack.IsEditable;
    }

    public bool CanResizeItem(TimelineItem item, float newStartTime, float newDuration)
    {
        return item.IsEditable && newDuration >= 0f;
    }

    public bool CanInsertItem(TimelineTrack track, float time)
    {
        return track.IsEditable;
    }

    public TimelineValidationResult ValidateMove(
        TimelineModel model,
        TimelineItem item,
        TimelineTrack targetTrack,
        float newStartTime,
        float newDuration)
    {
        return TimelineValidationResult.Valid;
    }
}
