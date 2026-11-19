using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Cutscenes;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>
/// Removes an action, and its children, from the tree. The load losses of the removed subtree leave the session with it
/// and come back at their original place in the list when the removal is undone, so that the save guard says the same
/// thing before the removal and after its undo. The root cannot be removed (P3).
/// </summary>
public sealed class RemoveCutsceneActionCommand : IEditorCommand
{
    private readonly CutsceneEditSession _session;
    private readonly List<CutsceneActionData> _children;
    private readonly int _index;
    private readonly List<(int Index, CutsceneLoadLoss Loss)> _removedLosses = [];

    /// <exception cref="ArgumentException">The action is the root, or is not in the tree.</exception>
    public RemoveCutsceneActionCommand(CutsceneEditSession session, CutsceneActionData action)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(action);

        string? refusal = GetRefusal(session, action);
        if (refusal != null)
        {
            throw new ArgumentException(refusal, nameof(action));
        }

        CutsceneActionTree.TryFindParent(session.Asset.RootAction!, action, out var parent, out _index);
        _children = CutsceneLoadFidelity.GetChildren(parent)!;
        _session = session;
        Action = action;
        Description = $"Remove {action.Type}";

        for (int index = 0; index < session.LoadLosses.Count; index++)
        {
            var loss = session.LoadLosses[index];
            if (loss.Action != null && CutsceneActionTree.Contains(action, loss.Action))
            {
                _removedLosses.Add((index, loss));
            }
        }
    }

    public string Description { get; }

    /// <summary>The removed action; the same instance is put back by <see cref="Undo"/>.</summary>
    public CutsceneActionData Action { get; }

    /// <summary>The reason an action cannot be removed, or null when it can.</summary>
    public static string? GetRefusal(CutsceneEditSession session, CutsceneActionData action)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(action);

        var root = session.Asset.RootAction;
        if (root == null || !CutsceneActionTree.Contains(root, action))
        {
            return "The action is not in the cutscene.";
        }

        return ReferenceEquals(root, action) ? "The root action cannot be removed." : null;
    }

    public void Execute()
    {
        _children.RemoveAt(_index);
        for (int index = 0; index < _removedLosses.Count; index++)
        {
            _session.RemoveLoadLoss(_removedLosses[index].Loss);
        }
    }

    public void Undo()
    {
        _children.Insert(_index, Action);
        for (int index = 0; index < _removedLosses.Count; index++)
        {
            _session.InsertLoadLoss(_removedLosses[index].Index, _removedLosses[index].Loss);
        }
    }
}
