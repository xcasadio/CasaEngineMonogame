using CasaEngine.Core.Logging;
using CasaEngine.Core.Serialization;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Serialization;

public enum SerializedPropertiesTestMode
{
    Idle,
    Racing,
}

/// <summary>A game component as a gameplay DLL would declare it: annotated properties, Clone through
/// <see cref="SerializedProperties.Copy"/>. Public and top level so <see cref="ElementFactory"/> creates it by name.</summary>
public class SerializedPropertiesTestComponent : EntityComponent
{
    [SerializedProperty("enabled_flag")]
    public bool EnabledFlag { get; set; }

    [SerializedProperty("lap_count")]
    public int LapCount { get; set; } = 3;

    [SerializedProperty("speed")]
    public float Speed { get; set; } = 1.5f;

    [SerializedProperty("precise")]
    public double Precise { get; set; }

    [SerializedProperty("label")]
    public string Label { get; set; } = "default";

    [SerializedProperty("track_asset_id")]
    public Guid TrackAssetId { get; set; }

    [SerializedProperty("mode")]
    public SerializedPropertiesTestMode Mode { get; set; }

    [SerializedProperty("offset2")]
    public Vector2 Offset2 { get; set; }

    [SerializedProperty("offset")]
    public Vector3 Offset { get; set; }

    [SerializedProperty("weights")]
    public Vector4 Weights { get; set; }

    [SerializedProperty("rotation")]
    public Quaternion Rotation { get; set; } = Quaternion.Identity;

    [SerializedProperty("tint")]
    public Color Tint { get; set; } = Color.White;

    /// <summary>Not annotated: never read nor written.</summary>
    public int Transient { get; set; }

    public SerializedPropertiesTestComponent()
    {
    }

    private SerializedPropertiesTestComponent(SerializedPropertiesTestComponent other) : base(other)
    {
        SerializedProperties.Copy(other, this);
    }

    public override EntityComponent Clone() => new SerializedPropertiesTestComponent(this);
}

public class SerializedPropertiesWithPrivateSetter
{
    [SerializedProperty("value")]
    public int Value { get; private set; }
}

public class SerializedPropertiesWithUnsupportedType
{
    [SerializedProperty("values")]
    public List<int> Values { get; set; } = new();
}

public class SerializedPropertiesWithDuplicateKey
{
    [SerializedProperty("value")]
    public int First { get; set; }

    [SerializedProperty("value")]
    public int Second { get; set; }
}

