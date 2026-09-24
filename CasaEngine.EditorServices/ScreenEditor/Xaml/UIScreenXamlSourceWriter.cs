using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace CasaEngine.EditorServices.ScreenEditor.Xaml;

/// <summary>
/// Writes a screen's live <see cref="XDocument"/> back to text while keeping the source formatting of everything
/// the editor did not touch (T4.1, engine ADR-0038 "Lossless editor round trip").
/// <para/>
/// <see cref="XDocument"/> keeps comments, whitespace nodes, namespace declarations and attribute order, but not
/// the text of a start tag (its line breaks between attributes, its quote style, the character references in its
/// attribute values) nor the escaping of a text node. So <see cref="CaptureSourceFormatting"/> records, when a
/// screen is parsed, each element's original start-tag text next to a signature of its attributes, and each text
/// node's original text next to its value; <see cref="Write"/> reuses those texts while the signature or the value
/// is unchanged, and synthesizes only the start tags and text the editor changed or created. End tags are always
/// written as <c>&lt;/name&gt;</c>.
/// </summary>
internal static class UIScreenXamlSourceWriter
{
    /// <summary>
    /// Records the original start-tag text of every element of <paramref name="document"/>, the original text of
    /// every text node, and the original XML declaration. <paramref name="document"/> must have been parsed from
    /// <paramref name="sourceText"/> with <see cref="LoadOptions.SetLineInfo"/>; a node whose text cannot be
    /// located gets no record and is always synthesized.
    /// </summary>
    public static void CaptureSourceFormatting(XDocument document, string sourceText)
    {
        // XmlReader counts lines on \n, \r\n and \r alike, and the written text is LF-based until the
        // serializer restores the original line ending, so every offset is taken in an LF-normalized copy.
        var text = NormalizeToLineFeed(sourceText);
        var lineStarts = ComputeLineStarts(text);

        if (document.Declaration != null && text.StartsWith("<?xml", StringComparison.Ordinal))
        {
            var declarationEnd = text.IndexOf("?>", StringComparison.Ordinal);
            if (declarationEnd > 0)
            {
                document.AddAnnotation(new OriginalDeclaration(text[..(declarationEnd + 2)]));
            }
        }

        foreach (var node in document.DescendantNodes())
        {
            var lineInfo = (IXmlLineInfo)node;
            if (!lineInfo.HasLineInfo() || lineInfo.LineNumber > lineStarts.Count)
            {
                continue;
            }

            var position = lineStarts[lineInfo.LineNumber - 1] + lineInfo.LinePosition - 1;
            if (node is XElement element)
            {
                // The line position of an element is its name, one character after the '<'.
                var start = position - 1;
                if (start < 0 || start >= text.Length || text[start] != '<')
                {
                    continue;
                }

                var end = FindStartTagEnd(text, start);
                if (end >= 0)
                {
                    element.AddAnnotation(new OriginalStartTag(text[start..(end + 1)], ComputeSignature(element)));
                }
            }
            else if (node is XText textNode && node is not XCData && position >= 0 && position <= text.Length)
            {
                // A text node's line position is its first character; its text runs to the next markup.
                var end = text.IndexOf('<', position);
                textNode.AddAnnotation(new OriginalText(text[position..(end < 0 ? text.Length : end)], textNode.Value));
            }
        }
    }

    /// <summary>Writes <paramref name="document"/> with LF line endings, reusing every original start tag and the
    /// original XML declaration that are still valid.</summary>
    public static string Write(XDocument document)
    {
        var builder = new StringBuilder();

        if (document.Declaration != null)
        {
            builder.Append(document.Annotation<OriginalDeclaration>()?.Text ?? document.Declaration.ToString());
        }

        foreach (var node in document.Nodes())
        {
            WriteNode(builder, node);
        }

        return builder.ToString();
    }

    private static void WriteNode(StringBuilder builder, XNode node)
    {
        switch (node)
        {
            case XElement element:
                WriteElement(builder, element);
                break;
            case XCData cdata:
                builder.Append("<![CDATA[").Append(cdata.Value).Append("]]>");
                break;
            case XText textNode:
                var original = textNode.Annotation<OriginalText>();
                if (original != null && string.Equals(original.Value, textNode.Value, StringComparison.Ordinal))
                {
                    builder.Append(original.Text);
                }
                else
                {
                    AppendEscapedText(builder, textNode.Value);
                }

                break;
            case XComment comment:
                builder.Append("<!--").Append(comment.Value).Append("-->");
                break;
            default:
                // Processing instructions and document types have no formatting of their own to lose.
                builder.Append(node.ToString(SaveOptions.DisableFormatting));
                break;
        }
    }

    private static void WriteElement(StringBuilder builder, XElement element)
    {
        var original = element.Annotation<OriginalStartTag>();
        if (original != null && string.Equals(original.Signature, ComputeSignature(element), StringComparison.Ordinal))
        {
            builder.Append(original.Text);
        }
        else
        {
            AppendSynthesizedStartTag(builder, element);
        }

        if (element.IsEmpty)
        {
            return;
        }

        foreach (var child in element.Nodes())
        {
            WriteNode(builder, child);
        }

        builder.Append("</").Append(QualifiedElementName(element)).Append('>');
    }

