using System;
using System.Collections.Generic;

namespace CasaEngine.Editor.Controls.Timeline;

internal sealed class TimelineModel
{
    public float DurationSeconds { get; set; } = 0f;

    public List<TimelineLane> Lanes { get; } = new();

    public List<TimelineEvent> Events { get; } = new();
}

internal sealed class TimelineLane
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Label { get; set; } = string.Empty;

    public bool IsEditable { get; set; } = true;
}

internal sealed class TimelineEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LaneId { get; set; }

    public float TimeSeconds { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string ValueText { get; set; } = string.Empty;

    public bool IsEditable { get; set; } = true;
}