[Collection(ProjectEnvironmentCollection.Name)]
public sealed class SerializedPropertiesTests : IDisposable
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

    public SerializedPropertiesTests()
    {
        Logs.AddLogger(_logger);
    }

    public void Dispose()
    {
        Logs.Close();
    }

    private static readonly Guid TrackId = new("0f3b5a2c-6c3e-4f61-8a54-0d3f1c2b9e10");

    private static JObject CreateFullNode() => JObject.Parse($$"""
        {
          "id": "6b1e3c1a-0b8f-4a49-9a77-2f9c0c3d5e11",
          "name": "Annotated",
          "type": "{{nameof(SerializedPropertiesTestComponent)}}",
          "enabled_flag": true,
          "lap_count": 7,
          "speed": 42.25,
          "precise": 0.125,
          "label": "Beginner",
          "track_asset_id": "{{TrackId}}",
          "mode": "Racing",
          "offset2": { "x": 1, "y": 2 },
          "offset": { "x": 3, "y": 4, "z": 5 },
          "weights": { "x": 6, "y": 7, "z": 8, "w": 9 },
          "rotation": { "x": 0, "y": 0.7071068, "z": 0, "w": 0.7071068 },
          "tint": { "r": 10, "g": 20, "b": 30, "a": 40 }
        }
        """);

    private static void AssertFullValues(SerializedPropertiesTestComponent component)
    {
        Assert.True(component.EnabledFlag);
        Assert.Equal(7, component.LapCount);
        Assert.Equal(42.25f, component.Speed);
        Assert.Equal(0.125, component.Precise);
        Assert.Equal("Beginner", component.Label);
        Assert.Equal(TrackId, component.TrackAssetId);
        Assert.Equal(SerializedPropertiesTestMode.Racing, component.Mode);
        Assert.Equal(new Vector2(1, 2), component.Offset2);
        Assert.Equal(new Vector3(3, 4, 5), component.Offset);
        Assert.Equal(new Vector4(6, 7, 8, 9), component.Weights);
        Assert.Equal(new Quaternion(0, 0.7071068f, 0, 0.7071068f), component.Rotation);
        Assert.Equal(new Color(10, 20, 30, 40), component.Tint);
    }

    [Fact]
    public void ElementFactoryLoad_ReadsEveryAnnotatedProperty()
    {
        var component = (SerializedPropertiesTestComponent)ElementFactory.Load<EntityComponent>(CreateFullNode());

        AssertFullValues(component);
        Assert.Equal("Annotated", component.Name);
        Assert.Empty(_logger.Errors);
    }

    [Fact]
    public void Load_MissingKeys_KeepTheCurrentValues()
    {
        var component = (SerializedPropertiesTestComponent)ElementFactory.Load<EntityComponent>(JObject.Parse($$"""
            { "id": "6b1e3c1a-0b8f-4a49-9a77-2f9c0c3d5e12", "name": "Old", "type": "{{nameof(SerializedPropertiesTestComponent)}}", "label": null }
            """));

        Assert.Equal(3, component.LapCount);
        Assert.Equal(1.5f, component.Speed);
        Assert.Equal("default", component.Label);
        Assert.Equal(Color.White, component.Tint);
        Assert.Empty(_logger.Errors);
    }

    [Fact]
    public void Load_UnreadableValue_IsReportedAndKeepsTheValue()
    {
        var node = CreateFullNode();
        node["lap_count"] = "seven";
        node["mode"] = "Flying";

        var component = (SerializedPropertiesTestComponent)ElementFactory.Load<EntityComponent>(node);

        Assert.Equal(3, component.LapCount);
        Assert.Equal(SerializedPropertiesTestMode.Idle, component.Mode);
        Assert.Equal("Beginner", component.Label);
        Assert.Equal(2, _logger.Errors.Count);
        Assert.Contains(_logger.Errors, error => error.Contains("'lap_count'") && error.Contains(nameof(SerializedPropertiesTestComponent)) && error.Contains("Annotated"));
        Assert.Contains(_logger.Errors, error => error.Contains("'mode'"));
    }

    [Fact]
    public void Copy_AndClone_KeepEveryAnnotatedValue()
    {
        var component = (SerializedPropertiesTestComponent)ElementFactory.Load<EntityComponent>(CreateFullNode());
        component.Transient = 99;

        var clone = (SerializedPropertiesTestComponent)component.Clone();

        AssertFullValues(clone);
        Assert.Equal(0, clone.Transient);
        Assert.Throws<ArgumentException>(() => SerializedProperties.Copy(component, new SerializedPropertiesWithDuplicateKey()));
    }

    [Fact]
    public void EntityClone_KeepsTheAnnotatedValuesOfItsComponents()
    {
        var entity = new Entity { Name = "Holder" };
        entity.AddComponent((SerializedPropertiesTestComponent)ElementFactory.Load<EntityComponent>(CreateFullNode()));

        var clone = entity.Clone();

        AssertFullValues(Assert.Single(clone.Components.OfType<SerializedPropertiesTestComponent>()));
    }

    [Fact]
    public void SaveEntity_ThenLoad_RoundTripsTheAnnotatedProperties()
    {
        var entity = new Entity { Name = "Holder" };
        entity.AddComponent((SerializedPropertiesTestComponent)ElementFactory.Load<EntityComponent>(CreateFullNode()));

        var saved = new JObject();
        EditorEntityJsonSerializer.SaveEntity(entity, saved);

        var componentNode = (JObject)saved["components"]![0]!;
        Assert.Equal("Racing", componentNode["mode"]!.Value<string>());
        Assert.Equal(TrackId.ToString(), componentNode["track_asset_id"]!.Value<string>());
        Assert.True(JToken.DeepEquals(JObject.Parse("""{ "x": 3.0, "y": 4.0, "z": 5.0 }"""), componentNode["offset"]));
        Assert.True(JToken.DeepEquals(JObject.Parse("""{ "r": 10, "g": 20, "b": 30, "a": 40 }"""), componentNode["tint"]));
        Assert.Null(componentNode["Transient"]);

        var reloaded = new Entity();
        reloaded.Load(saved);
        AssertFullValues(Assert.Single(reloaded.Components.OfType<SerializedPropertiesTestComponent>()));

        var resaved = new JObject();
        EditorEntityJsonSerializer.SaveEntity(reloaded, resaved);
        Assert.True(JToken.DeepEquals(saved, resaved), resaved.ToString());
    }

    [Fact]
    public void InvalidDeclarations_AreReportedAsDeveloperErrors()
    {
        Assert.Throws<InvalidOperationException>(() => SerializedProperties.GetMembers(typeof(SerializedPropertiesWithPrivateSetter)));
        Assert.Throws<InvalidOperationException>(() => SerializedProperties.GetMembers(typeof(SerializedPropertiesWithUnsupportedType)));
        Assert.Throws<InvalidOperationException>(() => SerializedProperties.GetMembers(typeof(SerializedPropertiesWithDuplicateKey)));
    }

    [Fact]
    public void GetMembers_KeepsTheDeclarationOrder()
    {
        var keys = SerializedProperties.GetMembers(typeof(SerializedPropertiesTestComponent)).Select(member => member.JsonName).ToArray();

        Assert.Equal(new[] { "enabled_flag", "lap_count", "speed", "precise", "label", "track_asset_id", "mode", "offset2", "offset", "weights", "rotation", "tint" }, keys);
    }
}
