using System;
using System.Collections.Generic;
using CasaEngine.Editor.Controls.Timeline;
using CasaEngine.Framework.Assets.Animations;
using MGUI.Core.UI;

namespace CasaEngine.Editor.Controls;

internal readonly record struct Animation2dTimelineTrackData(string Label, bool IsEditable = true, bool AllowsPropertyInsert = true, bool AllowsCustomEventInsert = true, bool AllowsTrackInsert = true, bool AllowsTrackDelete = false, IReadOnlyList<Animation2dTrackProperty>? DeletableTrackProperties = null);

internal readonly record struct Animation2dTimelineItemData(int TrackIndex, float StartTime, string Label, string ValueText = "", bool IsEditable = true, string ToolTipText = "");

internal sealed class Animation2dTimelineControl : TimelineControl
{
    private readonly Dictionary<Guid, int> _eventIndexById = new();
    private readonly Dictionary<Guid, int> _laneIndexById = new();
    private readonly Dictionary<Guid, Animation2dTimelineTrackData> _laneDataById = new();

    public event Action<float> ScrubRequested;

    public event Action<int> ItemSelected;

    public event Action<int> TrackSelected;

    public event Action<int, string>? TrackLabelEdited;

    public event Action<Animation2dTrackProperty, int, float>? TrackPropertyInsertRequested;

    public event Action<int>? TrackRequested;

    public event Action<int>? TrackDeleted;

    public event Action<int>? ItemCopied;

    public event Action<int, int, float>? ItemPasted;

    public event Action<int, float>? PersistedItemInsertRequested;

    public event Action<int, float> ItemTimeEdited;

    public event Action<int, float> ItemDuplicated;

    public event Action<int> ItemDeleted;

    public event Action<int, float> TrackInsertRequested;

    public Animation2dTimelineControl(MGWindow window)
        : base(window)
    {
        CornerHeaderText = string.Empty;
        TrackHeaderText = "Track 1";
        SelectedItemChanged += OnSelectedEventChanged;
        SelectedTrackChanged += OnSelectedLaneChanged;
        TrackLabelEditCommitted += OnLaneLabelEditCommitted;
        TimeScrubbed += timeSeconds => ScrubRequested?.Invoke(timeSeconds);
        ItemTimeEditCommitted += OnEventTimeEditCommitted;
        DuplicateRequested += OnDuplicateRequested;
        DeleteRequested += OnDeleteRequested;
        InsertRequested += OnInsertRequested;
        CopyRequested += OnCopyRequested;
        PasteRequested += OnPasteRequested;
    }

    public void SetTimelineData(IReadOnlyList<Animation2dTimelineTrackData> lanes, IReadOnlyList<Animation2dTimelineItemData> events, float durationSeconds)
    {
        _eventIndexById.Clear();
        _laneIndexById.Clear();
        _laneDataById.Clear();

        TimelineModel model = null;
        if (lanes != null)
        {
            model = new TimelineModel
            {
                DurationSeconds = Math.Max(0f, durationSeconds),
            };

            for (var laneIndex = 0; laneIndex < lanes.Count; laneIndex++)
            {
                Animation2dTimelineTrackData laneData = lanes[laneIndex];
                TimelineTrack lane = new()
                {
                    Id = Guid.NewGuid(),
                    Label = laneData.Label,
                    IsEditable = laneData.IsEditable,
                };

                model.Tracks.Add(lane);
                _laneIndexById[lane.Id] = laneIndex;
                _laneDataById[lane.Id] = laneData;
            }

            if (events != null)
            {
                for (var index = 0; index < events.Count; index++)
                {
                    Animation2dTimelineItemData eventData = events[index];
                    if (eventData.TrackIndex < 0 || eventData.TrackIndex >= model.Tracks.Count)
                    {
                        continue;
                    }

                    TimelineItem timelineEvent = new()
                    {
                        Id = Guid.NewGuid(),
                        TrackId = model.Tracks[eventData.TrackIndex].Id,
                        StartTime = eventData.StartTime,
                        ItemType = eventData.Label,
                        ValueText = eventData.ValueText,
                        ToolTipText = eventData.ToolTipText,
                        IsEditable = eventData.IsEditable,
                    };

                    model.Items.Add(timelineEvent);
                    _eventIndexById[timelineEvent.Id] = index;
                }
            }
        }

        SetModel(model);
    }

