using CasaEngine.Framework.Cutscenes;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>The kind of a row of the cutscene timeline (ADR-0137).</summary>
public enum CutsceneTimelineRowKind
{
    /// <summary>The ranges of the Repeat actions.</summary>
    Loops,

    /// <summary>The actions that only make the sequence wait.</summary>
    Flow,

    /// <summary>The actions that target one entity; one row per entity.</summary>
    Entity,

    Screen,
    Audio,
    WorldAndInput,

    /// <summary>Actions of an unknown type.</summary>
    Other,
}

/// <summary>How an item is drawn.</summary>
public enum CutsceneTimelineItemShape
{
    /// <summary>A bar with a duration.</summary>
    Bar,

    /// <summary>An instant, without duration.</summary>
    Marker,

    /// <summary>The span of a Repeat.</summary>
    Range,

    /// <summary>A bar that stays open until the end of the cutscene.</summary>
    OpenBar,
}

/// <summary>What an item stands for.</summary>
public enum CutsceneTimelineItemRole
{
    /// <summary>The action itself.</summary>
    Action,

    /// <summary>The drawn lap of a Repeat; the item's action is the Repeat.</summary>
    RepeatLap,

    /// <summary>The laps of a Repeat after the drawn one; the item's action is the Repeat.</summary>
    RepeatMoreLaps,
}

/// <summary>A row of the timeline.</summary>
public sealed class CutsceneTimelineRow
{
    public CutsceneTimelineRow(CutsceneTimelineRowKind kind, string label, string? entityName)
    {
        Kind = kind;
        Label = label;
        EntityName = entityName;
    }

    public CutsceneTimelineRowKind Kind { get; }

    public string Label { get; }

    /// <summary>The entity of an <see cref="CutsceneTimelineRowKind.Entity"/> row; empty for the row of unnamed targets.</summary>
    public string? EntityName { get; }

    /// <summary>The number of sub-rows that hold overlapping bars; at least 1.</summary>
    public int SubRowCount { get; internal set; } = 1;
}

/// <summary>An action, or the lap of a Repeat, placed in time.</summary>
public sealed class CutsceneTimelineItem
{
    internal CutsceneTimelineItem(
        CutsceneActionData action,
        CutsceneTimelineItemRole role,
        CutsceneTimelineItemShape shape,
        double start,
        double duration,
        string label,
        bool isEstimated,
        bool isMeasured)
    {
        Action = action;
        Role = role;
        Shape = shape;
        StartSeconds = start;
        DurationSeconds = duration;
        Label = label;
        IsEstimated = isEstimated;
        IsMeasured = isMeasured;
    }

    /// <summary>The action of the edited tree.</summary>
    public CutsceneActionData Action { get; }

    public CutsceneTimelineItemRole Role { get; }

    public CutsceneTimelineItemShape Shape { get; }

    /// <summary>The index of the row in <see cref="CutsceneTimelineLayout.Rows"/>.</summary>
    public int RowIndex { get; internal set; }

    /// <summary>The sub-row inside the row, 0 for the first.</summary>
    public int SubRow { get; internal set; }

    public double StartSeconds { get; }

    /// <summary>Zero for a marker.</summary>
    public double DurationSeconds { get; internal set; }

    /// <summary>The label, taken from <see cref="CutsceneActionSummary"/> for an action.</summary>
    public string Label { get; }

    /// <summary>True when the duration is a guess (frames converted at 60 per second, or an unmeasured duration).</summary>
    public bool IsEstimated { get; }

    /// <summary>True when the duration comes from a play.</summary>
    public bool IsMeasured { get; }

    public double EndSeconds => StartSeconds + DurationSeconds;

    /// <summary>
    /// True for the item that stands for the action itself: every item except the "more laps" range of a Repeat. Only a
    /// primary item can be dragged in time (P5).
    /// </summary>
    public bool IsPrimary => Role != CutsceneTimelineItemRole.RepeatMoreLaps;

    internal CutsceneTimelineRowKind RowKind { get; init; }

    internal string? EntityKey { get; init; }
}

/// <summary>The rows and dated items of a cutscene, built by <see cref="CutsceneTimelineProjection"/>.</summary>
public sealed class CutsceneTimelineLayout
{
    private readonly IReadOnlyDictionary<CutsceneActionData, double>? _containerStarts;

    public static CutsceneTimelineLayout Empty { get; } = new([], [], 0, null, null);

    internal CutsceneTimelineLayout(
        IReadOnlyList<CutsceneTimelineRow> rows,
        IReadOnlyList<CutsceneTimelineItem> items,
        double durationSeconds,
        double? loopStartSeconds,
        double? loopEndSeconds,
        IReadOnlyDictionary<CutsceneActionData, double>? containerStarts = null)
    {
        _containerStarts = containerStarts;
        Rows = rows;
        Items = items;
        DurationSeconds = durationSeconds;
        LoopStartSeconds = loopStartSeconds;
        LoopEndSeconds = loopEndSeconds;
    }

    public IReadOnlyList<CutsceneTimelineRow> Rows { get; }

    /// <summary>The items in drawing order: ranges, then bars, then markers.</summary>
    public IReadOnlyList<CutsceneTimelineItem> Items { get; }

    public double DurationSeconds { get; }

    /// <summary>When the cutscene ends with an endless loop: where the drawn lap starts.</summary>
    public double? LoopStartSeconds { get; }

    /// <summary>When the cutscene ends with an endless loop: where the drawn lap ends.</summary>
    public double? LoopEndSeconds { get; }

    /// <summary>
    /// The time at which a container (Sequence, Parallel, Repeat or At), empty or not, starts in this layout. For a
    /// container inside a Repeat this is its start in the drawn lap. False for any other action, and for an action that is
    /// not in the layout.
    /// </summary>
    public bool TryGetContainerStart(CutsceneActionData container, out double startSeconds)
    {
        ArgumentNullException.ThrowIfNull(container);

        if (_containerStarts != null && _containerStarts.TryGetValue(container, out startSeconds))
        {
            return true;
        }

        startSeconds = 0;
        return false;
    }

    /// <summary>
    /// The playhead position for a play time. For an endless loop whose lap lasts, the time wraps onto the drawn lap
    /// (ADR-0137); otherwise it is the time itself.
    /// </summary>
    public double MapPlayTime(double seconds)
    {
        if (LoopStartSeconds is not { } start || LoopEndSeconds is not { } end)
        {
            return seconds;
        }

        double lap = end - start;
        if (lap <= 0 || seconds <= start)
        {
            return seconds;
        }

        return start + ((seconds - start) % lap);
    }
}
