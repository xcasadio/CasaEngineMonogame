using CasaEngine.Framework.Cutscenes;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>
/// Navigation in the action tree of a cutscene: parent and index of an action, path of an action, and where a new action
/// goes (ADR-0097). Actions are compared by reference.
/// </summary>
public static class CutsceneActionTree
{
    /// <summary>True when <paramref name="action"/> is <paramref name="subtreeRoot"/> or one of its descendants.</summary>
    public static bool Contains(CutsceneActionData subtreeRoot, CutsceneActionData action)
    {
        ArgumentNullException.ThrowIfNull(subtreeRoot);
        ArgumentNullException.ThrowIfNull(action);

        if (ReferenceEquals(subtreeRoot, action))
        {
            return true;
        }

        var children = CutsceneLoadFidelity.GetChildren(subtreeRoot);
        if (children == null)
        {
            return false;
        }

        for (int index = 0; index < children.Count; index++)
        {
            if (Contains(children[index], action))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Finds the container that holds an action and the action's index in it. False for the root, and for an action that
    /// is not in the tree.
    /// </summary>
    public static bool TryFindParent(CutsceneActionData root, CutsceneActionData action, out CutsceneActionData parent, out int index)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(action);

        var children = CutsceneLoadFidelity.GetChildren(root);
        if (children != null)
        {
            for (int child = 0; child < children.Count; child++)
            {
                if (ReferenceEquals(children[child], action))
                {
                    parent = root;
                    index = child;
                    return true;
                }

                if (TryFindParent(children[child], action, out parent, out index))
                {
                    return true;
                }
            }
        }

        parent = root;
        index = -1;
        return false;
    }

    /// <summary>
    /// The path of an action in the format of the validator, such as <c>root_action.actions[1]</c>; null when the action
    /// is not in the tree.
    /// </summary>
    public static string? GetPath(CutsceneActionData root, CutsceneActionData action)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(action);

        return TryGetPath(root, action, "root_action", out string path) ? path : null;
    }

    /// <summary>
    /// Finds the action at a path of the format of <see cref="GetPath"/> (<c>root_action</c>, <c>root_action.actions[1].actions[2]</c>).
    /// False when the path is malformed or leads out of the tree.
    /// </summary>
    public static bool TryResolvePath(CutsceneActionData root, string path, out CutsceneActionData action)
    {
        ArgumentNullException.ThrowIfNull(root);

        action = root;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        const string rootToken = "root_action";
        string trimmed = path.Trim();
        if (!trimmed.StartsWith(rootToken, StringComparison.Ordinal))
        {
            return false;
        }

        const string segmentPrefix = ".actions[";
        int position = rootToken.Length;
        var current = root;
        while (position < trimmed.Length)
        {
            if (string.CompareOrdinal(trimmed, position, segmentPrefix, 0, segmentPrefix.Length) != 0)
            {
                return false;
            }

            int numberStart = position + segmentPrefix.Length;
            int close = trimmed.IndexOf(']', numberStart);
            if (close < 0
                || !int.TryParse(trimmed.AsSpan(numberStart, close - numberStart), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int childIndex))
            {
                return false;
            }

            var children = CutsceneLoadFidelity.GetChildren(current);
            if (children == null || childIndex >= children.Count)
            {
                return false;
            }

            current = children[childIndex];
            position = close + 1;
        }

        action = current;
        return true;
    }

    /// <summary>The number of actions of the tree, root included.</summary>
    public static int CountActions(CutsceneActionData root)
    {
        ArgumentNullException.ThrowIfNull(root);

        int count = 1;
        var children = CutsceneLoadFidelity.GetChildren(root);
        if (children != null)
        {
            for (int index = 0; index < children.Count; index++)
            {
                count += CountActions(children[index]);
            }
        }

        return count;
    }

    /// <summary>
    /// Where a new action goes (P2). A selected container receives it as its last child; any other selected action
    /// receives it just after itself, in its own parent; without a selection it goes at the end of the root, when the
    /// root is a container.
    /// </summary>
    /// <param name="selection">The selected action, or null.</param>
    /// <param name="parent">The container that receives the action.</param>
    /// <param name="index">The index of the new action in the container.</param>
    /// <param name="error">Why there is no target.</param>
    public static bool TryGetInsertionTarget(CutsceneActionData? root, CutsceneActionData? selection, out CutsceneActionData parent, out int index, out string error)
    {
        parent = null!;
        index = -1;
        error = string.Empty;

        if (root == null)
        {
            error = "The cutscene has no root action.";
            return false;
        }

        if (selection == null)
        {
            if (!CutsceneActionCatalog.CanHaveChildren(root))
            {
                error = $"The root action is a {root.Type}, which cannot hold actions.";
                return false;
            }

            parent = root;
            index = CutsceneActionCatalog.GetChildren(root).Count;
            return true;
        }

        if (!Contains(root, selection))
        {
            error = "The selected action is not in the cutscene.";
            return false;
        }

        if (CutsceneActionCatalog.CanHaveChildren(selection))
        {
            parent = selection;
            index = CutsceneActionCatalog.GetChildren(selection).Count;
            return true;
        }

        if (!TryFindParent(root, selection, out parent, out int selectionIndex))
        {
            error = $"The root action is a {root.Type}, which cannot hold actions.";
            return false;
        }

        index = selectionIndex + 1;
        return true;
    }

    private static bool TryGetPath(CutsceneActionData current, CutsceneActionData action, string path, out string result)
    {
        if (ReferenceEquals(current, action))
        {
            result = path;
            return true;
        }

        var children = CutsceneLoadFidelity.GetChildren(current);
        if (children != null)
        {
            for (int index = 0; index < children.Count; index++)
            {
                if (TryGetPath(children[index], action, $"{path}.actions[{index}]", out result))
                {
                    return true;
                }
            }
        }

        result = string.Empty;
        return false;
    }
}
