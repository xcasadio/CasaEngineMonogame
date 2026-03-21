using System.Xml.Linq;
using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Preview;
using Xunit;

namespace CasaEngine.Tests.ScreenEditor;

public class UIScreenPreviewBuilderTests
{
    [Fact]
    public void CreatePreviewMarkup_PreservesWindowRoot()
    {
        var document = new UIScreenDocument();
        var root = new UIScreenNode("Window")
        {
            Name = "MainWindow"
        };
        root.SetProperty("Width", "640");
        root.SetProperty("Height", "480");
        root.AddChild(new UIScreenNode("Button")
        {
            Name = "ConfirmButton"
        });
        document.SetRoot(root);

        var builder = new UIScreenPreviewBuilder();

        var markup = builder.CreatePreviewMarkup(document);
        var xDocument = XDocument.Parse(markup);

        Assert.Equal("Window", xDocument.Root?.Name.LocalName);
        Assert.Equal("MainWindow", xDocument.Root?.Attribute("Name")?.Value);
        Assert.Equal("Button", xDocument.Root?.Elements().Single().Name.LocalName);
    }

    [Fact]
    public void CreatePreviewMarkup_WrapsNonWindowRootInPreviewWindow()
    {
        var document = new UIScreenDocument();
        var root = new UIScreenNode("StackPanel")
        {
            Name = "LayoutRoot"
        };
        root.AddChild(new UIScreenNode("TextBlock"));
        document.SetRoot(root);

        var builder = new UIScreenPreviewBuilder();

        var markup = builder.CreatePreviewMarkup(document);
        var xDocument = XDocument.Parse(markup);
        var previewRoot = xDocument.Root;

        Assert.NotNull(previewRoot);
        Assert.Equal("Window", previewRoot!.Name.LocalName);
        Assert.Equal("Preview - LayoutRoot", previewRoot.Attribute("TitleText")?.Value);

        var wrappedContent = previewRoot.Elements().Single();
        Assert.Equal("StackPanel", wrappedContent.Name.LocalName);
        Assert.Equal("LayoutRoot", wrappedContent.Attribute("Name")?.Value);
    }
}