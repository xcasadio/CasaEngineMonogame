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

            var buttonsMapping = assetContentManager.LoadCopy<ButtonsMapping>(assetId);

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

    [Fact]
    public void AMappingWithoutTheRepeatFields_LoadsNulls_AndDoesNotGainThemWhenSaved()
    {
        var mapping = new ButtonsMapping();
        mapping.Load(JObject.Parse(File.ReadAllText(Path.Combine(FindRepoRoot(), "Projects", "RPGDemo", "buttonsMapping.buttonsMapping"))));

        Assert.Null(mapping.RepeatInitialDelayMs);
        Assert.Null(mapping.RepeatIntervalMs);

        Assert.True(EditorAssetJsonSerializer.TrySerialize(mapping, out var document));
        Assert.False(document.ContainsKey("repeat_initial_delay_ms"));
        Assert.False(document.ContainsKey("repeat_interval_ms"));
    }

    [Fact]
    public void TheRepeatFields_RoundTrip_EachOnItsOwn()
    {
        var original = WithIdentity("""{ "buttons": [], "repeat_initial_delay_ms": 333 }""");
        var mapping = new ButtonsMapping();
        mapping.Load(original);

        Assert.Equal(333, mapping.RepeatInitialDelayMs);
        Assert.Null(mapping.RepeatIntervalMs);

        Assert.True(EditorAssetJsonSerializer.TrySerialize(mapping, out var document));
        Assert.Equal(333, document["repeat_initial_delay_ms"]!.Value<int>());
        Assert.False(document.ContainsKey("repeat_interval_ms"));

        var both = new ButtonsMapping();
        both.Load(WithIdentity("""{ "buttons": [], "repeat_initial_delay_ms": 0, "repeat_interval_ms": 66 }"""));
        Assert.True(EditorAssetJsonSerializer.TrySerialize(both, out var bothDocument));

        var reloaded = new ButtonsMapping();
        reloaded.Load(bothDocument);
        Assert.Equal(0, reloaded.RepeatInitialDelayMs);
        Assert.Equal(66, reloaded.RepeatIntervalMs);
    }

    [Theory]
    [InlineData("""{ "buttons": [], "repeat_initial_delay_ms": "120" }""", "repeat_initial_delay_ms")]
    [InlineData("""{ "buttons": [], "repeat_initial_delay_ms": 1.5 }""", "repeat_initial_delay_ms")]
    [InlineData("""{ "buttons": [], "repeat_interval_ms": true }""", "repeat_interval_ms")]
    [InlineData("""{ "buttons": [], "repeat_interval_ms": null }""", "repeat_interval_ms")]
    public void ARepeatFieldThatIsNotAnInteger_FailsTheLoad_NamingTheField(string json, string field)
    {
        var mapping = new ButtonsMapping();

        var error = Assert.Throws<InvalidDataException>(() => mapping.Load(WithIdentity(json)));

        Assert.Contains(field, error.Message);
    }

    // ObjectBase.Load requires both keys.
    private static JObject WithIdentity(string json)
    {
        var document = JObject.Parse(json);
        document["id"] = "15eedf05-d2ad-4a26-b7f2-1f95c482fb17";
        document["name"] = "Mapping";
        return document;
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
