using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>
/// Guards the screen documents the demos ship, by loading the real files from
/// `CasaEngine.Demos/Content/Screens/` exactly as the runtime does: <see cref="XamlLoaderMode.Strict"/>, on a
/// headless desktop.
/// <para/>
/// This is what makes a XAML-authored screen safe to change. A typo in the markup used to surface only when
/// somebody launched the demo and watched; now it fails here, naming the file and the line. The theory picks
/// up any screen added later on its own.
/// </summary>
public class DemoScreenXamlTests
{
    public static TheoryData<string> ShippedScreens()
    {
        TheoryData<string> data = new();

        foreach (var path in Directory.EnumerateFiles(ScreensDirectory(), "*.xaml", SearchOption.AllDirectories))
        {
            data.Add(Path.GetFileName(path));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ShippedScreens))]
    public void EveryShippedDemoScreen_LoadsInStrictMode(string fileName)
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();

        var window = UIScreenLoader.Load(desktop, XamlDocumentSource.FromFile(ScreenPath(fileName)));

        Assert.NotNull(window);
    }

    [Fact]
    public void ThePauseMenu_DeclaresWhatItsScreenLooksUp()
    {
        // The three names PauseMenuScreen.OnWindowLoaded depends on, plus the size it centres itself by.
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();

        var window = UIScreenLoader.Load(desktop, XamlDocumentSource.FromFile(ScreenPath("pause-menu.xaml")));

        Assert.Equal("Pause", window.TitleText);
        Assert.Equal(300, window.WindowWidth);
        Assert.Equal(200, window.WindowHeight);

        Assert.True(window.TryGetElementByName("lblTitle", out MGTextBlock _));
        Assert.True(window.TryGetElementByName("lblDescription", out MGTextBlock _));
        Assert.True(window.TryGetElementByName("btnResume", out MGButton _));
    }

    [Fact]
    public void TheOverlayHud_DeclaresWhatItsScreenLooksUp()
    {
        // lblTime is rewritten every frame, so HudScreen finds it once at load; the two buttons carry its
        // callbacks. A rename in the markup breaks all three, and must break here rather than in the demo.
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();

        var window = UIScreenLoader.Load(desktop, XamlDocumentSource.FromFile(ScreenPath("ui-overlay-hud.xaml")));

        Assert.True(window.TryGetElementByName("lblTime", out MGTextBlock _));
        Assert.True(window.TryGetElementByName("btnPause", out MGButton _));
        Assert.True(window.TryGetElementByName("btnDialogue", out MGButton _));
    }

    [Fact]
    public void TheDemoNavigator_DeclaresTheEmptyListItsScreenFills()
    {
        // The demo buttons are data -- one per demo -- so the document declares an empty named panel and
        // DemoInfoScreen fills it. If that panel lost its name the navigator would have no entries at all.
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();

        var window = UIScreenLoader.Load(desktop, XamlDocumentSource.FromFile(ScreenPath("demo-info.xaml")));

        Assert.True(window.TryGetElementByName("lblTitle", out MGTextBlock _));
        Assert.True(window.TryGetElementByName("lblDescription", out MGTextBlock _));
        Assert.True(window.TryGetElementByName("lstDemos", out MGUI.Core.UI.Containers.MGStackPanel list));
        Assert.Empty(list.Children);
    }

    private static string ScreenPath(string fileName) => Path.Combine(ScreensDirectory(), fileName);

    private static string ScreensDirectory()
        => Path.Combine(FindRepositoryRoot(), "CasaEngine.Demos", "Content", "Screens");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.MonoGame.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("CasaEngine repository root was not found.");
    }
}
