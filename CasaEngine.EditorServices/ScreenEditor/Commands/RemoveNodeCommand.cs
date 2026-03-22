using CasaEngine.EditorServices.ScreenEditor.DocumentModel;

namespace CasaEngine.EditorServices.ScreenEditor.Commands;

/// <summary>
/// Reversible command that removes a <see cref="UIScreenNode"/> from the document.
/// </summary>
public sealed class RemoveNodeCommand : IUIScreenCommand
{
    private readonly UIScreenDocument _document;
    private readonly UIScreenNode _node;

    // Captured during Execute for Undo
    private UIScreenNode? _originalParent;
    private int _originalIndex;
    private bool _wasRoot;

    public string Description => $"Delete {_node.ControlType}";

    public RemoveNodeCommand(UIScreenDocument document, UIScreenNode node)
    {
        _document = document;
        _node     = node;
    }

    public void Execute()
    {
        _originalParent = _node.Parent;
        _wasRoot = ReferenceEquals(_document.Root, _node);

        if (_wasRoot)
        {
            _originalIndex = -1;
            _document.ClearRoot();
        }
        else if (_originalParent != null)
        {
            _originalIndex = IndexOf(_originalParent, _node);
            _originalParent.RemoveChild(_node);
        }
    }

    public void Undo()
    {
        if (_wasRoot)
        {
            _document.SetRoot(_node);
        }
        else if (_originalParent != null)
        {
            // Re-insert at original position if possible; if the parent's children shifted
            // due to other edits, this does a best-effort append.
            if (_originalIndex >= 0 && _originalIndex < _originalParent.Children.Count)
            {
                // UIScreenNode doesn't have InsertChild; use RemoveThenReAdd via helper
                InsertAt(_originalParent, _node, _originalIndex);
            }
            else
            {
                _originalParent.AddChild(_node);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────

    private static int IndexOf(UIScreenNode parent, UIScreenNode child)
    {
        for (int i = 0; i < parent.Children.Count; i++)
        {
            if (ReferenceEquals(parent.Children[i], child))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Inserts <paramref name="child"/> at <paramref name="index"/> by detaching all
    /// existing children beyond that index, re-inserting the target, then re-attaching.
    /// </summary>
    private static void InsertAt(UIScreenNode parent, UIScreenNode child, int index)
    {
        var tail = new UIScreenNode[parent.Children.Count - index];
        for (int i = 0; i < tail.Length; i++)
        {
            tail[i] = parent.Children[index];
            parent.RemoveChild(tail[i]);
        }

        parent.AddChild(child);

        foreach (var item in tail)
        {
            parent.AddChild(item);
        }
    }
}
