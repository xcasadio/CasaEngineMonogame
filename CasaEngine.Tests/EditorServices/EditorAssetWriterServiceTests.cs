using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Tests;
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

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}