    public void SetPlaybackState(float currentTimeSeconds, int selectedEventIndex, int selectedLaneIndex)
    {
        SetCurrentTimeSeconds(currentTimeSeconds);

        Guid? selectedEventId = null;
        if (selectedEventIndex >= 0)
        {
            foreach (KeyValuePair<Guid, int> pair in _eventIndexById)
            {
                if (pair.Value == selectedEventIndex)
                {
                    selectedEventId = pair.Key;
                    break;
                }
            }
        }

        Guid? selectedLaneId = null;
        if (selectedLaneIndex >= 0)
        {
            foreach (KeyValuePair<Guid, int> pair in _laneIndexById)
            {
                if (pair.Value == selectedLaneIndex)
                {
                    selectedLaneId = pair.Key;
                    break;
                }
            }
        }

        SetSelectedTrackId(selectedLaneId, false);
        SetSelectedItemId(selectedEventId, false);
    }

    private void OnSelectedEventChanged(TimelineItem selectedEvent)
    {
        if (selectedEvent == null)
        {
            ItemSelected?.Invoke(-1);
            return;
        }

        if (_eventIndexById.TryGetValue(selectedEvent.Id, out int eventIndex))
        {
            ItemSelected?.Invoke(eventIndex);
            return;
        }

        ItemSelected?.Invoke(-1);
    }

    private void OnSelectedLaneChanged(TimelineTrack selectedLane)
    {
        if (selectedLane == null)
        {
            TrackSelected?.Invoke(-1);
            return;
        }

        if (_laneIndexById.TryGetValue(selectedLane.Id, out int laneIndex))
        {
            TrackSelected?.Invoke(laneIndex);
            return;
        }

        TrackSelected?.Invoke(-1);
    }

    private void OnLaneLabelEditCommitted(TimelineTrack lane, string label)
    {
        if (_laneIndexById.TryGetValue(lane.Id, out int laneIndex))
        {
            TrackLabelEdited?.Invoke(laneIndex, label);
        }
    }

    private void OnEventTimeEditCommitted(TimelineItem timelineEvent, float timeSeconds)
    {
        if (_eventIndexById.TryGetValue(timelineEvent.Id, out int eventIndex))
        {
            ItemTimeEdited?.Invoke(eventIndex, timeSeconds);
        }
    }

    private void OnDuplicateRequested(TimelineItem timelineEvent, float timeSeconds)
    {
        if (_eventIndexById.TryGetValue(timelineEvent.Id, out int eventIndex))
        {
            ItemDuplicated?.Invoke(eventIndex, timeSeconds);
        }
    }

    private void OnDeleteRequested(TimelineItem timelineEvent)
    {
        if (_eventIndexById.TryGetValue(timelineEvent.Id, out int eventIndex))
        {
            ItemDeleted?.Invoke(eventIndex);
        }
    }

    private void OnInsertRequested(TimelineTrack lane, float timeSeconds)
    {
        if (_laneIndexById.TryGetValue(lane.Id, out int laneIndex))
        {
            TrackInsertRequested?.Invoke(laneIndex, timeSeconds);
        }
    }

    private void OnCopyRequested(TimelineItem timelineEvent)
    {
        if (_eventIndexById.TryGetValue(timelineEvent.Id, out int eventIndex))
        {
            ItemCopied?.Invoke(eventIndex);
        }
    }

    private void OnPasteRequested(TimelineTrack lane, float timeSeconds)
    {
        if (!_laneIndexById.TryGetValue(lane.Id, out int laneIndex))
        {
            return;
        }

        int sourceEventIndex = -1;
        if (Model != null && ViewState.SelectedItemId.HasValue)
        {
            for (var index = 0; index < Model.Items.Count; index++)
            {
                if (Model.Items[index].Id != ViewState.SelectedItemId.Value)
                {
                    continue;
                }

                _eventIndexById.TryGetValue(Model.Items[index].Id, out sourceEventIndex);
                break;
            }
        }

        ItemPasted?.Invoke(sourceEventIndex, laneIndex, timeSeconds);
    }

