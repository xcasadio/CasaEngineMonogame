using System.Diagnostics;
using CasaEngine.Framework.Cutscenes;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>
/// Decides, on each editor frame, by how many steps the preview world advances (ADR-0138). A pause is a freeze of the
/// preview world; going to a time is a replay by fixed steps, spread over the editor frames by a time budget. The class
/// does not touch the world: it asks an <see cref="ICutscenePreviewStepper"/> to step. <see cref="Advance"/> allocates
/// nothing.
/// </summary>
public sealed class CutscenePreviewTransport
{
    /// <summary>The fixed step of a seek and of a frame step, in seconds (one frame of the timeline).</summary>
    public const float StepSeconds = (float)(1.0 / CutsceneTimelineProjection.FramesPerSecond);

    /// <summary>The default time that <see cref="Advance"/> spends on the steps of a seek, in milliseconds.</summary>
    public const double DefaultBudgetMilliseconds = 8.0;

    // A seek stops when the play time is less than half a step away from the target, so that the accumulation of the
    // float steps does not cost one step too many or too few.
    private const double Tolerance = StepSeconds / 2.0;

    private readonly Func<long> _timestamp;
    private readonly long _ticksPerSecond;
    private int _pendingFrameSteps;

    /// <summary>Creates a transport that measures its budget with <see cref="Stopwatch"/>.</summary>
    public CutscenePreviewTransport()
        : this(Stopwatch.GetTimestamp, Stopwatch.Frequency)
    {
    }

    /// <summary>Creates a transport with an injected clock, so that tests are deterministic.</summary>
    /// <param name="timestamp">Returns the current time in ticks.</param>
    /// <param name="ticksPerSecond">The number of ticks of <paramref name="timestamp"/> in one second.</param>
    public CutscenePreviewTransport(Func<long> timestamp, long ticksPerSecond)
    {
        ArgumentNullException.ThrowIfNull(timestamp);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticksPerSecond);

        _timestamp = timestamp;
        _ticksPerSecond = ticksPerSecond;
    }

    public CutscenePreviewTransportMode Mode { get; private set; } = CutscenePreviewTransportMode.Running;

    /// <summary>The play time that the last <see cref="Seek"/> aimed at, in seconds.</summary>
    public double TargetSeconds { get; private set; }

    /// <summary>The time that one call of <see cref="Advance"/> may spend on the steps of a seek, in milliseconds.</summary>
    public double BudgetMilliseconds { get; set; } = DefaultBudgetMilliseconds;

    /// <summary>The frame steps requested by <see cref="StepFrames"/> and not done yet.</summary>
    public int PendingFrameSteps => _pendingFrameSteps;

    /// <summary>Starts a play: the world advances with the editor frames.</summary>
    public void Start()
    {
        Mode = CutscenePreviewTransportMode.Running;
        TargetSeconds = 0;
        _pendingFrameSteps = 0;
    }

    /// <summary>Freezes the preview world. A seek in progress is abandoned: the preview stays at the time it reached.</summary>
    public void Pause()
    {
        Mode = CutscenePreviewTransportMode.Paused;
    }

    /// <summary>Lets the preview world advance with the editor frames again.</summary>
    public void Resume()
    {
        Mode = CutscenePreviewTransportMode.Running;
        _pendingFrameSteps = 0;
    }

    /// <summary>
    /// Aims at a play time. When <paramref name="currentSeconds"/> is already less than a step away from it, the transport
    /// goes straight to <see cref="CutscenePreviewTransportMode.Paused"/> without any step.
    /// </summary>
    public void Seek(double targetSeconds, double currentSeconds)
    {
        TargetSeconds = Math.Max(0, targetSeconds);
        _pendingFrameSteps = 0;
        Mode = IsReached(currentSeconds) ? CutscenePreviewTransportMode.Paused : CutscenePreviewTransportMode.Seeking;
    }

    /// <summary>Pauses, and asks for <paramref name="count"/> steps of one frame, done by the next <see cref="Advance"/>.</summary>
    public void StepFrames(int count)
    {
        if (count <= 0)
        {
            return;
        }

        Mode = CutscenePreviewTransportMode.Paused;
        _pendingFrameSteps += count;
    }

    /// <summary>
    /// Advances the preview world for one editor frame and returns the number of steps done. Pause and seek exist only
    /// while a play runs: when no play runs, the transport goes back to Running and forgets the pending frame steps
    /// (ADR-0139). Running: one step of <paramref name="frameSeconds"/>. Paused: the requested frame steps, then nothing.
    /// Seeking: fixed steps while the play runs, is before the target and the budget allows, with at least one step; then
    /// Paused.
    /// </summary>
    public int Advance(ICutscenePreviewStepper stepper, float frameSeconds)
    {
        ArgumentNullException.ThrowIfNull(stepper);

        if (Mode != CutscenePreviewTransportMode.Running && !stepper.IsPlaying)
        {
            Mode = CutscenePreviewTransportMode.Running;
            _pendingFrameSteps = 0;
        }

        switch (Mode)
        {
            case CutscenePreviewTransportMode.Running:
                stepper.Step(frameSeconds);
                return 1;

            case CutscenePreviewTransportMode.Paused:
                return AdvanceFrameSteps(stepper);

            default:
                return AdvanceSeek(stepper);
        }
    }

    /// <summary>
    /// What the transport row shows. The play comes first: when no play runs, the mode of the transport does not count.
    /// </summary>
    /// <param name="isPlaying">True while a play runs.</param>
    /// <param name="mode">The mode of the transport.</param>
    /// <param name="state">The state of the director of the preview world.</param>
    /// <param name="endedByWorldChange">True when the play was stopped by a change of world that the preview refused.</param>
    public static CutscenePreviewStatus GetStatus(
        bool isPlaying,
        CutscenePreviewTransportMode mode,
        CutsceneRuntimeState state,
        bool endedByWorldChange)
    {
        if (isPlaying)
        {
            return mode switch
            {
                CutscenePreviewTransportMode.Seeking => CutscenePreviewStatus.Seeking,
                CutscenePreviewTransportMode.Paused => CutscenePreviewStatus.Paused,
                _ => CutscenePreviewStatus.Playing
            };
        }

        if (state is CutsceneRuntimeState.Completed or CutsceneRuntimeState.Failed
            || (state == CutsceneRuntimeState.Stopped && endedByWorldChange))
        {
            return CutscenePreviewStatus.Ended;
        }

        return CutscenePreviewStatus.Stopped;
    }

    private int AdvanceFrameSteps(ICutscenePreviewStepper stepper)
    {
        int done = 0;
        while (_pendingFrameSteps > 0 && stepper.IsPlaying)
        {
            stepper.Step(StepSeconds);
            _pendingFrameSteps--;
            done++;
        }

        _pendingFrameSteps = 0;
        return done;
    }

    private int AdvanceSeek(ICutscenePreviewStepper stepper)
    {
        long budgetTicks = (long)(BudgetMilliseconds * _ticksPerSecond / 1000.0);
        long start = _timestamp();
        int done = 0;

        while (true)
        {
            if (!stepper.IsPlaying)
            {
                // The play ended during the seek: the next call goes back to Running.
                break;
            }

            if (IsReached(stepper.PlayElapsedSeconds))
            {
                Mode = CutscenePreviewTransportMode.Paused;
                break;
            }

            if (done > 0 && _timestamp() - start >= budgetTicks)
            {
                break;
            }

            stepper.Step(StepSeconds);
            done++;
        }

        return done;
    }

    private bool IsReached(double currentSeconds) => currentSeconds >= TargetSeconds - Tolerance;
}
