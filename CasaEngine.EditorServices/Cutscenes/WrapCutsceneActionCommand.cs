using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Cutscenes;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>
/// Wraps an action in a new container (Sequence, Parallel or Repeat): the container, created by the catalogue, takes the
/// place of the action, which becomes its only child. The same action instance stays in the tree, so it keeps its load
/// losses. The root can be wrapped: the container becomes the new root (P2).
/// </summary>
public sealed class WrapCutsceneActionCommand : IEditorCommand
{
    private readonly CutsceneEditSession _session;
    private readonly List<CutsceneActionData>? _parentChildren;
    private readonly int _index;

    /// <exception cref="ArgumentException">The wrap is refused, see <see cref="GetRefusal"/>.</exception>
    public WrapCutsceneActionCommand(CutsceneEditSession session, CutsceneActionData action, string containerType)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(containerType);

        string? refusal = GetRefusal(session, action, containerType);
        if (refusal != null)
        {
            throw new ArgumentException(refusal, nameof(action));
        }

        if (CutsceneActionTree.TryFindParent(session.Asset.RootAction!, action, out var parent, out _index))
        {
            _parentChildren = CutsceneLoadFidelity.GetChildren(parent)!;
        }
        else
        {
            _index = -1;
        }

        _session = session;
        Action = action;
        Container = CutsceneActionCatalog.Create(containerType);
        Description = $"Wrap {action.Type} in {containerType}";
    }

    public string Description { get; }

    /// <summary>The wrapped action.</summary>
    public CutsceneActionData Action { get; }

    /// <summary>The new container; the same instance goes back into the tree when the command is redone.</summary>
    public CutsceneActionData Container { get; }

    /// <summary>
    /// The reason an action cannot be wrapped in a container of a type, or null when it can. Refused: an action that is not
    /// in the cutscene, and a type that is not a container type of the catalogue.
    /// </summary>
    public static string? GetRefusal(CutsceneEditSession session, CutsceneActionData action, string containerType)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(containerType);

        var root = session.Asset.RootAction;
        if (root == null || !CutsceneActionTree.Contains(root, action))
        {
            return "The action is not in the cutscene.";
        }

        return IsContainerType(containerType) ? null : $"'{containerType}' is not a container type.";
    }

    /// <summary>True when the type is in the catalogue and its actions hold a list of children.</summary>
    public static bool IsContainerType(string type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var kinds = CutsceneActionCatalog.Kinds;
        for (int index = 0; index < kinds.Count; index++)
        {
            if (kinds[index].Type == type)
            {
                return CutsceneActionCatalog.CanHaveChildren(CutsceneActionCatalog.Create(type));
            }
        }

        return false;
    }

    public void Execute()
    {
        CutsceneLoadFidelity.GetChildren(Container)!.Add(Action);
        if (_parentChildren == null)
        {
            _session.Asset.RootAction = Container;
        }
        else
        {
            _parentChildren[_index] = Container;
        }
    }

    public void Undo()
    {
        CutsceneLoadFidelity.GetChildren(Container)!.Remove(Action);
        if (_parentChildren == null)
        {
            _session.Asset.RootAction = Action;
        }
        else
        {
            _parentChildren[_index] = Action;
        }
    }
}
