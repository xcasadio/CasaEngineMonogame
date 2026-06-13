#nullable enable

using System;

namespace CasaEngine.Editor.Controls.Timeline.Editing;

public interface ITimelineAdapter
{
    TimelineModel BuildModel();

    void MoveItem(Guid itemId, Guid targetTrackId, float newStartTime);

    void ResizeItem(Guid itemId, float newStartTime, float newDuration);

    void DeleteItem(Guid itemId);

    void DuplicateItem(Guid itemId, Guid targetTrackId, float newStartTime);

    void InsertItem(Guid trackId, float time);

    void RenameTrack(Guid trackId, string newName);

    void OnSelectionChanged(Guid? itemId, Guid? trackId);

    void OnCurrentTimeChanged(float time);
}
