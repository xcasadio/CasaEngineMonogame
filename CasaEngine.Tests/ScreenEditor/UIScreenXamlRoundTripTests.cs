using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Xaml;
using Xunit;

namespace CasaEngine.Tests.ScreenEditor;

public class UIScreenXamlRoundTripTests
{
    [Fact]
    public void RoundTrip_PreservesSimpleHierarchyAndProperties()
    {
        const string xaml = """
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Name="MainWindow"
        Width="800"
        Height="600">
  <StackPanel Name="LayoutRoot" Orientation="Vertical">
    <Button Name="PlayButton" Text="Play" />
    <TextBlock Name="StatusText" Text="Ready" />
  </StackPanel>
</Window>
""";

        var parser = new UIScreenXamlParser();
        var serializer = new UIScreenXamlSerializer();

        var original = parser.Parse(xaml);
        var serialized = serializer.Serialize(original);
        var reparsed = parser.Parse(serialized);

        AssertDocumentEquivalent(original, reparsed);
    }

    [Fact]
    public void RoundTrip_PreservesPropertyElementContent()
    {
        const string xaml = """
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Name="DialogWindow">
  <Window.TitleBar>
    <DockPanel>
      <TextBlock Text="Dialog" />
    </DockPanel>
  </Window.TitleBar>
  <StackPanel>
    <TextBlock Text="Body" />
  </StackPanel>
</Window>
""";

        var parser = new UIScreenXamlParser();
        var serializer = new UIScreenXamlSerializer();

        var original = parser.Parse(xaml);
        var serialized = serializer.Serialize(original);
        var reparsed = parser.Parse(serialized);

        AssertDocumentEquivalent(original, reparsed);
    }

    private static void AssertDocumentEquivalent(UIScreenDocument expected, UIScreenDocument actual)
    {
        Assert.NotNull(expected.Root);
        Assert.NotNull(actual.Root);
        AssertNodeEquivalent(expected.Root!, actual.Root!);
    }

    private static void AssertNodeEquivalent(UIScreenNode expected, UIScreenNode actual)
    {
        Assert.Equal(expected.ControlType, actual.ControlType);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Properties.Count, actual.Properties.Count);

        foreach (var expectedProperty in expected.Properties)
        {
            Assert.True(actual.Properties.ContainsKey(expectedProperty.Key));
            var actualProperty = actual.Properties[expectedProperty.Key];
            Assert.Equal(expectedProperty.Value.ValueType, actualProperty.ValueType);
            Assert.Equal(expectedProperty.Value.SerializedValue, actualProperty.SerializedValue);
        }

        Assert.Equal(expected.Children.Count, actual.Children.Count);
        for (var index = 0; index < expected.Children.Count; index++)
        {
            AssertNodeEquivalent(expected.Children[index], actual.Children[index]);
        }
    }
}