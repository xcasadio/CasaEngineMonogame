using CasaEngine.Core.Logging;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.Dialogue.UI;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.MGUI;
using CasaEngine.Tests.UI;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Dialogue;

/// <summary>
/// A project replaces the built-in dialogue box markup by naming a <c>.uiscreen</c> asset in
/// <see cref="ProjectSettings.DialogueScreenAsset"/> (bound screens program, D12, P8, D14). Without the setting
/// nothing changes; once a replacement is loaded it is always used -- only a load failure falls back to the
/// built-in markup, with a warning. A replacement that is missing or mistypes an element the screen drives is
/// still used, with a warning per problem and that part simply not shown. The warnings are captured through the
/// engine's static log seam, so this class runs in <see cref="ProjectEnvironmentCollection"/>, like the other
/// classes that capture logs.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class DialogueScreenReplacementTests : IDisposable
{
    private const string ValidMarkup = """
        <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                Left="0" Top="0" Height="150" TitleText="Project dialogue" Padding="14">
          <StackPanel Name="pnlContent" Orientation="Vertical" Spacing="8">
            <TextBlock Name="lblMarker" Text="project" />
            <TextBlock Name="lblLine" Text="" WrapText="True" />
            <StackPanel Name="pnlChoices" Orientation="Vertical" />
          </StackPanel>
        </Window>
        """;

    private readonly string _root = Directory.CreateTempSubdirectory("dialogue-replacement-").FullName;
    private readonly Guid _screenId = Guid.NewGuid();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private sealed class ScreenEnvelopeLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            var asset = new UIScreenAsset();
            asset.Load(JObject.Parse(File.ReadAllText(fileName)));
            return asset;
        }

        public bool IsFileSupported(string fileName) => true;
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = new();

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) => Warnings.Add(msg);
        public void WriteError(string msg) { }
    }

    private static List<string> CaptureWarnings(Action action)
    {
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            action();
        }
        finally
        {
            Logs.Close(); // detaches every logger added during this test, including `logger`.
        }

        return logger.Warnings;
    }

    /// <summary>Writes the replacement's envelope and markup, and returns a manager whose project settings name
    /// <paramref name="setting"/> as the dialogue screen.</summary>
    private AssetContentManager NewAssets(string setting, string markup = ValidMarkup)
    {
        var info = new AssetInfo(_screenId) { Name = "ProjectDialogue", FileName = "ProjectDialogue.uiscreen" };
        File.WriteAllText(Path.Combine(_root, "ProjectDialogue.xaml"), markup);
        File.WriteAllText(Path.Combine(_root, info.FileName), $$"""
            {
              "id": "{{_screenId}}",
              "name": "ProjectDialogue",
              "source_xaml_file": "ProjectDialogue.xaml"
            }
            """);

        var settings = new ProjectSettings { DialogueScreenAsset = setting };
        var assets = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(settings, _root, id => id == _screenId ? info : null),
        };
        assets.RegisterAssetLoader(typeof(UIScreenAsset), new ScreenEnvelopeLoader());
        return assets;
    }

    private static DialogueScreen Build(DialogueScreen screen)
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        screen.BuildWindow(desktop);
        return screen;
    }

    private static bool Declares(DialogueScreen screen, string name)
        => screen.WindowForTests.TryGetElementByName(name, out MGElement _);

    [Fact]
    public void WithoutTheSetting_TheBuiltInMarkupIsUsed_AndNothingIsLogged()
    {
        var assets = NewAssets(setting: string.Empty);
        DialogueScreen screen = null;

        var warnings = CaptureWarnings(() => screen = Build(new DialogueScreen(new DialogueService(), static () => { }, null, assets)));

        Assert.False(screen.UsesReplacementMarkupForTests);
        Assert.Equal("Dialogue", screen.WindowForTests.TitleText);
        Assert.NotNull(screen.CloseButtonForTests);
        Assert.Empty(warnings);
        Assert.Equal(0, assets.CollectUnreferenced());
    }

    [Fact]
    public void WithAValidReplacement_ItIsUsed_DrivesTheLine_AndIsGivenBackOnDispose()
    {
        var assets = NewAssets(setting: _screenId.ToString());
        var service = new DialogueService();
        DialogueScreen screen = null;

        var warnings = CaptureWarnings(() =>
            screen = Build(new DialogueScreen(service, static () => { }, null, assets) { ShowCloseButton = false }));
        screen.Show();
        service.ShowLine(new DialogueLine("Bonjour."));

        Assert.Empty(warnings);
        Assert.True(screen.UsesReplacementMarkupForTests);
        Assert.Equal("Project dialogue", screen.WindowForTests.TitleText);
        Assert.True(Declares(screen, "lblMarker"));
        Assert.True(screen.WindowForTests.TryGetElementByName("lblLine", out MGTextBlock line));
        Assert.Contains("Bonjour.", line.Text);
        Assert.Null(screen.CloseButtonForTests);

        Assert.Equal(0, assets.CollectUnreferenced()); // the screen holds its envelope
        screen.Dispose();
        Assert.Equal(1, assets.CollectUnreferenced());
    }

    [Fact]
    public void AReplacementWithACloseButton_KeepsIt_WhenTheBoxShowsOne()
    {
        var markup = ValidMarkup.Replace(
            """<StackPanel Name="pnlChoices" Orientation="Vertical" />""",
            """<StackPanel Name="pnlChoices" Orientation="Vertical" /><Button Name="btnClose"><TextBlock Text="Fermer" /></Button>""");
        var assets = NewAssets(setting: _screenId.ToString(), markup);
        DialogueScreen screen = null;

        var warnings = CaptureWarnings(() => screen = Build(new DialogueScreen(new DialogueService(), static () => { }, null, assets)));

        Assert.Empty(warnings);
        Assert.True(screen.UsesReplacementMarkupForTests);
        Assert.NotNull(screen.CloseButtonForTests);
    }

    [Fact]
    public void AnUnknownAsset_FallsBack_AndTheWarningNamesIt()
    {
        var unknown = Guid.NewGuid().ToString();
        var assets = NewAssets(setting: unknown);
        DialogueScreen screen = null;

        var warnings = CaptureWarnings(() => screen = Build(new DialogueScreen(new DialogueService(), static () => { }, null, assets)));

        Assert.False(screen.UsesReplacementMarkupForTests);
        Assert.Equal("Dialogue", screen.WindowForTests.TitleText);
        var warning = Assert.Single(warnings);
        Assert.Contains(unknown, warning);
        Assert.Contains("cannot be loaded", warning);
    }

    [Fact]
    public void MalformedMarkup_FallsBack_WithAWarning()
    {
        var assets = NewAssets(setting: _screenId.ToString(), markup: ValidMarkup.Replace("<TextBlock Name=\"lblMarker\"", "<NoSuchElement Name=\"lblMarker\""));
        DialogueScreen screen = null;

        var warnings = CaptureWarnings(() => screen = Build(new DialogueScreen(new DialogueService(), static () => { }, null, assets)));

        Assert.False(screen.UsesReplacementMarkupForTests);
        Assert.Equal("Dialogue", screen.WindowForTests.TitleText);
        var warning = Assert.Single(warnings);
        Assert.Contains("XamlLoaderException", warning);
    }

    /// <summary>(a) No pnlChoices: the replacement is still used, the line shows, and the presenter's
    /// ShowChoices creates no button and throws nothing.</summary>
    [Fact]
    public void AReplacementWithoutPnlChoices_IsUsed_AndTheLineShows_ButChoicesAreSkipped()
    {
        var markup = ValidMarkup.Replace("""<StackPanel Name="pnlChoices" Orientation="Vertical" />""", "");
        var assets = NewAssets(setting: _screenId.ToString(), markup);
        var service = new DialogueService();
        DialogueScreen screen = null;

        var warnings = CaptureWarnings(() =>
            screen = Build(new DialogueScreen(service, static () => { }, null, assets) { ShowCloseButton = false }));
        screen.Show();
        service.ShowLine(new DialogueLine("Bonjour."));

        Assert.True(screen.UsesReplacementMarkupForTests);
        Assert.True(screen.WindowForTests.TryGetElementByName("lblLine", out MGTextBlock line));
        Assert.Contains("Bonjour.", line.Text);

        service.ShowChoices(new[] { "OUI", "NON" }); // must not throw: pnlChoices is missing
        Assert.Empty(screen.ChoiceButtonsForTests);

        var warning = Assert.Single(warnings);
        Assert.Contains("pnlChoices", warning);
    }

    /// <summary>(b) No lblLine: the replacement is still used, ShowLine throws nothing, and ShowChoices still
    /// adds its buttons to pnlChoices.</summary>
    [Fact]
    public void AReplacementWithoutLblLine_IsUsed_AndChoicesStillShow()
    {
        var markup = ValidMarkup.Replace("""<TextBlock Name="lblLine" Text="" WrapText="True" />""", "");
        var assets = NewAssets(setting: _screenId.ToString(), markup);
        var service = new DialogueService();
        DialogueScreen screen = null;

        var warnings = CaptureWarnings(() =>
            screen = Build(new DialogueScreen(service, static () => { }, null, assets) { ShowCloseButton = false }));
        screen.Show();
        service.ShowLine(new DialogueLine("Bonjour.")); // must not throw: no lblLine
        service.ShowChoices(new[] { "OUI", "NON" });

        Assert.True(screen.UsesReplacementMarkupForTests);
        Assert.Equal(2, screen.ChoiceButtonsForTests.Count);
        Assert.True(screen.WindowForTests.TryGetElementByName("pnlChoices", out MGElement choicesPanel));
        Assert.All(screen.ChoiceButtonsForTests, button => Assert.Same(choicesPanel, button.Parent));

        var warning = Assert.Single(warnings);
        Assert.Contains("lblLine", warning);
    }

    /// <summary>(c) lblLine declared as a Button: the replacement is still used, and the warning names both
    /// the actual type found and the expected one.</summary>
    [Fact]
    public void AReplacementWhoseLblLineIsAButton_IsUsed_AndNamesTheActualType()
    {
        var markup = ValidMarkup.Replace(
            """<TextBlock Name="lblLine" Text="" WrapText="True" />""",
            """<Button Name="lblLine"><TextBlock Text="x" /></Button>""");
        var assets = NewAssets(setting: _screenId.ToString(), markup);
        DialogueScreen screen = null;

        var warnings = CaptureWarnings(() =>
            screen = Build(new DialogueScreen(new DialogueService(), static () => { }, null, assets) { ShowCloseButton = false }));

        Assert.True(screen.UsesReplacementMarkupForTests);
        var warning = Assert.Single(warnings);
        Assert.Contains("lblLine", warning);
        Assert.Contains("MGButton", warning);
        Assert.Contains("TextBlock", warning);
    }

    /// <summary>(d) btnClose declared inside pnlChoices, not as a direct child of pnlContent, with
    /// ShowCloseButton false: the replacement is still used, nothing is logged (btnClose is not a contract
    /// problem when the box shows no close button), and the button is simply hidden.</summary>
    [Fact]
    public void ABtnCloseOutsidePnlContent_IsHidden_WhenTheBoxShowsNoCloseButton()
    {
        var markup = ValidMarkup.Replace(
            """<StackPanel Name="pnlChoices" Orientation="Vertical" />""",
            """<StackPanel Name="pnlChoices" Orientation="Vertical"><Button Name="btnClose"><TextBlock Text="Fermer" /></Button></StackPanel>""");
        var assets = NewAssets(setting: _screenId.ToString(), markup);
        DialogueScreen screen = null;

        var warnings = CaptureWarnings(() =>
            screen = Build(new DialogueScreen(new DialogueService(), static () => { }, null, assets) { ShowCloseButton = false }));

        Assert.True(screen.UsesReplacementMarkupForTests);
        Assert.Null(screen.CloseButtonForTests);
        Assert.Empty(warnings);
        Assert.True(screen.WindowForTests.TryGetElementByName("btnClose", out MGElement closeButton));
        Assert.Equal(Visibility.Collapsed, closeButton.Visibility);
    }

    /// <summary>(d') No pnlContent at all (its children sit directly under another panel), but btnClose is
    /// present, with ShowCloseButton false: the replacement is still used, nothing throws, the button is
    /// hidden, and one warning names pnlContent.</summary>
    [Fact]
    public void AReplacementWithoutPnlContent_IsUsed_AndTheOrphanedCloseButtonIsHidden()
    {
        var markup = """
            <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                    Left="0" Top="0" Height="150" TitleText="Project dialogue" Padding="14">
              <StackPanel Name="pnlOuter" Orientation="Vertical" Spacing="8">
                <TextBlock Name="lblLine" Text="" WrapText="True" />
                <StackPanel Name="pnlChoices" Orientation="Vertical" />
                <Button Name="btnClose"><TextBlock Text="Fermer" /></Button>
              </StackPanel>
            </Window>
            """;
        var assets = NewAssets(setting: _screenId.ToString(), markup);
        DialogueScreen screen = null;

        var warnings = CaptureWarnings(() =>
            screen = Build(new DialogueScreen(new DialogueService(), static () => { }, null, assets) { ShowCloseButton = false }));

        Assert.True(screen.UsesReplacementMarkupForTests);
        Assert.Null(screen.CloseButtonForTests);
        var warning = Assert.Single(warnings);
        Assert.Contains("pnlContent", warning);
        Assert.True(screen.WindowForTests.TryGetElementByName("btnClose", out MGElement closeButton));
        Assert.Equal(Visibility.Collapsed, closeButton.Visibility);
    }

    /// <summary>(e) No btnClose with ShowCloseButton true: the replacement is still used, CloseButtonForTests
    /// stays null, and one warning names btnClose.</summary>
    [Fact]
    public void AReplacementWithoutBtnClose_IsUsed_WhenTheBoxShowsOne()
    {
        var assets = NewAssets(setting: _screenId.ToString());
        DialogueScreen screen = null;

        var warnings = CaptureWarnings(() => screen = Build(new DialogueScreen(new DialogueService(), static () => { }, null, assets)));

        Assert.True(screen.UsesReplacementMarkupForTests);
        Assert.Null(screen.CloseButtonForTests);
        Assert.Contains("btnClose", Assert.Single(warnings));
    }

    [Fact]
    public void TheAssetManagerConstructor_RefusesANullManager()
    {
        Assert.Throws<ArgumentNullException>(() => new DialogueScreen(new DialogueService(), static () => { }, null, null));
    }
}
