using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Configuration;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Cutscenes;

[Collection(ProjectEnvironmentCollection.Name)]
public sealed class CutsceneAssetJsonSerializerTests
{
    [Fact]
    public void SaveLoad_RoundTripsTypedActions()
    {
        CutsceneAsset asset = CreateAsset();
        var node = new JObject();

        CutsceneAssetJsonSerializer.Save(asset, node);

        var loaded = new CutsceneAsset();
        loaded.Load(node);

        Assert.Equal(asset.Id, loaded.Id);
        Assert.Equal("IntroWait", loaded.Name);
        SequenceCutsceneActionData root = Assert.IsType<SequenceCutsceneActionData>(loaded.RootAction);
        Assert.Equal(2, root.Actions.Count);
        Assert.Equal(0.5f, Assert.IsType<WaitCutsceneActionData>(root.Actions[0]).Seconds);

        ParallelCutsceneActionData parallel = Assert.IsType<ParallelCutsceneActionData>(root.Actions[1]);
        Assert.Equal(2, parallel.Actions.Count);
        Assert.Equal(1.0f, Assert.IsType<WaitCutsceneActionData>(parallel.Actions[0]).Seconds);
        Assert.Equal(0.25f, Assert.IsType<WaitCutsceneActionData>(parallel.Actions[1]).Seconds);
    }

    [Fact]
    public void SaveLoad_RoundTripsMoveToAction()
    {
        var asset = new CutsceneAsset
        {
            Name = "MoveHero",
            RootAction = new MoveToCutsceneActionData
            {
                EntityName = "Hero",
                Destination = new Vector3(1f, 2f, 3f),
                StoppingDistance = 0.25f,
                TimeoutSeconds = 5f,
            }
        };
        var node = new JObject();

        CutsceneAssetJsonSerializer.Save(asset, node);

        var loaded = new CutsceneAsset();
        loaded.Load(node);

        MoveToCutsceneActionData moveTo = Assert.IsType<MoveToCutsceneActionData>(loaded.RootAction);
        Assert.Equal("Hero", moveTo.EntityName);
        Assert.Equal(new Vector3(1f, 2f, 3f), moveTo.Destination);
        Assert.Equal(0.25f, moveTo.StoppingDistance);
        Assert.Equal(5f, moveTo.TimeoutSeconds);
    }

    [Fact]
    public void SaveLoad_RoundTripsNavigateToAction()
    {
        var asset = new CutsceneAsset
        {
            Name = "NavigateHero",
            RootAction = new NavigateToCutsceneActionData
            {
                EntityName = "Hero",
                Destination = new Vector3(2f, 0f, 4f),
                StoppingDistance = 0.2f,
                TimeoutSeconds = 6f,
            }
        };
        var node = new JObject();

        CutsceneAssetJsonSerializer.Save(asset, node);

        var loaded = new CutsceneAsset();
        loaded.Load(node);

        NavigateToCutsceneActionData navigateTo = Assert.IsType<NavigateToCutsceneActionData>(loaded.RootAction);
        Assert.Equal("Hero", navigateTo.EntityName);
        Assert.Equal(new Vector3(2f, 0f, 4f), navigateTo.Destination);
        Assert.Equal(0.2f, navigateTo.StoppingDistance);
        Assert.Equal(6f, navigateTo.TimeoutSeconds);
    }