    private static void AppendSynthesizedStartTag(StringBuilder builder, XElement element)
    {
        builder.Append('<').Append(QualifiedElementName(element));

        var elementNamespace = element.Name.Namespace;
        if (elementNamespace != XNamespace.None
            && string.IsNullOrEmpty(element.GetPrefixOfNamespace(elementNamespace))
            && element.GetDefaultNamespace() != elementNamespace)
        {
            // A new element in a namespace nothing in scope declares: declare it as the default here.
            AppendAttribute(builder, "xmlns", elementNamespace.NamespaceName);
        }

        var generatedPrefixCount = 0;
        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration)
            {
                var declarationName = attribute.Name.Namespace == XNamespace.Xmlns
                    ? "xmlns:" + attribute.Name.LocalName
                    : "xmlns";
                AppendAttribute(builder, declarationName, attribute.Value);
                continue;
            }

            var attributeNamespace = attribute.Name.Namespace;
            if (attributeNamespace == XNamespace.None)
            {
                AppendAttribute(builder, attribute.Name.LocalName, attribute.Value);
                continue;
            }

            var prefix = attributeNamespace == XNamespace.Xml ? "xml" : element.GetPrefixOfNamespace(attributeNamespace);
            if (string.IsNullOrEmpty(prefix))
            {
                // A namespaced attribute with no prefix in scope: declare one on this element.
                generatedPrefixCount++;
                prefix = "p" + generatedPrefixCount;
                AppendAttribute(builder, "xmlns:" + prefix, attributeNamespace.NamespaceName);
            }

            AppendAttribute(builder, prefix + ":" + attribute.Name.LocalName, attribute.Value);
        }

        builder.Append(element.IsEmpty ? " />" : ">");
    }

    private static string QualifiedElementName(XElement element)
    {
        var elementNamespace = element.Name.Namespace;
        if (elementNamespace == XNamespace.None)
        {
            return element.Name.LocalName;
        }

        var prefix = element.GetPrefixOfNamespace(elementNamespace);
        return string.IsNullOrEmpty(prefix) ? element.Name.LocalName : prefix + ":" + element.Name.LocalName;
    }

    private static void AppendAttribute(StringBuilder builder, string name, string value)
    {
        builder.Append(' ').Append(name).Append("=\"");
        foreach (var character in value)
        {
            switch (character)
            {
                case '&': builder.Append("&amp;"); break;
                case '<': builder.Append("&lt;"); break;
                case '>': builder.Append("&gt;"); break;
                case '"': builder.Append("&quot;"); break;
                case '\n': builder.Append("&#xA;"); break;
                case '\r': builder.Append("&#xD;"); break;
                case '\t': builder.Append("&#x9;"); break;
                default: builder.Append(character); break;
            }
        }

        builder.Append('"');
    }

    private static void AppendEscapedText(StringBuilder builder, string value)
    {
        foreach (var character in value)
        {
            switch (character)
            {
                case '&': builder.Append("&amp;"); break;
                case '<': builder.Append("&lt;"); break;
                case '>': builder.Append("&gt;"); break;
                default: builder.Append(character); break;
            }
        }
    }

    /// <summary>The attributes of <paramref name="element"/> in order (names with their namespace, and values)
    /// plus whether it is self-closing: anything that would make its original start-tag text wrong.</summary>
    private static string ComputeSignature(XElement element)
    {
        var builder = new StringBuilder();
        foreach (var attribute in element.Attributes())
        {
            builder.Append(attribute.Name.ToString()).Append('\u0001').Append(attribute.Value).Append('\u0002');
        }

        builder.Append(element.IsEmpty ? "/>" : ">");
        return builder.ToString();
    }

    /// <summary>The index of the '>' closing the start tag that begins at <paramref name="start"/>, skipping any
    /// '>' inside a quoted attribute value, or -1.</summary>
    private static int FindStartTagEnd(string text, int start)
    {
        var quote = '\0';
        for (var index = start + 1; index < text.Length; index++)
        {
            var character = text[index];
            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }
            }
            else if (character == '"' || character == '\'')
            {
                quote = character;
            }
            else if (character == '>')
            {
                return index;
            }
        }

        return -1;
    }

    private static List<int> ComputeLineStarts(string text)
    {
        var lineStarts = new List<int> { 0 };
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n')
            {
                lineStarts.Add(index + 1);
            }
        }

        return lineStarts;
    }

    private static string NormalizeToLineFeed(string text)
        => text.Replace("\r\n", "\n").Replace("\r", "\n");

    private sealed record OriginalStartTag(string Text, string Signature);

    private sealed record OriginalDeclaration(string Text);

    private sealed record OriginalText(string Text, string Value);
}
