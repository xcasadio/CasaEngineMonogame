namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>
/// What the transport of the cutscene preview needs from the preview session: whether a play is running, the time of that
/// play, and a way to advance the preview world by one step (ADR-0099).
/// </summary>
public interface ICutscenePreviewStepper
{
    /// <summary>True while the play of the cutscene runs and has not ended.</summary>
    bool IsPlaying { get; }

    /// <summary>The time of the play in seconds, as measured by the director of the preview world.</summary>
    double PlayElapsedSeconds { get; }

    /// <summary>Advances the preview world by <paramref name="seconds"/>.</summary>
    void Step(float seconds);
}
