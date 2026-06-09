using System;
using System.Collections.Generic;
using CasaEngine.Editor.Controls.Timeline;
using MGUI.Core.UI;

namespace CasaEngine.Editor.Controls;

internal readonly record struct Animation2dTimelineLaneData(string Label, bool IsEditable = true);

internal readonly record struct Animation2dTimelineEventData(int LaneIndex, float TimeSeconds, string Label, string ValueText = "", bool IsEditable = true);

internal sealed class Animation2dTimelineControl : TimelineControl
{
    private readonly Dictionary<Guid, int> _eventIndexById = new();
    private readonly Dictionary<Guid, int> _laneIndexById = new();

    public event Action<float> ScrubRequested;

    public event Action<int> EventSelected;

    public event Action<int> LaneSelected;

    public event Action<int, string>? LaneLabelEdited;

    public event Action<int, float> EventTimeEdited;

    public event Action<int, float> EventDuplicated;

    public event Action<int> EventDeleted;

    public event Action<int, float> LaneInsertRequested;

    public Animation2dTimelineControl(MGWindow window)
        : base(window)
    {
        CornerHeaderText = string.Empty;
        TrackHeaderText = "track 01";
        SelectedEventChanged += OnSelectedEventChanged;
        SelectedLaneChanged += OnSelectedLaneChanged;
        LaneLabelEditCommitted += OnLaneLabelEditCommitted;
        TimeScrubbed += timeSeconds => ScrubRequested?.Invoke(timeSeconds);
        EventTimeEditCommitted += OnEventTimeEditCommitted;
        DuplicateRequested += OnDuplicateRequested;
        DeleteRequested += OnDeleteRequested;
        InsertRequested += OnInsertRequested;
    }

    public void SetTimelineData(IReadOnlyList<Animation2dTimelineLaneData> lanes, IReadOnlyList<Animation2dTimelineEventData> events, float durationSeconds)
    {
        _eventIndexById.Clear();
        _laneIndexById.Clear();

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
}