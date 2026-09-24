using System.ComponentModel;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;
using MGUI.Core.UI.DataBinding;
using MGUI.Core.UI.XAML;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>
/// A screen gives its window's data bindings back when it is disposed. MGUI keeps every binding in a static
/// registry until it is removed, and a binding left there keeps its target element, the window above it and the
/// data context reachable: a game that rebuilds its screens at every world change would pile them up. Loading the
/// XAML creates bindings, so this class runs in <see cref="MguiDataBindingCollection"/>.
/// </summary>
[Collection(MguiDataBindingCollection.Name)]
public class XamlUIScreenBaseBindingReleaseTests
{
    private const string Markup = """
        <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                xmlns:dataBinding="clr-namespace:MGUI.Core.UI.DataBinding;assembly=MGUI.Core"
                Left="0" Top="0" Width="320" Height="200">
          <StackPanel Orientation="Vertical">
            <TextBlock Name="lblTitle" Text="{dataBinding:MGBinding Path=Title}" />
            <TextBlock Name="lblSubtitle" Text="{dataBinding:MGBinding Path=Subtitle}" />
          </StackPanel>
        </Window>
        """;

    [Fact]
    public void DisposingAScreen_TakesItsWindowsBindingsOutOfTheRegistry()
    {
        var screen = new BoundScreen();
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        screen.BuildWindow(desktop);
        desktop.Update();
        var tree = screen.LoadedWindow.TraverseVisualTree().ToHashSet();

        Assert.Equal("Hello", screen.Title.Text);
        Assert.Equal(2, DataBindingManager.Bindings.Count(b => b.TargetObject is MGElement e && tree.Contains(e)));

        screen.Dispose();

        Assert.DoesNotContain(DataBindingManager.Bindings, b => b.TargetObject is MGElement e && tree.Contains(e));
        screen.Dispose(); // idempotent
    }

    private sealed class TitleViewModel : INotifyPropertyChanged
    {
        public string Title => "Hello";
        public string Subtitle => "World";

        public event PropertyChangedEventHandler PropertyChanged
        {
            add { }
            remove { }
        }
    }

    private sealed class BoundScreen : XamlUIScreenBase
    {
        public BoundScreen()
            : base(XamlDocumentSource.FromString(Markup, "bound-screen.xaml"))
        {
        }

        public override UILayer Layer => UILayer.HUD;

        public MGWindow LoadedWindow => Window;

        public MGTextBlock Title { get; private set; }

        protected override void OnWindowLoaded(MGWindow window)
        {
            Title = FindControl<MGTextBlock>("lblTitle");
            window.WindowDataContext = new TitleViewModel();
        }
    }
}
