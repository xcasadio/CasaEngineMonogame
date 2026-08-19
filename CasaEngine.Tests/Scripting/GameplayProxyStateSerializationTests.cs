using CasaEngine.Core.Serialization;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scripting;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Scripting;

/// <summary>
/// A gameplay proxy carrying member state, as a gameplay DLL would author it:
/// Save/Load overrides call the base methods, Clone copies the state.
/// Public and top level so <see cref="CasaEngine.Framework.Assets.ElementFactory"/> can create it by name.
/// </summary>
public class StatefulEntityScriptStub : GameplayProxy
{
    public int HitPoints { get; set; }
    public string Label { get; set; }
    public Guid TargetId { get; set; }

    public StatefulEntityScriptStub()
    {
    }

    protected StatefulEntityScriptStub(StatefulEntityScriptStub other)
    {
        HitPoints = other.HitPoints;
        Label = other.Label;
        TargetId = other.TargetId;
    }

    public override void Load(JObject element)
    {
        base.Load(element);
        HitPoints = element["hit_points"]?.GetInt32() ?? 0;
        Label = element["label"]?.GetString();
        TargetId = element["target_id"]?.GetGuid() ?? Guid.Empty;
    }

    public override void Save(JObject element)
    {
        base.Save(element);
        element.Add("hit_points", HitPoints);
        element.Add("label", Label);
        element.Add("target_id", TargetId);
    }

    public override IGameplayProxy Clone() => new StatefulEntityScriptStub(this);
    public override void InitializeWithWorld(World world) { }
    public override void Update(float elapsedTime) { }
    public override void Draw() { }
    public override void OnHit(Collision collision) { }
    public override void OnHitEnded(Collision collision) { }
    public override void OnBeginPlay(World world) { }
    public override void OnEndPlay(World world) { }
}

/// <summary>Second proxy class, target of the class-change scenario.</summary>
public class AlternateEntityScriptStub : GameplayProxy
{
    public override IGameplayProxy Clone() => new AlternateEntityScriptStub();
    public override void InitializeWithWorld(World world) { }
    public override void Update(float elapsedTime) { }
    public override void Draw() { }
    public override void OnHit(Collision collision) { }
    public override void OnHitEnded(Collision collision) { }
    public override void OnBeginPlay(World world) { }
    public override void OnEndPlay(World world) { }
}

/// <summary>World-level gameplay proxy carrying state.</summary>
public class StatefulWorldScriptStub : GameplayProxy
{
    public int Score { get; set; }
    public string Stage { get; set; }

    public override void Load(JObject element)
    {
        base.Load(element);
        Score = element["score"]?.GetInt32() ?? 0;
        Stage = element["stage"]?.GetString();
    }

    public override void Save(JObject element)
    {
        base.Save(element);
        element.Add("score", Score);
        element.Add("stage", Stage);
    }

    public override IGameplayProxy Clone() => new StatefulWorldScriptStub { Score = Score, Stage = Stage };
    public override void InitializeWithWorld(World world) { }
    public override void Update(float elapsedTime) { }
    public override void Draw() { }
    public override void OnHit(Collision collision) { }
    public override void OnHitEnded(Collision collision) { }
    public override void OnBeginPlay(World world) { }
    public override void OnEndPlay(World world) { }
}

/// <summary>
/// Covers the "script" state node of entity and world documents: the round trip through the
/// editor serializers, survival through cloning, and the backward compatible absence of the node.
/// </summary>
public class GameplayProxyStateSerializationTests
{
    private static readonly Guid SomeGuid = new("6f1a3c5e-8b2d-4f7a-9c0e-1d2b3a4c5d6e");

    [Fact]
    public void EntityProxyState_RoundTripsThroughTheEntitySerializer()
    {
        var entity = new Entity { GameplayProxyClassName = nameof(StatefulEntityScriptStub) };
        entity.Initialize();

        var proxy = Assert.IsType<StatefulEntityScriptStub>(entity.GameplayProxy);
        proxy.Name = "HeroScript";
        proxy.HitPoints = 42;
        proxy.Label = "hero";
        proxy.TargetId = SomeGuid;

        var node = new JObject();
        EditorEntityJsonSerializer.SaveEntity(entity, node);

        Assert.IsType<JObject>(node["script"]);

        var loaded = new Entity();
        loaded.Load(node);
        loaded.Initialize();

        var loadedProxy = Assert.IsType<StatefulEntityScriptStub>(loaded.GameplayProxy);
        Assert.Equal("HeroScript", loadedProxy.Name);
        Assert.Equal(42, loadedProxy.HitPoints);
        Assert.Equal("hero", loadedProxy.Label);
        Assert.Equal(SomeGuid, loadedProxy.TargetId);
    }

