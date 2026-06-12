using System;
using System.Collections.Generic;
using CasaEngine.Editor.Controls.Timeline;
using CasaEngine.Framework.Assets.Animations;
using MGUI.Core.UI;

namespace CasaEngine.Editor.Controls;

internal readonly record struct Animation2dTimelineLaneData(string Label, bool IsEditable = true, bool AllowsPropertyInsert = true, bool AllowsCustomEventInsert = true, bool AllowsTrackInsert = true, bool AllowsTrackDelete = false, IReadOnlyList<Animation2dTrackProperty>? DeletableTrackProperties = null);

internal readonly record struct Animation2dTimelineEventData(int LaneIndex, float TimeSeconds, string Label, string ValueText = "", bool IsEditable = true, string ToolTipText = "");

internal sealed class Animation2dTimelineControl : TimelineControl
{
    private readonly Dictionary<Guid, int> _eventIndexById = new();
    private readonly Dictionary<Guid, int> _laneIndexById = new();
    private readonly Dictionary<Guid, Animation2dTimelineLaneData> _laneDataById = new();

    public event Action<float> ScrubRequested;

    public event Action<int> EventSelected;

    public event Action<int> LaneSelected;

    public event Action<int, string>? LaneLabelEdited;

    public event Action<Animation2dTrackProperty, int, float>? TrackPropertyInsertRequested;

    public event Action<int>? TrackRequested;

    public event Action<int>? TrackDeleted;

    public event Action<int>? EventCopied;

    public event Action<int, int, float>? EventPasted;

    public event Action<int, float>? PersistedEventInsertRequested;

    public event Action<int, float> EventTimeEdited;

    public event Action<int, float> EventDuplicated;

    public event Action<int> EventDeleted;

    public event Action<int, float> LaneInsertRequested;

    public Animation2dTimelineControl(MGWindow window)
        : base(window)
    {
        CornerHeaderText = string.Empty;
        TrackHeaderText = "Track 1";
        SelectedEventChanged += OnSelectedEventChanged;
        SelectedLaneChanged += OnSelectedLaneChanged;
        LaneLabelEditCommitted += OnLaneLabelEditCommitted;
        TimeScrubbed += timeSeconds => ScrubRequested?.Invoke(timeSeconds);
        EventTimeEditCommitted += OnEventTimeEditCommitted;
        DuplicateRequested += OnDuplicateRequested;
        DeleteRequested += OnDeleteRequested;
        InsertRequested += OnInsertRequested;
        CopyRequested += OnCopyRequested;
        PasteRequested += OnPasteRequested;
    }

    public void SetTimelineData(IReadOnlyList<Animation2dTimelineLaneData> lanes, IReadOnlyList<Animation2dTimelineEventData> events, float durationSeconds)
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
                Animation2dTimelineLaneData laneData = lanes[laneIndex];
                TimelineLane lane = new()
                {
                    Id = Guid.NewGuid(),
                    Label = laneData.Label,
                    IsEditable = laneData.IsEditable,
                };

