using System;

namespace CasaEngine.Editor.Controls.Timeline;

internal sealed class TimelineViewState
{
    public float PixelsPerSecond { get; set; } = 96f;

    public float ScrollX { get; set; }

    public Guid? SelectedEventId { get; set; }
}