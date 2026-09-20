using CasaEngine.Engine.Environment;
using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>
/// Pins the runtime bridge from a UI screen asset to a live MGUI window.
/// <para/>
/// These run on a headless desktop (<see cref="HeadlessUiTestHarness"/>) and never construct a
/// <c>UIRoot</c>: that type is sealed around a MonoGame backend, which is exactly why the loader takes an
/// <see cref="MGDesktop"/>.
/// <para/>
/// They share the project-environment collection because path resolution reads
/// <see cref="EngineEnvironment.ProjectPath"/>, a process-wide static.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class UIScreenLoaderTests
{
    private const string ValidWindow = """
        <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                Left="0" Top="0" Width="320" Height="200" TitleText="Loader Test">
          <StackPanel Orientation="Vertical">
            <TextBlock Name="lblTitle" Text="Hello" />
          </StackPanel>
        </Window>
        """;

    [Fact]
    public void Load_BuildsTheWindow_FromXamlBesideTheAsset()
    {
        using var project = new TempProject();
        var assetPath = project.WriteAsset("MainMenu.uiscreen", "MainMenu.xaml");
        project.WriteXaml("MainMenu.xaml", ValidWindow);

        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var window = UIScreenLoader.Load(desktop, ReadAsset(assetPath), assetPath);

        Assert.NotNull(window);
        Assert.True(window.TryGetElementByName("lblTitle", out MGTextBlock title));
        Assert.Equal("Hello", title.Text);
    }

    [Fact]
    public void Load_FindsTheXaml_RelativeToTheProject_WhenItIsNotBesideTheAsset()
    {
        using var project = new TempProject();
        // The asset sits in a sub-folder and points at a path that only resolves from the project root.
        var assetPath = project.WriteAsset("Screens/MainMenu.uiscreen", "Shared/MainMenu.xaml");
        project.WriteXaml("Shared/MainMenu.xaml", ValidWindow);

        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();

        project.WithProjectPath(() =>
        {
            var window = UIScreenLoader.Load(desktop, ReadAsset(assetPath), assetPath);
            Assert.True(window.TryGetElementByName("lblTitle", out MGTextBlock _));
        });
    }

    [Fact]
    public void Load_AcceptsAnAbsoluteSourcePath()
    {
        using var project = new TempProject();
        var xamlPath = project.WriteXaml("Elsewhere.xaml", ValidWindow);
        var assetPath = project.WriteAsset("MainMenu.uiscreen", xamlPath);

        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var window = UIScreenLoader.Load(desktop, ReadAsset(assetPath), assetPath);

        Assert.True(window.TryGetElementByName("lblTitle", out MGTextBlock _));
    }

    [Fact]
    public void Load_Refuses_AnAssetThatNamesNoXamlFile()
    {
        using var project = new TempProject();
        var assetPath = project.WriteAsset("Empty.uiscreen", string.Empty);

        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var error = Assert.Throws<InvalidOperationException>(
            () => UIScreenLoader.Load(desktop, ReadAsset(assetPath), assetPath));

        Assert.Contains("SourceXamlFile", error.Message);
    }

    [Fact]
    public void Load_Reports_EveryPathItTried_WhenNoCandidateExists()
    {
        using var project = new TempProject();
        var assetPath = project.WriteAsset("Missing.uiscreen", "Missing.xaml");

        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();

        project.WithProjectPath(() =>
        {
            var error = Assert.Throws<FileNotFoundException>(
                () => UIScreenLoader.Load(desktop, ReadAsset(assetPath), assetPath));

            // The point of the message: a developer must see WHERE it looked, not just that it failed.
            Assert.Contains("Missing.xaml", error.Message);
            Assert.Contains(project.Root, error.Message);
        });
    }

    [Fact]
    public void Load_Reports_MalformedXml_AsAScreenDiagnostic()
    {
        var error = LoadMarkupExpectingFailure("""
            <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core">
              <StackPanel>
            </Window>
            """);

        Assert.Equal(XamlLoaderDiagnosticCode.ParseFailure, error.Diagnostic.Code);
    }

    [Fact]
    public void Load_Refuses_AnUnknownElement()
    {
        // Strict is the whole reason the runtime parses this way: Compatibility would not look.
        var error = LoadMarkupExpectingFailure("""
            <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                    Left="0" Top="0" Width="320" Height="200">
              <ThisControlDoesNotExist />
            </Window>
            """);

        Assert.Equal(XamlLoaderDiagnosticCode.UnknownType, error.Diagnostic.Code);
    }

    [Fact]
    public void Load_Refuses_ANameDeclaredTwice()
    {
        // A duplicate name would silently break every lookup by name, which is how screens find controls.
        var error = LoadMarkupExpectingFailure("""
            <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                    Left="0" Top="0" Width="320" Height="200">
              <StackPanel Orientation="Vertical">
                <TextBlock Name="lblSame" Text="first" />
                <TextBlock Name="lblSame" Text="second" />
              </StackPanel>
            </Window>
            """);

        Assert.Equal(XamlLoaderDiagnosticCode.DuplicateElementName, error.Diagnostic.Code);
    }

    [Fact]
    public void Load_Reports_ADuplicateNameATemplateCloned_AsAScreenDiagnostic()
    {
        // The one failure the loader really adds something to. Building the tree and attaching it are two
        // steps, and MGUI wraps only the first: a name a template applies to every item is rejected while
        // the built tree reaches the window's index, after XAMLParser has returned, so it escapes bare.
        // Without the loader's catch this would surface as MGDuplicateElementNameException with no file.
        // Markup borrowed from MGUI's own DuplicateElementNameTests.
        var error = LoadMarkupExpectingFailure("""
            <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" Left="20" Top="20" Width="400" Height="380">
                <ListBox Name="Options" Width="220" Height="120">
                    <ListBox.ItemTemplate>
                        <ContentTemplate>
                            <TextBlock Name="ItemLabel" Text="Item" />
                        </ContentTemplate>
                    </ListBox.ItemTemplate>
                    <TextBlock Text="Option 1" />
                    <TextBlock Text="Option 2" />
                </ListBox>
            </Window>
            """);

        Assert.Equal(XamlLoaderDiagnosticCode.DuplicateElementName, error.Diagnostic.Code);
        Assert.IsType<MGDuplicateElementNameException>(error.InnerException);
    }

    [Fact]
    public void Load_LeavesWindowRegistration_ToTheCaller()
    {
        // ScreenStack adds on push and removes on pop. A loader that registered would leak a window per build.
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var window = UIScreenLoader.Load(desktop, XamlDocumentSource.FromString(ValidWindow, "inline"));

        Assert.DoesNotContain(window, desktop.Windows);
    }

    [Fact]
    public void Load_BuildsAFreshWindow_OnEveryCall()
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var first = UIScreenLoader.Load(desktop, XamlDocumentSource.FromString(ValidWindow, "inline"));
        var second = UIScreenLoader.Load(desktop, XamlDocumentSource.FromString(ValidWindow, "inline"));

        Assert.NotSame(first, second);
    }

    [Fact]
    public void Load_AppliesTheThemeTheAssetNames()
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        MGTheme expected = new(desktop.Theme.FontSettings.DefaultFontFamily);
        desktop.Resources.AddTheme("LoaderTestTheme", expected);

        var window = UIScreenLoader.Load(desktop, XamlDocumentSource.FromString(ValidWindow, "inline"), "LoaderTestTheme");

        Assert.Same(expected, window.Theme);
    }

    [Fact]
    public void Load_StillLoads_WhenTheNamedThemeIsUnknown()
    {
        // GetThemeOrDefault already falls back; a missing theme must not cost the player their screen.
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var window = UIScreenLoader.Load(desktop, XamlDocumentSource.FromString(ValidWindow, "inline"), "NoSuchTheme");

        Assert.NotNull(window);
        Assert.True(window.TryGetElementByName("lblTitle", out MGTextBlock _));
    }

    private static XamlLoaderException LoadMarkupExpectingFailure(string markup)
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        return Assert.Throws<XamlLoaderException>(
            () => UIScreenLoader.Load(desktop, XamlDocumentSource.FromString(markup, "inline")));
    }

    private static UIScreenAsset ReadAsset(string assetFilePath)
    {
        var asset = new UIScreenAsset();
        asset.Load(Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(assetFilePath)));
        return asset;
    }

    /// <summary>A throwaway project directory, plus the save/restore of the process-wide project path.</summary>
    private sealed class TempProject : IDisposable
    {
        public string Root { get; }

        public TempProject() => Root = Directory.CreateTempSubdirectory("uiscreen-loader-").FullName;

        public string WriteAsset(string relativePath, string sourceXamlFile)
        {
            var path = Combine(relativePath);
            var escaped = sourceXamlFile.Replace("\\", "\\\\");
            File.WriteAllText(path, $$"""
                {
                  "id": "3f2b9e5c-9a1d-4f0b-9c3a-6d7e8f901234",
                  "name": "LoaderTest",
                  "source_xaml_file": "{{escaped}}"
                }
                """);
            return path;
        }

        public string WriteXaml(string relativePath, string markup)
        {
            var path = Combine(relativePath);
            File.WriteAllText(path, markup);
            return path;
        }

        /// <summary>Runs <paramref name="body"/> with this directory as the engine's project path.</summary>
        public void WithProjectPath(Action body)
        {
            var previous = EngineEnvironment.ProjectPath;
            EngineEnvironment.ProjectPath = Root;

            try
            {
                body();
            }
            finally
            {
                EngineEnvironment.ProjectPath = previous;
            }
        }

        private string Combine(string relativePath)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
                // A locked temp file must not turn a passing test red.
            }
        }
    }
}
