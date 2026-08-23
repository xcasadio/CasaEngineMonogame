using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Input;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Input;

[Collection(ProjectEnvironmentCollection.Name)]
public class ButtonsMappingAssetLoaderTests
{
    [Fact]
    public void AssetLoaderRegistry_RegistersButtonsMappingLoader()
    {
        var assetContentManager = new AssetContentManager();

        // Would throw InvalidOperationException("IAssetLoader not found for the type ...")
        // before ButtonsMapping was registered in AssetLoaderRegistry.
        var exception = Record.Exception(() => AssetLoaderRegistry.RegisterLoaders(assetContentManager));

        Assert.Null(exception);
    }

    [Fact]
    public void AssetLoader_LoadsRealRPGDemoButtonsMappingFile()
    {
        var repoRoot = FindRepoRoot();
        var filePath = Path.Combine(repoRoot, "Projects", "RPGDemo", "buttonsMapping.buttonsMapping");

        var jsonDocument = JObject.Parse(File.ReadAllText(filePath));
        var buttonsMapping = new ButtonsMapping();
        buttonsMapping.Load(jsonDocument);

        Assert.Equal(5, buttonsMapping.Buttons.Count);
        Assert.Contains(buttonsMapping.Buttons, button => button.Name == "Action");
        Assert.Contains(buttonsMapping.Buttons, button => button.Name == "MoveUp");
        Assert.Contains(buttonsMapping.Buttons, button => button.Name == "MoveDown");
        Assert.Contains(buttonsMapping.Buttons, button => button.Name == "MoveLeft");
        Assert.Contains(buttonsMapping.Buttons, button => button.Name == "MoveRight");
    }

    [Fact]
    public void AssetContentManager_LoadsButtonsMappingThroughCatalog()
    {
        string projectDirectory = CreateTempDirectory();
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = projectDirectory;
            EditorAssetCatalogService.Clear();

            var repoRoot = FindRepoRoot();
            var sourceFilePath = Path.Combine(repoRoot, "Projects", "RPGDemo", "buttonsMapping.buttonsMapping");
            var sourceDocument = JObject.Parse(File.ReadAllText(sourceFilePath));
            Guid assetId = Guid.Parse(sourceDocument["id"]!.Value<string>()!);
            string relativeFileName = "buttonsMapping.buttonsMapping";

            File.Copy(sourceFilePath, Path.Combine(projectDirectory, relativeFileName));

            EditorAssetCatalogService.Add(new AssetInfo(assetId)
            {
                Name = sourceDocument["name"]!.Value<string>()!,
                FileName = relativeFileName,
                AssetType = AssetInfo.InferAssetType(relativeFileName),
            });

            var assetContentManager = new AssetContentManager();
            AssetLoaderRegistry.RegisterLoaders(assetContentManager);

            var buttonsMapping = assetContentManager.Load<ButtonsMapping>(assetId);

            Assert.Equal(5, buttonsMapping.Buttons.Count);
            Assert.Contains(buttonsMapping.Buttons, button => button.Name == "MoveRight");
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Directory.Packages.props")))
        {
            dir = dir.Parent;
        }

        return dir!.FullName;
    }
}
