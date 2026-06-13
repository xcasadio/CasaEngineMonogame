#nullable enable

using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;

namespace CasaEngine.Editor.Controls.Timeline.Rendering;

public sealed class TimelineRenderContext
{
    public required TimelineModel Model { get; init; }

    public required TimelineViewTransform Transform { get; init; }

    public float CurrentTime { get; init; }

    public ITextMeasurementEngine? TextEngine { get; init; }

    public ResolvedFont Font { get; init; }

    public float FontScale { get; init; } = 1f;
}
