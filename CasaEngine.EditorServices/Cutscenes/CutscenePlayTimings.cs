using CasaEngine.Framework.Cutscenes;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>
/// Records the duration of the actions of a play (ADR-0098). A play runs a copy of the edited asset: the map given to
/// <see cref="Begin"/> turns the copied actions of the notifications into the edited ones. Only the first occurrence of
/// each action is kept. Once a play has begun, <see cref="OnStarted"/> and <see cref="OnEnded"/> allocate nothing.
/// </summary>
public sealed class CutscenePlayTimings
{
    private readonly Dictionary<CutsceneActionData, double> _starts = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<CutsceneActionData, double> _durations = new(ReferenceEqualityComparer.Instance);
    private IReadOnlyDictionary<CutsceneActionData, CutsceneActionData>? _map;

    /// <summary>The measured durations by edited action, in seconds.</summary>
    public IReadOnlyDictionary<CutsceneActionData, double> Durations => _durations;

    /// <summary>
    /// Changes when the measures are forgotten or when a duration that the timeline projection uses is recorded
    /// (<see cref="CutsceneTimelineProjection.UsesMeasuredDuration"/>); an action that merely ends does not change it.
    /// </summary>
    public int Version { get; private set; }

    /// <summary>
    /// Starts the record of a play: forgets the previous measures and sets the map from the played copy to the edited
    /// actions. The tables are sized for the whole tree so that recording does not grow them.
    /// </summary>
    public void Begin(IReadOnlyDictionary<CutsceneActionData, CutsceneActionData> copyToOriginal)
    {
        ArgumentNullException.ThrowIfNull(copyToOriginal);

        _map = copyToOriginal;
        _starts.Clear();
        _durations.Clear();
        _starts.EnsureCapacity(copyToOriginal.Count);
        _durations.EnsureCapacity(copyToOriginal.Count);
        Version++;
    }

    /// <summary>Forgets the measures. The map of a play in progress is kept, so that the play can go on recording.</summary>
    public void Clear()
    {
        _starts.Clear();
        _durations.Clear();
        Version++;
    }

    public void OnStarted(in CutsceneActionNotification notification)
    {
        if (_map == null || !_map.TryGetValue(notification.Action, out var original) || _starts.ContainsKey(original))
        {
            return;
        }

        _starts[original] = notification.ElapsedSeconds;
    }

    public void OnEnded(in CutsceneActionNotification notification)
    {
        if (_map == null || !_map.TryGetValue(notification.Action, out var original))
        {
            return;
        }

        // For these two the notification ends right after the start, whatever the length of the effect (ADR-0098).
        if (original is MoveSpriteCutsceneActionData or TintSpriteCutsceneActionData)
        {
            return;
        }

        if (_durations.ContainsKey(original) || !_starts.TryGetValue(original, out double start))
        {
            return;
        }

        _durations[original] = Math.Max(0, notification.ElapsedSeconds - start);
        if (CutsceneTimelineProjection.UsesMeasuredDuration(original))
        {
            Version++;
        }
    }

    /// <summary>
    /// Sets the duration of an effect that the notifications cannot measure, such as a sprite move. Only the first
    /// occurrence counts.
    /// </summary>
    /// <param name="action">The edited action.</param>
    public void SetEffectDuration(CutsceneActionData action, double seconds)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_durations.ContainsKey(action))
        {
            return;
        }

        _durations[action] = Math.Max(0, seconds);
        Version++;
    }

    public bool TryGetDuration(CutsceneActionData action, out double seconds) => _durations.TryGetValue(action, out seconds);
}