    internal override MGContextMenu? CreateContextMenu(TimelineTrack? lane, TimelineItem? timelineEvent, float cursorTimeSeconds)
    {
        if (ParentWindow == null)
        {
            return null;
        }

        MGContextMenu menu = new(ParentWindow, string.Empty);
        float actualCursorTime = Math.Clamp(cursorTimeSeconds, 0f, GetTimelineEndSeconds());

        int laneIndex = -1;
        if (lane != null)
        {
            _laneIndexById.TryGetValue(lane.Id, out laneIndex);
        }

        if (lane != null && _laneDataById.TryGetValue(lane.Id, out Animation2dTimelineTrackData laneData))
        {
            AddInsertionSubmenu(menu, laneData, "Add at cursor", laneIndex, actualCursorTime);
            AddInsertionSubmenu(menu, laneData, "Add at playhead", laneIndex, CurrentTimeSeconds);
        }

        TimelineItem? selectedEvent = timelineEvent;
        if (selectedEvent == null && Model != null && ViewState.SelectedItemId.HasValue)
        {
            for (var index = 0; index < Model.Items.Count; index++)
            {
                if (Model.Items[index].Id == ViewState.SelectedItemId.Value)
                {
                    selectedEvent = Model.Items[index];
                    break;
                }
            }
        }

        if (selectedEvent != null && selectedEvent.IsEditable)
        {
            if (menu.Items.Count > 0)
            {
                menu.AddSeparator();
            }

            string itemLabel = string.IsNullOrWhiteSpace(selectedEvent.ItemType) ? "item" : selectedEvent.ItemType;
            menu.AddButton($"Copy {itemLabel}", _ => NotifyCopyRequested(selectedEvent));
            if (lane != null)
            {
                menu.AddButton($"Paste {itemLabel} at cursor", _ => NotifyPasteRequested(lane, actualCursorTime));
            }

            menu.AddButton($"Delete {itemLabel}", _ => NotifyDeleteRequested(selectedEvent));
        }

        return menu;
    }

    private void AddInsertionSubmenu(MGContextMenu menu, Animation2dTimelineTrackData laneData, string label, int laneIndex, float timeSeconds)
    {
        if (!laneData.AllowsPropertyInsert && !laneData.AllowsCustomEventInsert)
        {
            return;
        }

        MGContextMenuButton button = menu.AddButton(label, _ => { });
        MGContextMenu submenu = new(menu);
        if (laneData.AllowsPropertyInsert)
        {
            AddPropertyInsert(submenu, "attachment", Animation2dTrackProperty.Sprite, laneIndex, timeSeconds);
            AddPropertyInsert(submenu, "localPositionOffset", Animation2dTrackProperty.Position, laneIndex, timeSeconds);
            AddPropertyInsert(submenu, "visible", Animation2dTrackProperty.Visible, laneIndex, timeSeconds);
            AddPropertyInsert(submenu, "flipX", Animation2dTrackProperty.FlipX, laneIndex, timeSeconds);
            AddPropertyInsert(submenu, "flipY", Animation2dTrackProperty.FlipY, laneIndex, timeSeconds);
            AddPropertyInsert(submenu, "rotation", Animation2dTrackProperty.Rotation, laneIndex, timeSeconds);
            AddPropertyInsert(submenu, "drawOrder", Animation2dTrackProperty.DrawOrder, laneIndex, timeSeconds);
        }

        if (laneData.AllowsCustomEventInsert)
        {
            submenu.AddButton("custom event", _ => PersistedItemInsertRequested?.Invoke(laneIndex, timeSeconds));
        }

        button.Submenu = submenu;
    }

    private void AddPropertyInsert(MGContextMenu menu, string label, Animation2dTrackProperty property, int laneIndex, float timeSeconds)
    {
        menu.AddButton(label, _ => TrackPropertyInsertRequested?.Invoke(property, laneIndex, timeSeconds));
    }

    internal override MGContextMenu? CreateTrackHeaderContextMenu(TimelineTrack lane)
    {
        if (!_laneIndexById.TryGetValue(lane.Id, out int laneIndex)
            || !_laneDataById.TryGetValue(lane.Id, out Animation2dTimelineTrackData laneData)
            || ParentWindow == null)
        {
            return null;
        }

        MGContextMenu menu = new(ParentWindow, string.Empty);
        if (laneData.AllowsTrackInsert)
        {
            menu.AddButton("Add new track", _ => TrackRequested?.Invoke(laneIndex));
        }

        if (laneData.AllowsTrackDelete)
        {
            if (menu.Items.Count > 0)
            {
                menu.AddSeparator();
            }

            menu.AddButton("Delete track", _ => TrackDeleted?.Invoke(laneIndex));
        }

        return menu.Items.Count > 0 ? menu : null;
    }
}