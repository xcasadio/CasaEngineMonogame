using CasaEngine.EditorServices.ScreenEditor.Commands;
using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Preview;
using CasaEngine.EditorServices.ScreenEditor.Xaml;
using CasaEngine.Framework.UI.MGUI;
using CasaEngine.Tests.UI;
using MGUI.Core.UI.XAML;
using Xunit;

namespace CasaEngine.Tests.ScreenEditor;

/// <summary>
/// Pins the container the editor wraps the document root in, when the root is duplicated or a node is pasted
/// with no target: it must be a type the screen loader knows, or the screen saved afterwards no longer loads.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class UIScreenRootWrapperTests
{
    [Fact]
    public void Duplicate_OfTheRoot_WrapsItInAStackPanel_TheLoaderKnows()
    {
        var root = Node("StackPanel", "pnl1", Node("Button", "btn1"));
        var document = NewDocument(root);

        new DuplicateNodeCommand(document, root.Id).Execute();

        AssertLoads(document);
        Assert.Equal("StackPanel", document.Root!.ControlType);
    }

    [Fact]
    public void Paste_WithoutTarget_OnAnExistingRoot_WrapsItInAStackPanel_TheLoaderKnows()
    {
        var root = Node("StackPanel", "pnl1", Node("Button", "btn1"));
        var document = NewDocument(root);

        new PasteNodeCommand(document, root.DeepClone(), null).Execute();

        AssertLoads(document);
        Assert.Equal("StackPanel", document.Root!.ControlType);
    }

    private static void AssertLoads(UIScreenDocument document)
    {
        // The loader only takes a Window root: the serialized document is placed in one, as the editor preview does.
        var xaml = new UIScreenPreviewBuilder(new UIScreenXamlSerializer()).CreatePreviewMarkup(document);
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var loaded = UIScreenLoader.Load(desktop, XamlDocumentSource.FromString(xaml, "inline"));

        Assert.NotNull(loaded);
    }

    private static UIScreenNode Node(string controlType, string name, params UIScreenNode[] children)
    {
        var node = new UIScreenNode(controlType) { Name = name };
        foreach (var child in children)
        {
            node.AddChild(child);
        }

        return node;
    }

    private static UIScreenDocument NewDocument(UIScreenNode root)
    {
        var document = new UIScreenDocument();
        document.SetRoot(root);
        return document;
    }
}
