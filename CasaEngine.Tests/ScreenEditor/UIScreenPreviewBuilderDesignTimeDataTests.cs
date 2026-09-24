using CasaEngine.EditorServices.ScreenEditor.Preview;
using CasaEngine.EditorServices.ScreenEditor.Xaml;
using CasaEngine.Framework.UI.MGUI;
using CasaEngine.Tests.UI;
using MGUI.Core.UI;
using Xunit;

namespace CasaEngine.Tests.ScreenEditor;

/// <summary>
/// T4.2 (ADR-0038, "Design-time data"): <see cref="UIScreenPreviewBuilder"/>'s asset-aware overloads load a
/// screen's optional design-time data file, instantiate and populate the view model it names, and bind it as
/// the preview window's data context so bound controls show real values instead of the empty/mock placeholder.
/// A missing, invalid, or otherwise unusable design-time data file is reported without throwing, and the
/// preview still renders (without a data context).
/// </summary>
public sealed class UIScreenPreviewBuilderDesignTimeDataTests
{
    private const string WindowXamlHeader =
        "<Window xmlns=\"clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core\" " +
        "xmlns:dataBinding=\"clr-namespace:MGUI.Core.UI.DataBinding;assembly=MGUI.Core\">";

    /// <summary>A screen with a <c>TextBlock</c> bound to <see cref="TestViewModel.WeaponName"/>.</summary>
    private const string BoundScreenXaml =
        WindowXamlHeader +
        "<TextBlock Name=\"lblWeapon\" Text=\"{dataBinding:MGBinding Path=WeaponName}\" /></Window>";

    public sealed class TestViewModel
    {
        public string WeaponName { get; set; } = string.Empty;
    }

    private static UIScreenAsset NewAsset(string designTimeDataFile) => new() { DesignTimeDataFile = designTimeDataFile };

    [Fact]
    public void Build_WithNoDesignTimeDataFile_HasNoErrorAndNoDataContext()
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var document = new UIScreenXamlParser().Parse(BoundScreenXaml);
        var builder = new UIScreenPreviewBuilder();

        var window = builder.Build(desktop, document, NewAsset(string.Empty), "/does/not/matter.uiscreen", out var error);

        Assert.Null(error);
        Assert.Null(window.WindowDataContext);
    }

    [Fact]
    public void Build_WithValidDesignTimeDataFile_BindsTheViewModelAsDataContext_AndUpdatesBoundText()
    {
        var directory = CreateTempDirectory();
        try
        {
            var dataFile = Path.Combine(directory, "screen.designtime.json");
            // ElementFactory resolves types by their simple (non-namespace-qualified) name -- see
            // UIScreenDesignTimeDataLoader's doc comment -- so "TestViewModel" is enough here.
            File.WriteAllText(dataFile, /*lang=json,strict*/ """
                {
                  "view_model_type": "TestViewModel",
                  "values": { "WeaponName": "Poignard" }
                }
                """);

            var assetFilePath = Path.Combine(directory, "screen.uiscreen");
            var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
            var document = new UIScreenXamlParser().Parse(BoundScreenXaml);
            var builder = new UIScreenPreviewBuilder();

            var window = builder.Build(desktop, document, NewAsset("screen.designtime.json"), assetFilePath, out var error);

            Assert.Null(error);
            Assert.IsType<TestViewModel>(window.WindowDataContext);
            Assert.Equal("Poignard", ((TestViewModel)window.WindowDataContext).WeaponName);

            desktop.Windows.Add(window);
            desktop.Update();

            var textBlock = window.GetElementByName<MGTextBlock>("lblWeapon");
            Assert.Equal("Poignard", textBlock.Text);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Build_WithMissingDesignTimeDataFile_ReportsAnErrorAndRendersWithoutDataContext()
    {
        var directory = CreateTempDirectory();
        try
        {
            var assetFilePath = Path.Combine(directory, "screen.uiscreen");
            var desktop = HeadlessUiTestHarness.NewDesktop().Desktop;
            var document = new UIScreenXamlParser().Parse(BoundScreenXaml);
            var builder = new UIScreenPreviewBuilder();

            var builtWindow = builder.Build(desktop, document, NewAsset("missing.designtime.json"), assetFilePath, out var error);

            Assert.NotNull(error);
            Assert.Contains("missing.designtime.json", error);
            Assert.Null(builtWindow.WindowDataContext);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Build_WithInvalidJson_ReportsAnErrorAndRendersWithoutDataContext()
    {
        var directory = CreateTempDirectory();
        try
        {
            var dataFile = Path.Combine(directory, "screen.designtime.json");
            File.WriteAllText(dataFile, "{ not valid json");

            var assetFilePath = Path.Combine(directory, "screen.uiscreen");
            var desktop = HeadlessUiTestHarness.NewDesktop().Desktop;
            var document = new UIScreenXamlParser().Parse(BoundScreenXaml);
            var builder = new UIScreenPreviewBuilder();

            var window = builder.Build(desktop, document, NewAsset("screen.designtime.json"), assetFilePath, out var error);

            Assert.NotNull(error);
            Assert.Null(window.WindowDataContext);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Build_WithUnknownViewModelType_ReportsAnErrorAndRendersWithoutDataContext()
    {
        var directory = CreateTempDirectory();
        try
        {
            var dataFile = Path.Combine(directory, "screen.designtime.json");
            File.WriteAllText(dataFile, /*lang=json,strict*/ """
                { "view_model_type": "ThereIsNoSuchTypeAnywhere", "values": {} }
                """);

            var assetFilePath = Path.Combine(directory, "screen.uiscreen");
            var desktop = HeadlessUiTestHarness.NewDesktop().Desktop;
            var document = new UIScreenXamlParser().Parse(BoundScreenXaml);
            var builder = new UIScreenPreviewBuilder();

            var window = builder.Build(desktop, document, NewAsset("screen.designtime.json"), assetFilePath, out var error);

            Assert.NotNull(error);
            Assert.Contains("ThereIsNoSuchTypeAnywhere", error);
            Assert.Null(window.WindowDataContext);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("""{ "view_model_type": { "x": 1 }, "values": {} }""", "not a string")]
    [InlineData("""{ "view_model_type": 42 }""", "not a string")]
    [InlineData("""{ "view_model_type": "TestViewModel", "values": [ 1, 2 ] }""", "not a JSON object")]
    [InlineData("""[ "view_model_type", "TestViewModel" ]""", "JSON object")]
    public void Build_WithMalformedDesignTimeData_ReportsAnErrorAndRendersWithoutDataContext(string json, string expectedMessagePart)
    {
        var directory = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(directory, "screen.designtime.json"), json);

            var assetFilePath = Path.Combine(directory, "screen.uiscreen");
            var desktop = HeadlessUiTestHarness.NewDesktop().Desktop;
            var document = new UIScreenXamlParser().Parse(BoundScreenXaml);

            var window = new UIScreenPreviewBuilder().Build(desktop, document, NewAsset("screen.designtime.json"), assetFilePath, out var error);

            Assert.NotNull(error);
            Assert.Contains(expectedMessagePart, error);
            Assert.NotNull(window);
            Assert.Null(window.WindowDataContext);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_WithAnUnusablePath_ReportsAnErrorInsteadOfThrowing()
    {
        var result = UIScreenDesignTimeDataLoader.Load(NewAsset("bad\0name.json"), Path.Combine(Path.GetTempPath(), "screen.uiscreen"));

        Assert.NotNull(result.ErrorMessage);
        Assert.Null(result.DataContext);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "casaengine-designtime-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
