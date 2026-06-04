using System;
using System.Collections.Generic;

namespace CasaEngine.Editor.Controls.Timeline;

internal sealed class TimelineModel
{
    public float DurationSeconds { get; set; } = 0f;

    public List<TimelineEvent> Events { get; } = new();
}

internal sealed class TimelineEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public float TimeSeconds { get; set; }

    public string EventType { get; set; } = string.Empty;
}