                model.Lanes.Add(lane);
                _laneIndexById[lane.Id] = laneIndex;
                _laneDataById[lane.Id] = laneData;
            }

            if (events != null)
            {
                for (var index = 0; index < events.Count; index++)
                {
                    Animation2dTimelineEventData eventData = events[index];
                    if (eventData.LaneIndex < 0 || eventData.LaneIndex >= model.Lanes.Count)
                    {
                        continue;
                    }

                    TimelineEvent timelineEvent = new()
                    {
                        Id = Guid.NewGuid(),
                        LaneId = model.Lanes[eventData.LaneIndex].Id,
                        TimeSeconds = eventData.TimeSeconds,
                        EventType = eventData.Label,
                        ValueText = eventData.ValueText,
                        ToolTipText = eventData.ToolTipText,
                        IsEditable = eventData.IsEditable,
                    };

                    model.Events.Add(timelineEvent);
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

        SetSelectedLaneId(selectedLaneId, false);
        SetSelectedEventId(selectedEventId, false);
    }

    private void OnSelectedEventChanged(TimelineEvent selectedEvent)
    {
        if (selectedEvent == null)
        {
            EventSelected?.Invoke(-1);
            return;
        }

        if (_eventIndexById.TryGetValue(selectedEvent.Id, out int eventIndex))
        {
            EventSelected?.Invoke(eventIndex);
            return;
        }

        EventSelected?.Invoke(-1);
    }

    private void OnSelectedLaneChanged(TimelineLane selectedLane)
    {
        if (selectedLane == null)
        {
            LaneSelected?.Invoke(-1);
            return;
        }

        if (_laneIndexById.TryGetValue(selectedLane.Id, out int laneIndex))
        {
            LaneSelected?.Invoke(laneIndex);
            return;
        }

        LaneSelected?.Invoke(-1);
    }

    private void OnLaneLabelEditCommitted(TimelineLane lane, string label)
    {
        if (_laneIndexById.TryGetValue(lane.Id, out int laneIndex))
        {
            LaneLabelEdited?.Invoke(laneIndex, label);
        }
    }

    private void OnEventTimeEditCommitted(TimelineEvent timelineEvent, float timeSeconds)
    {
        if (_eventIndexById.TryGetValue(timelineEvent.Id, out int eventIndex))
        {
            EventTimeEdited?.Invoke(eventIndex, timeSeconds);
        }
    }

    private void OnDuplicateRequested(TimelineEvent timelineEvent, float timeSeconds)
    {
        if (_eventIndexById.TryGetValue(timelineEvent.Id, out int eventIndex))
        {
            EventDuplicated?.Invoke(eventIndex, timeSeconds);
        }
    }

    private void OnDeleteRequested(TimelineEvent timelineEvent)
    {
        if (_eventIndexById.TryGetValue(timelineEvent.Id, out int eventIndex))
        {
            EventDeleted?.Invoke(eventIndex);
        }
    }

    private void OnInsertRequested(TimelineLane lane, float timeSeconds)
    {
        if (_laneIndexById.TryGetValue(lane.Id, out int laneIndex))
        {
            LaneInsertRequested?.Invoke(laneIndex, timeSeconds);
        }
    }

    private void OnCopyRequested(TimelineEvent timelineEvent)
    {
        if (_eventIndexById.TryGetValue(timelineEvent.Id, out int eventIndex))
        {
            EventCopied?.Invoke(eventIndex);
        }
    }

    private void OnPasteRequested(TimelineLane lane, float timeSeconds)
    {
        if (!_laneIndexById.TryGetValue(lane.Id, out int laneIndex))
        {
            return;
        }

        int sourceEventIndex = -1;
        if (Model != null && ViewState.SelectedEventId.HasValue)
        {
            for (var index = 0; index < Model.Events.Count; index++)
            {
                if (Model.Events[index].Id != ViewState.SelectedEventId.Value)
                {
                    continue;
                }

                _eventIndexById.TryGetValue(Model.Events[index].Id, out sourceEventIndex);
                break;
            }
        }

        EventPasted?.Invoke(sourceEventIndex, laneIndex, timeSeconds);
    }

    internal override MGContextMenu? CreateContextMenu(TimelineLane? lane, TimelineEvent? timelineEvent, float cursorTimeSeconds)
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

        if (lane != null && _laneDataById.TryGetValue(lane.Id, out Animation2dTimelineLaneData laneData))
        {
            AddInsertionSubmenu(menu, laneData, "Add at cursor", laneIndex, actualCursorTime);
            AddInsertionSubmenu(menu, laneData, "Add at playhead", laneIndex, CurrentTimeSeconds);
        }

        TimelineEvent? selectedEvent = timelineEvent;
        if (selectedEvent == null && Model != null && ViewState.SelectedEventId.HasValue)
        {
            for (var index = 0; index < Model.Events.Count; index++)
            {
                if (Model.Events[index].Id == ViewState.SelectedEventId.Value)
                {
                    selectedEvent = Model.Events[index];
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

            string itemLabel = string.IsNullOrWhiteSpace(selectedEvent.EventType) ? "item" : selectedEvent.EventType;
            menu.AddButton($"Copy {itemLabel}", _ => NotifyCopyRequested(selectedEvent));
            if (lane != null)
            {
                menu.AddButton($"Paste {itemLabel} at cursor", _ => NotifyPasteRequested(lane, actualCursorTime));
            }

            menu.AddButton($"Delete {itemLabel}", _ => NotifyDeleteRequested(selectedEvent));
        }

        return menu;
    }

    private void AddInsertionSubmenu(MGContextMenu menu, Animation2dTimelineLaneData laneData, string label, int laneIndex, float timeSeconds)
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
            submenu.AddButton("custom event", _ => PersistedEventInsertRequested?.Invoke(laneIndex, timeSeconds));
        }

        button.Submenu = submenu;
    }

    private void AddPropertyInsert(MGContextMenu menu, string label, Animation2dTrackProperty property, int laneIndex, float timeSeconds)
    {
        menu.AddButton(label, _ => TrackPropertyInsertRequested?.Invoke(property, laneIndex, timeSeconds));
    }

    internal override MGContextMenu? CreateTrackHeaderContextMenu(TimelineLane lane)
    {
        if (!_laneIndexById.TryGetValue(lane.Id, out int laneIndex)
            || !_laneDataById.TryGetValue(lane.Id, out Animation2dTimelineLaneData laneData)
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