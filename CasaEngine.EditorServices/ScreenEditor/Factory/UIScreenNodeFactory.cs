using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Toolbox;

namespace CasaEngine.EditorServices.ScreenEditor.Factory;

/// <summary>
/// Creates new <see cref="UIScreenNode"/> instances from toolbox entries and
/// inserts them into the document at the appropriate position.
/// </summary>
public static class UIScreenNodeFactory
{
    /// <summary>
    /// Creates a new node from <paramref name="entry"/> and inserts it into the document.
    /// <para>
    /// Insertion logic:
    /// <list type="bullet">
    ///   <item>If <paramref name="parentNode"/> is non-null and allows children, append as a child.</item>
    ///   <item>If the document has no root, make the new node the root.</item>
    ///   <item>Otherwise append as a sibling after <paramref name="parentNode"/>.</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="document">Target document.</param>
    /// <param name="entry">Toolbox entry describing the control type and default properties.</param>
    /// <param name="parentNode">
    /// The node that should receive the new child, or null to add at document root.
    /// </param>
    /// <returns>The newly created node.</returns>
    public static UIScreenNode Create(
        UIScreenDocument document,
        UIControlRegistryEntry entry,
        UIScreenNode? parentNode)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(entry);

        var newNode = new UIScreenNode(entry.ControlType);

        // Apply default properties
        foreach (var (name, value) in entry.DefaultProperties)
        {
            if (value != null)
            {
                newNode.SetProperty(name, value);
            }
        }

        InsertNode(document, newNode, parentNode);
        return newNode;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Internal helpers
    // ─────────────────────────────────────────────────────────────────────

    private static void InsertNode(
        UIScreenDocument document,
        UIScreenNode newNode,
        UIScreenNode? parentNode)
    {
        if (document.Root == null)
        {
            // Empty document: new node becomes the root
            document.SetRoot(newNode);
            return;
        }

        if (parentNode == null)
        {
            // No selection — append to document root if it supports children,
            // otherwise replace root only when root itself cannot have children
            if (CanHaveChildren(document.Root))
            {
                document.Root.AddChild(newNode);
            }
            // else: silently do nothing (callers should provide a valid parent)
            return;
        }

        if (CanHaveChildren(parentNode))
        {
            parentNode.AddChild(newNode);
        }
        else if (parentNode.Parent != null)
        {
            // Insert as sibling (after parentNode)
            parentNode.Parent.AddChild(newNode);
        }
        else
        {
            // parentNode is root and is a leaf — append to root as new sibling is impossible;
            // fall back to replacing document root with a StackPanel wrapping both
            WrapRootAndInsert(document, newNode);
        }
    }

    /// <summary>
    /// Wraps the current root in a StackPanel and appends the new node as a second child.
    /// Used only when the selected parent is the document root and is a leaf control.
    /// </summary>
    private static void WrapRootAndInsert(UIScreenDocument document, UIScreenNode newNode)
    {
        var oldRoot = document.Root!;
        document.ClearRoot();

        var wrapper = new UIScreenNode("StackPanel");
        wrapper.SetProperty("Orientation", "Vertical");
        wrapper.AddChild(oldRoot);
        wrapper.AddChild(newNode);
        document.SetRoot(wrapper);
    }

    /// <summary>
    /// Returns true when a node can receive additional child elements.
    /// Leaf / content-only controls where a second child is semantically invalid
    /// are excluded.
    /// </summary>
    public static bool CanHaveChildren(UIScreenNode node) =>
        node.ControlType switch
        {
            // Layout containers always accept children
            "StackPanel"   => true,
            "DockPanel"    => true,
            "Grid"         => true,
            "UniformGrid"  => true,
            "ScrollViewer" => true,  // single content, but we allow it
            "Border"       => true,  // single content
            "Expander"     => true,
            "Window"       => true,
            // Content controls that wrap a single child
            "Button"       => true,
            "CheckBox"     => true,
            "RadioButton"  => true,
            // Leaf controls
            "TextBlock"    => false,
            "TextBox"      => false,
            "Image"        => false,
            "Slider"       => false,
            "ProgressBar"  => false,
            "ComboBox"     => false,
            "ListBox"      => false,
            // Unknown types default to allowing children
            _              => true,
        };
}
