using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Tests.Serialization;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

/// <summary>
/// Plan offline-world-writer T1: <see cref="EditorWorldWriter.SaveWorld(World, IReadOnlyList{Entity})"/> writes a world from
/// entities that are neither initialized nor added to the world, in the format <see cref="EditorWorldWriter.SaveWorld(World)"/> writes.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class EditorWorldWriterTests
{
    private const string WorldFile = "Offline.world";

    private static World CreateWorld(string name = "Offline")
    {
        var world = new World { Name = name, FileName = WorldFile };
        world.EnvironmentSettings.Shadows.FilterTaps = 64;
        world.EnvironmentSettings.ColorPipeline.Mode = ColorPipelineMode.LinearHdr;
        return world;
    }

    private static List<Entity> CreateMixedEntities()
    {
        var light = new Entity { Name = "Sun", RootComponent = new LightComponent { Name = "SunLight" } };
        var empty = new Entity { Name = "Empty" };
        var annotated = new Entity { Name = "Holder" };
        annotated.AddComponent(new SerializedPropertiesTestComponent { Name = "Annotated", LapCount = 7, Label = "Beginner" });
        return [light, empty, annotated];
    }

    private static List<Entity> CreateLights()
    {
        return
        [
            new Entity(Guid.Parse("11111111-1111-1111-1111-111111111111")) { Name = "A", RootComponent = new LightComponent { Name = "LA" } },
            new Entity(Guid.Parse("22222222-2222-2222-2222-222222222222")) { Name = "B", RootComponent = new LightComponent { Name = "LB" } },
        ];
    }

    private static void WithProject(Action<string> body)
    {
        string projectPath = Path.Combine(Path.GetTempPath(), "casa-world-writer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectPath);
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = projectPath;
            body(projectPath);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(projectPath, recursive: true);
        }
    }

    private static JObject ReadWorld(string projectPath) => JObject.Parse(File.ReadAllText(Path.Combine(projectPath, WorldFile)));

    private static void RemoveIds(JToken token)
    {
        if (token is JObject obj)
        {
            obj.Remove("id");
        }

        foreach (var child in token.Children().ToList())
        {
            RemoveIds(child);
        }
    }

    [Fact]
    public void SaveWorld_WithEntities_WritesOneInlineReferencePerEntityInOrder()
    {
        WithProject(projectPath =>
        {
            var world = CreateWorld();
            var entities = CreateMixedEntities();

            EditorWorldWriter.SaveWorld(world, entities);

            var document = ReadWorld(projectPath);
            var references = Assert.IsType<JArray>(document["entity_references"]);
            Assert.Equal(3, references.Count);
            Assert.Equal(new[] { "Sun", "Empty", "Holder" }, references.Select(r => (string?)r["entity"]!["name"]));
            Assert.All(references, r => Assert.Equal(Guid.Empty.ToString(), (string?)r["asset_id"]));

            var holderComponents = Assert.IsType<JArray>(references[2]["entity"]!["components"]);
            var annotated = Assert.IsType<JObject>(Assert.Single(holderComponents));
            Assert.Equal(7, (int?)annotated["lap_count"]);
            Assert.Equal("Beginner", (string?)annotated["label"]);

            // The world-level keys are those of SaveWorld(World), in the same order.
            var expectedKeys = new JObject();
            EditorEntityJsonSerializer.SaveWorld(world, expectedKeys);
            Assert.Equal(expectedKeys.Properties().Select(p => p.Name), document.Properties().Select(p => p.Name));

            Assert.Equal(64, (int?)document["environment"]!["shadows"]!["filter_taps"]);
            Assert.Equal("LinearHdr", (string?)document["environment"]!["color_pipeline"]!["mode"]);
        });
    }

    [Fact]
    public void SaveWorld_WithEntities_EqualsTheExistingPathForAWorldOfLights()
    {
        WithProject(projectPath =>
        {
            // Component ids cannot be fixed (LightComponent has no id constructor), so every "id" is removed from both documents.
            var offlineWorld = CreateWorld();
            EditorWorldWriter.SaveWorld(offlineWorld, CreateLights());
            var offlineDocument = ReadWorld(projectPath);

            var existingWorld = CreateWorld();
            foreach (var entity in CreateLights())
            {
                EditorWorldEditingService.AddEntityReference(existingWorld, new EntityReference { AssetId = Guid.Empty, Entity = entity });
            }

            EditorWorldWriter.SaveWorld(existingWorld);
            var existingDocument = ReadWorld(projectPath);

            RemoveIds(offlineDocument);
            RemoveIds(existingDocument);
            Assert.True(JToken.DeepEquals(existingDocument, offlineDocument), offlineDocument + Environment.NewLine + existingDocument);
        });
    }

    [Fact]
    public void SaveWorld_WithEntities_WritesEachEntityLikeTheEntityWriter()
    {
        WithProject(projectPath =>
        {
            var entities = CreateMixedEntities();

            EditorWorldWriter.SaveWorld(CreateWorld(), entities);

            var references = (JArray)ReadWorld(projectPath)["entity_references"]!;
            for (var i = 0; i < entities.Count; i++)
            {
                var expected = new JObject();
                EditorEntityJsonSerializer.SaveEntity(entities[i], expected);
                Assert.Equal(expected.ToString(), references[i]["entity"]!.ToString());
            }
        });
    }

    [Fact]
    public void SaveWorld_WithEntities_LoadsBackAndSavesTheSameBytes()
    {
        WithProject(projectPath =>
        {
            var world = CreateWorld();
            var entities = CreateMixedEntities();

            EditorWorldWriter.SaveWorld(world, entities);
            var firstBytes = File.ReadAllBytes(Path.Combine(projectPath, WorldFile));

            var loaded = new World();
            loaded.Load(ReadWorld(projectPath));
            Assert.Equal(world.Name, loaded.Name);
            Assert.Equal(64, loaded.EnvironmentSettings.Shadows.FilterTaps);

            EditorWorldWriter.SaveWorld(world, entities);
            Assert.Equal(firstBytes, File.ReadAllBytes(Path.Combine(projectPath, WorldFile)));
        });
    }

    [Fact]
    public void SaveWorld_WithEntities_HasNoSideEffectOnTheWorldNorTheEntities()
    {
        WithProject(_ =>
        {
            var world = CreateWorld();
            var entities = CreateMixedEntities();

            EditorWorldWriter.SaveWorld(world, entities);

            Assert.Empty(world.Entities);
            Assert.Empty(EditorEntityJsonSerializer.GetEntityReferences(world));
            Assert.All(entities, entity => Assert.False(entity.IsInitialized));
            Assert.Equal(new[] { "Sun", "Empty", "Holder" }, entities.Select(e => e.Name));
        });
    }

    [Fact]
    public void SaveWorld_WithEntities_RejectsNullsAndAWorldThatAlreadyHoldsReferences()
    {
        WithProject(projectPath =>
        {
            var world = CreateWorld();
            var entities = CreateMixedEntities();

            Assert.Throws<ArgumentNullException>(() => EditorWorldWriter.SaveWorld(null!, entities));
            Assert.Throws<ArgumentNullException>(() => EditorWorldWriter.SaveWorld(world, null!));
            Assert.Throws<ArgumentNullException>(() => EditorWorldWriter.SaveWorld(world, new List<Entity> { entities[0], null! }));

            var worldWithReference = CreateWorld();
            EditorWorldEditingService.AddEntityReference(worldWithReference, new EntityReference
            {
                AssetId = Guid.Empty,
                Entity = new Entity { Name = "Held", RootComponent = new LightComponent() },
            });
            Assert.Throws<InvalidOperationException>(() => EditorWorldWriter.SaveWorld(worldWithReference, entities));

            Assert.False(File.Exists(Path.Combine(projectPath, WorldFile)));
        });
    }
}
