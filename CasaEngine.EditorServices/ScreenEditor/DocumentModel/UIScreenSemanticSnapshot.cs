using System.Text;

namespace CasaEngine.EditorServices.ScreenEditor.DocumentModel;

/// <summary>
/// Computes a deterministic, order-sensitive string that represents everything a
/// <see cref="UIScreenDocument"/> means -- control types, names, property values (including bindings) and
/// child order, plus resource entries -- but none of the pure-formatting trivia (comments, namespace
/// declarations, prefixes, attribute order, XML declaration, line endings). Two snapshots are equal exactly
/// when the two documents would drive the same runtime UI, regardless of how they were authored.
/// <para/>
/// <see cref="EditorServices.ScreenEditor.Session.UIScreenEditorSession.Save"/> (T4.1, engine ADR-0038
/// "Lossless editor round trip") compares the current document's snapshot against the one taken right
/// after parsing to decide, robustly, whether the document has any semantic change since it was opened --
/// not merely by trusting an "is dirty" flag a caller may have set, or missed, incorrectly. When nothing
/// changed, the session replays the original file bytes verbatim instead of re-serializing, which is what
/// makes an unmodified open+save byte-identical.
/// </summary>
internal static class UIScreenSemanticSnapshot
{
    public static string Compute(UIScreenDocument document)
    {
        var builder = new StringBuilder();

        if (document.Root != null)
        {
            AppendNode(builder, document.Root);
        }

        builder.Append("|RESOURCES:").Append(UIScreenDocument.ComputeResourcesSignature(document.Resources));

        return builder.ToString();
    }

    private static void AppendNode(StringBuilder builder, UIScreenNode node)
    {
        builder.Append('<').Append(node.ControlType).Append('#').Append(node.Name ?? string.Empty).Append('[');

        foreach (var property in node.Properties.Values.OrderBy(static value => value.Name, StringComparer.Ordinal))
        {
            builder.Append(property.Name).Append('=').Append(property.ValueType).Append(':')
                   .Append(property.EffectiveSerializedValue ?? string.Empty).Append(';');
        }

        builder.Append("]{");

        foreach (var child in node.Children)
        {
            AppendNode(builder, child);
        }

        builder.Append("}>");
    }
}
