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

        // A document parsed from XAML text keeps the live tree it was parsed from (T4.1, engine ADR-0038
        // "Lossless editor round trip"): patch it in place and reuse it, so every comment, namespace
        // declaration, prefix, attribute order and untouched whitespace node survives. A document created
        // fresh in the editor (no source text) has no such tree, and keeps the v1 synthesis behaviour.
        return document.SourceXDocument != null
            ? SerializeWithFidelity(document)
            : SerializeSynthetic(document);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Fidelity path: patch the original live XDocument in place
    // ─────────────────────────────────────────────────────────────────────

    private static string SerializeWithFidelity(UIScreenDocument document)
    {
        var xDocument = document.SourceXDocument!;
        var rootElement = xDocument.Root
            ?? throw new InvalidOperationException("The document's source XAML tree has no root element.");

        ReconcileElement(rootElement, document.Root!);
        ReconcileResources(document, rootElement);

        // Written by UIScreenXamlSourceWriter rather than XDocument.ToString(): that one re-emits every start
        // tag on one line with double quotes, so the first edit anywhere would reformat every element with
        // attributes on several lines. The writer keeps each untouched start tag's original text, and the
        // original XML declaration (or its absence).
        var text = UIScreenXamlSourceWriter.Write(xDocument);

        return NormalizeLineEndings(text, document.OriginalLineEnding);
    }

    private static void ReconcileElement(XElement element, UIScreenNode node)
    {
        ReconcileAttributes(element, node);
        ReconcilePropertyElements(element, node);
        ReconcileChildren(element, node);
    }

    private static void ReconcileAttributes(XElement element, UIScreenNode node)
    {
        var keptLocalNames = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(node.Name))
        {
            SetOrCreateAttribute(element, "Name", node.Name);
            keptLocalNames.Add("Name");
        }

        foreach (var property in node.Properties.Values)
        {
            if (string.Equals(property.ValueType, "xaml", StringComparison.Ordinal))
            {
                continue;
            }

            SetOrCreateAttribute(element, property.Name, property.EffectiveSerializedValue ?? string.Empty);
            keptLocalNames.Add(property.Name);
        }

        // Any attribute the model no longer has was removed in the editor; anything else on the element
        // (namespace declarations, and every attribute still in the model) is left completely untouched.
        foreach (var attribute in element.Attributes().Where(a => !a.IsNamespaceDeclaration).ToList())
        {
            if (!keptLocalNames.Contains(attribute.Name.LocalName))
            {
                attribute.Remove();
            }
        }
    }

    private static void SetOrCreateAttribute(XElement element, string localName, string value)
    {
        var existing = element.Attributes().FirstOrDefault(a => !a.IsNamespaceDeclaration && a.Name.LocalName == localName);
        if (existing != null)
        {
            // Only touch it if the value actually changed, so an attribute the editor never modified keeps
            // its original prefix, and the element is not marked "changed" by any XDocument bookkeeping.
            if (!string.Equals(existing.Value, value, StringComparison.Ordinal))
            {
                existing.Value = value;
            }

            return;
        }

        // A brand-new attribute has no original prefix to preserve; SetAttributeValue appends it after
        // every existing attribute, which is the required position for a newly added one.
        element.SetAttributeValue(localName, value);
    }

    private static void ReconcilePropertyElements(XElement element, UIScreenNode node)
    {
        var keptElements = new HashSet<XElement>();

        foreach (var property in node.Properties.Values)
        {
            if (!string.Equals(property.ValueType, "xaml", StringComparison.Ordinal))
            {
                continue;
            }

            if (property.SourceElement != null && property.SourceElement.Parent == element)
            {
                keptElements.Add(property.SourceElement);

                var currentRaw = GetRawInnerXml(property.SourceElement);
                if (!string.Equals(currentRaw, property.SerializedValue, StringComparison.Ordinal))
                {
                    // Only the edited property element's own content is rebuilt; its position among its
                    // siblings, and everything else in the document, is untouched.
                    property.SourceElement.RemoveNodes();
                    foreach (var fragment in ParseFragment(property.SerializedValue))
                    {
                        property.SourceElement.Add(fragment);
                    }
                }

                continue;
            }

            // A brand-new property element (added in the editor, no XAML source): synthesize it and place
            // it first, matching where Window.Resources has always been injected.
            XNamespace defaultNamespace = DefaultNamespaceValue;
            var qualifiedName = property.ElementQualifiedName ?? $"{node.ControlType}.{property.Name}";
            var newElement = new XElement(defaultNamespace + qualifiedName);
            foreach (var fragment in ParseFragment(property.SerializedValue))
            {
                newElement.Add(fragment);
            }

            element.AddFirst(newElement);
            property.SourceElement = newElement;
            property.ElementQualifiedName = qualifiedName;
            keptElements.Add(newElement);
        }

        foreach (var existingPropertyElement in element.Elements().Where(IsPropertyElement).ToList())
        {
            if (string.Equals(existingPropertyElement.Name.LocalName, "Window.Resources", StringComparison.Ordinal))
            {
                continue; // Resources are reconciled separately by ReconcileResources.
            }

            if (!keptElements.Contains(existingPropertyElement))
            {
                existingPropertyElement.Remove();
            }
        }
    }

    private static void ReconcileChildren(XElement element, UIScreenNode node)
    {
        var desired = new List<XElement>(node.Children.Count);
        var created = new HashSet<XElement>();
        foreach (var child in node.Children)
        {
            if (child.SourceElement != null && child.SourceElement.Parent == element)
            {
                ReconcileElement(child.SourceElement, child);
                desired.Add(child.SourceElement);
            }
            else
            {
                var synthesized = SerializeNode(child, isRoot: false);
                child.SourceElement = synthesized;
                desired.Add(synthesized);
                created.Add(synthesized);
            }
        }

        var current = element.Elements().Where(IsRegularChildElement).ToList();

        // The common case -- no child added, removed or reordered -- leaves the tree completely untouched,
        // which is what keeps an unrelated attribute edit from disturbing any sibling's comments or
        // whitespace.
        if (current.SequenceEqual(desired))
        {
            return;
        }

        // Every change below moves whole blocks -- a child with the indentation and the comments before it
        // (GetLeadingBlock) -- so the children the editor did not touch keep their own lines.
        foreach (var existing in current)
        {
            if (!desired.Contains(existing))
            {
                RemoveWithLeadingComments(existing);
            }
        }

        ReorderKeptChildren(element, desired.Where(child => !created.Contains(child)).ToList());

        for (var index = 0; index < desired.Count; index++)
        {
            if (created.Contains(desired[index]))
            {
                InsertCreatedChild(element, desired, index);
            }
        }
    }

    /// <summary>
    /// Puts the children still in the tree in <paramref name="keptInOrder"/>'s order. Each child moves with its
    /// leading block into the slot another child's block occupied, so property elements, trailing whitespace
    /// and every block that did not move stay exactly where they were.
    /// </summary>
    private static void ReorderKeptChildren(XElement element, List<XElement> keptInOrder)
    {
        var inTree = element.Elements().Where(IsRegularChildElement).ToList();
        if (inTree.SequenceEqual(keptInOrder))
        {
            return;
        }

        var blocks = new List<List<XNode>>(inTree.Count);
        var slots = new List<XElement>(inTree.Count);
        foreach (var child in inTree)
        {
            var block = GetLeadingBlock(child);
            block.Add(child);
            blocks.Add(block);

            var slot = new XElement("slot");
            block[0].AddBeforeSelf(slot);
            slots.Add(slot);
            foreach (var blockNode in block)
            {
                blockNode.Remove();
            }
        }

        for (var index = 0; index < slots.Count; index++)
        {
            var block = blocks[inTree.IndexOf(keptInOrder[index])];
            slots[index].ReplaceWith(block.Cast<object>().ToArray());
        }
    }

    /// <summary>
    /// Inserts the child the editor created at <paramref name="index"/> of <paramref name="desired"/>: after its
    /// previous sibling, indented like it, or else before its next sibling's block, or else at the end.
    /// </summary>
    private static void InsertCreatedChild(XElement element, List<XElement> desired, int index)
    {
        var created = desired[index];

        if (index > 0)
        {
            var previous = desired[index - 1];
            var indentation = GetIndentation(previous);
            if (indentation != null)
            {
                previous.AddAfterSelf(new XText(indentation), created);
            }
            else
            {
                previous.AddAfterSelf(created);
            }

            return;
        }

        var next = desired.Skip(1).FirstOrDefault(child => child.Parent == element);
        if (next != null)
        {
            var nextBlock = GetLeadingBlock(next);
            var anchor = nextBlock.Count > 0 ? nextBlock[0] : next;
            var indentation = GetIndentation(next);
            if (indentation != null)
            {
                anchor.AddBeforeSelf(new XText(indentation), created);
            }
            else
            {
                anchor.AddBeforeSelf(created);
            }

            return;
        }

        element.Add(created);
    }

    /// <summary>The whitespace-only text right before <paramref name="child"/> (its indentation), or null.</summary>
    private static string? GetIndentation(XElement child)
        => child.PreviousNode is XText text && text is not XCData && string.IsNullOrWhiteSpace(text.Value)
            ? text.Value
            : null;

    /// <summary>
    /// The comments and whitespace-only text nodes right before <paramref name="child"/>, back to the previous
    /// element or the start of its parent: its indentation and the comments attached to it, in document order.
    /// </summary>
    private static List<XNode> GetLeadingBlock(XElement child)
    {
        var block = new List<XNode>();
        var cursor = child.PreviousNode;
        while (cursor is XComment || (cursor is XText text && text is not XCData && string.IsNullOrWhiteSpace(text.Value)))
        {
            block.Insert(0, cursor);
            cursor = cursor.PreviousNode;
        }

        return block;
    }

    private static bool IsRegularChildElement(XElement element)
        => !element.Name.LocalName.Contains('.', StringComparison.Ordinal);

    /// <summary>
    /// Removes <paramref name="element"/> together with its leading block (T4.1, engine ADR-0038 "Lossless
    /// editor round trip": deleting a node deletes the comments attached just before it): the comments right
    /// before it and the whitespace around them, including its own indentation, so no blank or
    /// indentation-only line is left behind.
    /// </summary>
    private static void RemoveWithLeadingComments(XElement element)
    {
        foreach (var node in GetLeadingBlock(element))
        {
            node.Remove();
        }

        element.Remove();
    }

    private static void ReconcileResources(UIScreenDocument document, XElement rootElement)
    {
        if (!string.Equals(rootElement.Name.LocalName, "Window", StringComparison.Ordinal))
        {
            return;
        }

        var currentSignature = UIScreenDocument.ComputeResourcesSignature(document.Resources);
        if (string.Equals(currentSignature, document.BaselineResourcesSignature, StringComparison.Ordinal))
        {
            return; // Untouched since parse: leave the original element (or absence of one) exactly as is.
        }

        document.SourceResourcesElement?.Remove();
        document.SourceResourcesElement = null;

        if (document.Resources.Count == 0)
        {
            document.BaselineResourcesSignature = currentSignature;
            return;
        }

        XNamespace defaultNamespace = DefaultNamespaceValue;
        XNamespace xamlNamespace = XamlNamespaceValue;

        var resourcesElement = new XElement(defaultNamespace + "Window.Resources");
        foreach (var entry in document.Resources)
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

        rootElement.AddFirst(resourcesElement);
        document.SourceResourcesElement = resourcesElement;
        document.BaselineResourcesSignature = currentSignature;
    }

    private static string GetRawInnerXml(XElement element)
    {
        return string.Concat(element.Nodes().Select(n => n.ToString(SaveOptions.DisableFormatting)));
    }

    private static string NormalizeLineEndings(string text, string? targetLineEnding)
    {
        if (string.IsNullOrEmpty(targetLineEnding) || string.Equals(targetLineEnding, "\n", StringComparison.Ordinal))
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
        return normalized.Replace("\n", targetLineEnding);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Synthetic path: v1 behaviour for a document with no source XAML text
    // ─────────────────────────────────────────────────────────────────────

    private static string SerializeSynthetic(UIScreenDocument document)
    {
        var rootElement = SerializeNode(document.Root!, isRoot: true);

        // Inject Window.Resources if any are defined
        if (document.Resources.Count > 0 && string.Equals(document.Root!.ControlType, "Window", StringComparison.Ordinal))
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

            var qualifiedName = property.ElementQualifiedName ?? $"{node.ControlType}.{property.Name}";
            var propertyElement = new XElement(defaultNamespace + qualifiedName);
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

    private static bool IsPropertyElement(XElement element)
        => element.Name.LocalName.Contains('.', StringComparison.Ordinal);

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
