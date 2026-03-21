using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using Xunit;

namespace CasaEngine.Tests.ScreenEditor;

public class UIScreenDocumentModelTests
{
    [Fact]
    public void NodeCreation_AssignsControlTypeAndStableId()
    {
        var node = new UIScreenNode("StackPanel");

        Assert.Equal("StackPanel", node.ControlType);
        Assert.NotEqual(default, node.Id);
        Assert.Null(node.Parent);
        Assert.Empty(node.Children);
    }

    [Fact]
    public void AddChild_AssignsParentAndSupportsReparenting()
    {
        var firstParent = new UIScreenNode("Grid");
        var secondParent = new UIScreenNode("DockPanel");
        var child = new UIScreenNode("Button");

        firstParent.AddChild(child);

        Assert.Same(firstParent, child.Parent);
        Assert.Single(firstParent.Children);

        secondParent.AddChild(child);

        Assert.Empty(firstParent.Children);
        Assert.Single(secondParent.Children);
        Assert.Same(secondParent, child.Parent);
    }

    [Fact]
    public void SetProperty_CreatesAndUpdatesPropertyValues()
    {
        var node = new UIScreenNode("TextBlock");

        var initial = node.SetProperty("Text", "Hello", "string");
        var updated = node.SetProperty("Text", "World", "string");

        Assert.Same(initial, updated);
        Assert.True(node.TryGetProperty("Text", out var propertyValue));
        Assert.NotNull(propertyValue);
        Assert.Equal("World", propertyValue.SerializedValue);
        Assert.Equal("string", propertyValue.ValueType);
    }

    [Fact]
    public void StableIds_RemainUnchangedAcrossMetadataAndPropertyUpdates()
    {
        var document = new UIScreenDocument();
        var root = new UIScreenNode("Canvas")
        {
            Name = "RootCanvas",
            DesignFlags = UIScreenDesignFlags.Locked,
        };
        var child = new UIScreenNode("Label");
        root.AddChild(child);
        document.SetRoot(root);

        var rootId = root.Id;
        var childId = child.Id;

        root.Name = "UpdatedRootCanvas";
        root.SetProperty("Width", "640", "double");
        child.SetProperty("Text", "Status", "string");

        Assert.Equal(rootId, root.Id);
        Assert.Equal(childId, child.Id);
        Assert.NotEqual(root.Id, child.Id);
        Assert.Same(root, document.FindNode(rootId));
        Assert.Same(child, document.FindNode(childId));
    }
}