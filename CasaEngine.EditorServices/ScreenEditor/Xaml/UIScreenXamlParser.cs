using System.Xml.Linq;
using CasaEngine.EditorServices.ScreenEditor.DocumentModel;

namespace CasaEngine.EditorServices.ScreenEditor.Xaml;

public sealed class UIScreenXamlParser
{
    public UIScreenDocument Parse(string xaml)
    {
        if (string.IsNullOrWhiteSpace(xaml))
        {
            throw new ArgumentException("XAML content cannot be null or whitespace.", nameof(xaml));
        }

        var document = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        if (document.Root == null)
        {
            throw new InvalidOperationException("XAML content must contain a root element.");
        }

        // XDocument does not keep a start tag's own formatting (line breaks between attributes, quote style,
        // character references): record it, so a save rewrites only the start tags the editor changed.
        UIScreenXamlSourceWriter.CaptureStartTags(document, xaml);

        var screenDocument = new UIScreenDocument
        {
            // Kept so the fidelity serializer can patch this same live tree in place on Save instead of
            // rebuilding it from the abstract model (T4.1, engine ADR-0038 "Lossless editor round trip") --
            // this is what preserves every comment, namespace declaration, prefix and untouched whitespace
            // node the source carries, including any comment before or after the root element itself.
            SourceXDocument = document,
            OriginalLineEnding = DetectLineEnding(xaml),
        };

        // Extract Window.Resources if present
        if (string.Equals(document.Root.Name.LocalName, "Window", StringComparison.Ordinal))
        {
            var resourcesElement = document.Root.Elements()
                .FirstOrDefault(e => string.Equals(e.Name.LocalName, "Window.Resources", StringComparison.Ordinal));
            if (resourcesElement != null)
            {
                screenDocument.SourceResourcesElement = resourcesElement;
                ParseResources(resourcesElement, screenDocument);
            }
        }

        screenDocument.BaselineResourcesSignature = UIScreenDocument.ComputeResourcesSignature(screenDocument.Resources);

        screenDocument.SetRoot(ParseElement(document.Root));

        screenDocument.BaselineSemanticSnapshot = UIScreenSemanticSnapshot.Compute(screenDocument);

        return screenDocument;
    }

    private static void ParseResources(XElement resourcesElement, UIScreenDocument document)
    {
        XNamespace xamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";
        foreach (var child in resourcesElement.Elements())
        {
            var key = child.Attribute(xamlNs + "Key")?.Value
                   ?? child.Attribute("Key")?.Value;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            document.Resources.Add(new UIScreenResourceEntry
            {
                Key = key,
                XamlValue = GetRawInnerXml(child),
            });
        }
    }

    public UIScreenDocument ParseFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));
        }

        // Two reads: File.ReadAllText handles BOM/encoding detection for the string the XML parser needs,
        // while the raw bytes are kept so an unmodified save can replay the file byte-for-byte (T4.1,
        // engine ADR-0038 "Lossless editor round trip") -- including its BOM presence and encoding, which
        // XDocument does not otherwise let us reproduce exactly.
        var bytes = File.ReadAllBytes(filePath);
        var text = File.ReadAllText(filePath);

        var document = Parse(text);
        document.OriginalBytes = bytes;
        return document;
    }

    private UIScreenNode ParseElement(XElement element)
    {
        var node = new UIScreenNode(element.Name.LocalName)
        {
            SourceElement = element,
        };

        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration)
            {
                continue;
            }

            if (string.Equals(attribute.Name.LocalName, "Name", StringComparison.Ordinal))
            {
                node.Name = attribute.Value;
                continue;
            }

            node.SetProperty(attribute.Name.LocalName, attribute.Value);
        }

        foreach (var childElement in element.Elements())
        {
            // Window.Resources is handled separately as a document-level resource dictionary
            if (string.Equals(childElement.Name.LocalName, "Window.Resources", StringComparison.Ordinal))
            {
                continue;
            }

            if (IsPropertyElement(childElement))
            {
                var qualifiedName = childElement.Name.LocalName;
                var propertyName = GetPropertyElementName(childElement);
                var rawContent = GetRawInnerXml(childElement);
                var propertyValue = node.SetProperty(propertyName, rawContent, "xaml");
                // The full owner-qualified name (e.g. "Canvas.Left", not just "Left") is kept verbatim so
                // the serializer never has to guess it back from `{node.ControlType}.{Name}`, which is
                // wrong for an attached property whose owner differs from the node it is set on.
                propertyValue.ElementQualifiedName = qualifiedName;
                propertyValue.SourceElement = childElement;
                continue;
            }

            node.AddChild(ParseElement(childElement));
        }

        return node;
    }

    private static bool IsPropertyElement(XElement element)
    {
        return element.Name.LocalName.Contains('.', StringComparison.Ordinal);
    }

    private static string GetPropertyElementName(XElement element)
    {
        var localName = element.Name.LocalName;
        var separatorIndex = localName.IndexOf('.', StringComparison.Ordinal);
        return separatorIndex >= 0 ? localName[(separatorIndex + 1)..] : localName;
    }

    private static string GetRawInnerXml(XElement element)
    {
        return string.Concat(element.Nodes().Select(node => node.ToString(SaveOptions.DisableFormatting)));
    }

    /// <summary>Detects "\r\n" vs "\n" from the first line break found in the raw source text.</summary>
    private static string DetectLineEnding(string text)
    {
        var index = text.IndexOf('\n');
        if (index <= 0)
        {
            return index == 0 ? "\n" : "\r\n";
        }

        return text[index - 1] == '\r' ? "\r\n" : "\n";
    }
}
