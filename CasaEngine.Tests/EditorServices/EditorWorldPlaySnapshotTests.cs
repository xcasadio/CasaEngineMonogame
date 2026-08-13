using CasaEngine.EditorServices;
using CasaEngine.EditorServices.PlayMode;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

public class EditorWorldPlaySnapshotTests
{
    private static World CreateEditWorld()
    {
        var world = new World
        {
            Name = "EditWorld",
            FileName = "EditWorld.world",
            GameplayProxyClassName = "ScriptArcBallCamera",
        };

        var entity = new Entity
        {
            Name = "Hero",
            RootComponent = new LightComponent(),
        };

        EditorWorldEditingService.AddEntityReference(world, new EntityReference
        {
            AssetId = Guid.Empty,
            Entity = entity,
        });

        return world;
    }

    [Fact]
    public void Capture_WritesWorldDocumentShape()
    {
        var world = CreateEditWorld();

        var snapshot = EditorWorldPlaySnapshot.Capture(world);

        Assert.Equal("EditWorld", (string?)snapshot["name"]);
        Assert.Equal("ScriptArcBallCamera", (string?)snapshot["script_class_name"]);
        var references = Assert.IsType<JArray>(snapshot["entity_references"]);
        var referenceNode = Assert.IsType<JObject>(Assert.Single(references));
        var entityNode = Assert.IsType<JObject>(referenceNode["entity"]);
        Assert.Equal("Hero", (string?)entityNode["name"]);
    }

    [Fact]
    public void CreatePlayWorld_ProducesIndependentWorldWithSameContent()
    {
        var editWorld = CreateEditWorld();

        var playWorld = EditorWorldPlaySnapshot.CreatePlayWorld(editWorld);

        Assert.NotSame(editWorld, playWorld);
        Assert.Equal(editWorld.Name, playWorld.Name);
        Assert.Equal(editWorld.FileName, playWorld.FileName);
        Assert.Equal(editWorld.GameplayProxyClassName, playWorld.GameplayProxyClassName);

        // The play copy carries the reference data; entities materialize at LoadContent,
        // exactly like a world loaded from disk.
        Assert.Empty(playWorld.Entities);
    }

    [Fact]
    public void CreatePlayWorld_DoesNotTouchTheEditWorld()
    {
        var editWorld = CreateEditWorld();
        var entityBefore = Assert.Single(editWorld.Entities);

        EditorWorldPlaySnapshot.CreatePlayWorld(editWorld);

        var entityAfter = Assert.Single(editWorld.Entities);
        Assert.Same(entityBefore, entityAfter);
        Assert.Equal("EditWorld", editWorld.Name);
    }

    [Fact]
    public void Capture_ProducesLoadableInlineEntityData()
    {
        var editWorld = CreateEditWorld();
        var snapshot = EditorWorldPlaySnapshot.Capture(editWorld);

        var references = Assert.IsType<JArray>(snapshot["entity_references"]);
        var entityNode = Assert.IsType<JObject>(Assert.IsType<JObject>(references[0])["entity"]);

        var materialized = new CasaEngine.Framework.Assets.AssetContentManager().Load<Entity>(entityNode);

        Assert.Equal("Hero", materialized.Name);
        Assert.IsType<LightComponent>(materialized.RootComponent);
    }
}
