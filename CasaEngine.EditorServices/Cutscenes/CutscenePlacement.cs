using System.Globalization;
using CasaEngine.EditorServices.History;
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
/// Placing the actions of a cutscene in time on the timeline (ADR-0102): pure computations (which items can be dragged
/// and what a drag changes, the conversion of a dropped time into the <c>seconds</c> of an At) and the undoable commands
/// that place an action at a time and add an action at a time.
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

    /// <summary>
    /// The reason an action cannot be placed at a time, or null when it can. Refused: a time that is not a finite number
    /// or is below 0, an action that is not in the cutscene, an action that no drag moves (<see cref="Resolve(CutsceneActionData, CutsceneActionData)"/>
    /// gives nothing), and a time that is already the <c>seconds</c> of the At the placement would change.
    /// </summary>
    public static string? GetPlaceRefusal(CutsceneEditSession session, CutsceneActionData action, double seconds)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(action);

        if (!double.IsFinite(seconds) || seconds < 0)
        {
            return "The time must be a number of seconds, not below 0.";
        }

        var root = session.Asset.RootAction;
        if (root == null || !CutsceneActionTree.Contains(root, action))
        {
            return "The action is not in the cutscene.";
        }

        var rule = Resolve(root, action);
        switch (rule.Kind)
        {
            case CutscenePlacementKind.ChangeAt:
                return (float)seconds == rule.At!.Seconds ? "The action is already at this time." : null;

            case CutscenePlacementKind.WrapInAt:
                return null;

            default:
                return "This action cannot be placed in time: only the first action of a group can be moved.";
        }
    }

    /// <summary>
    /// Creates the command that places an action at a time (P5), as one history entry. Depending on the rule it writes the
    /// <c>seconds</c> of the At that holds the action, or wraps the node in a new At of that many seconds; a wrap at 0
    /// seconds is allowed. Returns null, with the reason, when the placement is refused (<see cref="GetPlaceRefusal"/>).
    /// </summary>
    /// <param name="seconds">The new <c>seconds</c> of the At, already aligned (see <see cref="ConvertDrop"/>).</param>
    public static IEditorCommand? TryCreatePlace(CutsceneEditSession session, CutsceneActionData action, double seconds, out string error)
    {
        error = GetPlaceRefusal(session, action, seconds) ?? string.Empty;
        if (error.Length > 0)
        {
            return null;
        }

        var rule = Resolve(session.Asset.RootAction!, action);
        string description = $"Place {action.Type} at {FormatSeconds(seconds)}";
        float value = (float)seconds;

        if (rule.Kind == CutscenePlacementKind.ChangeAt)
        {
            var at = rule.At!;
            var property = CutsceneActionPropertyEditor.FindProperty(at, SecondsKey)!;
            return new EditorCompositeCommand(description, new SetCutsceneActionPropertyCommand(session, at, property, value));
        }

        var wrap = new WrapCutsceneActionCommand(session, rule.Node!, CutsceneActionTypes.At);
        if (!(value > 0))
        {
            return new EditorCompositeCommand(description, wrap);
        }

        var secondsProperty = CutsceneActionPropertyEditor.FindProperty(wrap.Container, SecondsKey)!;
        return new EditorCompositeCommand(description, wrap, new SetCutsceneActionPropertyCommand(session, wrap.Container, secondsProperty, value));
    }

    /// <summary>
    /// The Parallel that receives an action added at a time (P9): the selected action when it is a Parallel, otherwise the
    /// nearest Parallel that holds it. Null when there is none, when nothing is selected, or when the selection is not in
    /// the tree.
    /// </summary>
    public static ParallelCutsceneActionData? FindTargetParallel(CutsceneActionData root, CutsceneActionData? selected)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (selected == null || !CutsceneActionTree.Contains(root, selected))
        {
            return null;
        }

        var node = selected;
        while (true)
        {
            if (node is ParallelCutsceneActionData parallel)
            {
                return parallel;
            }

            if (!CutsceneActionTree.TryFindParent(root, node, out var parent, out _))
            {
                return null;
            }

            node = parent;
        }
    }

    /// <summary>
    /// The reason an action cannot be added at a time, or null when it can. Refused: a time that is not a finite number or
    /// is below 0, a target that is not a Parallel of the cutscene, and a type that is not in the catalogue.
    /// </summary>
    public static string? GetAddRefusal(CutsceneEditSession session, CutsceneActionData target, string type, double seconds)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(type);

        if (!double.IsFinite(seconds) || seconds < 0)
        {
            return "The time must be a number of seconds, not below 0.";
        }

        var root = session.Asset.RootAction;
        if (target is not ParallelCutsceneActionData || root == null || !CutsceneActionTree.Contains(root, target))
        {
            return "The target is not a Parallel of the cutscene.";
        }

        var kinds = CutsceneActionCatalog.Kinds;
        for (int index = 0; index < kinds.Count; index++)
        {
            if (kinds[index].Type == type)
            {
                return null;
            }
        }

        return $"'{type}' is not an action type of the catalogue.";
    }

    /// <summary>
    /// Creates the command that adds an action at a time (P9), as one history entry: at the end of the Parallel, an At of
    /// that many seconds that holds a new action of the type, created by the catalogue. Returns null, with the reason, when
    /// the addition is refused (<see cref="GetAddRefusal"/>).
    /// </summary>
    /// <param name="seconds">The <c>seconds</c> of the At, already aligned and counted from the start of the Parallel.</param>
    /// <param name="created">The new action, inside the At; null when refused.</param>
    public static IEditorCommand? TryCreateAdd(CutsceneEditSession session, CutsceneActionData target, string type, double seconds, out CutsceneActionData? created, out string error)
    {
        created = null;
        error = GetAddRefusal(session, target, type, seconds) ?? string.Empty;
        if (error.Length > 0)
        {
            return null;
        }

        var at = (AtCutsceneActionData)CutsceneActionCatalog.Create(CutsceneActionTypes.At);
        at.Seconds = (float)seconds;
        created = CutsceneActionCatalog.Create(type);
        at.Actions.Add(created);

        var insert = new InsertCutsceneActionCommand(session, target, CutsceneActionCatalog.GetChildren(target).Count, at);
        return new EditorCompositeCommand($"Add {type} at {FormatSeconds(seconds)}", insert);
    }

    private const string SecondsKey = "seconds";

    private static string FormatSeconds(double seconds) => seconds.ToString("0.###", CultureInfo.InvariantCulture) + " s";

    private static double Align(double seconds)
    {
        if (!(seconds > 0))
        {
            return 0;
        }

        return Math.Round(seconds * FramesPerSecond, MidpointRounding.AwayFromZero) / FramesPerSecond;
    }
}
