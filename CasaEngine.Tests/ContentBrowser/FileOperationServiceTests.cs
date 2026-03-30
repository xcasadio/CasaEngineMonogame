using System;
using System.Drawing.Imaging;
using System.IO;
using CasaEngine.Editor.ContentBrowser.Models;
using CasaEngine.Editor.ContentBrowser.Services;
using Xunit;

namespace CasaEngine.Tests.ContentBrowser;

public sealed class FileOperationServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileOperationService _service = new();

    public FileOperationServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame_ContentBrowserOps", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public void CreateDirectory_Rename_Move_And_Delete_UpdateDiskAndModel()
    {
        File.WriteAllText(Path.Combine(_rootPath, "hero.png"), "seed");
        var root = FileSystemScanner.ScanDirectory(_rootPath);
        _service.SetRoot(root);

        Assert.True(_service.CreateDirectory(_rootPath, "Textures"));
        var texturesFolder = Assert.Single(root.SubFolders);
        Assert.Equal("Textures", texturesFolder.Name);

        var heroFile = Assert.Single(root.Files);
        Assert.True(_service.Rename(heroFile, "hero-renamed.png"));
        var renamedFile = Assert.Single(root.Files);
        Assert.Equal("hero-renamed.png", renamedFile.Name);
        Assert.True(File.Exists(Path.Combine(_rootPath, "hero-renamed.png")));

        Assert.True(_service.Move(renamedFile, texturesFolder));
        Assert.Empty(root.Files);
        texturesFolder = Assert.Single(root.SubFolders);
        var movedFile = Assert.Single(texturesFolder.Files);
        Assert.Equal("hero-renamed.png", movedFile.Name);
        Assert.True(File.Exists(Path.Combine(texturesFolder.FullPath, "hero-renamed.png")));

        Assert.True(_service.Delete(movedFile));
        texturesFolder = Assert.Single(root.SubFolders);
        Assert.Empty(texturesFolder.Files);
        Assert.False(File.Exists(Path.Combine(texturesFolder.FullPath, "hero-renamed.png")));
    }

    public void Dispose()
    {
        _service.Dispose();
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }
}