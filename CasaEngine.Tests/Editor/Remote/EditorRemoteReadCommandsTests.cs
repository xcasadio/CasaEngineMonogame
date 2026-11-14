using System.Text.Json;
using CasaEngine.Editor.Log;
using CasaEngine.Editor.Remote;
using CasaEngine.Editor.Remote.Protocol;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Editor.Remote;

/// <summary>ADR-0092, task T2.2: the read commands and the log cursor.</summary>
public sealed class EditorRemoteReadCommandsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "casa-remote-read-" + Guid.NewGuid().ToString("N"));
    private readonly TestHost _host = new();
    private readonly EditorRemoteControl _control;

    public EditorRemoteReadCommandsTests()
    {
        _control = new EditorRemoteControl(_host, _directory);
    }

    public void Dispose()
    {
        _control.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    internal sealed class TestHost : IEditorRemoteHost
    {
        public string EditorVersion => "1.0";

        public string ProjectFilePath => null;

        public string CurrentWorldName => CurrentWorld?.Name;

        public bool IsPlaySessionActive { get; set; }

        public bool HasUnsavedWorldChanges { get; set; }

        public World CurrentWorld { get; set; }

        public Entity SelectedEntity { get; set; }

        public EntityComponent SelectedComponent { get; set; }

        public LoggerEditor Logger { get; } = new() { MaxEntries = 10 };
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

    private (Entity Parent, Entity Child, RemoteReadTestComponent Component) CreateWorld()
    {
        var world = new World { Name = "Level" };
        var parent = new Entity { Name = "Parent" };
        var child = new Entity { Name = "Child" };
        var component = new RemoteReadTestComponent { Speed = 2.5f, Offset = new Vector3(1, 2, 3) };
        CasaEngine.EditorServices.EditorWorldEditingService.AttachEntity(world, parent, null);
        CasaEngine.EditorServices.EditorWorldEditingService.AttachEntity(world, child, parent);
        child.AddComponent(component);
        _host.CurrentWorld = world;
        return (parent, child, component);
    }

    [Fact]
    public void WorldEntities_ListsTheTreeWithPathsAndComponents()
    {
        var (parent, child, component) = CreateWorld();

        JsonElement result = Ok(Call("world.entities"));

        Assert.Equal("Level", result.GetProperty("world_name").GetString());
        var entities = result.GetProperty("entities").EnumerateArray().ToList();
        Assert.Equal(2, entities.Count);
        JsonElement childJson = entities.Single(e => e.GetProperty("id").GetGuid() == child.Id);
        Assert.Equal("Parent/Child", childJson.GetProperty("path").GetString());
        Assert.Equal(parent.Id, childJson.GetProperty("parent_id").GetGuid());
        JsonElement componentJson = Assert.Single(childJson.GetProperty("components").EnumerateArray());
        Assert.Equal(component.Id, componentJson.GetProperty("id").GetGuid());
        Assert.Equal("list", componentJson.GetProperty("placement").GetString());
        Assert.False(componentJson.TryGetProperty("properties", out _));
    }

    [Fact]
    public void EntityGet_ReturnsOnlyEditableProperties_AsJsonValues()
    {
        var (_, child, _) = CreateWorld();

        JsonElement result = Ok(Call("entity.get", new { entity_id = child.Id.ToString() }));

        JsonElement properties = Assert.Single(result.GetProperty("components").EnumerateArray()).GetProperty("properties");
        var byName = properties.EnumerateArray().ToDictionary(p => p.GetProperty("name").GetString()!);
        Assert.Equal(2.5f, byName["Speed"].GetProperty("value").GetSingle());
        Assert.Equal("float", byName["Speed"].GetProperty("type").GetString());
        Assert.Equal(new[] { 1f, 2f, 3f }, byName["Offset"].GetProperty("value").EnumerateArray().Select(v => v.GetSingle()));
        Assert.Equal("enum:Slow|Fast", byName["Mode"].GetProperty("type").GetString());
        Assert.False(byName.ContainsKey("Name"));
        Assert.False(byName.ContainsKey("Owner"));
        Assert.False(byName.ContainsKey("ReadOnlyValue"));
        Assert.False(byName.ContainsKey("Hidden"));
        Assert.False(byName.ContainsKey("Unsupported"));
    }

    [Fact]
    public void EntityGet_UnknownOrMalformedIds_AreRefused()
    {
        CreateWorld();

        Assert.Equal(RemoteErrorReasons.NotFound, Call("entity.get", new { entity_id = Guid.NewGuid().ToString() }).Error!.Data!.Reason);
        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("entity.get", new { entity_id = "nope" }).Error!.Code);
        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("entity.get").Error!.Code);
    }

    [Fact]
    public void WithoutAWorld_WorldCommandsAreNotFound()
    {
        Assert.Equal(RemoteErrorReasons.NotFound, Call("world.entities").Error!.Data!.Reason);
    }

    [Fact]
    public void ComponentTypes_AndSelection()
    {
        var (_, child, component) = CreateWorld();
        _host.SelectedEntity = child;
        _host.SelectedComponent = component;

        Assert.Contains(Ok(Call("component.types")).GetProperty("types").EnumerateArray(), t => t.GetString() == nameof(RemoteReadTestComponent));
        JsonElement selection = Ok(Call("selection.get"));
        Assert.Equal(child.Id, selection.GetProperty("entity_id").GetGuid());
        Assert.Equal(component.Id, selection.GetProperty("component_id").GetGuid());
    }

    /// <summary>Plan T2.2: the cursor survives a trim of the editor log, reports the lost entries and repeats none.</summary>
    [Fact]
    public void LogsRead_CursorSurvivesATrim()
    {
        for (int i = 0; i < 5; i++)
        {
            _host.Logger.WriteInfo("first " + i);
        }

        JsonElement first = Ok(Call("logs.read", new { from_sequence = 0 }));
        Assert.Equal(5, first.GetProperty("entries").GetArrayLength());
        long cursor = first.GetProperty("next_sequence").GetInt64();
        Assert.Equal(5, cursor);

        for (int i = 0; i < 20; i++)
        {
            _host.Logger.WriteInfo("second " + i);
        }

        JsonElement second = Ok(Call("logs.read", new { from_sequence = cursor, max_entries = 200 }));
        var sequences = second.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("sequence").GetInt64()).ToList();
        Assert.True(second.GetProperty("lost").GetInt64() > 0);
        Assert.Equal(sequences.Distinct().Count(), sequences.Count);
        Assert.Equal(cursor + second.GetProperty("lost").GetInt64(), sequences[0]);
        Assert.Equal(25, second.GetProperty("next_sequence").GetInt64());
        Assert.Equal("second 19", second.GetProperty("entries").EnumerateArray().Last().GetProperty("message").GetString());
    }

    [Fact]
    public void LogsRead_IsCappedInCountAndText()
    {
        _host.Logger.MaxEntries = 1000;
        for (int i = 0; i < 300; i++)
        {
            _host.Logger.WriteInfo(new string('x', 5000));
        }

        JsonElement page = Ok(Call("logs.read", new { from_sequence = 0, max_entries = 200 }));
        var entries = page.GetProperty("entries").EnumerateArray().ToList();

        Assert.True(entries.Count < 200);
        Assert.All(entries, e => Assert.True(e.GetProperty("message").GetString()!.Length <= EditorRemoteWorldModel.MaxReturnedTextLength + 1));
        Assert.True(entries.Sum(e => e.GetProperty("message").GetString()!.Length) <= EditorRemoteLogReader.MaxCharactersPerRead);
        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("logs.read", new { max_entries = 500 }).Error!.Code);
    }

    public enum TestMode
    {
        Slow,
        Fast,
    }

    public sealed class RemoteReadTestComponent : EntityComponent
    {
        public float Speed { get; set; }

        public Vector3 Offset { get; set; }

        public TestMode Mode { get; set; }

        public int ReadOnlyValue => 3;

        [System.ComponentModel.Browsable(false)]
        public int Hidden { get; set; }

        public Matrix Unsupported { get; set; }

        public override RemoteReadTestComponent Clone() => new() { Speed = Speed, Offset = Offset, Mode = Mode };
    }
}
