using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>
/// Guards the screens a real catalogued project ships, loaded the way the game loads them: through the
/// envelope, in <see cref="XamlLoaderMode.Strict"/>, on a headless desktop.
/// <para/>
/// Unlike the engine demos, which pass a plain document, these go through the loader's other door -- an
/// envelope plus the path it was read from -- and exercise the relative resolution of `source_xaml_file`
/// against real files rather than temporary ones.
/// </summary>
public class ProjectScreenAssetTests
{
    public static TheoryData<string> RpgDemoScreens()
    {
        TheoryData<string> data = new();

        foreach (var path in Directory.EnumerateFiles(RpgDemoScreensDirectory(), "*.uiscreen", SearchOption.AllDirectories))
        {
            data.Add(Path.GetRelativePath(RpgDemoScreensDirectory(), path));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(RpgDemoScreens))]
    public void EveryRpgDemoScreen_LoadsThroughItsEnvelope(string relativePath)
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var envelopePath = Path.Combine(RpgDemoScreensDirectory(), relativePath);

        var window = UIScreenLoader.Load(desktop, ReadEnvelope(envelopePath), envelopePath);

        Assert.NotNull(window);
    }

    [Fact]
    public void TheTitleScreen_KeepsTheButtonNamesTheLegacyScreenUsed()
    {
        // ButtonStartGame and ButtonExit were the legacy screen's own names, and the only thing in that
        // whole dead format worth carrying forward. Keeping them keeps the original intent readable.
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var envelopePath = Path.Combine(RpgDemoScreensDirectory(), "TitleScreen", "TitleScreen.uiscreen");

        var window = UIScreenLoader.Load(desktop, ReadEnvelope(envelopePath), envelopePath);

        Assert.True(window.TryGetElementByName("ButtonStartGame", out MGButton _));
        Assert.True(window.TryGetElementByName("ButtonExit", out MGButton _));
    }

    [Fact]
    public void TheGameOverScreen_DeclaresItsReturnButton()
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var envelopePath = Path.Combine(RpgDemoScreensDirectory(), "GameOver", "GameOverScreen.uiscreen");

        var window = UIScreenLoader.Load(desktop, ReadEnvelope(envelopePath), envelopePath);

        Assert.True(window.TryGetElementByName("btnReturnToTitle", out MGButton _));
    }

    [Fact]
    public void TheMainHud_DeclaresAnEmptyPortraitSlotAndItsLifeBar()
    {
        // The portrait's texture is a PNG the world loads from disk at run time, so the document declares
        // the slot and the screen fills it -- or collapses it when the file is missing.
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var envelopePath = Path.Combine(RpgDemoScreensDirectory(), "MainHUD", "MainHUD.uiscreen");

        var window = UIScreenLoader.Load(desktop, ReadEnvelope(envelopePath), envelopePath);

        Assert.True(window.TryGetElementByName("imgPortrait", out MGImage portrait));
        Assert.Null(portrait.Source);
        Assert.True(window.TryGetElementByName("pgbLife", out MGProgressBar lifeBar));
        Assert.Equal(0f, lifeBar.Minimum);
        Assert.Equal(100f, lifeBar.Maximum);
    }

    /// <summary>
    /// The three RPGDemo screens land where their old C# arithmetic put them, on the harness's 640x480 desktop.
    /// </summary>
    [Theory]
    // title screen: centred, 320x200.
    [InlineData("TitleScreen", "TitleScreen.uiscreen", (640 - 320) / 2, (480 - 200) / 2)]
    // game over: centred, 380x180.
    [InlineData("GameOver", "GameOverScreen.uiscreen", (640 - 380) / 2, (480 - 180) / 2)]
    // HUD: bottom-left corner inset by 12, 230x66.
    [InlineData("MainHUD", "MainHUD.uiscreen", 12, 480 - 66 - 12)]
    public void EachScreen_LandsWhereItsOldArithmeticPutIt(string folder, string envelope, int left, int top)
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var envelopePath = Path.Combine(RpgDemoScreensDirectory(), folder, envelope);

        var window = UIScreenLoader.Load(desktop, ReadEnvelope(envelopePath), envelopePath);

        Assert.Equal(left, window.Left);
        Assert.Equal(top, window.Top);
    }

    private static UIScreenAsset ReadEnvelope(string envelopePath)
    {
        var asset = new UIScreenAsset();
        asset.Load(JObject.Parse(File.ReadAllText(envelopePath)));
        return asset;
    }

    private static string RpgDemoScreensDirectory()
        => Path.Combine(FindRepositoryRoot(), "Projects", "RPGDemo", "Screens");

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
