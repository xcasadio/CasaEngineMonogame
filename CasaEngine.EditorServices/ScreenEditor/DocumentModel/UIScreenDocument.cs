namespace CasaEngine.EditorServices.ScreenEditor.DocumentModel;

public sealed class UIScreenDocument
{
    public string SchemaVersion { get; set; } = "1";

    public UIScreenNode? Root { get; private set; }

    /// <summary>
    /// Named resource entries (analogous to WPF ResourceDictionary) that will be
    /// serialized inside <c>Window.Resources</c> in the XAML output.
    /// </summary>
    public List<UIScreenResourceEntry> Resources { get; } = new();

    public void SetRoot(UIScreenNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        root.DetachFromParent();
        Root = root;
    }

    public void ClearRoot()
    {
        Root = null;
    }

    public UIScreenNode? FindNode(DocumentNodeId id)
    {
        if (Root == null)
        {
            return null;
        }

        return FindNode(Root, id);
    }

    private static UIScreenNode? FindNode(UIScreenNode node, DocumentNodeId id)
    {
        if (node.Id == id)
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            var result = FindNode(child, id);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}