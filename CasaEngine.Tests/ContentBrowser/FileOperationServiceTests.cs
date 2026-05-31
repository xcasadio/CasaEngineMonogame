using System;
using System.Drawing.Imaging;
using System.IO;
using CasaEngine.Editor.ContentBrowser.Models;
using CasaEngine.Editor.ContentBrowser.Services;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Tests;
using Xunit;

namespace CasaEngine.Tests.ContentBrowser;

[Collection(ProjectEnvironmentCollection.Name)]
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

    [Fact]
    public void Import_TiledTmxReportsImporterWarnings()
    {
        string externalDirectory = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame_ContentBrowserExternal", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(externalDirectory);
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = _rootPath;
            EditorAssetCatalogService.Clear();

            string imagePath = Path.Combine(externalDirectory, "tiles.png");
            File.WriteAllBytes(imagePath, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

            string tsxPath = Path.Combine(externalDirectory, "tiles.tsx");
            File.WriteAllText(tsxPath,
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <tileset version="1.10" tiledversion="1.10.2" name="tiles" tilewidth="16" tileheight="16" tilecount="1" columns="1">
                 <image source="tiles.png" width="16" height="16"/>
                </tileset>
                """);

            string tmxPath = Path.Combine(externalDirectory, "level.tmx");
            File.WriteAllText(tmxPath,
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <map version="1.10" tiledversion="1.10.2" orientation="orthogonal" width="1" height="1" tilewidth="16" tileheight="16" infinite="0">
                 <tileset firstgid="1" source="tiles.tsx"/>
                 <layer id="1" name="Ground" width="1" height="1">
                  <data encoding="csv">2147483649</data>
                 </layer>
                </map>
                """);

            var root = FileSystemScanner.ScanDirectory(_rootPath);
            _service.SetRoot(root);
            string warning = string.Empty;
            _service.WarningOccurred += message => warning = message;

            Assert.True(_service.Import(new[] { tmxPath }, root));

            Assert.Contains("Tiled map", warning, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("flip", warning, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(Path.Combine(_rootPath, "level.tileMap")));
            Assert.True(File.Exists(Path.Combine(_rootPath, "level.tileset")));
            Assert.True(File.Exists(Path.Combine(_rootPath, "tiles.texture")));
            Assert.True(File.Exists(Path.Combine(_rootPath, "tiles.png")));
            Assert.False(Directory.Exists(Path.Combine(_rootPath, "level_Imported")));
            Assert.False(Directory.Exists(Path.Combine(_rootPath, "Textures")));
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(externalDirectory, true);
        }
    }

    [Fact]
    public void TryImportOperation_TiledTmjCreatesFlatAssetsAndUndoRemovesThem()
    {
        string externalDirectory = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame_ContentBrowserExternal", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(externalDirectory);
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = _rootPath;
            EditorAssetCatalogService.Clear();

            string imagePath = Path.Combine(externalDirectory, "tiles.png");
            File.WriteAllBytes(imagePath, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

            string tmjPath = Path.Combine(externalDirectory, "level.tmj");
            File.WriteAllText(tmjPath,
                """
                {
                    "type": "map",
                    "orientation": "orthogonal",
                    "width": 1,
                    "height": 1,
                    "tilewidth": 16,
                    "tileheight": 16,
                    "infinite": false,
                    "tilesets": [
                        {
                            "firstgid": 1,
                            "name": "tiles",
                            "tilewidth": 16,
                            "tileheight": 16,
                            "tilecount": 1,
                            "columns": 1,
                            "image": "tiles.png",
                            "imagewidth": 16,
                            "imageheight": 16
                        }
                    ],
                    "layers": [
                        {
                            "id": 1,
                            "name": "Ground",
                            "type": "tilelayer",
                            "width": 1,
                            "height": 1,
                            "data": [1]
                        }
                    ]
                }
                """);

            var root = FileSystemScanner.ScanDirectory(_rootPath);
            _service.SetRoot(root);

            Assert.True(_service.TryImportOperation(new[] { tmjPath }, root.FullPath, out var operation));
            string importedMapPath = Path.Combine(_rootPath, "level.tmj");
            string tileMapPath = Path.Combine(_rootPath, "level.tileMap");
            string tileSetPath = Path.Combine(_rootPath, "level.tileset");
            string textureWrapperPath = Path.Combine(_rootPath, "tiles.texture");
            string texturePath = Path.Combine(_rootPath, "tiles.png");

            Assert.True(File.Exists(importedMapPath));
            Assert.True(File.Exists(tileMapPath));
            Assert.True(File.Exists(tileSetPath));
            Assert.True(File.Exists(textureWrapperPath));
            Assert.True(File.Exists(texturePath));
            Assert.False(Directory.Exists(Path.Combine(_rootPath, "level_Imported")));
            Assert.False(Directory.Exists(Path.Combine(_rootPath, "Textures")));

            Assert.True(operation.Undo(_service));
            Assert.False(File.Exists(importedMapPath));
            Assert.False(File.Exists(tileMapPath));
            Assert.False(File.Exists(tileSetPath));
            Assert.False(File.Exists(textureWrapperPath));
            Assert.False(File.Exists(texturePath));
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(externalDirectory, true);
        }
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