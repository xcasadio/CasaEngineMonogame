using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Cutscenes;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>
/// Replaces a container that is not the root by its children, in order, at its place; an empty container is simply
/// removed. What belongs to the container itself is lost with it, such as the laps of a Repeat, and its load losses leave
/// the session; the undo puts back the same container, with the same children in the same order, and its load losses at
/// their place in the list. The load losses of the children do not move (P2).
/// </summary>
public sealed class UngroupCutsceneActionCommand : IEditorCommand
{
    private readonly CutsceneEditSession _session;
    private readonly List<CutsceneActionData> _parentChildren;
    private readonly int _index;
    private readonly List<CutsceneActionData> _containerChildren;
    private readonly List<(int Index, CutsceneLoadLoss Loss)> _removedLosses = [];

    /// <exception cref="ArgumentException">The ungroup is refused, see <see cref="GetRefusal"/>.</exception>
    public UngroupCutsceneActionCommand(CutsceneEditSession session, CutsceneActionData container)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(container);

        string? refusal = GetRefusal(session, container);
        if (refusal != null)
        {
            throw new ArgumentException(refusal, nameof(container));
        }

        CutsceneActionTree.TryFindParent(session.Asset.RootAction!, container, out var parent, out _index);
        _parentChildren = CutsceneLoadFidelity.GetChildren(parent)!;
        _containerChildren = CutsceneLoadFidelity.GetChildren(container)!;
        _session = session;
        Container = container;
        Description = $"Ungroup {container.Type}";

        for (int index = 0; index < session.LoadLosses.Count; index++)
        {
            var loss = session.LoadLosses[index];
            if (ReferenceEquals(loss.Action, container))
            {
                _removedLosses.Add((index, loss));
            }
        }
    }

    public string Description { get; }

    /// <summary>The ungrouped container; the same instance goes back by <see cref="Undo"/>.</summary>
    public CutsceneActionData Container { get; }

    /// <summary>
    /// The reason a container cannot be ungrouped, or null when it can. Refused: an action that is not in the cutscene,
    /// the root, and an action that is not a container.
    /// </summary>
    public static string? GetRefusal(CutsceneEditSession session, CutsceneActionData container)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(container);

        var root = session.Asset.RootAction;
        if (root == null || !CutsceneActionTree.Contains(root, container))
        {
            return "The action is not in the cutscene.";
        }

        if (ReferenceEquals(root, container))
        {
            return "The root action cannot be ungrouped.";
        }

        return CutsceneActionCatalog.CanHaveChildren(container) ? null : $"A {container.Type} action holds no actions to ungroup.";
    }

    public void Execute()
    {
        _parentChildren.RemoveAt(_index);
        _parentChildren.InsertRange(_index, _containerChildren);
        for (int index = 0; index < _removedLosses.Count; index++)
        {
            _session.RemoveLoadLoss(_removedLosses[index].Loss);
        }
    }

    public void Undo()
    {
        _parentChildren.RemoveRange(_index, _containerChildren.Count);
        _parentChildren.Insert(_index, Container);
        for (int index = 0; index < _removedLosses.Count; index++)
        {
            _session.InsertLoadLoss(_removedLosses[index].Index, _removedLosses[index].Loss);
        }
    }
}
