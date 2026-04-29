using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using CasaEngine.Tests;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

[Collection(ProjectEnvironmentCollection.Name)]
public class EditorAssetCatalogServiceTests
{
    [Fact]
    public void Save_WithEmptyCatalog_WritesEmptyAssetInfosArray()
    {
        string tempDirectory = CreateTempDirectory();
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = tempDirectory;
            EditorAssetCatalogService.Clear();

            EditorAssetCatalogService.Save();

            string catalogFilePath = Path.Combine(tempDirectory, "AssetInfos.json");
            Assert.True(File.Exists(catalogFilePath));

            var document = JObject.Parse(File.ReadAllText(catalogFilePath));
            var assetInfos = Assert.IsType<JArray>(document["asset_infos"]);
            Assert.Empty(assetInfos);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void Save_WithAssets_PersistsEntriesThatCanBeReloaded()
    {
        string tempDirectory = CreateTempDirectory();
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = tempDirectory;
            EditorAssetCatalogService.Clear();

            Guid screenAssetId = Guid.Parse("11111111-2222-3333-4444-555555555555");
            Guid worldAssetId = Guid.Parse("66666666-7777-8888-9999-aaaaaaaaaaaa");

            EditorAssetCatalogService.Add(new AssetInfo(screenAssetId)
            {
                FileName = "UI/MainScreen.uiscreen",
            });

            EditorAssetCatalogService.Add(new AssetInfo(worldAssetId)
            {
                Name = "DefaultWorld",
                FileName = "DefaultWorld.world",
            });

            EditorAssetCatalogService.Save();

            string catalogFilePath = Path.Combine(tempDirectory, "AssetInfos.json");
            EditorAssetCatalogService.Clear();
            AssetCatalog.Load(catalogFilePath);

            var screenAsset = Assert.IsType<AssetInfo>(AssetCatalog.Get(screenAssetId));
            Assert.Equal("MainScreen", screenAsset.Name);
            Assert.Equal("UI/MainScreen.uiscreen", screenAsset.FileName);
            Assert.Equal("uiscreen", screenAsset.AssetType);

            var worldAsset = Assert.IsType<AssetInfo>(AssetCatalog.Get(worldAssetId));
            Assert.Equal("DefaultWorld", worldAsset.Name);
            Assert.Equal("DefaultWorld.world", worldAsset.FileName);
            Assert.Equal("world", worldAsset.AssetType);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
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