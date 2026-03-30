using System;
using System.IO;
using CasaEngine.Editor.ContentBrowser.Models;
using CasaEngine.Editor.ContentBrowser.Services;
using Xunit;

namespace CasaEngine.Tests.ContentBrowser;

public sealed class FileSystemScannerTests : IDisposable
{
    private readonly string _rootPath;

    public FileSystemScannerTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame_ContentBrowserTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public void ScanDirectory_BuildsFoldersAndFilesTree()
    {
        var texturesPath = Path.Combine(_rootPath, "Textures");
        Directory.CreateDirectory(texturesPath);
        File.WriteAllText(Path.Combine(texturesPath, "hero.png"), "texture-data");

        var root = FileSystemScanner.ScanDirectory(_rootPath);

        Assert.True(root.IsDirectory);
        Assert.Single(root.SubFolders);
        var folder = Assert.Single(root.SubFolders);
        Assert.Equal("Textures", folder.Name);

        var file = Assert.Single(folder.Files);
        Assert.Equal("hero.png", file.Name);
        Assert.Equal(ContentItemType.Texture, file.Type);
        Assert.Equal(folder, file.Parent);
    }

    [Fact]
    public void Refresh_UpdatesDirectoryChildrenInPlace()
    {
        var root = FileSystemScanner.ScanDirectory(_rootPath);
        Assert.Empty(root.Children);

        var createdFile = Path.Combine(_rootPath, "new-file.cs");
        File.WriteAllText(createdFile, "class Demo {}\n");

        FileSystemScanner.Refresh(root);

        var file = Assert.Single(root.Files);
        Assert.Equal("new-file.cs", file.Name);
        Assert.Equal(ContentItemType.Script, file.Type);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }
}