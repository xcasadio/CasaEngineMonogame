using System.Xml.Linq;

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

    // ── Lossless round trip (T4.1, engine ADR-0038 "Lossless editor round trip") ──────────────
    //
    // These fields exist only so the parser and the serializer can cooperate to make an
    // unmodified open+save byte-identical, and a modified save keep everything the abstract
    // document model does not itself represent (comments, namespace declarations, prefixes,
    // attribute order, the XML declaration, line endings). They are internal: no editor panel
    // (hierarchy, inspector, commands, undo/redo) needs to know about them.

    /// <summary>The exact bytes read from the source XAML file, or null when this document has no
    /// source file (parsed from a raw string, or created fresh in the editor). Used to make an
    /// unmodified save byte-identical: <see cref="BaselineSemanticSnapshot"/> decides whether to
    /// replay these bytes verbatim instead of re-serializing.</summary>
    internal byte[]? OriginalBytes { get; set; }

    /// <summary>The source file's line-ending style ("\r\n" or "\n"), detected from the raw text at
    /// parse time. The fidelity serializer normalizes its output to this style.</summary>
    internal string? OriginalLineEnding { get; set; }

    /// <summary>The live <see cref="XDocument"/> this document was parsed from, or null when there is
    /// no source (a raw-string parse with no file, or a document created fresh in the editor). The
    /// fidelity serializer patches this tree in place -- updating only the attributes and elements
    /// that changed -- and serializes it directly, instead of rebuilding a new tree from the abstract
    /// model, so every comment, namespace, prefix and untouched whitespace node survives.</summary>
    internal XDocument? SourceXDocument { get; set; }

    /// <summary>The original <c>Window.Resources</c> element, if the source document had one. Left
    /// untouched by the fidelity serializer when <see cref="Resources"/> did not change since parse.</summary>
    internal XElement? SourceResourcesElement { get; set; }

    /// <summary>A canonical semantic snapshot of <see cref="Resources"/> taken right after parsing,
    /// compared against <see cref="ComputeResourcesSignature"/> of the current value at save time to
    /// decide whether <see cref="SourceResourcesElement"/> needs to be rebuilt.</summary>
    internal string? BaselineResourcesSignature { get; set; }

    /// <summary>
    /// A canonical semantic snapshot of the whole document (see <c>UIScreenSemanticSnapshot</c>), taken
    /// right after parsing. <see cref="EditorServices.ScreenEditor.Session.UIScreenEditorSession.Save"/>
    /// compares a fresh snapshot against this one to decide -- robustly, not merely from an "is dirty"
    /// flag that a caller might have set or missed -- whether the document actually changed since it was
    /// opened. When it did not, the session replays <see cref="OriginalBytes"/> verbatim.
    /// </summary>
    internal string? BaselineSemanticSnapshot { get; set; }

    /// <summary>
    /// Computes a deterministic signature of a resource-entry sequence: same keys and XAML values in the
    /// same order produce the same signature. Order matters (resources can be looked up by declaration
    /// order in some XAML consumers), so this is not a set comparison.
    /// </summary>
    internal static string ComputeResourcesSignature(IEnumerable<UIScreenResourceEntry> resources)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var resource in resources)
        {
            builder.Append(resource.Key).Append('\u0002').Append(resource.XamlValue).Append('\u0001');
        }

        return builder.ToString();
    }

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