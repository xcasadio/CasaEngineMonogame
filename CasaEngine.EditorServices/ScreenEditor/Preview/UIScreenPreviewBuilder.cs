using System.Collections.Generic;
using System.Xml.Linq;
using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Xaml;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;

namespace CasaEngine.EditorServices.ScreenEditor.Preview;

public sealed class UIScreenPreviewBuilder
{
    private const string DefaultNamespaceValue = "clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core";
    private const string XamlNamespaceValue = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>Prefix used to tag each element in preview XAML with its document node ID.</summary>
    public const string NodeIdNamePrefix = "_cse_";

    private readonly UIScreenXamlSerializer _serializer;

    public UIScreenPreviewBuilder()
        : this(new UIScreenXamlSerializer())
    {
    }

    public UIScreenPreviewBuilder(UIScreenXamlSerializer serializer)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public MGWindow Build(MGDesktop desktop, UIScreenDocument document)
    {
        ArgumentNullException.ThrowIfNull(desktop);

        var previewMarkup = CreatePreviewMarkup(document);
        return XAMLParser.LoadRootWindow(desktop, previewMarkup, SanitizeXAMLString: false, ReplaceLinebreakLiterals: true);
    }

    /// <summary>
    /// Builds the preview window and returns a mapping from each
    /// <see cref="DocumentNodeId"/> to the corresponding runtime <see cref="MGElement"/>.
    /// </summary>
    public (MGWindow Window, IReadOnlyDictionary<DocumentNodeId, MGElement> NodeMap) BuildWithMapping(
        MGDesktop desktop, UIScreenDocument document)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentNullException.ThrowIfNull(document);

        var idToName = new Dictionary<DocumentNodeId, string>();
        var taggedMarkup = CreateTaggedMarkup(document, idToName);
        var window = XAMLParser.LoadRootWindow(desktop, taggedMarkup, SanitizeXAMLString: false, ReplaceLinebreakLiterals: true);

        var map = new Dictionary<DocumentNodeId, MGElement>(idToName.Count);
        foreach (var (id, name) in idToName)
        {
            if (window.TryGetElementByName(name, out var element))
            {
                map[id] = element;
            }
        }

        return (window, map);
    }

    public string CreatePreviewMarkup(UIScreenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Root == null)
        {
            throw new InvalidOperationException("UIScreenDocument must have a root node before preview markup can be generated.");
        }

        var serializedMarkup = _serializer.Serialize(document);
        if (string.Equals(document.Root.ControlType, "Window", StringComparison.Ordinal))
        {
            return serializedMarkup;
        }

        return WrapInPreviewWindow(serializedMarkup, document.Root.Name);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Tagged markup (used for BuildWithMapping)
    // ─────────────────────────────────────────────────────────────────────

    private string CreateTaggedMarkup(UIScreenDocument document, Dictionary<DocumentNodeId, string> outIdToName)
    {
        var baseMarkup = CreatePreviewMarkup(document);

        var xDoc = XDocument.Parse(baseMarkup, LoadOptions.PreserveWhitespace);
        if (xDoc.Root == null || document.Root == null)
        {
            return baseMarkup;
        }

        // Determine which XElement corresponds to the document root
        XElement docRootXml;
        if (string.Equals(document.Root.ControlType, "Window", StringComparison.Ordinal))
        {
            docRootXml = xDoc.Root;
        }
        else
        {
            // Non-Window root was wrapped in a preview <Window> — document root is the first
            // direct content child (skip property elements like "Window.Resources")
            docRootXml = xDoc.Root.Elements()
                .FirstOrDefault(e => !e.Name.LocalName.Contains('.'))!;
            if (docRootXml == null)
            {
                return baseMarkup;
            }
        }

        TagElement(docRootXml, document.Root, outIdToName);

        return xDoc.ToString(SaveOptions.None);
    }

    private static void TagElement(XElement xmlElement, UIScreenNode docNode, Dictionary<DocumentNodeId, string> outIdToName)
    {
        var idName = $"{NodeIdNamePrefix}{docNode.Id.Value:N}";
        xmlElement.SetAttributeValue("Name", idName);
        outIdToName[docNode.Id] = idName;

        // Collect direct document-level child XML elements
        // (skip property-wrapper elements like "StackPanel.Children")
        var controlType = xmlElement.Name.LocalName;
        var childElements = new List<XElement>();
        CollectDocumentChildXElements(xmlElement, controlType, childElements);

        var index = 0;
        foreach (var child in docNode.Children)
        {
            if (index < childElements.Count)
            {
                TagElement(childElements[index], child, outIdToName);
            }

            index++;
        }
    }

    private static void CollectDocumentChildXElements(XElement element, string controlType, List<XElement> result)
    {
        foreach (var child in element.Elements())
        {
            if (child.Name.LocalName.StartsWith(controlType + ".", StringComparison.Ordinal))
            {
                // Property wrapper — recurse to find the actual child elements inside
                CollectDocumentChildXElements(child, controlType, result);
            }
            else
            {
                result.Add(child);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────

    private static string WrapInPreviewWindow(string serializedMarkup, string? rootName)
    {
        var document = XDocument.Parse(serializedMarkup, LoadOptions.PreserveWhitespace);
        if (document.Root == null)
        {
            throw new InvalidOperationException("Serialized screen preview markup must contain a root element.");
        }

        XNamespace defaultNamespace = DefaultNamespaceValue;
        XNamespace xamlNamespace = XamlNamespaceValue;

        var wrapper = new XElement(defaultNamespace + "Window",
            new XAttribute(XNamespace.Xmlns + "x", xamlNamespace),
            new XAttribute("TitleText", GetPreviewTitle(rootName)),
            new XAttribute("Width", 1280),
            new XAttribute("Height", 720),
            new XAttribute("Padding", "0"),
            new XAttribute("WindowStyle", "None"),
            new XAttribute("CanCloseWindow", false),
            new XAttribute("IsUserResizable", false),
            document.Root);

        return new XDocument(new XDeclaration("1.0", "utf-8", null), wrapper).ToString(SaveOptions.None);
    }

    private static string GetPreviewTitle(string? rootName)
        => string.IsNullOrWhiteSpace(rootName)
            ? "Preview"
            : $"Preview - {rootName}";
}
