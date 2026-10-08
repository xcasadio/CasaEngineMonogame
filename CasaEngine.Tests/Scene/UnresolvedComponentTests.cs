using CasaEngine.Core.Logging;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Scene;

/// <summary>A component of unknown type is kept as an <see cref="UnresolvedComponent"/> and written back unchanged (ADR-0072).</summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class UnresolvedComponentTests : IDisposable
{
    private sealed class CapturingLogger : ILogger
    {
        public List<string> Errors { get; } = new();

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) { }
        public void WriteError(string msg) => Errors.Add(msg);
    }

    private readonly CapturingLogger _logger = new();

    public UnresolvedComponentTests()
    {
        Logs.AddLogger(_logger);
    }

    public void Dispose()
    {
        Logs.Close();
    }

    private const string Transform = """{ "position": { "x": 1.0, "y": 2.0, "z": 3.0 }, "scale": { "x": 1.0, "y": 1.0, "z": 1.0 }, "rotation": { "x": 0.0, "y": 0.0, "z": 0.0, "w": 1.0 } }""";

    private static JObject UnknownComponent(string id, string name, bool withSceneData) => JObject.Parse($$"""
        {
          "id": "{{id}}",
          "name": "{{name}}",
          "type": "RaceTrackRoadComponentThatNoAssemblyDeclares",
          "track_asset_id": "0f3b5a2c-6c3e-4f61-8a54-0d3f1c2b9e10",
          "width_scale": 1.25,
          "nested": { "values": [ 1, 2, 3 ] }
          {{(withSceneData ? $$""", "local_transform": {{Transform}}, "children_component": [ { "id": "4c1f7e2a-1b3d-4e5f-8a9b-0c1d2e3f4a5b", "name": "KnownChild", "type": "TransformComponent", "local_transform": {{Transform}}, "children_component": [] } ]""" : "")}}
        }
        """);

    private static JObject KnownRootWithChild(JObject child) => JObject.Parse($$"""
        {
          "id": "9a8b7c6d-5e4f-4a3b-9c2d-1e0f9a8b7c6d",
          "name": "KnownRoot",
          "type": "TransformComponent",
          "local_transform": {{Transform}},
          "children_component": [ {{child.ToString()}} ]
        }
        """);

    private static JObject EntityNode(JToken root, params JObject[] components) => new()
    {
        ["id"] = "11111111-2222-4333-8444-555555555555",
        ["name"] = "Holder",
        ["root_component"] = root ?? JValue.CreateNull(),
        ["components"] = new JArray(components.Cast<object>().ToArray()),
        ["script_class_name"] = JValue.CreateNull(),
    };

    private static Entity Load(JObject node)
    {
        var entity = new Entity();
        entity.Load(node);
        return entity;
    }

    private static JObject Save(Entity entity)
    {
        var node = new JObject();
        EditorEntityJsonSerializer.SaveEntity(entity, node);
        return node;
    }

    private static JObject WithoutId(JToken node)
    {
        var copy = (JObject)node.DeepClone();
        copy.Remove("id");
        return copy;
    }

    [Fact]
    public void UnknownComponent_LoadsAndIsWrittenBackUnchanged()
    {
        var original = UnknownComponent("a1b2c3d4-0000-4000-8000-000000000001", "Road", withSceneData: false);

        var entity = Load(EntityNode(null, original));

        var unresolved = Assert.IsType<UnresolvedComponent>(Assert.Single(entity.Components));
        Assert.Equal("RaceTrackRoadComponentThatNoAssemblyDeclares", unresolved.UnresolvedTypeName);
        Assert.Contains(_logger.Errors, error => error.Contains("RaceTrackRoadComponentThatNoAssemblyDeclares") && error.Contains("'Road'"));

        Assert.True(JToken.DeepEquals(original, Save(entity)["components"]![0]), Save(entity).ToString());
    }

    [Fact]
    public void UnknownRootComponent_KeepsItsTransformAndItsChildrenInTheNode()
    {
        var original = UnknownComponent("a1b2c3d4-0000-4000-8000-000000000002", "RootRoad", withSceneData: true);

        var entity = Load(EntityNode(original));

        var root = Assert.IsType<UnresolvedComponent>(entity.RootComponent);
        Assert.Equal(new Vector3(1, 2, 3), root.LocalTransform.Position);
        Assert.Empty(root.Children);
        Assert.True(JToken.DeepEquals(original, Save(entity)["root_component"]));
    }

    [Fact]
    public void UnknownChildComponent_IsWrittenBackUnchanged()
    {
        var child = UnknownComponent("a1b2c3d4-0000-4000-8000-000000000003", "ChildRoad", withSceneData: true);
        var root = KnownRootWithChild(child);

        var entity = Load(EntityNode(root));

        Assert.IsType<UnresolvedComponent>(Assert.Single(entity.RootComponent.Children));
        Assert.True(JToken.DeepEquals(root, Save(entity)["root_component"]), Save(entity).ToString());
    }

    [Fact]
    public void MovedUnresolvedRoot_WritesItsCurrentTransform()
    {
        var original = UnknownComponent("a1b2c3d4-0000-4000-8000-000000000004", "RootRoad", withSceneData: true);
        var entity = Load(EntityNode(original));

        entity.RootComponent.LocalTransform.Position = new Vector3(7, 8, 9);

        var saved = (JObject)Save(entity)["root_component"]!;
        Assert.Equal(7f, saved["local_transform"]!["position"]!["x"]!.Value<float>());
        Assert.Equal(1.25, saved["width_scale"]!.Value<double>());
    }

    [Fact]
    public void ClonedEntity_KeepsTheNodesOfItsUnresolvedComponents()
    {
        var rootNode = UnknownComponent("a1b2c3d4-0000-4000-8000-000000000005", "RootRoad", withSceneData: true);
        var componentNode = UnknownComponent("a1b2c3d4-0000-4000-8000-000000000006", "Road", withSceneData: false);
        var entity = Load(EntityNode(rootNode, componentNode));

        var clone = entity.Clone();
        var saved = Save(clone);

        //  A clone gets new ids; everything else of the kept nodes is written back.
        Assert.True(JToken.DeepEquals(WithoutId(rootNode), WithoutId(saved["root_component"]!)), saved.ToString());
        Assert.True(JToken.DeepEquals(WithoutId(componentNode), WithoutId(saved["components"]![0]!)), saved.ToString());
        Assert.NotEqual(rootNode["id"]!.Value<string>(), saved["root_component"]!["id"]!.Value<string>());
    }

    [Fact]
    public void ClonedEntity_KeepsTheNodeOfAnUnresolvedChild()
    {
        var child = UnknownComponent("a1b2c3d4-0000-4000-8000-000000000007", "ChildRoad", withSceneData: true);
        var entity = Load(EntityNode(KnownRootWithChild(child)));

        var saved = Save(entity.Clone());

        Assert.True(JToken.DeepEquals(WithoutId(child), WithoutId(saved["root_component"]!["children_component"]![0]!)), saved.ToString());
    }

    [Fact]
    public void TypeThatIsNotAComponent_IsKeptAsUnresolved()
    {
        var original = JObject.Parse("""{ "id": "a1b2c3d4-0000-4000-8000-000000000008", "name": "NotAComponent", "type": "Entity" }""");

        var entity = Load(EntityNode(null, original));

        Assert.IsType<UnresolvedComponent>(Assert.Single(entity.Components));
        Assert.Contains(_logger.Errors, error => error.Contains("is not a EntityComponent"));
        Assert.True(JToken.DeepEquals(original, Save(entity)["components"]![0]));
    }
}
