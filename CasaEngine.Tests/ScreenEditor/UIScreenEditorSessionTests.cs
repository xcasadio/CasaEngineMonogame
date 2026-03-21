using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Session;
using CasaEngine.Engine;
using CasaEngine.Framework.GUI.MGUI;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.ScreenEditor;

public class UIScreenEditorSessionTests
{
    [Fact]
    public void OpenSaveReload_PersistsDocumentChanges()
    {
        string tempDirectory = CreateTempDirectory();
        string assetPath = Path.Combine(tempDirectory, "MainScreen.uiscreen");
        string xamlPath = Path.Combine(tempDirectory, "MainScreen.xaml");
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = tempDirectory;
            File.WriteAllText(xamlPath, """
<?xml version="1.0" encoding="utf-8"?>
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TitleText="InitialTitle">
  <TextBlock Name="Label" Text="Hello" />
</Window>
""");

            var asset = CreateAsset("MainScreen", "MainScreen.uiscreen", "MainScreen.xaml");
            WriteAsset(assetPath, asset);

            var session = new UIScreenEditorSession();
            session.Open(asset, assetPath);

            Assert.NotNull(session.Document);
            Assert.False(session.IsDirty);
            Assert.NotNull(session.PreviewMarkup);

            session.Document!.Root!.SetProperty("TitleText", "UpdatedTitle");
            session.MarkDirty();
            session.Save();

            Assert.False(session.IsDirty);
            Assert.Contains("UpdatedTitle", File.ReadAllText(xamlPath));

            session.Reload();

            Assert.NotNull(session.Document);
            Assert.Equal("UpdatedTitle", session.Document!.Root!.Properties["TitleText"].SerializedValue);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void SetSelection_RejectsUnknownNode()
    {
        string tempDirectory = CreateTempDirectory();
        string assetPath = Path.Combine(tempDirectory, "MainScreen.uiscreen");
        string xamlPath = Path.Combine(tempDirectory, "MainScreen.xaml");
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = tempDirectory;
            File.WriteAllText(xamlPath, """
<?xml version="1.0" encoding="utf-8"?>
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" />
""");

            var asset = CreateAsset("MainScreen", "MainScreen.uiscreen", "MainScreen.xaml");
            WriteAsset(assetPath, asset);

            var session = new UIScreenEditorSession();
            session.Open(asset, assetPath);

            Assert.Throws<InvalidOperationException>(() => session.SetSelection(DocumentNodeId.NewId()));
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void Open_InvalidXaml_CapturesErrorState()
    {
        string tempDirectory = CreateTempDirectory();
        string assetPath = Path.Combine(tempDirectory, "BrokenScreen.uiscreen");
        string xamlPath = Path.Combine(tempDirectory, "BrokenScreen.xaml");
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = tempDirectory;
            File.WriteAllText(xamlPath, "<Window>");

            var asset = CreateAsset("BrokenScreen", "BrokenScreen.uiscreen", "BrokenScreen.xaml");
            WriteAsset(assetPath, asset);

            var session = new UIScreenEditorSession();
            session.Open(asset, assetPath);

            Assert.Null(session.Document);
            Assert.Null(session.PreviewMarkup);
            Assert.False(string.IsNullOrWhiteSpace(session.LastErrorMessage));
            Assert.Same(asset, session.CurrentAsset);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static UIScreenAsset CreateAsset(string name, string fileName, string sourceXamlFile)
    {
        return new UIScreenAsset
        {
            Name = name,
            FileName = fileName,
            SourceXamlFile = sourceXamlFile,
        };
    }

    private static void WriteAsset(string assetPath, UIScreenAsset asset)
    {
        var document = new JObject
        {
            ["id"] = asset.Id.ToString(),
            ["name"] = asset.Name,
            ["source_xaml_file"] = asset.SourceXamlFile,
            ["theme_name"] = asset.ThemeName,
            ["preview_resolution"] = new JObject
            {
                ["x"] = asset.PreviewResolution.X,
                ["y"] = asset.PreviewResolution.Y,
            },
            ["resource_files"] = new JArray(),
        };

        File.WriteAllText(assetPath, document.ToString());
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}