using System.Collections.Generic;
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

        // Inject Window.Resources if any are defined
        if (document.Resources.Count > 0 && string.Equals(document.Root.ControlType, "Window", StringComparison.Ordinal))
        {
            InjectResources(rootElement, document.Resources);
        }

        var xDocument = new XDocument(new XDeclaration("1.0", "utf-8", null), rootElement);
        return xDocument.ToString(SaveOptions.None);
    }

    private static void InjectResources(XElement windowElement, List<UIScreenResourceEntry> resources)
    {
        XNamespace defaultNamespace = DefaultNamespaceValue;
        XNamespace xamlNamespace = XamlNamespaceValue;

        var resourcesElement = new XElement(defaultNamespace + "Window.Resources");
        foreach (var entry in resources)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.XamlValue))
            {
                continue;
            }

            foreach (var fragment in ParseFragment(entry.XamlValue))
            {
                if (fragment is XElement fragmentElement)
                {
                    fragmentElement.SetAttributeValue(xamlNamespace + "Key", entry.Key);
                }

                resourcesElement.Add(fragment);
            }
        }

        windowElement.AddFirst(resourcesElement);
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

            element.Add(new XAttribute(property.Name, property.EffectiveSerializedValue ?? string.Empty));
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