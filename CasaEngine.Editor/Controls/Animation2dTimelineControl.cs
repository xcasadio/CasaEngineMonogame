#nullable enable

using System;
using System.Collections.Generic;
using CasaEngine.Editor.Controls.Timeline;
using CasaEngine.Framework.Assets.Animations;
using MGUI.Core.UI;

namespace CasaEngine.Editor.Controls;

internal sealed class Animation2dTimelineControl : TimelineControl
{
    public new event Action<float>? ScrubRequested;

    public event Action<int>? EventSelected;

    public Animation2dTimelineControl(MGWindow window)
        : base(window)
    {
        CornerHeaderText = string.Empty;
        TrackHeaderText = "track 01";
        SelectedEventChanged += OnSelectedEventChanged;
        TimeScrubbed += timeSeconds => ScrubRequested?.Invoke(timeSeconds);
    }

    public void SetTimelineData(IReadOnlyList<AnimationEventAsset>? events, float durationSeconds)
    {
        TimelineModel? model = null;
        if (events != null)
        {
            model = new TimelineModel
            {
                DurationSeconds = Math.Max(0f, durationSeconds),
            };

            for (var index = 0; index < events.Count; index++)
            {
                AnimationEventAsset animationEvent = events[index];
                model.Events.Add(new TimelineEvent
                {
                    Id = Guid.NewGuid(),
                    TimeSeconds = animationEvent.TimeSeconds,
                    EventType = animationEvent.EventName,
                });
            }
        }

        SetModel(model);
    }

    public void SetPlaybackState(float currentTimeSeconds, int selectedEventIndex)
    {
        SetCurrentTimeSeconds(currentTimeSeconds);

        Guid? selectedEventId = null;
        if (selectedEventIndex >= 0 && Model != null && selectedEventIndex < Model.Events.Count)
        {
            selectedEventId = Model.Events[selectedEventIndex].Id;
        }

        SetSelectedEventId(selectedEventId, false);
    }

    private void OnSelectedEventChanged(TimelineEvent? selectedEvent)
    {
        if (selectedEvent == null || Model == null)
        {
            EventSelected?.Invoke(-1);
            return;
        }

        for (var index = 0; index < Model.Events.Count; index++)
        {
            if (Model.Events[index].Id == selectedEvent.Id)
            {
                EventSelected?.Invoke(index);
                return;
            }
        }

        EventSelected?.Invoke(-1);
    }
}