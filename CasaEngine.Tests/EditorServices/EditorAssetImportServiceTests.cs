using CasaEngine.EditorServices;
using CasaEngine.Engine;
using CasaEngine.Framework;
using CasaEngine.Framework.Assets;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

public class EditorAssetImportServiceTests
{
    [Fact]
    public void ImportFile_StaticModelAuthorsMaterialAssets()
    {
        string repositoryRoot = FindRepositoryRoot();
        string sourceFilePath = Path.Combine(repositoryRoot, "Projects", "SampleProject", "Car.x");
        Assert.True(File.Exists(sourceFilePath));

        string tempDirectory = CreateTempDirectory();
        string destinationFilePath = Path.Combine(tempDirectory, "Car.x");
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = tempDirectory;
            EditorAssetCatalogService.Clear();

            bool catalogChanged = EditorAssetImportService.ImportFile(sourceFilePath, destinationFilePath);

            Assert.True(catalogChanged);

            string importedMaterialsDirectory = Path.Combine(tempDirectory, "Car_Imported", "Materials");
            Assert.True(Directory.Exists(importedMaterialsDirectory));

            string[] materialFiles = Directory.GetFiles(importedMaterialsDirectory, "*" + Constants.FileNameExtensions.Material);
            Assert.NotEmpty(materialFiles);

            foreach (string materialFile in materialFiles)
            {
                var materialDocument = JObject.Parse(File.ReadAllText(materialFile));
                Assert.Equal("lit-diffuse", (string?)materialDocument["definition_id"]);
                Assert.NotNull(materialDocument["properties"]);
                Assert.Null(materialDocument["type"]);

                string relativeMaterialPath = Path.GetRelativePath(tempDirectory, materialFile);
                var assetInfo = AssetCatalog.GetByFileName(relativeMaterialPath);
                Assert.NotNull(assetInfo);
                Assert.Equal(Constants.FileNameExtensions.Material, Path.GetExtension(assetInfo!.FileName));
            }

            string staticModelPath = Path.Combine(tempDirectory, "Car.staticModel");
            Assert.True(File.Exists(staticModelPath));

            var staticModelDocument = JObject.Parse(File.ReadAllText(staticModelPath));
            var meshesNode = Assert.IsType<JArray>(staticModelDocument["meshes"]);
            bool hasMaterialBinding = false;

            foreach (var meshToken in meshesNode)
            {
                var meshNode = Assert.IsType<JObject>(meshToken);
                string? materialAssetIdText = (string?)meshNode["material_asset_id"];
                if (string.IsNullOrWhiteSpace(materialAssetIdText)
                    || !Guid.TryParse(materialAssetIdText, out var materialAssetId)
                    || materialAssetId == Guid.Empty)
                {
                    continue;
                }

                hasMaterialBinding = true;
                var assetInfo = AssetCatalog.Get(materialAssetId);
                Assert.NotNull(assetInfo);
                Assert.Equal(Constants.FileNameExtensions.Material, Path.GetExtension(assetInfo!.FileName));
            }

            Assert.True(hasMaterialBinding);
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

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.Editor.MonoGame.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test output directory.");
    }
}