using CasaEngine.Framework.Cutscenes;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>
/// Keeps a copy of one cutscene action, for every cutscene document that shares the instance (P3). It is not the
/// clipboard of the system. Each paste request gives a new copy, so that two pastes never share a list or an instance.
/// </summary>
public sealed class CutsceneClipboard
{
    private CutsceneActionData? _content;

    /// <summary>True when an action has been copied.</summary>
    public bool HasContent => _content != null;

    /// <summary>The action type of the copied action, or null when the clipboard is empty.</summary>
    public string? ContentType => _content?.Type;

    /// <summary>
    /// The reason an action of a session cannot be copied or cut, or null when it can (P3): the action is not in the
    /// tree, or its subtree holds an unknown type, a format error or a load loss that a copy would lose. Unlike a
    /// duplicate, the root can be copied.
    /// </summary>
    public static string? GetCopyRefusal(CutsceneEditSession session, CutsceneActionData action)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(action);

        var root = session.Asset.RootAction;
        if (root == null || !CutsceneActionTree.Contains(root, action))
        {
            return "The action is not in the cutscene.";
        }

        if (InsertCutsceneActionCommand.HasUnknownOrFormatError(action))
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
    /// Keeps a copy of an action and its subtree. When the copy is refused (<see cref="GetCopyRefusal"/>), returns the
    /// reason and leaves the clipboard as it was; otherwise returns null.
    /// </summary>
    public string? TryCopy(CutsceneEditSession session, CutsceneActionData action)
    {
        string? refusal = GetCopyRefusal(session, action);
        if (refusal != null)
        {
            return refusal;
        }

        _content = CutsceneActionCatalog.Clone(action);
        return null;
    }

    /// <summary>A new copy of the kept action, or null when the clipboard is empty.</summary>
    public CutsceneActionData? CreatePaste()
    {
        return _content == null ? null : CutsceneActionCatalog.Clone(_content);
    }

    /// <summary>Forgets the kept action.</summary>
    public void Clear() => _content = null;
}
