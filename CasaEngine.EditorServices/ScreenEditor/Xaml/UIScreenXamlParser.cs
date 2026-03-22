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

        var document = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace);
        if (document.Root == null)
        {
            throw new InvalidOperationException("XAML content must contain a root element.");
        }

        var screenDocument = new UIScreenDocument();

        // Extract Window.Resources if present
        if (string.Equals(document.Root.Name.LocalName, "Window", StringComparison.Ordinal))
        {
            var resourcesElement = document.Root.Elements()
                .FirstOrDefault(e => string.Equals(e.Name.LocalName, "Window.Resources", StringComparison.Ordinal));
            if (resourcesElement != null)
            {
                ParseResources(resourcesElement, screenDocument);
            }
        }

        screenDocument.SetRoot(ParseElement(document.Root));
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
                XamlValue = string.Concat(child.Nodes().Select(n => n.ToString(SaveOptions.DisableFormatting))),
            });
        }
    }

    public UIScreenDocument ParseFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));
        }

        return Parse(File.ReadAllText(filePath));
    }

    private UIScreenNode ParseElement(XElement element)
    {
        var node = new UIScreenNode(element.Name.LocalName);

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
                var propertyName = GetPropertyElementName(childElement);
                var rawContent = GetRawInnerXml(childElement);
                node.SetProperty(propertyName, rawContent, "xaml");
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
}