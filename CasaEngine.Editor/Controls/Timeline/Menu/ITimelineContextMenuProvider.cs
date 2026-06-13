#nullable enable

using MGUI.Core.UI;

namespace CasaEngine.Editor.Controls.Timeline.Menu;

internal interface ITimelineContextMenuProvider
{
    MGContextMenu? CreateContextMenu(TimelineControl timeline, TimelineTrack? track, TimelineItem? item, float cursorTime);

    MGContextMenu? CreateTrackHeaderContextMenu(TimelineControl timeline, TimelineTrack track);
}