    [Fact]
    public void WorldProxyState_RoundTripsThroughTheWorldSerializer()
    {
        var world = new World { GameplayProxyClassName = nameof(StatefulWorldScriptStub) };
        world.CreateGameplayProxy();

        var proxy = Assert.IsType<StatefulWorldScriptStub>(world.GameplayProxy);
        proxy.Score = 1300;
        proxy.Stage = "castle";

        var node = new JObject();
        EditorEntityJsonSerializer.SaveWorld(world, node);

        Assert.IsType<JObject>(node["script"]);

        var loaded = new World();
        loaded.Load(node);
        loaded.CreateGameplayProxy();

        var loadedProxy = Assert.IsType<StatefulWorldScriptStub>(loaded.GameplayProxy);
        Assert.Equal(1300, loadedProxy.Score);
        Assert.Equal("castle", loadedProxy.Stage);
    }

    [Fact]
    public void ClonedEntity_KeepsTheProxyStateThroughInitialize()
    {
        var entity = new Entity { GameplayProxyClassName = nameof(StatefulEntityScriptStub) };
        entity.Initialize();

        var proxy = Assert.IsType<StatefulEntityScriptStub>(entity.GameplayProxy);
        proxy.HitPoints = 7;
        proxy.Label = "spawned";

        var clone = entity.Clone();
        clone.Initialize();

        var cloneProxy = Assert.IsType<StatefulEntityScriptStub>(clone.GameplayProxy);
        Assert.NotSame(proxy, cloneProxy);
        Assert.Equal(7, cloneProxy.HitPoints);
        Assert.Equal("spawned", cloneProxy.Label);
    }

    [Fact]
    public void EntityDocumentWithoutScriptNode_LoadsWithAFreshProxy()
    {
        var entity = new Entity { GameplayProxyClassName = nameof(StatefulEntityScriptStub) };
        entity.Initialize();

        var node = new JObject();
        EditorEntityJsonSerializer.SaveEntity(entity, node);
        node.Remove("script");

        var loaded = new Entity();
        loaded.Load(node);
        loaded.Initialize();

        var loadedProxy = Assert.IsType<StatefulEntityScriptStub>(loaded.GameplayProxy);
        Assert.Equal(0, loadedProxy.HitPoints);
        Assert.Null(loadedProxy.Label);
    }

    [Fact]
    public void WorldDocumentWithoutScriptNode_LoadsWithAFreshProxy()
    {
        var world = new World { GameplayProxyClassName = nameof(StatefulWorldScriptStub) };

        var node = new JObject();
        EditorEntityJsonSerializer.SaveWorld(world, node);

        Assert.Null(node["script"]);

        var loaded = new World();
        loaded.Load(node);
        loaded.CreateGameplayProxy();

        var loadedProxy = Assert.IsType<StatefulWorldScriptStub>(loaded.GameplayProxy);
        Assert.Equal(0, loadedProxy.Score);
    }

    [Fact]
    public void ChangedProxyClassName_RecreatesTheProxyOnTheNextInitialize()
    {
        var entity = new Entity { GameplayProxyClassName = nameof(StatefulEntityScriptStub) };
        entity.Initialize();
        Assert.IsType<StatefulEntityScriptStub>(entity.GameplayProxy);

        //editor scenario: the user picks another script class on an existing entity;
        //the change takes effect on the next initialized instance (spawn clone, play world)
        entity.GameplayProxyClassName = nameof(AlternateEntityScriptStub);
        var clone = entity.Clone();
        clone.Initialize();

        Assert.IsType<AlternateEntityScriptStub>(clone.GameplayProxy);
    }

    [Fact]
    public void PartialScriptNode_WithoutTheBaseKeys_StillLoads()
    {
        var entity = new Entity { GameplayProxyClassName = nameof(StatefulEntityScriptStub) };
        entity.Initialize();

        var node = new JObject();
        EditorEntityJsonSerializer.SaveEntity(entity, node);
        node["script"] = new JObject { ["hit_points"] = 5 };

        var loaded = new Entity();
        loaded.Load(node);
        loaded.Initialize();

        var loadedProxy = Assert.IsType<StatefulEntityScriptStub>(loaded.GameplayProxy);
        Assert.Equal(5, loadedProxy.HitPoints);
    }

    [Fact]
    public void UninitializedLoadedEntity_KeepsItsScriptStateWhenResaved()
    {
        var entity = new Entity { GameplayProxyClassName = nameof(StatefulEntityScriptStub) };
        entity.Initialize();
        ((StatefulEntityScriptStub)entity.GameplayProxy).HitPoints = 42;

        var node = new JObject();
        EditorEntityJsonSerializer.SaveEntity(entity, node);

        //load without initializing: the proxy does not exist yet, only its stored state
        var reloaded = new Entity();
        reloaded.Load(node);
        Assert.Null(reloaded.GameplayProxy);

        var resavedNode = new JObject();
        EditorEntityJsonSerializer.SaveEntity(reloaded, resavedNode);

        Assert.Equal(42, (int?)resavedNode["script"]?["hit_points"]);
    }
}
