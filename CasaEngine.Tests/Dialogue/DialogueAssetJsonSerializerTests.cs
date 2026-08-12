using CasaEngine.Engine.Environment;
using CasaEngine.Compiler.Dialogue;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Configuration;
using CasaEngine.Framework.Dialogue.Assets;
using CasaEngine.Framework.Dialogue.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Dialogue;

[Collection(ProjectEnvironmentCollection.Name)]
public sealed class DialogueAssetJsonSerializerTests
{
    [Fact]
    public void SaveLoad_RoundTripsMinimalDialogueAsset()
    {
        DialogueAsset asset = CreateAsset();
        var node = new JObject();

        DialogueAssetJsonSerializer.Save(asset, node);

        var loaded = new DialogueAsset();
        loaded.Load(node);

        Assert.Equal(asset.Id, loaded.Id);
        Assert.Equal("Greeting", loaded.Name);
        Assert.Equal(DialogueAsset.CurrentVersion, loaded.Version);
        Assert.Equal("Start", loaded.StartNode);
        Assert.True(loaded.HasCompiledProgram);
        Assert.Equal(asset.ProgramBytes, loaded.ProgramBytes);
        Assert.Equal("Bonjour depuis CasaEngine.", loaded.LineTexts["line:greeting"]);
    }

    [Fact]
    public void Loader_SupportsDialogueExtensionAndLoadsAsset()
    {
        string fileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + Constants.FileNameExtensions.Dialogue);
        try
        {
            DialogueAsset asset = CreateAsset();
            var node = new JObject();
            DialogueAssetJsonSerializer.Save(asset, node);
            File.WriteAllText(fileName, node.ToString(Formatting.Indented));

            var loader = new DialogueAssetLoader();
            object loaded = loader.LoadAsset(fileName, new AssetContentManager());

            Assert.True(loader.IsFileSupported(fileName));
            var loadedAsset = Assert.IsType<DialogueAsset>(loaded);
            Assert.Equal("Greeting", loadedAsset.Name);
            Assert.Equal("Bonjour depuis CasaEngine.", loadedAsset.LineTexts["line:greeting"]);
        }
        finally
        {
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
        }
    }

    [Fact]
    public void AssetContentManager_LoadsDialogueAssetThroughRegistry()
    {
        Guid assetId = Guid.NewGuid();
        string rootDirectory = Path.Combine(Path.GetTempPath(), "CasaEngineDialogueTests", Guid.NewGuid().ToString("N"));
        string assetRelativePath = "Greeting" + Constants.FileNameExtensions.Dialogue;
        string assetFullPath = Path.Combine(rootDirectory, assetRelativePath);
        string catalogPath = Path.Combine(rootDirectory, "AssetInfos.json");
        string previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            Directory.CreateDirectory(rootDirectory);

            DialogueAsset asset = CreateAsset();
            var assetNode = new JObject();
            DialogueAssetJsonSerializer.Save(asset, assetNode);
            assetNode["id"] = assetId.ToString();
            File.WriteAllText(assetFullPath, assetNode.ToString(Formatting.Indented));

            var catalogNode = new JObject
            {
                ["asset_infos"] = new JArray
                {
                    new JObject
                    {
                        ["id"] = assetId.ToString(),
                        ["name"] = "Greeting",
                        ["file_name"] = assetRelativePath,
                        ["asset_type"] = "dialogue"
                    }
                }
            };
            File.WriteAllText(catalogPath, catalogNode.ToString(Formatting.Indented));

            AssetCatalog.Load(catalogPath);
            EngineEnvironment.ProjectPath = rootDirectory;

            var assetContentManager = new AssetContentManager();
            AssetLoaderRegistry.RegisterLoaders(assetContentManager);

            DialogueAsset loaded = assetContentManager.Load<DialogueAsset>(assetId, cache: false);

            Assert.Equal(assetId, loaded.AssetId);
            Assert.Equal("Greeting", loaded.Name);
            Assert.Equal("Bonjour depuis CasaEngine.", loaded.LineTexts["line:greeting"]);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CompiledYarnAsset_LoadsThroughDialogueLoader()
    {
        string sourceFileName = Path.Combine(FindRepositoryRoot(), "CasaEngine.Tests", "Dialogue", "Fixtures", "greeting.yarn");
        string assetFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + Constants.FileNameExtensions.Dialogue);

        try
        {
            var compiler = new YarnDialogueCompiler();
            YarnDialogueCompilationResult result = compiler.CompileFile(sourceFileName);
            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));

            DialogueAsset asset = DialogueAsset.FromCompiledProgram("Greeting", "Start", result.ProgramBytes, result.LineTexts);
            var node = new JObject();
            DialogueAssetJsonSerializer.Save(asset, node);
            File.WriteAllText(assetFileName, node.ToString(Formatting.Indented));

            var loader = new DialogueAssetLoader();
            var loadedAsset = Assert.IsType<DialogueAsset>(loader.LoadAsset(assetFileName, new AssetContentManager()));

            Assert.Equal("Greeting", loadedAsset.Name);
            Assert.Equal("Start", loadedAsset.StartNode);
            Assert.True(loadedAsset.HasCompiledProgram);
            Assert.Equal(result.ProgramBytes, loadedAsset.ProgramBytes);
            Assert.Contains(loadedAsset.LineTexts.Values, text => text == "Bonjour depuis CasaEngine.");
        }
        finally
        {
            if (File.Exists(assetFileName))
            {
                File.Delete(assetFileName);
            }
        }
    }

    [Fact]
    public void CanMigrate_RejectsFutureVersions()
    {
        Assert.True(DialogueAssetJsonSerializer.CanMigrate(DialogueAsset.CurrentVersion));
        Assert.False(DialogueAssetJsonSerializer.CanMigrate(DialogueAsset.CurrentVersion + 1));
    }

    private static DialogueAsset CreateAsset()
    {
        var asset = new DialogueAsset
        {
            Name = "Greeting",
            StartNode = "Start",
            ProgramBytes = new byte[] { 1, 2, 3, 4 },
        };
        asset.LineTexts["line:greeting"] = "Bonjour depuis CasaEngine.";
        return asset;
    }

    private static string FindRepositoryRoot()
    {
        string directory = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(directory, "CasaEngine.MonoGame.sln")))
        {
            DirectoryInfo parent = Directory.GetParent(directory);
            if (parent == null)
            {
                throw new InvalidOperationException("Cannot find repository root.");
            }

            directory = parent.FullName;
        }

        return directory;
    }
}