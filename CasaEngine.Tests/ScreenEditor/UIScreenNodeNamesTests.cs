using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Naming;
using Xunit;

namespace CasaEngine.Tests.ScreenEditor;

public class UIScreenNodeNamesTests
{
    [Theory]
    [InlineData("Button", "btn")]
    [InlineData("TextBlock", "lbl")]
    [InlineData("Image", "img")]
    [InlineData("TextBox", "inp")]
    [InlineData("ListBox", "lst")]
    [InlineData("ComboBox", "cmb")]
    [InlineData("CheckBox", "chk")]
    [InlineData("RadioButton", "rdo")]
    [InlineData("Slider", "sld")]
    [InlineData("ProgressBar", "prg")]
    [InlineData("Window", "wnd")]
    [InlineData("StackPanel", "pnl")]
    [InlineData("DockPanel", "pnl")]
    [InlineData("Grid", "pnl")]
    [InlineData("UniformGrid", "pnl")]
    [InlineData("ScrollViewer", "pnl")]
    [InlineData("Border", "pnl")]
    [InlineData("Expander", "pnl")]
    [InlineData("OverlayPanel", "pnl")]
    public void GetPrefix_FollowsTheConventionTable(string controlType, string expected)
    {
        Assert.Equal(expected, UIScreenNodeNames.GetPrefix(controlType));
    }

    [Theory]
    [InlineData("MGStackPanel", "pnl")]
    [InlineData("MGButton", "btn")]
    [InlineData("MGTextBlock", "lbl")]
    public void GetPrefix_ReadsTheTypeWithTheMguiPrefix(string controlType, string expected)
    {
        Assert.Equal(expected, UIScreenNodeNames.GetPrefix(controlType));
    }

    [Theory]
    [InlineData("ContextMenu", "contextMenu")]
    [InlineData("MGContextMenu", "contextMenu")]
    public void GetPrefix_FallsBackToTheTypeNameForAnUnknownType(string controlType, string expected)
    {
        Assert.Equal(expected, UIScreenNodeNames.GetPrefix(controlType));
    }

    [Fact]
    public void CreateDefaultName_StartsAtOneThenCounts()
    {
        var document = new UIScreenDocument();
        var root = new UIScreenNode("StackPanel") { Name = "pnlRoot" };
        document.SetRoot(root);

        Assert.Equal("img1", UIScreenNodeNames.CreateDefaultName(document, "Image"));

        root.AddChild(new UIScreenNode("Image") { Name = "img1" });
        Assert.Equal("img2", UIScreenNodeNames.CreateDefaultName(document, "Image"));
        Assert.Equal("btn1", UIScreenNodeNames.CreateDefaultName(document, "Button"));
    }

    [Fact]
    public void CreateDefaultName_ReusesAGap()
    {
        var document = new UIScreenDocument();
        var root = new UIScreenNode("StackPanel") { Name = "pnlRoot" };
        document.SetRoot(root);
        root.AddChild(new UIScreenNode("Image") { Name = "img2" });

        Assert.Equal("img1", UIScreenNodeNames.CreateDefaultName(document, "Image"));
    }

    [Fact]
    public void CreateDefaultName_ComparesOrdinally()
    {
        var document = new UIScreenDocument();
        document.SetRoot(new UIScreenNode("Image") { Name = "IMG1" });

        Assert.Equal("img1", UIScreenNodeNames.CreateDefaultName(document, "Image"));
    }

    [Fact]
    public void CreateDefaultName_RespectsTheReservedNames()
    {
        var document = new UIScreenDocument();
        var reserved = new HashSet<string>(StringComparer.Ordinal) { "img1", "img2" };

        Assert.Equal("img3", UIScreenNodeNames.CreateDefaultName(document, "Image", reserved));
    }

    [Fact]
    public void AssignUniqueNames_KeepsAFreeName()
    {
        var document = new UIScreenDocument();
        document.SetRoot(new UIScreenNode("StackPanel") { Name = "pnlRoot" });
        var subtree = new UIScreenNode("Button") { Name = "btnPlay" };

        UIScreenNodeNames.AssignUniqueNames(document, subtree);

        Assert.Equal("btnPlay", subtree.Name);
    }

    [Fact]
    public void AssignUniqueNames_AppendsTheSmallestFreeNumberFromTwo()
    {
        var document = new UIScreenDocument();
        var root = new UIScreenNode("StackPanel") { Name = "pnlRoot" };
        document.SetRoot(root);
        root.AddChild(new UIScreenNode("Button") { Name = "btnPlay" });
        var subtree = new UIScreenNode("Button") { Name = "btnPlay" };

        UIScreenNodeNames.AssignUniqueNames(document, subtree);

        Assert.Equal("btnPlay2", subtree.Name);
    }

    [Fact]
    public void AssignUniqueNames_ReplacesTheTrailingDigits()
    {
        var document = new UIScreenDocument();
        var root = new UIScreenNode("StackPanel") { Name = "pnlRoot" };
        document.SetRoot(root);
        for (var i = 0; i <= 5; i++)
        {
            root.AddChild(new UIScreenNode("TextBlock") { Name = "lblStat" + i });
        }

        var subtree = new UIScreenNode("TextBlock") { Name = "lblStat0" };

        UIScreenNodeNames.AssignUniqueNames(document, subtree);

        Assert.Equal("lblStat6", subtree.Name);
    }

    [Fact]
    public void AssignUniqueNames_GivesAnUnnamedNodeADefaultName_AndCountsTheNamesGivenInTheSubtree()
    {
        var document = new UIScreenDocument();
        document.SetRoot(new UIScreenNode("StackPanel") { Name = "pnlRoot" });
        var subtree = new UIScreenNode("StackPanel");
        subtree.AddChild(new UIScreenNode("Button") { Name = "btn1" });
        subtree.AddChild(new UIScreenNode("Button"));
        subtree.AddChild(new UIScreenNode("Button") { Name = "btn1" });

        var names = UIScreenNodeNames.AssignUniqueNames(document, subtree);

        Assert.Equal("pnl1", subtree.Name);
        Assert.Equal("btn1", subtree.Children[0].Name);
        Assert.Equal("btn2", subtree.Children[1].Name);
        Assert.Equal("btn3", subtree.Children[2].Name);
        Assert.Equal(4, names.Count);
        Assert.Contains("btn3", names);
    }
}
