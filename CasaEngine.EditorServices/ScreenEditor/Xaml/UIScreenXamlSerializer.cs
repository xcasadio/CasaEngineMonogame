using System.Xml.Linq;
using CasaEngine.EditorServices.ScreenEditor.DocumentModel;

namespace CasaEngine.EditorServices.ScreenEditor.Xaml;

public sealed class UIScreenXamlSerializer
{
    private const string DefaultNamespaceValue = "clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core";
    private const string XamlNamespaceValue = "http://schemas.microsoft.com/winfx/2006/xaml";

    public string Serialize(UIScreenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Root == null)
        {
            throw new InvalidOperationException("UIScreenDocument must have a root node before it can be serialized.");
        }

        var rootElement = SerializeNode(document.Root, isRoot: true);
        var xDocument = new XDocument(new XDeclaration("1.0", "utf-8", null), rootElement);
        return xDocument.ToString(SaveOptions.None);
    }

    private static XElement SerializeNode(UIScreenNode node, bool isRoot)
    {
        XNamespace defaultNamespace = DefaultNamespaceValue;
        XNamespace xamlNamespace = XamlNamespaceValue;

        var element = new XElement(defaultNamespace + node.ControlType);
        if (isRoot)
        {
            element.Add(new XAttribute(XNamespace.Xmlns + "x", xamlNamespace));
        }

        if (!string.IsNullOrWhiteSpace(node.Name))
        {
            element.Add(new XAttribute("Name", node.Name));
        }

        foreach (var property in node.Properties.Values.OrderBy(static value => value.Name, StringComparer.Ordinal))
        {
            if (string.Equals(property.ValueType, "xaml", StringComparison.Ordinal))
            {
                continue;
            }

            element.Add(new XAttribute(property.Name, property.SerializedValue ?? string.Empty));
        }

        foreach (var property in node.Properties.Values.OrderBy(static value => value.Name, StringComparer.Ordinal))
        {
            if (!string.Equals(property.ValueType, "xaml", StringComparison.Ordinal))
            {
                continue;
            }

            var propertyElement = new XElement(defaultNamespace + $"{node.ControlType}.{property.Name}");
            foreach (var contentNode in ParseFragment(property.SerializedValue))
            {
                propertyElement.Add(contentNode);
            }

            element.Add(propertyElement);
        }

        foreach (var child in node.Children)
        {
            element.Add(SerializeNode(child, isRoot: false));
        }

        return element;
    }

    private static IEnumerable<XNode> ParseFragment(string? rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            yield break;
        }

        var wrapper = XDocument.Parse(
            $"<Root xmlns=\"{DefaultNamespaceValue}\" xmlns:x=\"{XamlNamespaceValue}\">{rawContent}</Root>",
            LoadOptions.PreserveWhitespace);

        if (wrapper.Root == null)
        {
            yield break;
        }

        foreach (var node in wrapper.Root.Nodes())
        {
            yield return node;
        }
    }
}