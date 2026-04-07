using CasaEngine.EditorServices;
using CasaEngine.Engine;
using CasaEngine.Framework;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Materials;
using CasaEngine.Tests;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

[Collection(ProjectEnvironmentCollection.Name)]
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

    [Fact]
    public void ImportFile_ReflectionCubemapResourceAlone_DoesNotPersistReflectionProperty()
    {
        string workspaceRoot = FindWorkspaceRoot();
        string sourceFilePath = Path.Combine(workspaceRoot, "RacingGame", "Content", "Models", "Sign.X");
        Assert.True(File.Exists(sourceFilePath));

        string tempDirectory = CreateTempDirectory();
        string destinationFilePath = Path.Combine(tempDirectory, "Sign.X");
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = tempDirectory;
            EditorAssetCatalogService.Clear();

            bool catalogChanged = EditorAssetImportService.ImportFile(sourceFilePath, destinationFilePath);

            Assert.True(catalogChanged);

            MaterialAsset material = Assert.Single(LoadImportedMaterials(tempDirectory, "Sign_Imported"));

            Assert.False(TryReadReflectionTextureId(material, out _));
            Assert.True(material.TryGetPropertyValue("ambient_color", out var ambientValue));
            Assert.True(ambientValue.TryGetVector3(out var ambientColor));
            Assert.True(ambientColor.X > 0.3f);
            Assert.Equal(RenderQueue.Opaque, material.Queue);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ImportFile_OptionalReflectionHint_PersistsReflectionProperty()
    {
        string workspaceRoot = FindWorkspaceRoot();
        string sourceFilePath = Path.Combine(workspaceRoot, "RacingGame", "Content", "Models", "Sign.X");
        Assert.True(File.Exists(sourceFilePath));

        string tempDirectory = CreateTempDirectory();
        string destinationFilePath = Path.Combine(tempDirectory, "Sign.X");
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = tempDirectory;
            EditorAssetCatalogService.Clear();

            bool catalogChanged = EditorAssetImportService.ImportFile(
                sourceFilePath,
                destinationFilePath,
                new StubLegacyImportProfile(new LegacyMaterialImportInterpretation(
                    LegacyMaterialSurfaceIntent.ReflectiveLit,
                    LegacyMaterialImportHint.Reflection)));

            Assert.True(catalogChanged);

            MaterialAsset material = Assert.Single(LoadImportedMaterials(tempDirectory, "Sign_Imported"));
            Assert.True(TryReadReflectionTextureId(material, out var reflectionTextureId));
            Assert.NotEqual(Guid.Empty, reflectionTextureId);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ImportFile_AlphaCutoutLegacyModel_UsesOptionalLegacyImportProfileInterpretation()
    {
        string workspaceRoot = FindWorkspaceRoot();
        string sourceFilePath = Path.Combine(workspaceRoot, "RacingGame", "Content", "Models", "AlphaPalm.X");
        Assert.True(File.Exists(sourceFilePath));

        string tempDirectory = CreateTempDirectory();
        string destinationFilePath = Path.Combine(tempDirectory, "AlphaPalm.X");
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = tempDirectory;
            EditorAssetCatalogService.Clear();

            bool catalogChanged = EditorAssetImportService.ImportFile(
                sourceFilePath,
                destinationFilePath,
                new StubLegacyImportProfile(new LegacyMaterialImportInterpretation(
                    LegacyMaterialSurfaceIntent.AlphaCutoutLit,
                    LegacyMaterialImportHint.AlphaCutout)));

            Assert.True(catalogChanged);

            var materials = LoadImportedMaterials(tempDirectory, "AlphaPalm_Imported");
            MaterialAsset[] alphaCutoutMaterials = materials.Where(candidate => candidate.Queue == RenderQueue.AlphaTest).ToArray();

            Assert.NotEmpty(alphaCutoutMaterials);
            Assert.All(alphaCutoutMaterials, material =>
            {
                Assert.Equal("CullNone", material.RasterizerStateName);
                Assert.True(material.TryGetPropertyValue("alpha_cutoff", out var alphaCutoffValue));
                Assert.True(alphaCutoffValue.TryGetFloat(out var alphaCutoff));
                Assert.Equal(0.35f, alphaCutoff);
            });
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ImportFile_BrightAmbientHint_ComesFromProfileInsteadOfAssetNaming()
    {
        string workspaceRoot = FindWorkspaceRoot();
        string sourceFilePath = Path.Combine(workspaceRoot, "RacingGame", "Content", "Models", "AlphaPalm.X");
        Assert.True(File.Exists(sourceFilePath));

        string neutralProjectDirectory = CreateTempDirectory();
        string hintedProjectDirectory = CreateTempDirectory();
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EditorAssetCatalogService.Clear();

            EngineEnvironment.ProjectPath = neutralProjectDirectory;
            bool neutralCatalogChanged = EditorAssetImportService.ImportFile(
                sourceFilePath,
                Path.Combine(neutralProjectDirectory, "AlphaPalm.X"));

            EditorAssetCatalogService.Clear();

            EngineEnvironment.ProjectPath = hintedProjectDirectory;
            bool hintedCatalogChanged = EditorAssetImportService.ImportFile(
                sourceFilePath,
                Path.Combine(hintedProjectDirectory, "AlphaPalm.X"),
                new StubLegacyImportProfile(new LegacyMaterialImportInterpretation(
                    LegacyMaterialSurfaceIntent.OpaqueLit,
                    LegacyMaterialImportHint.BrightAmbient)));

            Assert.True(neutralCatalogChanged);
            Assert.True(hintedCatalogChanged);

            var neutralMaterials = LoadImportedMaterials(neutralProjectDirectory, "AlphaPalm_Imported");
            var hintedMaterials = LoadImportedMaterials(hintedProjectDirectory, "AlphaPalm_Imported");

            Assert.Equal(neutralMaterials.Count, hintedMaterials.Count);
            Assert.Contains(neutralMaterials, material => ReadAmbientColor(material).X < 128f / 255f);
            Assert.All(hintedMaterials, material =>
            {
                Vector3 ambientColor = ReadAmbientColor(material);
                Assert.InRange(ambientColor.X, 128f / 255f, 1.0f);
                Assert.InRange(ambientColor.Y, 128f / 255f, 1.0f);
                Assert.InRange(ambientColor.Z, 128f / 255f, 1.0f);
                Assert.Equal(RenderQueue.Opaque, material.Queue);
            });
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(neutralProjectDirectory, recursive: true);
            Directory.Delete(hintedProjectDirectory, recursive: true);
        }
    }

    [Fact]
    public void ImportFile_PassesOptionalLegacyImportProfileToImporter()
    {
        string workspaceRoot = FindWorkspaceRoot();
        string sourceFilePath = Path.Combine(workspaceRoot, "RacingGame", "Content", "Models", "Sign.X");
        Assert.True(File.Exists(sourceFilePath));

        string tempDirectory = CreateTempDirectory();
        string destinationFilePath = Path.Combine(tempDirectory, "Sign.X");
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = tempDirectory;
            EditorAssetCatalogService.Clear();

            bool catalogChanged = EditorAssetImportService.ImportFile(
                sourceFilePath,
                destinationFilePath,
                new StubLegacyImportProfile(new LegacyMaterialImportInterpretation(
                    LegacyMaterialSurfaceIntent.AlphaCutoutLit,
                    LegacyMaterialImportHint.AlphaCutout)));

            Assert.True(catalogChanged);

            MaterialAsset material = Assert.Single(LoadImportedMaterials(tempDirectory, "Sign_Imported"));
            Assert.Equal(RenderQueue.AlphaTest, material.Queue);
            Assert.Equal("CullNone", material.RasterizerStateName);
            Assert.True(material.TryGetPropertyValue("ambient_color", out var ambientValue));
            Assert.True(ambientValue.TryGetVector3(out var ambientColor));
            Assert.InRange(ambientColor.X, 0.30f, 0.35f);
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

    private static string FindWorkspaceRoot()
    {
        string repositoryRoot = FindRepositoryRoot();
        string? workspaceRoot = Directory.GetParent(repositoryRoot)?.FullName;
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new DirectoryNotFoundException("Unable to locate the workspace root from the repository root.");
        }

        return workspaceRoot;
    }

    private static IReadOnlyList<MaterialAsset> LoadImportedMaterials(string projectDirectory, string importedFolderName)
    {
        string importedMaterialsDirectory = Path.Combine(projectDirectory, importedFolderName, "Materials");
        Assert.True(Directory.Exists(importedMaterialsDirectory));

        return Directory
            .GetFiles(importedMaterialsDirectory, "*" + Constants.FileNameExtensions.Material)
            .Select(materialFile =>
            {
                var materialDocument = JObject.Parse(File.ReadAllText(materialFile));
                var material = new MaterialAsset();
                material.Load(materialDocument);
                return material;
            })
            .ToArray();
    }

    private static Vector3 ReadAmbientColor(MaterialAsset material)
    {
        Assert.True(material.TryGetPropertyValue("ambient_color", out var ambientValue));
        Assert.True(ambientValue.TryGetVector3(out var ambientColor));
        return ambientColor;
    }

    private static bool TryReadReflectionTextureId(MaterialAsset material, out Guid reflectionTextureId)
    {
        reflectionTextureId = Guid.Empty;
        return material.TryGetPropertyValue("reflection_texture", out var reflectionTextureValue)
            && reflectionTextureValue.TryGetTextureId(out reflectionTextureId)
            && reflectionTextureId != Guid.Empty;
    }

    private sealed class StubLegacyImportProfile : ILegacyMaterialImportProfile
    {
        private readonly LegacyMaterialImportInterpretation _interpretation;

        public StubLegacyImportProfile(LegacyMaterialImportInterpretation interpretation)
        {
            _interpretation = interpretation;
        }

        public LegacyMaterialImportInterpretation Interpret(in LegacyMaterialImportContext context)
            => _interpretation;
    }
}