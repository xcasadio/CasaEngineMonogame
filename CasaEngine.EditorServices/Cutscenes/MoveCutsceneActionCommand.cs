using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Cutscenes;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>
/// Moves an action, and its children, to another place of the tree. The same instance changes place, with no copy, so it
/// keeps its load losses and everything attached to the instance. The destination is a container and an index among its
/// children before the move: moving an action to the index of its own position, or just after it, changes nothing and
/// is refused (P1).
/// </summary>
public sealed class MoveCutsceneActionCommand : IEditorCommand
{
    private readonly List<CutsceneActionData> _sourceChildren;
    private readonly int _sourceIndex;
    private readonly List<CutsceneActionData> _destinationChildren;
    private readonly int _destinationIndex;

    /// <exception cref="ArgumentException">The move is refused, see <see cref="GetRefusal"/>.</exception>
    public MoveCutsceneActionCommand(CutsceneEditSession session, CutsceneActionData action, CutsceneActionData destination, int index)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(destination);

        string? refusal = GetRefusal(session, action, destination, index);
        if (refusal != null)
        {
            throw new ArgumentException(refusal, nameof(action));
        }

        CutsceneActionTree.TryFindParent(session.Asset.RootAction!, action, out var source, out _sourceIndex);
        _sourceChildren = CutsceneLoadFidelity.GetChildren(source)!;
        _destinationChildren = CutsceneLoadFidelity.GetChildren(destination)!;

        // The index counts the children before the move: once the action leaves the same list from an earlier place, the
        // children after it are one place closer to the start.
        _destinationIndex = ReferenceEquals(_sourceChildren, _destinationChildren) && _sourceIndex < index ? index - 1 : index;
        Action = action;
        Destination = destination;
        Description = $"Move {action.Type}";
    }

    public string Description { get; }

    /// <summary>The moved action; the same instance goes back by <see cref="Undo"/>.</summary>
    public CutsceneActionData Action { get; }

    /// <summary>The container that receives the action.</summary>
    public CutsceneActionData Destination { get; }

    /// <summary>
    /// The reason an action cannot be moved to a place, or null when it can. The index counts the children of
    /// <paramref name="destination"/> before the move. Refused: an action or a destination that is not in the cutscene, the
    /// root, a destination that is not a container, a destination that is the action itself or one of its descendants,
    /// an index outside the child list, and a move that changes nothing.
    /// </summary>
    public static string? GetRefusal(CutsceneEditSession session, CutsceneActionData action, CutsceneActionData destination, int index)
    {
        ArgumentNullException.ThrowIfNull(session);

        return GetRefusal(session.Asset.RootAction, action, destination, index);
    }

    /// <summary>The same refusal for a tree given by its root, with no session. A null root holds no action.</summary>
    public static string? GetRefusal(CutsceneActionData? root, CutsceneActionData action, CutsceneActionData destination, int index)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(destination);

        if (root == null || !CutsceneActionTree.Contains(root, action))
        {
            return "The action is not in the cutscene.";
        }

        if (ReferenceEquals(root, action))
        {
            return "The root action cannot be moved.";
        }

        if (!CutsceneActionTree.Contains(root, destination))
        {
            return "The destination is not in the cutscene.";
        }

        var destinationChildren = CutsceneLoadFidelity.GetChildren(destination);
        if (destinationChildren == null)
        {
            return $"A {destination.Type} action cannot hold actions.";
        }

        if (CutsceneActionTree.Contains(action, destination))
        {
            return "An action cannot be moved into itself or into one of its own actions.";
        }

        if (index < 0 || index > destinationChildren.Count)
        {
            return $"The index must be between 0 and {destinationChildren.Count}.";
        }

        CutsceneActionTree.TryFindParent(root, action, out var source, out int sourceIndex);
        if (ReferenceEquals(source, destination) && (index == sourceIndex || index == sourceIndex + 1))
        {
            return "The action is already at this place.";
        }

        return null;
    }

    public void Execute()
    {
        _sourceChildren.RemoveAt(_sourceIndex);
        _destinationChildren.Insert(_destinationIndex, Action);
    }

    public void Undo()
    {
        _destinationChildren.RemoveAt(_destinationIndex);
        _sourceChildren.Insert(_sourceIndex, Action);
    }
}
