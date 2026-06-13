using System;

namespace CasaEngine.Editor.Controls.Timeline.Rendering;

[Flags]
public enum TimelineItemVisualState
{
    None = 0,
    Selected = 1 << 0,
    Hovered = 1 << 1,
    Dragging = 1 << 2,
    Invalid = 1 << 3,
    Disabled = 1 << 4
}
