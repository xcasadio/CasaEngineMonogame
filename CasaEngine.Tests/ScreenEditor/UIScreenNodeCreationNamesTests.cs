using CasaEngine.EditorServices.ScreenEditor.Commands;
using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Toolbox;
using CasaEngine.EditorServices.ScreenEditor.Xaml;
using CasaEngine.Framework.UI.MGUI;
using CasaEngine.Tests.UI;
using MGUI.Core.UI.XAML;
using Xunit;

namespace CasaEngine.Tests.ScreenEditor;

[Collection(ProjectEnvironmentCollection.Name)]
public class UIScreenNodeCreationNamesTests
{
    [Fact]
    public void AddNode_NamesTheNewElementsAfterTheirType()
    {
        var document = NewDocument(Node("StackPanel", "pnlRoot"));

        var addImage = new AddNodeCommand(document, Entry("Image"), document.Root);
        addImage.Execute();
        var addButton = new AddNodeCommand(document, Entry("Button"), document.Root);
        addButton.Execute();

        Assert.Equal("img1", addImage.CreatedNode!.Name);
        Assert.Equal("btn1", addButton.CreatedNode!.Name);
    }

    [Fact]
    public void AddNode_OnALeafRoot_NamesTheWrapper_AndKeepsEveryNameDistinct()
    {
        var document = NewDocument(Node("Image", "img1"));
        var command = new AddNodeCommand(document, Entry("Image"), document.Root);

        command.Execute();

        Assert.Equal("StackPanel", document.Root!.ControlType);
        Assert.Matches("^pnl[0-9]+$", document.Root.Name);
        AssertDistinctNames(document);
        var names = AllNames(document);

        command.Undo();
        command.Execute();

        AssertDistinctNames(document);
        Assert.Equal(names, AllNames(document));
    }

    [Fact]
    public void Duplicate_RenamesTheCopyAndItsDescendants()
    {
        var button = Node("Button", "btnPlay", Node("StackPanel", "pnlPlayFace"));
        var document = NewDocument(Node("StackPanel", "pnlRoot", button));
        var command = new DuplicateNodeCommand(document, button.Id);

        command.Execute();

        Assert.Equal("btnPlay2", command.CreatedNode!.Name);
        Assert.Equal("pnlPlayFace2", command.CreatedNode.Children[0].Name);
        AssertDistinctNames(document);
    }

    [Fact]
    public void Duplicate_ReplacesTheTrailingDigitsWithTheSmallestFreeNumber()
    {
        var stats = Enumerable.Range(0, 6).Select(i => Node("TextBlock", "lblStat" + i)).ToArray();
        var document = NewDocument(Node("StackPanel", "pnlRoot", stats));
        var command = new DuplicateNodeCommand(document, stats[0].Id);

        command.Execute();

        Assert.Equal("lblStat6", command.CreatedNode!.Name);
    }

    [Fact]
    public void Duplicate_UndoThenRedo_GivesTheSameNames()
    {
        var button = Node("Button", "btnPlay", Node("StackPanel", "pnlPlayFace"));
        var document = NewDocument(Node("StackPanel", "pnlRoot", button));
        var command = new DuplicateNodeCommand(document, button.Id);

        command.Execute();
        var names = AllNames(document);
        command.Undo();
        command.Execute();

        Assert.Equal(names, AllNames(document));
    }

    [Fact]
    public void Duplicate_OfTheRoot_NamesTheWrapper_AndKeepsEveryNameDistinct()
    {
        var root = Node("StackPanel", "pnl1", Node("Button", "btn1"));
        var document = NewDocument(root);
        var command = new DuplicateNodeCommand(document, root.Id);

        command.Execute();

        Assert.NotSame(root, document.Root);
        AssertDistinctNames(document);
        var names = AllNames(document);

        command.Undo();
        Assert.Same(root, document.Root);
        command.Execute();

        AssertDistinctNames(document);
        Assert.Equal(names, AllNames(document));
    }

    [Fact]
    public void Paste_TwiceTheSameSubtree_KeepsEveryNameDistinct()
    {
        var slot = Node("StackPanel", "pnlPlaySlot", Node("Button", "btnPlay"));
        var document = NewDocument(Node("StackPanel", "pnlRoot", slot));

        var first = new PasteNodeCommand(document, slot.DeepClone(), slot.Id);
        first.Execute();
        var second = new PasteNodeCommand(document, slot.DeepClone(), slot.Id);
        second.Execute();

        AssertDistinctNames(document);
        Assert.Equal("pnlPlaySlot2", first.InsertedNode.Name);
        Assert.Equal("btnPlay2", first.InsertedNode.Children[0].Name);

        var names = AllNames(document);
        second.Undo();
        second.Execute();
        Assert.Equal(names, AllNames(document));
    }

    [Fact]
    public void Paste_WithoutTarget_OnAnExistingRoot_NamesTheWrapper_AndKeepsEveryNameDistinct()
    {
        var root = Node("StackPanel", "pnl1", Node("Button", "btn1"));
        var document = NewDocument(root);
        var command = new PasteNodeCommand(document, root.DeepClone(), null);

        command.Execute();

        Assert.NotSame(root, document.Root);
        AssertDistinctNames(document);
        var names = AllNames(document);

        command.Undo();
        Assert.Same(root, document.Root);
        command.Execute();

        AssertDistinctNames(document);
        Assert.Equal(names, AllNames(document));
    }

    [Fact]
    public void ADuplicatedDocument_Serialized_LoadsWithoutADuplicateElementName()
    {
        var play = Node("Button", "btnPlay", Node("TextBlock", "lblPlay"));
        var window = Node("Window", "wndMain", Node("StackPanel", "pnlMain", play));
        window.SetProperty("Left", "0");
        window.SetProperty("Top", "0");
        window.SetProperty("Width", "320");
        window.SetProperty("Height", "200");
        var document = NewDocument(window);

        new DuplicateNodeCommand(document, play.Id).Execute();

        var xaml = new UIScreenXamlSerializer().Serialize(document);
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var loaded = UIScreenLoader.Load(desktop, XamlDocumentSource.FromString(xaml, "inline"));

        Assert.NotNull(loaded);
    }

    private static UIControlRegistryEntry Entry(string controlType) =>
        UIControlRegistry.Default.TryGet(controlType)
        ?? throw new InvalidOperationException("Unknown toolbox entry " + controlType);

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

    private static List<string?> AllNames(UIScreenDocument document)
    {
        var names = new List<string?>();
        Collect(document.Root!, names);
        return names;
    }

    private static void Collect(UIScreenNode node, List<string?> names)
    {
        names.Add(node.Name);
        foreach (var child in node.Children)
        {
            Collect(child, names);
        }
    }

    private static void AssertDistinctNames(UIScreenDocument document)
    {
        var names = AllNames(document);
        Assert.All(names, name => Assert.False(string.IsNullOrEmpty(name)));
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }
}
