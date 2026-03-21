namespace CasaEngine.EditorServices.ScreenEditor.DocumentModel;

public sealed class UIScreenDocument
{
    public string SchemaVersion { get; set; } = "1";

    public UIScreenNode? Root { get; private set; }

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
}