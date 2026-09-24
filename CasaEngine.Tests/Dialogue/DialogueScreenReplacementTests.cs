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
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Dialogue;

/// <summary>
/// A project replaces the built-in dialogue box markup by naming a <c>.uiscreen</c> asset in
/// <see cref="ProjectSettings.DialogueScreenAsset"/> (bound screens program, D12, P8). Without the setting nothing
/// changes; a valid replacement is used; a replacement that cannot be loaded or breaks the element contract falls
/// back to the built-in markup with a warning. The warnings are captured through the engine's static log seam, so
/// this class runs in <see cref="ProjectEnvironmentCollection"/>, like the other classes that capture logs.
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

    [Theory]
    [InlineData("<TextBlock Name=\"lblLine\" Text=\"\" WrapText=\"True\" />", "", "declares no 'lblLine'")]
    [InlineData("<TextBlock Name=\"lblLine\" Text=\"\" WrapText=\"True\" />", "<Button Name=\"lblLine\"><TextBlock Text=\"x\" /></Button>", "'lblLine' as a MGButton")]
    [InlineData("<StackPanel Name=\"pnlChoices\" Orientation=\"Vertical\" />", "<StackPanel Name=\"pnlChoices\" Orientation=\"Vertical\"><Button Name=\"btnClose\"><TextBlock Text=\"x\" /></Button></StackPanel>", "not a direct child of 'pnlContent'")]
    public void AReplacementThatBreaksTheContract_FallsBack_AndSaysWhy(string declared, string replacement, string expected)
    {
        var assets = NewAssets(setting: _screenId.ToString(), markup: ValidMarkup.Replace(declared, replacement));
        DialogueScreen screen = null;

        var warnings = CaptureWarnings(() =>
            screen = Build(new DialogueScreen(new DialogueService(), static () => { }, null, assets) { ShowCloseButton = false }));

        Assert.False(screen.UsesReplacementMarkupForTests);
        Assert.Equal("Dialogue", screen.WindowForTests.TitleText);
        Assert.False(Declares(screen, "lblMarker"));
        var warning = Assert.Single(warnings);
        Assert.Contains(expected, warning);
    }

    /// <summary>A box that shows a close button needs one in the replacement; the built-in markup has it.</summary>
    [Fact]
    public void AReplacementWithoutACloseButton_FallsBack_WhenTheBoxShowsOne()
    {
        var assets = NewAssets(setting: _screenId.ToString());
        DialogueScreen screen = null;

        var warnings = CaptureWarnings(() => screen = Build(new DialogueScreen(new DialogueService(), static () => { }, null, assets)));

        Assert.False(screen.UsesReplacementMarkupForTests);
        Assert.NotNull(screen.CloseButtonForTests);
        Assert.Contains("declares no 'btnClose'", Assert.Single(warnings));
    }

    [Fact]
    public void TheAssetManagerConstructor_RefusesANullManager()
    {
        Assert.Throws<ArgumentNullException>(() => new DialogueScreen(new DialogueService(), static () => { }, null, null));
    }
}
