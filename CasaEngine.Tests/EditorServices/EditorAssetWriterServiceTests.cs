using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Tests;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

[Collection(ProjectEnvironmentCollection.Name)]
public class EditorAssetWriterServiceTests
{
    [Fact]
    public void SaveAsset_WithEntity_RaisesAssetSavedWithEntitySaveSource()
    {
        string tempDirectory = CreateTempDirectory();
        string assetDirectory = Path.Combine(tempDirectory, "Entities");
        string relativePath = Path.Combine("Entities", "Box.entity");
        string? previousProjectPath = EngineEnvironment.ProjectPath;
        EditorAssetSavedEventArgs? raisedEvent = null;

        try
        {
            EngineEnvironment.ProjectPath = tempDirectory;
            Directory.CreateDirectory(assetDirectory);

            var entity = new Entity
            {
                AssetId = Guid.Parse("1961629e-6215-47be-961b-854befa6c235"),
                Name = "Box",
                FileName = relativePath,
                RootComponent = new PlayerStartComponent
                {
                    Name = "Root",
                },
            };

            void OnAssetSaved(object? sender, EditorAssetSavedEventArgs e)
            {
                raisedEvent = e;
            }

            EditorAssetWriterService.AssetSaved += OnAssetSaved;
            try
            {
                EditorAssetWriterService.SaveAsset(relativePath, entity, EditorAssetSaveSource.EntityAssetEditorPanel);
            }
            finally
            {
                EditorAssetWriterService.AssetSaved -= OnAssetSaved;
            }

            string fullPath = Path.Combine(tempDirectory, relativePath);
            Assert.True(File.Exists(fullPath));

            Assert.NotNull(raisedEvent);
            Assert.Equal(relativePath, raisedEvent!.RelativePath);
            Assert.Equal(fullPath, raisedEvent.FullPath);
            Assert.Equal(entity.Id, raisedEvent.AssetId);
            Assert.Equal(EditorAssetSaveSource.EntityAssetEditorPanel, raisedEvent.SaveSource);

            var document = JObject.Parse(File.ReadAllText(fullPath));
            Assert.Equal(entity.Id, (Guid?)document["id"]);
            Assert.Equal("Box", (string?)document["name"]);
            Assert.NotNull(document["root_component"]);
            Assert.Equal("PlayerStartComponent", (string?)document["root_component"]?["type"]);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void SaveAsset_WithEntitySceneTransforms_PersistsRootAndChildCoordinates()
    {
        string tempDirectory = CreateTempDirectory();
        string assetDirectory = Path.Combine(tempDirectory, "Entities");
        string relativePath = Path.Combine("Entities", "TransformedBox.entity");
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = tempDirectory;
            Directory.CreateDirectory(assetDirectory);

            var rootComponent = new PlayerStartComponent
            {
                Name = "Root",
                LocalPosition = new Vector3(1.5f, -2.0f, 3.25f),
                LocalScale = new Vector3(1.25f, 2.5f, 0.75f),
                LocalOrientation = Quaternion.CreateFromYawPitchRoll(0.35f, -0.2f, 0.5f),
            };

            var childComponent = new PlayerStartComponent
            {
                Name = "Child",
                LocalPosition = new Vector3(-4.0f, 5.5f, 6.75f),
                LocalScale = new Vector3(0.5f, 0.75f, 1.5f),
                LocalOrientation = Quaternion.CreateFromYawPitchRoll(-0.15f, 0.4f, -0.25f),
            };
            rootComponent.AddChildComponent(childComponent);

            var entity = new Entity
            {
                Name = "TransformedBox",
                FileName = relativePath,
                RootComponent = rootComponent,
            };

            EditorAssetWriterService.SaveAsset(relativePath, entity, EditorAssetSaveSource.EntityAssetEditorPanel);

            string fullPath = Path.Combine(tempDirectory, relativePath);
            var document = JObject.Parse(File.ReadAllText(fullPath));
            var rootCoordinates = document["root_component"]?["local_transform"];
            var childCoordinates = document["root_component"]?["children_component"]?[0]?["local_transform"];

            Assert.NotNull(rootCoordinates);
            Assert.NotNull(childCoordinates);

            AssertVector3(rootCoordinates!["position"], rootComponent.LocalPosition);
            AssertVector3(rootCoordinates["scale"], rootComponent.LocalScale);
            AssertQuaternion(rootCoordinates["rotation"], rootComponent.LocalOrientation);

            AssertVector3(childCoordinates!["position"], childComponent.LocalPosition);
            AssertVector3(childCoordinates["scale"], childComponent.LocalScale);
            AssertQuaternion(childCoordinates["rotation"], childComponent.LocalOrientation);
        }
        finally
        {
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

    private static void AssertVector3(JToken? token, Vector3 expected)
    {
        Assert.NotNull(token);
        Assert.Equal(expected.X, (float?)token!["x"]);
        Assert.Equal(expected.Y, (float?)token["y"]);
        Assert.Equal(expected.Z, (float?)token["z"]);
    }

    private static void AssertQuaternion(JToken? token, Quaternion expected)
    {
        Assert.NotNull(token);
        Assert.Equal(expected.X, (float?)token!["x"]);
        Assert.Equal(expected.Y, (float?)token["y"]);
        Assert.Equal(expected.Z, (float?)token["z"]);
        Assert.Equal(expected.W, (float?)token["w"]);
    }
}