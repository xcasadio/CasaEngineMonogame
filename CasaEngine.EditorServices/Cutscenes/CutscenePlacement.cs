using CasaEngine.Framework.Cutscenes;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>What dragging an item of the cutscene timeline in time does to the action tree (P5).</summary>
public enum CutscenePlacementKind
{
    /// <summary>Nothing: the item cannot be dragged.</summary>
    None,

    /// <summary>The drag changes the <c>seconds</c> of an existing At action.</summary>
    ChangeAt,

    /// <summary>The drag wraps a node, a child of a Parallel, in a new At action.</summary>
    WrapInAt,
}

/// <summary>
/// The outcome of <see cref="CutscenePlacement.Resolve"/>: the kind of drag, and the nodes it acts on.
/// </summary>
public readonly struct CutscenePlacementRule
{
    /// <summary>The rule that says nothing can be dragged.</summary>
    public static CutscenePlacementRule None => default;

    private CutscenePlacementRule(CutscenePlacementKind kind, AtCutsceneActionData? at, CutsceneActionData? node, ParallelCutsceneActionData? parallel)
    {
        Kind = kind;
        At = at;
        Node = node;
        Parallel = parallel;
    }

    public CutscenePlacementKind Kind { get; }

    /// <summary>For <see cref="CutscenePlacementKind.ChangeAt"/>: the At whose <c>seconds</c> changes.</summary>
    public AtCutsceneActionData? At { get; }

    /// <summary>For <see cref="CutscenePlacementKind.WrapInAt"/>: the node that goes inside the new At.</summary>
    public CutsceneActionData? Node { get; }

    /// <summary>For <see cref="CutscenePlacementKind.WrapInAt"/>: the Parallel that holds <see cref="Node"/>.</summary>
    public ParallelCutsceneActionData? Parallel { get; }

    /// <summary>True when the item can be dragged.</summary>
    public bool CanDrag => Kind != CutscenePlacementKind.None;

    internal static CutscenePlacementRule Change(AtCutsceneActionData at) => new(CutscenePlacementKind.ChangeAt, at, null, null);

    internal static CutscenePlacementRule Wrap(CutsceneActionData node, ParallelCutsceneActionData parallel)
        => new(CutscenePlacementKind.WrapInAt, null, node, parallel);
}

/// <summary>
/// Pure computations for placing the actions of a cutscene in time on the timeline (ADR-0102): which items can be
/// dragged and what a drag changes, and the conversion of a dropped time into the <c>seconds</c> of an At.
/// </summary>
public static class CutscenePlacement
{
    /// <summary>The timeline aligns a dragged time on this many frames per second.</summary>
    public const double FramesPerSecond = CutsceneTimelineProjection.FramesPerSecond;

    /// <summary>
    /// Applies the rule of P5 to an action. Starting from the action, it goes up to the first parent that decides:
    /// an At whose first child the node is changes that At, and an At whose later child the node is gives nothing; a
    /// Parallel wraps the node in a new At; a Sequence or a Repeat whose first child the node is hands the decision to its
    /// own parent with itself as the node, and gives nothing when the node is a later child; the root gives nothing. An
    /// action that is not in the tree gives nothing. It depends only on the tree.
    /// </summary>
    public static CutscenePlacementRule Resolve(CutsceneActionData root, CutsceneActionData action)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(action);

        var node = action;
        while (CutsceneActionTree.TryFindParent(root, node, out var parent, out int index))
        {
            switch (parent)
            {
                case AtCutsceneActionData at:
                    return index == 0 ? CutscenePlacementRule.Change(at) : CutscenePlacementRule.None;

                case ParallelCutsceneActionData parallel:
                    return CutscenePlacementRule.Wrap(node, parallel);

                case SequenceCutsceneActionData or RepeatCutsceneActionData:
                    if (index != 0)
                    {
                        return CutscenePlacementRule.None;
                    }

                    node = parent;
                    break;

                default:
                    return CutscenePlacementRule.None;
            }
        }

        return CutscenePlacementRule.None;
    }

    /// <summary>
    /// The rule of a timeline item: <see cref="CutscenePlacementRule.None"/> for an item that is not the primary item of
    /// its action (the "more laps" range of a Repeat), otherwise the rule of its action.
    /// </summary>
    public static CutscenePlacementRule Resolve(CutsceneActionData root, CutsceneTimelineItem item)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(item);

        return item.IsPrimary ? Resolve(root, item.Action) : CutscenePlacementRule.None;
    }

    /// <summary>
    /// Converts a dropped time into the new <c>seconds</c> (P6), by difference: the current <c>seconds</c> (0 when the
    /// node is wrapped) plus the move of the item (dropped time minus the current start of the item), aligned on
    /// 1/60 s and not below 0. A value that is not a finite number leaves the current <c>seconds</c>, aligned.
    /// </summary>
    /// <param name="currentSeconds">The <c>seconds</c> of the At now; 0 for a wrap.</param>
    /// <param name="itemStartSeconds">The start of the item in the timeline now.</param>
    /// <param name="dropSeconds">The time the item is dropped at.</param>
    public static double ConvertDrop(double currentSeconds, double itemStartSeconds, double dropSeconds)
    {
        double current = double.IsFinite(currentSeconds) && currentSeconds > 0 ? currentSeconds : 0;
        if (!double.IsFinite(itemStartSeconds) || !double.IsFinite(dropSeconds))
        {
            return Align(current);
        }

        return Align(current + (dropSeconds - itemStartSeconds));
    }

    /// <summary>
    /// The lowest time an item can be dragged to: the time at which the <c>seconds</c> would be 0, that is the current
    /// start of the item minus the current <c>seconds</c> (0 for a wrap). Never below 0.
    /// </summary>
    public static double LowestInstant(double currentSeconds, double itemStartSeconds)
    {
        double current = double.IsFinite(currentSeconds) && currentSeconds > 0 ? currentSeconds : 0;
        double start = double.IsFinite(itemStartSeconds) ? itemStartSeconds : 0;
        return Math.Max(0, start - current);
    }

    private static double Align(double seconds)
    {
        if (!(seconds > 0))
        {
            return 0;
        }

        return Math.Round(seconds * FramesPerSecond, MidpointRounding.AwayFromZero) / FramesPerSecond;
    }
}
