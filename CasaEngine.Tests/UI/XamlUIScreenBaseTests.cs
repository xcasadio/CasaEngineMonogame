using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>
/// Pins the base class screens inherit once their tree lives in XAML.
/// <para/>
/// Everything here drives <c>BuildWindow(MGDesktop)</c> and never constructs a <c>UIRoot</c> -- that type is
/// sealed around a MonoGame backend, which is the whole reason the seam takes a desktop.
/// </summary>
public class XamlUIScreenBaseTests
{
    private const string Markup = """
        <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                Left="0" Top="0" Width="320" Height="200">
          <StackPanel Name="pnlRoot" Orientation="Vertical">
            <TextBlock Name="lblTitle" Text="Hello" />
          </StackPanel>
        </Window>
        """;

    [Fact]
    public void BuildWindow_LoadsTheTree_AndHandsItToTheScreen()
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var screen = new FakeScreen();

        var window = screen.BuildWindow(desktop);

        Assert.NotNull(window);
        Assert.Same(window, screen.LoadedWindow);
        Assert.Equal(1, screen.LoadedCount);
        Assert.Equal("Hello", screen.Title.Text);
    }

    [Fact]
    public void GetWindows_YieldsNothing_UntilTheScreenIsBuilt()
    {
        var screen = new FakeScreen();
        Assert.Empty(screen.GetWindows());

        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var window = screen.BuildWindow(desktop);

        Assert.Same(window, Assert.Single(screen.GetWindows()));
    }

    [Fact]
    public void FindControl_Names_TheMissingControlAndTheDocument()
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var screen = new FakeScreen();
        screen.BuildWindow(desktop);

        var error = Assert.Throws<InvalidOperationException>(() => screen.Find<MGTextBlock>("lblNotThere"));

        // A screen whose XAML lost a name must say so at once, naming both the control and the document.
        Assert.Contains("lblNotThere", error.Message);
        Assert.Contains("FakeScreen", error.Message);
        Assert.Contains("fake-screen.xaml", error.Message);
    }

    [Fact]
    public void FindControl_Says_WhenTheNameBelongsToAnotherKindOfControl()
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var screen = new FakeScreen();
        screen.BuildWindow(desktop);

        // TryGetElementByName<T> answers false for this too, so the distinction has to be made here.
        var error = Assert.Throws<InvalidOperationException>(() => screen.Find<MGButton>("lblTitle"));

        Assert.Contains("lblTitle", error.Message);
        Assert.Contains(nameof(MGButton), error.Message);
        Assert.Contains(nameof(MGTextBlock), error.Message);
    }

    [Fact]
    public void FindControl_Refuses_BeforeTheScreenIsBuilt()
    {
        var screen = new FakeScreen();

        var error = Assert.Throws<InvalidOperationException>(() => screen.Find<MGTextBlock>("lblTitle"));

        Assert.Contains("before its XAML was loaded", error.Message);
    }

    [Fact]
    public void AnAssetBackedScreen_LoadsThroughTheAssetDoor()
    {
        var root = Directory.CreateTempSubdirectory("xaml-screen-base-").FullName;

        try
        {
            var xamlPath = Path.Combine(root, "fake.xaml");
            File.WriteAllText(xamlPath, Markup);

            var asset = new UIScreenAsset { SourceXamlFile = "fake.xaml" };
            var assetPath = Path.Combine(root, "fake.uiscreen");
            File.WriteAllText(assetPath, "{}");

            var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
            var screen = new FakeScreen(asset, assetPath);

            var window = screen.BuildWindow(desktop);

            Assert.NotNull(window);
            Assert.Equal("Hello", screen.Title.Text);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AnAssetBackedScreen_NamesTheEnvelope_WhenAControlIsMissing()
    {
        var root = Directory.CreateTempSubdirectory("xaml-screen-base-").FullName;

        try
        {
            File.WriteAllText(Path.Combine(root, "fake.xaml"), Markup);
            var assetPath = Path.Combine(root, "fake.uiscreen");
            File.WriteAllText(assetPath, "{}");

            var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
            var screen = new FakeScreen(new UIScreenAsset { SourceXamlFile = "fake.xaml" }, assetPath);
            screen.BuildWindow(desktop);

            var error = Assert.Throws<InvalidOperationException>(() => screen.Find<MGTextBlock>("lblNotThere"));

            Assert.Contains(assetPath, error.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AScreen_Refuses_ASourceItCannotName()
    {
        Assert.Throws<ArgumentNullException>(() => new FakeScreen((XamlDocumentSource)null));
        Assert.Throws<ArgumentNullException>(() => new FakeScreen(null, "some.uiscreen"));
        Assert.Throws<ArgumentException>(() => new FakeScreen(new UIScreenAsset(), "  "));
    }

    /// <summary>A screen that does nothing but what the base class asks of it.</summary>
    private sealed class FakeScreen : XamlUIScreenBase
    {
        public MGWindow LoadedWindow { get; private set; }
        public int LoadedCount { get; private set; }
        public MGTextBlock Title { get; private set; }

        public override UILayer Layer => UILayer.HUD;

        public FakeScreen()
            : base(XamlDocumentSource.FromString(Markup, "fake-screen.xaml"))
        {
        }

        public FakeScreen(XamlDocumentSource source)
            : base(source)
        {
        }

        public FakeScreen(UIScreenAsset asset, string assetFilePath)
            : base(asset, assetFilePath)
        {
        }

        protected override void OnWindowLoaded(MGWindow window)
        {
            LoadedWindow = window;
            LoadedCount++;
            Title = FindControl<MGTextBlock>("lblTitle");
        }

        /// <summary>Exposes the protected lookup so the tests can drive it on its own.</summary>
        public T Find<T>(string name) where T : MGElement => FindControl<T>(name);
    }
}
