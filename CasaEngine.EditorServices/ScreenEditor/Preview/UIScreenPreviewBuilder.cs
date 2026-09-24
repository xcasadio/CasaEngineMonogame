using System.Xml.Linq;
using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Xaml;
using CasaEngine.Framework.UI.MGUI;
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

    public MGWindow Build(MGDesktop desktop, UIScreenDocument document, int width = 1280, int height = 720)
    {
        ArgumentNullException.ThrowIfNull(desktop);

        var previewMarkup = CreatePreviewMarkup(document, width, height);
        return XAMLParser.LoadRootWindow(desktop, previewMarkup, SanitizeXAMLString: false, ReplaceLinebreakLiterals: true);
    }

    /// <summary>
    /// Same as <see cref="Build(MGDesktop, UIScreenDocument, int, int)"/>, but also loads <paramref name="asset"/>'s
    /// optional design-time data file (ADR-0038, "Design-time data") and, when it names one successfully, sets
    /// the built window's <see cref="MGWindow.WindowDataContext"/> to the populated view model so every bound
    /// control in the screen previews with real data. A missing or invalid design-time data file is reported
    /// through <paramref name="designTimeDataError"/> instead of throwing; the window is still built and
    /// returned, without a data context.
    /// </summary>
    public MGWindow Build(MGDesktop desktop, UIScreenDocument document, UIScreenAsset asset, string assetFilePath,
        out string designTimeDataError, int width = 1280, int height = 720)
    {
        var window = Build(desktop, document, width, height);
        designTimeDataError = ApplyDesignTimeDataContext(window, asset, assetFilePath);
        return window;
    }

    /// <summary>
    /// Builds the preview window and returns a mapping from each
    /// <see cref="DocumentNodeId"/> to the corresponding runtime <see cref="MGElement"/>.
    /// </summary>
    public (MGWindow Window, IReadOnlyDictionary<DocumentNodeId, MGElement> NodeMap) BuildWithMapping(
        MGDesktop desktop, UIScreenDocument document, int width = 1280, int height = 720)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentNullException.ThrowIfNull(document);

        var idToName = new Dictionary<DocumentNodeId, string>();
        var taggedMarkup = CreateTaggedMarkup(document, idToName, width, height);
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

    /// <summary>
    /// Same as <see cref="BuildWithMapping(MGDesktop, UIScreenDocument, int, int)"/>, but also loads
    /// <paramref name="asset"/>'s optional design-time data file (ADR-0038, "Design-time data") the same way
    /// <see cref="Build(MGDesktop, UIScreenDocument, UIScreenAsset, string, out string, int, int)"/> does.
    /// </summary>
    public (MGWindow Window, IReadOnlyDictionary<DocumentNodeId, MGElement> NodeMap) BuildWithMapping(
        MGDesktop desktop, UIScreenDocument document, UIScreenAsset asset, string assetFilePath,
        out string designTimeDataError, int width = 1280, int height = 720)
    {
        var (window, map) = BuildWithMapping(desktop, document, width, height);
        designTimeDataError = ApplyDesignTimeDataContext(window, asset, assetFilePath);
        return (window, map);
    }

    /// <summary>Loads <paramref name="asset"/>'s design-time data file (if any), sets it as <paramref name="window"/>'s
    /// data context on success, and returns the error message on failure (or null when there was nothing to
    /// load or loading succeeded).</summary>
    private static string ApplyDesignTimeDataContext(MGWindow window, UIScreenAsset asset, string assetFilePath)
    {
        var result = UIScreenDesignTimeDataLoader.Load(asset, assetFilePath);
        if (result.DataContext != null)
        {
            window.WindowDataContext = result.DataContext;
        }

        return result.ErrorMessage;
    }

    public string CreatePreviewMarkup(UIScreenDocument document, int width = 1280, int height = 720)
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

        return WrapInPreviewWindow(serializedMarkup, document.Root.Name, width, height);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Tagged markup (used for BuildWithMapping)
    // ─────────────────────────────────────────────────────────────────────

    private string CreateTaggedMarkup(UIScreenDocument document, Dictionary<DocumentNodeId, string> outIdToName, int width = 1280, int height = 720)
    {
        var baseMarkup = CreatePreviewMarkup(document, width, height);

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

        // Inject mock data for text-capable controls when in design time
        if (UIDesignModeContext.IsDesignTime)
        {
            InjectMockData(xmlElement, docNode);
        }

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

    /// <summary>
    /// Injects placeholder content into text-capable elements that have no explicit
    /// text set, so the design-time preview is populated with readable sample values.
    /// </summary>
    private static void InjectMockData(XElement xmlElement, UIScreenNode docNode)
    {
        var controlType = xmlElement.Name.LocalName;

        switch (controlType)
        {
            case "TextBlock":
            case "TextBox":
            {
                var textAttr = xmlElement.Attribute("Text");
                if (textAttr == null || string.IsNullOrWhiteSpace(textAttr.Value))
                {
                    var key = !string.IsNullOrWhiteSpace(docNode.Name) ? docNode.Name : controlType;
                    xmlElement.SetAttributeValue("Text", UIScreenMockDataContext.GetText(key));
                }

                break;
            }

            case "Button":
            {
                // Inject button label only when no content is set yet (i.e. no child elements)
                if (!xmlElement.HasElements)
                {
                    var key = !string.IsNullOrWhiteSpace(docNode.Name) ? docNode.Name : "Label";
                    xmlElement.SetAttributeValue("Content", UIScreenMockDataContext.GetText(key));
                }

                break;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────

    private static string WrapInPreviewWindow(string serializedMarkup, string? rootName, int width = 1280, int height = 720)
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
            new XAttribute("Width", width),
            new XAttribute("Height", height),
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
