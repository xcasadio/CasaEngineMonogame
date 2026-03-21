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