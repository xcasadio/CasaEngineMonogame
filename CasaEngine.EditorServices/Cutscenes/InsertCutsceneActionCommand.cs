using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Cutscenes;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>
/// Inserts an action in the child list of a container. Undoing removes the same instance, and redoing puts it back, so
/// that the action keeps its identity across undo and redo.
/// </summary>
public sealed class InsertCutsceneActionCommand : IEditorCommand
{
    private readonly List<CutsceneActionData> _children;
    private readonly int _index;

    /// <exception cref="ArgumentException">The parent is not a container, or the index is outside its child list.</exception>
    public InsertCutsceneActionCommand(CutsceneEditSession session, CutsceneActionData parent, int index, CutsceneActionData action)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(action);

        _children = CutsceneLoadFidelity.GetChildren(parent)
            ?? throw new ArgumentException($"A {parent.Type} action cannot hold actions.", nameof(parent));
        if (index < 0 || index > _children.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"The index must be between 0 and {_children.Count}.");
        }

        Parent = parent;
        Action = action;
        _index = index;
        Description = $"Add {action.Type}";
    }

    public string Description { get; }

    /// <summary>The container that receives the action.</summary>
    public CutsceneActionData Parent { get; }

    /// <summary>The inserted action.</summary>
    public CutsceneActionData Action { get; }

    /// <summary>
    /// The reason a duplicate of an action is refused, or null when it is allowed (P3): the root is not duplicated, and
    /// neither is a subtree that holds an unknown type, a format error or a load loss, because the clone would lose
    /// these marks.
    /// </summary>
    public static string? GetDuplicateRefusal(CutsceneEditSession session, CutsceneActionData action)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(action);

        var root = session.Asset.RootAction;
        if (root == null || !CutsceneActionTree.Contains(root, action))
        {
            return "The action is not in the cutscene.";
        }

        if (ReferenceEquals(root, action))
        {
            return "The root action cannot be duplicated.";
        }

        if (HasUnknownOrFormatError(action))
        {
            return "This action holds an unknown type or a format error that a copy would lose: fix it first.";
        }

        for (int index = 0; index < session.LoadLosses.Count; index++)
        {
            var loss = session.LoadLosses[index];
            if (loss.Action != null && CutsceneActionTree.Contains(action, loss.Action))
            {
                return "This action holds a value of the file that a copy would lose: edit that field first.";
            }
        }

        return null;
    }

    /// <summary>
    /// Creates the command that inserts a clone of an action just after it. Returns null, with the reason, when the
    /// duplicate is refused (<see cref="GetDuplicateRefusal"/>).
    /// </summary>
    public static InsertCutsceneActionCommand? TryCreateDuplicate(CutsceneEditSession session, CutsceneActionData source, out string error)
    {
        error = GetDuplicateRefusal(session, source) ?? string.Empty;
        if (error.Length > 0)
        {
            return null;
        }

        CutsceneActionTree.TryFindParent(session.Asset.RootAction!, source, out var parent, out int index);
        return new InsertCutsceneActionCommand(session, parent, index + 1, CutsceneActionCatalog.Clone(source));
    }

    public void Execute() => _children.Insert(_index, Action);

    public void Undo() => _children.RemoveAt(_index);

    private static bool HasUnknownOrFormatError(CutsceneActionData action)
    {
        if (action is UnknownCutsceneActionData)
        {
            return true;
        }

        var formatErrors = CutsceneActionPropertyEditor.GetFormatErrors(action);
        if (formatErrors is { Count: > 0 })
        {
            return true;
        }

        var children = CutsceneLoadFidelity.GetChildren(action);
        if (children != null)
        {
            for (int index = 0; index < children.Count; index++)
            {
                if (HasUnknownOrFormatError(children[index]))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
