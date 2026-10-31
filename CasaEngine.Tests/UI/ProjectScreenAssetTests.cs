using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Fonts;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using Microsoft.Xna.Framework;
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

    /// <summary>
    /// The menu of SampleProject is valid against that project's own catalogue, the checks being those a game runs
    /// when it opens the screen (ADR-0078).
    /// </summary>
    [Theory]
    [InlineData("main-menu.uiscreen")]
    [InlineData("menu-popup.uiscreen")]
    public void EverySampleMenuScreen_PassesTheChecksOfADataDrivenScreen(string fileName)
    {
        var projectPath = Path.Combine(FindRepositoryRoot(), "Projects", "SampleProject");
        var catalog = JObject.Parse(File.ReadAllText(Path.Combine(projectPath, "AssetInfos.json")))["asset_infos"]!;
        bool IsCatalogued(string key, string value, string assetType)
            => catalog.Any(info => (string?)info[key] == value && (string?)info["asset_type"] == assetType);

        var assets = new AssetContentManager();
        var soundId = Guid.NewGuid();
        using var soundHold = assets.Register(soundId, new SoundAsset());

        var context = new UIScreenActionContext
        {
            ScreenExists = name => IsCatalogued("name", name, "uiscreen"),
            WorldExists = worldFile => IsCatalogued("file_name", worldFile, "world"),
            AcquireSound = name => IsCatalogued("name", name, "sound")
                ? assets.Acquire<SoundAsset>(soundId)
                : throw new InvalidOperationException($"No sound '{name}'."),
            IsTopScreen = static _ => true,
            OpenScreen = static _ => { },
            CloseScreen = static _ => { },
            ChangeWorld = static _ => { },
            PlaySound = static _ => { },
            Quit = static () => { },
            StartFade = static _ => { },
            IsFadeRunning = static () => false,
        };

        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var envelopePath = Path.Combine(projectPath, "Screens", fileName);
        var screen = new DataDrivenScreen(ReadEnvelope(envelopePath), envelopePath, context);

        Assert.NotNull(screen.BuildWindow(desktop));
        screen.Dispose();
    }

    /// <summary>
    /// The screens of PixelUiSample are valid against that project's own catalogue, resolution and mapping, and their
    /// font is what the XAML names (ADR-0079, ADR-0080). The font itself needs a graphics device: it is proved by
    /// running the sample.
    /// </summary>
    [Theory]
    [InlineData("title")]
    [InlineData("options")]
    public void ThePixelSampleScreens_PassTheChecksOfADataDrivenScreen(string screenName)
    {
        var projectPath = Path.Combine(FindRepositoryRoot(), "Projects", "PixelUiSample");
        var catalog = JObject.Parse(File.ReadAllText(Path.Combine(projectPath, "AssetInfos.json")))["asset_infos"]!;
        var project = JObject.Parse(File.ReadAllText(Path.Combine(projectPath, "PixelUiSample.json")));
        JToken? Find(string key, string value, string assetType)
            => catalog.FirstOrDefault(info => (string?)info[key] == value && (string?)info["asset_type"] == assetType);

        Assert.Equal(string.Empty, (string?)project["GameplayDllName"]);
        Assert.True((bool?)project["VirtualResolution"]?["ScaleUI"]);

        var assets = new AssetContentManager();
        var soundId = Guid.NewGuid();
        using var soundHold = assets.Register(soundId, new SoundAsset());

        var heldFonts = new List<string>();
        var context = new UIScreenActionContext
        {
            ScreenExists = name => Find("name", name, "uiscreen") != null,
            WorldExists = worldFile => Find("file_name", worldFile, "world") != null,
            AcquireSound = name => Find("name", name, "sound") != null
                ? assets.Acquire<SoundAsset>(soundId)
                : throw new InvalidOperationException($"No sound '{name}'."),
            IsTopScreen = static _ => true,
            OpenScreen = static _ => { },
            CloseScreen = static _ => { },
            ChangeWorld = static _ => { },
            PlaySound = static _ => { },
            Quit = static () => { },
            StartFade = static _ => { },
            IsFadeRunning = static () => false,
            DesignResolution = new Point((int)project["VirtualResolution"]!["Width"]!, (int)project["VirtualResolution"]!["Height"]!),
            AcquireFont = name =>
            {
                Assert.NotNull(Find("name", name, "fnt"));
                heldFonts.Add(name);
                return new MemoryStream();
            },
            HasUIInputMapping = !string.IsNullOrEmpty((string?)project["UIInputMapping"]),
        };

        var (desktop, _) = HeadlessUiTestHarness.NewDesktop(960, 540);
        var envelopePath = Path.Combine(projectPath, "Screens", screenName + ".uiscreen");
        var screen = new DataDrivenScreen(ReadEnvelope(envelopePath), envelopePath, context);

        var window = screen.BuildWindow(desktop);
        screen.Dispose();

        Assert.Equal(3f, window.Scale);
        var font = Assert.Single(heldFonts);

        var fontFileName = (string)Find("name", font, "fnt")!["file_name"]!;
        var descriptor = BitmapFontDescriptor.Parse(File.ReadAllText(Path.Combine(projectPath, fontFileName)), fontFileName);
        Assert.Contains($"FontFamily=\"{descriptor.Face}\"", File.ReadAllText(Path.Combine(projectPath, "Screens", screenName + ".xaml")));

        var pageFileName = BitmapFontDescriptor.CatalogFileNameNextTo(
            projectPath, Path.Combine(projectPath, fontFileName), Assert.Single(descriptor.PageFiles));
        Assert.NotNull(Find("file_name", pageFileName, "png"));
    }

    /// <summary>
    /// The mapping PixelUiSample names is catalogued and passes the checks of the UI (ADR-0080), and its sound reaches
    /// a catalogued file. Reading it on a real device is proved by running the sample.
    /// </summary>
    [Fact]
    public void ThePixelSampleMapping_PassesTheChecksOfTheUI()
    {
        var projectPath = Path.Combine(FindRepositoryRoot(), "Projects", "PixelUiSample");
        var catalog = JObject.Parse(File.ReadAllText(Path.Combine(projectPath, "AssetInfos.json")))["asset_infos"]!;
        var project = JObject.Parse(File.ReadAllText(Path.Combine(projectPath, "PixelUiSample.json")));

        var mappingInfo = catalog.Single(info => (string?)info["name"] == (string?)project["UIInputMapping"]);
        Assert.Equal("buttonsmapping", (string?)mappingInfo["asset_type"]);

        var mapping = new ButtonsMapping();
        mapping.Load(JObject.Parse(File.ReadAllText(Path.Combine(projectPath, (string)mappingInfo["file_name"]!))));
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();

        _ = new UINavigationInput(desktop, mapping);

        Assert.False(desktop.UseRawNavigationInput);

        var soundInfo = catalog.Single(info => (string?)info["asset_type"] == "sound");
        var sound = JObject.Parse(File.ReadAllText(Path.Combine(projectPath, (string)soundInfo["file_name"]!)));
        Assert.Contains(catalog, info => (string?)info["id"] == (string?)sound["audio_file_asset_id"]);
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
