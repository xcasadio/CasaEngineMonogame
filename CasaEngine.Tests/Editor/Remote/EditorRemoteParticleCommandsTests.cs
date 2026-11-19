using System.Text.Json;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Remote;
using CasaEngine.Editor.Remote.Protocol;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Particles;
using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Xunit;

namespace CasaEngine.Tests.Editor.Remote;

/// <summary>ADR-0093, task T2.3: particle creation, the P5 bounds of the particle keys and the particle drop.</summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class EditorRemoteParticleCommandsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "casa-remote-particle-" + Guid.NewGuid().ToString("N"));
    private readonly RemoteTestHost _host;
    private readonly EditorRemoteControl _control;
    private readonly List<Guid> _catalogEntries = new();

    public EditorRemoteParticleCommandsTests()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "project", "Particles"));
        _host = new RemoteTestHost { ProjectDirectory = Path.Combine(_directory, "project") };
        _control = new EditorRemoteControl(_host, Path.Combine(_directory, "discovery"));
        EditorHistoryService.Current.IsSuspended = false;
        EditorHistoryService.Current.Remove(EditorRemoteEditCommands.WorldContext);
    }

    public void Dispose()
    {
        _control.Dispose();
        EditorHistoryService.Current.Remove(EditorRemoteEditCommands.WorldContext);
        foreach (Guid id in _catalogEntries)
        {
            AssetCatalog.RemoveInternal(id, out _);
        }

        Directory.Delete(_directory, recursive: true);
    }

    private Guid Catalog(string fileName, string name = null)
    {
        Guid id = Guid.NewGuid();
        AssetCatalog.AddInternal(id, name ?? "remote-particle-test-" + id.ToString("N"), fileName);
        _catalogEntries.Add(id);
        string path = Path.Combine(_host.ProjectDirectory!, fileName.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}");
        return id;
    }

    private RemoteResponse Call(string method, object parameters = null)
    {
        var call = new EditorRemoteCall(new RemoteRequest
        {
            Id = 1,
            Method = method,
            Params = parameters == null ? null : RemoteMessageCodec.ToElement(parameters),
        });
        _control.Dispatcher.Enqueue(call);
        _control.Pump();
        Assert.True(call.IsCompleted);
        return call.Completion.Result;
    }

    private static JsonElement Ok(RemoteResponse response)
    {
        Assert.True(response.Error == null, response.Error?.ToString());
        return response.Result!.Value;
    }

    [Fact]
    public void Create_GoesThroughTheChecksOfP4()
    {
        string name = "RemoteSpark" + Guid.NewGuid().ToString("N")[..8];

        Ok(Call("particle.create", new { name, folder = "Particles", preset = "spark" }));

        Assert.Equal(("Particles" + Path.DirectorySeparatorChar + name + ".particle", "spark"), Assert.Single(_host.CreatedParticles));
        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("particle.create", new { name = "X", folder = "../out" }).Error!.Code);
    }

    /// <summary>Security review S2-2: an out-of-bounds count can no longer crash the editor, numbers must be finite, enums named.</summary>
    [Theory]
    [InlineData("maxparticles", "2147483647")]
    [InlineData("maxparticles", "0")]
    [InlineData("emitter0.maxparticles", "100001")]
    [InlineData("duration", "NaN")]
    [InlineData("duration", "Infinity")]
    [InlineData("flipbookcolumns", "65")]
    [InlineData("flipbookfps", "241")]
    [InlineData("shape", "1")]
    [InlineData("blend", "NotABlendMode")]
    [InlineData("looping", "maybe")]
    [InlineData("unknownkey", "1")]
    public void Validate_RefusesValuesOutsideTheInspectorBounds(string key, string value)
    {
        Assert.NotNull(EditorRemoteParticleCommands.Validate(Normalize(key), value));
    }

    [Theory]
    [InlineData("maxparticles", "500")]
    [InlineData("duration", "2.5")]
    [InlineData("flipbookcolumns", "64")]
    [InlineData("looping", "false")]
    [InlineData("flipbookcurve", "linear")]
    [InlineData("colorlifetime", "fire")]
    public void Validate_AcceptsValuesInsideTheBounds(string key, string value)
    {
        Assert.Null(EditorRemoteParticleCommands.Validate(Normalize(key), value));
    }

    [Fact]
    public void Validate_EnumByNameAndTextureByCatalogType()
    {
        Guid texture = Catalog("Textures/smoke.texture");
        Guid sprite = Catalog("Sprites/hero.sprite");

        Assert.Null(EditorRemoteParticleCommands.Validate("shape", Enum.GetNames<ParticleShapeType>()[0].ToLowerInvariant()));
        Assert.Null(EditorRemoteParticleCommands.Validate("blend", Enum.GetNames<ParticleBlendMode>()[0]));
        Assert.Null(EditorRemoteParticleCommands.Validate("texture", texture.ToString()));
        Assert.Null(EditorRemoteParticleCommands.Validate("texture", Guid.Empty.ToString()));
        Assert.NotNull(EditorRemoteParticleCommands.Validate("texture", sprite.ToString()));
        Assert.NotNull(EditorRemoteParticleCommands.Validate("texture", Guid.NewGuid().ToString()));
    }

    [Fact]
    public void SetProperty_PassesTheTextAndTheValidator()
    {
        Guid id = Catalog("Particles/Fire.particle");

        Ok(Call("particle.set_property", new { asset = id.ToString(), property = "emitter0.maxparticles", value = 500 }));

        Assert.Equal((id, "emitter0.maxparticles", "500"), Assert.Single(_host.ParticleSets));
        Assert.Equal(nameof(EditorRemoteParticleCommands.Validate), _host.LastParticleValidator!.Method.Name);
    }

    [Fact]
    public void Get_DescribesTheEmitters()
    {
        Guid id = Catalog("Particles/Fire.particle");
        var effect = new ParticleEffectAsset { Name = "Fire" };
        effect.Emitters.Add(new ParticleEmitterDefinition { Name = "Flames", MaxParticles = 64 });
        _host.ParticleAsset = effect;

        JsonElement result = Ok(Call("particle.get", new { asset = id.ToString() }));

        JsonElement emitter = Assert.Single(result.GetProperty("emitters").EnumerateArray());
        Assert.Equal(64, emitter.GetProperty("max_particles").GetInt32());
        Assert.Contains(result.GetProperty("keys").EnumerateArray(), key => key.GetString() == "maxparticles");
    }

    [Fact]
    public void Drop_AddsAParticleSystem_AndIsUndoable()
    {
        Guid id = Catalog("Particles/Fire.particle");
        var world = new World { Name = "Level" };
        var entity = new Entity { Name = "Torch" };
        CasaEngine.EditorServices.EditorWorldEditingService.AttachEntity(world, entity, null);
        _host.CurrentWorld = world;

        Ok(Call("particle.drop", new { entity_id = entity.Id.ToString(), asset = id.ToString() }));

        var component = entity.GetComponent<ParticleSystemComponent>();
        Assert.NotNull(component);
        Assert.Equal(id, component!.ParticleEffectAssetId);
        Assert.True(Ok(Call("history.undo")).GetProperty("done").GetBoolean());
        Assert.Null(entity.GetComponent<ParticleSystemComponent>());

        Guid material = Catalog("Materials/Stone.material");
        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("particle.drop", new { entity_id = entity.Id.ToString(), asset = material.ToString() }).Error!.Code);
    }

    /// <summary>The validator receives keys normalised by the panel; the tests mirror its rule (lower case, emitter prefix removed).</summary>
    private static string Normalize(string key)
    {
        int dot = key.IndexOf('.');
        return (dot >= 0 ? key[(dot + 1)..] : key).ToLowerInvariant();
    }
}
