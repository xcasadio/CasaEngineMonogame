#nullable enable

using System;
using System.Collections.Generic;

namespace CasaEngine.Editor.Controls.Timeline;

public sealed class TimelineModel
{
    public float DurationSeconds { get; set; } = 0f;

    public TimelineTimeUnit TimeUnit { get; set; } = TimelineTimeUnit.Seconds;

    public float FrameRate { get; set; } = 60f;

    public List<TimelineTrack> Tracks { get; } = new();

    public List<TimelineItem> Items { get; } = new();
}

public sealed class TimelineTrack
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Label { get; set; } = string.Empty;

    public bool IsEditable { get; set; } = true;
}

public enum TimelineItemKind
{
    Instant,
    Duration,
    Range,
    Marker
}

public sealed class TimelineItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TrackId { get; set; }

    public float StartTime { get; set; }

    public float Duration { get; set; } = 0f;

    public TimelineItemKind Kind { get; set; } = TimelineItemKind.Instant;

    public string ItemType { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ValueText { get; set; } = string.Empty;

    public string ToolTipText { get; set; } = string.Empty;

    public bool IsEditable { get; set; } = true;

    public bool CanMove { get; set; } = true;

    public bool CanResizeStart { get; set; }

    public bool CanResizeEnd { get; set; }

    public object? Source { get; set; }
}
