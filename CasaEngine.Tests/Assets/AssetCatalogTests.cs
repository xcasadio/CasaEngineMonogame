using System.Text;
using CasaEngine.Framework.Assets;
using Xunit;

namespace CasaEngine.Tests.Assets;

[Collection(ProjectEnvironmentCollection.Name)]
public class AssetCatalogTests
{
    [Fact]
    public void Load_WithFullEntries_PopulatesCatalogAndIndexes()
    {
        string catalogFilePath = WriteCatalogFile("""
            {
              "asset_infos": [
                {
                  "id": "5b65a744-7adb-4d93-bca0-6afe15a6d6ec",
                  "name": "Main Menu",
                  "file_name": "Screens\\main-menu.uiscreen",
                  "asset_type": "uiscreen"
                },
                {
                  "id": "ca9a6b74-76f2-4710-8724-33bad1ca77c2",
                  "name": "DefaultWorld",
                  "file_name": "DefaultWorld.world",
                  "asset_type": "world"
                }
              ]
            }
            """);

        try
        {
            AssetCatalog.Load(catalogFilePath);

            Assert.True(AssetCatalog.IsLoaded);
            Assert.Equal(2, AssetCatalog.AssetInfos.Count());

            var screenAsset = AssetCatalog.Get(Guid.Parse("5b65a744-7adb-4d93-bca0-6afe15a6d6ec"));
            Assert.NotNull(screenAsset);
            Assert.Equal("Main Menu", screenAsset.Name);
            Assert.Equal("Screens\\main-menu.uiscreen", screenAsset.FileName);
            Assert.Equal("uiscreen", screenAsset.AssetType);

            Assert.Same(screenAsset, AssetCatalog.Get("Main Menu"));
            Assert.Same(screenAsset, AssetCatalog.GetByFileName("Screens\\main-menu.uiscreen"));

            var worldAsset = AssetCatalog.Get(Guid.Parse("ca9a6b74-76f2-4710-8724-33bad1ca77c2"));
            Assert.NotNull(worldAsset);
            Assert.Equal("world", worldAsset.AssetType);
        }
        finally
        {
            CleanUp(catalogFilePath);
        }
    }

    [Fact]
    public void Load_WithMissingNameAndAssetType_DerivesThemFromFileName()
    {
        string catalogFilePath = WriteCatalogFile("""
            {
              "asset_infos": [
                {
                  "id": "11111111-2222-3333-4444-555555555555",
                  "file_name": "Entities\\Box.entity"
                }
              ]
            }
            """);

        try
        {
            AssetCatalog.Load(catalogFilePath);

            var assetInfo = AssetCatalog.Get(Guid.Parse("11111111-2222-3333-4444-555555555555"));
            Assert.NotNull(assetInfo);
            Assert.Equal("Box", assetInfo.Name);
            Assert.Equal("entity", assetInfo.AssetType);
        }
        finally
        {
            CleanUp(catalogFilePath);
        }
    }

    [Fact]
    public void Load_WithoutAssetInfosNode_LeavesCatalogEmptyButLoaded()
    {
        string catalogFilePath = WriteCatalogFile("{}");

        try
        {
            AssetCatalog.Load(catalogFilePath);

            Assert.True(AssetCatalog.IsLoaded);
            Assert.Empty(AssetCatalog.AssetInfos);
        }
        finally
        {
            CleanUp(catalogFilePath);
        }
    }

    [Fact]
    public void Load_WithUnknownProperties_IgnoresThem()
    {
        string catalogFilePath = WriteCatalogFile("""
            {
              "version": 2,
              "metadata": { "generator": "test" },
              "asset_infos": [
                {
                  "id": "11111111-2222-3333-4444-555555555555",
                  "name": "Box",
                  "file_name": "Entities\\Box.entity",
                  "asset_type": "entity",
                  "tags": ["a", "b"],
                  "import_settings": { "scale": 1.0 }
                }
              ]
            }
            """);

        try
        {
            AssetCatalog.Load(catalogFilePath);

            var assetInfo = AssetCatalog.Get(Guid.Parse("11111111-2222-3333-4444-555555555555"));
            Assert.NotNull(assetInfo);
            Assert.Equal("Box", assetInfo.Name);
            Assert.Equal("Entities\\Box.entity", assetInfo.FileName);
        }
        finally
        {
            CleanUp(catalogFilePath);
        }
    }

    [Fact]
    public void Load_WithUtf8ByteOrderMark_ParsesSuccessfully()
    {
        string catalogFilePath = WriteCatalogFile("""
            {
              "asset_infos": [
                {
                  "id": "11111111-2222-3333-4444-555555555555",
                  "name": "Box",
                  "file_name": "Entities\\Box.entity",
                  "asset_type": "entity"
                }
              ]
            }
            """,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        try
        {
            AssetCatalog.Load(catalogFilePath);

            Assert.NotNull(AssetCatalog.Get(Guid.Parse("11111111-2222-3333-4444-555555555555")));
        }
        finally
        {
            CleanUp(catalogFilePath);
        }
    }

    [Fact]
    public void Load_WithEntryWithoutId_Throws()
    {
        string catalogFilePath = WriteCatalogFile("""
            {
              "asset_infos": [
                {
                  "name": "Box",
                  "file_name": "Entities\\Box.entity"
                }
              ]
            }
            """);

        try
        {
            Assert.Throws<InvalidDataException>(() => AssetCatalog.Load(catalogFilePath));
        }
        finally
        {
            CleanUp(catalogFilePath);
        }
    }

    private static string WriteCatalogFile(string content, Encoding? encoding = null)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string filePath = Path.Combine(directory, "AssetInfos.json");

        if (encoding == null)
        {
            File.WriteAllText(filePath, content);
        }
        else
        {
            File.WriteAllText(filePath, content, encoding);
        }

        return filePath;
    }

    private static void CleanUp(string catalogFilePath)
    {
        AssetCatalog.ClearInternal();

        string? directory = Path.GetDirectoryName(catalogFilePath);
        if (directory != null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
