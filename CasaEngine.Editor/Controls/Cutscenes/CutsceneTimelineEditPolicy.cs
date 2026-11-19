#nullable enable

using System;
using CasaEngine.Editor.Controls.Timeline;
using CasaEngine.Editor.Controls.Timeline.Editing;
using CasaEngine.EditorServices.Cutscenes;

namespace CasaEngine.Editor.Controls.Cutscenes;

/// <summary>
/// The edit policy of the cutscene timeline (ADR-0141): an item that can be dragged in time moves along the frames of the
/// preview (1/60 s) and never below the time where the delay of its At would be 0; nothing else can be edited, so there is no
/// resize and no insertion. The generic control only calls <see cref="SnapTime"/> while a drag goes on, and
/// <see cref="CanMoveItem"/> when it is released.
/// </summary>
internal sealed class CutsceneTimelineEditPolicy : ITimelineEditPolicy
{
    private const float FramesPerSecond = (float)CutscenePlacement.FramesPerSecond;

    public float SnapTime(float time, TimelineSnapContext context)
    {
        if (context.Item?.Source is not CutsceneTimelineItemInfo { CanDrag: true } info)
        {
            return time;
        }

        float aligned = MathF.Round(time * FramesPerSecond) / FramesPerSecond;
        return MathF.Max(aligned, info.LowestInstantSeconds);
    }

    // The tracks of the cutscene timeline are not editable (nothing is inserted on them): only the item says whether it moves.
    public bool CanMoveItem(TimelineItem item, TimelineTrack targetTrack, float newStartTime)
    {
        return item.IsEditable && item.CanMove && item.Source is CutsceneTimelineItemInfo { CanDrag: true };
    }

    public bool CanResizeItem(TimelineItem item, float newStartTime, float newDuration)
    {
        return false;
    }

    public bool CanInsertItem(TimelineTrack track, float time)
    {
        return false;
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
