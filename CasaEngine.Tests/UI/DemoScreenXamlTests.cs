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

    [Theory]
    [InlineData("chkShowModel")]
    [InlineData("chkShowSkeleton")]
    [InlineData("chkFootLock")]
    [InlineData("chkUseDefaultDuration")]
    [InlineData("btnDeactivateAll")]
    [InlineData("btnActivateAll")]
    [InlineData("btnPauseContinue")]
    [InlineData("btnSingleStep")]
    [InlineData("btnWalkToIdle")]
    [InlineData("btnIdleToWalk")]
    [InlineData("btnWalkToRun")]
    [InlineData("btnRunToWalk")]
    [InlineData("sldStepSize")]
    [InlineData("sldCustomDuration")]
    [InlineData("sldTimeScale")]
    [InlineData("sldIdleWeight")]
    [InlineData("sldWalkWeight")]
    [InlineData("sldRunWeight")]
    public void TheBlendingControls_DeclareEveryControlItsScreenBinds(string controlName)
    {
        // Eighteen lookups, every one of them wired to an event the demo listens for. A name dropped in the
        // markup would throw on the first frame of that demo; here it names itself.
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();

        var window = UIScreenLoader.Load(desktop, XamlDocumentSource.FromFile(ScreenPath("blending-controls.xaml")));

        Assert.True(window.TryGetElementByName(controlName, out MGElement _), $"'{controlName}' is not declared.");
    }

    /// <summary>
    /// Every position each demo screen used to compute in C#, now declared, checked against the arithmetic
    /// it replaced on the harness's 640x480 desktop.
    /// </summary>
    [Theory]
    // pause menu: centred, 300x200.
    [InlineData("pause-menu.xaml", (640 - 300) / 2, (480 - 200) / 2, 300, 200)]
    // F1 hint: centred horizontally, 14px above the bottom, 300x36.
    [InlineData("demo-hint.xaml", (640 - 300) / 2, 480 - 36 - 14, 300, 36)]
    // demo navigator: top-right corner inset by 10, 300 wide, 440 tall and the view has room for it.
    [InlineData("demo-info.xaml", 640 - 300 - 10, 10, 300, 440)]
    // blending controls: same corner, but 560 does NOT fit in 480 -- the height is capped to the space the
    // margin leaves, which is exactly what Math.Min(560, viewport - 20) used to do.
    [InlineData("blending-controls.xaml", 640 - 320 - 10, 10, 320, 480 - 20)]
    public void EachPlacedScreen_LandsWhereItsOldArithmeticPutIt(string fileName, int left, int top, int width, int height)
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();

        var window = UIScreenLoader.Load(desktop, XamlDocumentSource.FromFile(ScreenPath(fileName)));

        Assert.Equal(left, window.Left);
        Assert.Equal(top, window.Top);
        Assert.Equal(width, window.WindowWidth);
        Assert.Equal(height, window.WindowHeight);
    }

    [Fact]
    public void APlacedScreen_KeepsItsFullContentArea()
    {
        // The inset from the screen edge must not eat into the window's own content. Margin on a root window
        // would have done exactly that -- it insets the content, like a second padding -- which is why
        // placement has its own ScreenMargin. Checked against a screen that declares no inset at all.
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();

        var placed = UIScreenLoader.Load(desktop, XamlDocumentSource.FromFile(ScreenPath("demo-info.xaml")));
        var unplaced = UIScreenLoader.Load(desktop, XamlDocumentSource.FromFile(ScreenPath("pause-menu.xaml")));

        Assert.Equal(default, placed.Margin);
        Assert.Equal(default, unplaced.Margin);
    }

    [Theory]
    [InlineData("ui-overlay-hud.xaml")]
    [InlineData("screen-effect-smoke-hud.xaml")]
    public void TheTwoCornerHuds_KeepTheirAbsolutePosition(string fileName)
    {
        // These two were always at a fixed offset, so they declare no placement and must not acquire one.
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();

        var window = UIScreenLoader.Load(desktop, XamlDocumentSource.FromFile(ScreenPath(fileName)));

        Assert.False(window.HasScreenPlacement);
        Assert.Equal(10, window.Left);
        Assert.Equal(10, window.Top);
    }

    [Theory]
    [InlineData("ui-overlay-hud.xaml", "Press F1 to toggle the demo navigator",
        new[] { "lblTitle", "lblTime", "lblHint", "btnPause", "btnDialogue" })]
    [InlineData("screen-effect-smoke-hud.xaml", "Press 1: alpha fade to black and back, BelowUI (default)",
        new[] { "lblTitle", "lblBelowUiHint", "lblAboveUiHint", "lblExpectation" })]
    public void TheTwoCornerHuds_GrowWithTheirText(string fileName, string wrappingLine, string[] rowNames)
    {
        // Both used to declare a fixed Height that their own text outgrew once its hint lines wrapped, so the
        // window cut its last rows: "Open Dialogue" shrank to a sliver, and the smoke's expectation line
        // vanished. This harness measures a character as half the font size, narrower than the real font, so
        // the shipped text fits here even at the old fixed height; lengthening the line that wraps does what
        // the real font does. A window whose height follows its content keeps every row inside, whatever the
        // text becomes. Same check as the dialogue's clipped-choice defect.
        var markup = File.ReadAllText(ScreenPath(fileName));
        var lengthened = string.Join(" ", Enumerable.Repeat(wrappingLine, 6));
        Assert.Contains(wrappingLine, markup);
        markup = markup.Replace(wrappingLine, lengthened);

        var (desktop, runtime) = HeadlessUiTestHarness.NewDesktop();
        var window = UIScreenLoader.Load(desktop, XamlDocumentSource.FromString(markup, fileName));
        desktop.Windows.Add(window);
        HeadlessUiTestHarness.AdvanceFrame(runtime, desktop, 16);

        var windowBounds = new Microsoft.Xna.Framework.Rectangle(window.Left, window.Top, window.WindowWidth, window.WindowHeight);
        foreach (var name in rowNames)
        {
            Assert.True(window.TryGetElementByName(name, out MGElement row), $"'{name}' is not declared.");

            var bounds = row.LayoutBounds;
            Assert.True(bounds.Width > 0 && bounds.Height > 0, $"'{name}' was not laid out with a real size.");
            Assert.True(windowBounds.Contains(bounds), $"'{name}' at {bounds} is cut by the window {windowBounds}.");
        }
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
