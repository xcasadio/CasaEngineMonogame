using System.Threading;
using CasaEngine.Editor.Controls;
using CasaEngine.EditorServices.ScreenEditor.Commands;
using CasaEngine.Framework.UI.MGUI;
using CasaEngine.Tests.UI;
using MGUI.Core.UI;
using Xunit;

namespace CasaEngine.Tests.Controls;

/// <summary>T4.4 (D13): "File &gt; Save" writes modified screen documents through
/// <see cref="CasaEngine.EditorServices.ScreenEditor.Session.UIScreenDocumentFileWriter"/>. Covers
/// <see cref="UIScreenPreviewPanel.ShouldReload"/> directly (pure decision) and, through a headless
/// <see cref="MGWindow"/>, <see cref="UIScreenPreviewPanel.TrySaveDocument"/> and the reload-skip it
/// enables.</summary>
public class UIScreenPreviewPanelTests
{
    [Fact]
    public void ShouldReload_IdenticalXamlAndAssetBytes_ReturnsFalse()
    {
        var xaml = new byte[] { 1, 2, 3 };
        var asset = new byte[] { 4, 5, 6 };

        Assert.False(UIScreenPreviewPanel.ShouldReload(xaml, (byte[])xaml.Clone(), asset, (byte[])asset.Clone()));
    }

    [Fact]
    public void ShouldReload_DifferentXaml_ReturnsTrue()
    {
        var asset = new byte[] { 4, 5, 6 };

        Assert.True(UIScreenPreviewPanel.ShouldReload(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 9 }, asset, (byte[])asset.Clone()));
    }

    [Fact]
    public void ShouldReload_DifferentAsset_ReturnsTrue()
    {
        var xaml = new byte[] { 1, 2, 3 };

        Assert.True(UIScreenPreviewPanel.ShouldReload(xaml, (byte[])xaml.Clone(), new byte[] { 4, 5, 6 }, new byte[] { 4, 5, 9 }));
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void ShouldReload_AnyNullArgument_ReturnsTrue(bool nullXamlOnDisk, bool nullDocumentBytes, bool nullAssetOnDisk, bool nullLoadedAssetBytes)
    {
        var bytes = new byte[] { 1 };

        Assert.True(UIScreenPreviewPanel.ShouldReload(
            nullXamlOnDisk ? null : bytes,
            nullDocumentBytes ? null : bytes,
            nullAssetOnDisk ? null : bytes,
            nullLoadedAssetBytes ? null : bytes));
    }

    [Fact]
    public void TrySaveDocument_WritesModifiedDocument_AndReloadSkipsSameInstance()
    {
        string tempDirectory = CreateTempDirectory();
        string assetPath = Path.Combine(tempDirectory, "MainScreen.uiscreen");
        string xamlPath = Path.Combine(tempDirectory, "MainScreen.xaml");

        try
        {
            File.WriteAllText(xamlPath, """
<?xml version="1.0" encoding="utf-8"?>
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TitleText="InitialTitle">
  <TextBlock Name="Label" Text="Hello" />
</Window>
""");

            var asset = new UIScreenAsset
            {
                Name = "MainScreen",
                FileName = "MainScreen.uiscreen",
                SourceXamlFile = xamlPath,
            };
            File.WriteAllText(assetPath, $$"""
{
  "id": "{{Guid.NewGuid()}}",
  "name": "MainScreen",
  "source_xaml_file": {{System.Text.Json.JsonSerializer.Serialize(xamlPath)}},
  "theme_name": "",
  "preview_resolution": { "x": 1920, "y": 1080 },
  "resource_files": []
}
""");

            var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
            var window = new MGWindow(desktop, 0, 0, 400, 300);
            desktop.Windows.Add(window);

            var panel = new UIScreenPreviewPanel(window);
            panel.LoadAsset(asset, assetPath);

            var documentBeforeSave = panel.CurrentDocument;
            Assert.NotNull(documentBeforeSave);

            documentBeforeSave!.Root!.SetProperty("TitleText", "UpdatedTitle");

            Assert.True(panel.TrySaveDocument(out string errorMessage));
            Assert.Null(errorMessage);
            Assert.Contains("UpdatedTitle", File.ReadAllText(xamlPath));

            // The FileSystemWatcher this panel set up in LoadAsset fires on its own background thread for
            // the write TrySaveDocument just performed -- the same watcher event a real external edit
            // would raise. Poll Update() for a bit to give that event a chance to arrive and be picked up:
            // whether or not it does, ShouldReload must see the file as unchanged and skip the reload, so
            // the document instance never changes either way.
            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (DateTime.UtcNow < deadline)
            {
                panel.Update();
                Assert.Same(documentBeforeSave, panel.CurrentDocument);
                Thread.Sleep(20);
            }
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
