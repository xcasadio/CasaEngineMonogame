namespace CasaEngine.Editor.Controls.Timeline.Editing;

public sealed class TimelineSnapSettings
{
    public bool IsEnabled { get; set; } = true;

    public TimelineSnapMode Mode { get; set; } = TimelineSnapMode.Step;

    public float Step { get; set; } = 0.1f;

    public float FrameRate { get; set; } = 60f;
}
