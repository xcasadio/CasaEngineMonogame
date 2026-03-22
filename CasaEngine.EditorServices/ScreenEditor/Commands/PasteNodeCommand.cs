using CasaEngine.EditorServices.ScreenEditor.DocumentModel;

namespace CasaEngine.EditorServices.ScreenEditor.Commands;

/// <summary>
/// Inserts a pre-built node (e.g. parsed from clipboard) as the next sibling of a target node.
/// If no target is provided, the node is inserted as the document root or into a StackPanel wrapper.
/// </summary>
public sealed class PasteNodeCommand : IUIScreenCommand
{
    private readonly UIScreenDocument _document;
    private readonly UIScreenNode _nodeToInsert;
    private readonly DocumentNodeId? _targetSiblingId;

    private UIScreenNode? _originalRoot;

    public PasteNodeCommand(UIScreenDocument document, UIScreenNode nodeToInsert, DocumentNodeId? targetSiblingId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(nodeToInsert);
        _document = document;
        _nodeToInsert = nodeToInsert;
        _targetSiblingId = targetSiblingId;
    }

    public string Description => "Paste node";

    /// <summary>The inserted node (same as <see cref="_nodeToInsert"/>).</summary>
    public UIScreenNode InsertedNode => _nodeToInsert;

    public void Execute()
    {
        var target = _targetSiblingId.HasValue ? _document.FindNode(_targetSiblingId.Value) : null;

        if (target?.Parent is { } parent)
        {
            var idx = IndexOf(parent, target);
            InsertAt(parent, _nodeToInsert, idx + 1);
        }
        else if (_document.Root != null)
        {
            // Insert after root (wrap both in StackPanel)
            _originalRoot = _document.Root;
            var wrapper = new UIScreenNode("MGStackPanel");
            _document.SetRoot(wrapper);
            wrapper.AddChild(_originalRoot);
            wrapper.AddChild(_nodeToInsert);
        }
        else
        {
            _document.SetRoot(_nodeToInsert);
        }
    }

    public void Undo()
    {
        if (_originalRoot != null)
        {
            _document.SetRoot(_originalRoot);
            _originalRoot = null;
        }
        else
        {
            _nodeToInsert.DetachFromParent();
            if (ReferenceEquals(_document.Root, _nodeToInsert))
            {
                _document.ClearRoot();
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
            if (ReferenceEquals(parent.Children[i], child)) return i;
        }

        return parent.Children.Count - 1;
    }

    private static void InsertAt(UIScreenNode parent, UIScreenNode child, int index)
    {
        var count = parent.Children.Count;
        if (index >= count)
        {
            parent.AddChild(child);
            return;
        }

        var tail = new UIScreenNode[count - index];
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
