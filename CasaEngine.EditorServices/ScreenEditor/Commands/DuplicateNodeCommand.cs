using CasaEngine.EditorServices.ScreenEditor.DocumentModel;

namespace CasaEngine.EditorServices.ScreenEditor.Commands;

/// <summary>
/// Deep-clones a node and inserts the clone as the next sibling.
/// If the node is the document root, both it and the clone are wrapped in a new MGStackPanel.
/// </summary>
public sealed class DuplicateNodeCommand : IUIScreenCommand
{
    private readonly UIScreenDocument _document;
    private readonly DocumentNodeId _sourceId;

    private UIScreenNode? _clone;
    private UIScreenNode? _originalRoot; // non-null only in the root-wrap code path

    public DuplicateNodeCommand(UIScreenDocument document, DocumentNodeId sourceId)
    {
        ArgumentNullException.ThrowIfNull(document);
        _document = document;
        _sourceId = sourceId;
    }

    public string Description => "Duplicate node";

    /// <summary>The cloned node, available after <see cref="Execute"/>.</summary>
    public UIScreenNode? CreatedNode => _clone;

    public void Execute()
    {
        var source = _document.FindNode(_sourceId);
        if (source == null) return;

        _clone = source.DeepClone();

        if (source.Parent is { } parent)
        {
            // Insert clone right after source
            var idx = IndexOf(parent, source);
            InsertAt(parent, _clone, idx + 1);
        }
        else
        {
            // Source is the document root — wrap in a new StackPanel
            _originalRoot = source;
            var wrapper = new UIScreenNode("MGStackPanel");
            _document.SetRoot(wrapper);
            wrapper.AddChild(source);
            wrapper.AddChild(_clone);
        }
    }

    public void Undo()
    {
        if (_clone == null) return;

        if (_originalRoot != null)
        {
            _document.SetRoot(_originalRoot);
            _originalRoot = null;
        }
        else
        {
            _clone.DetachFromParent();
        }

        _clone = null;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Helpers  (mirror of RemoveNodeCommand.InsertAt)
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