    [Fact]
    public void Validate_ReportsV1ErrorsAndWarnings()
    {
        var asset = new CutsceneAsset
        {
            Name = "InvalidCutscene",
            RootAction = new SequenceCutsceneActionData
            {
                Actions =
                {
                    new WaitCutsceneActionData { Seconds = -1f },
                    new ParallelCutsceneActionData(),
                    new UnknownCutsceneActionData("MoveActorTo")
                }
            }
        };

        CutsceneValidationResult result = asset.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Messages, message => message.Severity == CutsceneValidationSeverity.Error && message.Message.Contains("Wait.seconds"));
        Assert.Contains(result.Messages, message => message.Severity == CutsceneValidationSeverity.Warning && message.Message.Contains("Parallel"));
        Assert.Contains(result.Messages, message => message.Severity == CutsceneValidationSeverity.Error && message.Message.Contains("MoveActorTo"));
    }

    [Fact]
    public void Validate_ReportsMoveToErrors()
    {
        var asset = new CutsceneAsset
        {
            Name = "InvalidMoveTo",
            RootAction = new MoveToCutsceneActionData
            {
                StoppingDistance = -1f,
                TimeoutSeconds = -1f,
            }
        };

        CutsceneValidationResult result = asset.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Messages, message => message.Severity == CutsceneValidationSeverity.Error && message.Message.Contains("MoveTo.entity"));
        Assert.Contains(result.Messages, message => message.Severity == CutsceneValidationSeverity.Error && message.Message.Contains("MoveTo.stopping_distance"));
        Assert.Contains(result.Messages, message => message.Severity == CutsceneValidationSeverity.Error && message.Message.Contains("MoveTo.timeout_seconds"));
    }

    [Fact]
    public void Validate_ReportsNavigateToErrors()
    {
        var asset = new CutsceneAsset
        {
            Name = "InvalidNavigateTo",
            RootAction = new NavigateToCutsceneActionData
            {
                StoppingDistance = -1f,
                TimeoutSeconds = -1f,
            }
        };

        CutsceneValidationResult result = asset.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Messages, message => message.Severity == CutsceneValidationSeverity.Error && message.Message.Contains("NavigateTo.entity"));
        Assert.Contains(result.Messages, message => message.Severity == CutsceneValidationSeverity.Error && message.Message.Contains("NavigateTo.stopping_distance"));
        Assert.Contains(result.Messages, message => message.Severity == CutsceneValidationSeverity.Error && message.Message.Contains("NavigateTo.timeout_seconds"));
    }

    [Fact]
    public void Loader_SupportsCutsceneExtensionAndLoadsAsset()
    {
        string fileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + Constants.FileNameExtensions.Cutscene);
        try
        {
            CutsceneAsset asset = CreateAsset();
            var node = new JObject();
            CutsceneAssetJsonSerializer.Save(asset, node);
            File.WriteAllText(fileName, node.ToString(Formatting.Indented));

            var loader = new CutsceneAssetLoader();
            object? loaded = loader.LoadAsset(fileName, new AssetContentManager());

            Assert.True(loader.IsFileSupported(fileName));
            var loadedAsset = Assert.IsType<CutsceneAsset>(loaded);
            Assert.Equal("IntroWait", loadedAsset.Name);
            Assert.True(loadedAsset.Validate().IsValid);
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
    public void AssetContentManager_LoadsCutsceneAssetThroughRegistry()
    {
        Guid assetId = Guid.NewGuid();
        string rootDirectory = Path.Combine(Path.GetTempPath(), "CasaEngineCutsceneTests", Guid.NewGuid().ToString("N"));
        string assetRelativePath = "IntroWait" + Constants.FileNameExtensions.Cutscene;
        string assetFullPath = Path.Combine(rootDirectory, assetRelativePath);
        string catalogPath = Path.Combine(rootDirectory, "AssetInfos.json");
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            Directory.CreateDirectory(rootDirectory);

            CutsceneAsset asset = CreateAsset();
            var assetNode = new JObject();
            CutsceneAssetJsonSerializer.Save(asset, assetNode);
            assetNode["id"] = assetId.ToString();
            File.WriteAllText(assetFullPath, assetNode.ToString(Formatting.Indented));

            var catalogNode = new JObject
            {
                ["asset_infos"] = new JArray
                {
                    new JObject
                    {
                        ["id"] = assetId.ToString(),
                        ["name"] = "IntroWait",
                        ["file_name"] = assetRelativePath,
                        ["asset_type"] = "cutscene"
                    }
                }
            };
            File.WriteAllText(catalogPath, catalogNode.ToString(Formatting.Indented));

            AssetCatalog.Load(catalogPath);
            EngineEnvironment.ProjectPath = rootDirectory;

            var assetContentManager = new AssetContentManager();
            AssetLoaderRegistry.RegisterLoaders(assetContentManager);

            CutsceneAsset loaded = assetContentManager.Load<CutsceneAsset>(assetId, cache: false);

            Assert.Equal(assetId, loaded.AssetId);
            Assert.Equal("IntroWait", loaded.Name);
            Assert.True(loaded.Validate().IsValid);
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

    private static CutsceneAsset CreateAsset()
    {
        return new CutsceneAsset
        {
            Name = "IntroWait",
            RootAction = new SequenceCutsceneActionData
            {
                Actions =
                {
                    new WaitCutsceneActionData { Seconds = 0.5f },
                    new ParallelCutsceneActionData
                    {
                        Actions =
                        {
                            new WaitCutsceneActionData { Seconds = 1.0f },
                            new WaitCutsceneActionData { Seconds = 0.25f }
                        }
                    }
                }
            }
        };
    }
}