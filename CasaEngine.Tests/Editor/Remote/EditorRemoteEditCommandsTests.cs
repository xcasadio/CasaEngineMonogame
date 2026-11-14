using System.Text.Json;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Remote;
using CasaEngine.Editor.Remote.Protocol;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Editor.Remote;

/// <summary>ADR-0092, task T3.1: the edit commands, their history context and the security checks of plan P5 and P8.</summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class EditorRemoteEditCommandsTests : IDisposable
{
    private static readonly EditorHistoryContext OtherContext = new(EditorHistoryContextKind.Material, "remote-edit-tests-other");
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "casa-remote-edit-" + Guid.NewGuid().ToString("N"));
    private readonly RemoteTestHost _host;
    private readonly EditorRemoteControl _control;
    private readonly World _world = new() { Name = "Level", FileName = "Level.world" };

    public EditorRemoteEditCommandsTests()
    {
        EditorHistoryService.Current.IsSuspended = false;
        EditorHistoryService.Current.Remove(EditorRemoteEditCommands.WorldContext);
        EditorHistoryService.Current.Remove(OtherContext);
        Directory.CreateDirectory(Path.Combine(_directory, "project"));
        _host = new RemoteTestHost
        {
            ProjectFilePath = Path.Combine(_directory, "project", "game.casaproject"),
            ProjectDirectory = Path.Combine(_directory, "project"),
            CurrentWorld = _world,
        };
        _control = new EditorRemoteControl(_host, Path.Combine(_directory, "discovery"));
    }

    public void Dispose()
    {
        _control.Dispose();
        EditorHistoryService.Current.Deactivate();
        EditorHistoryService.Current.Remove(EditorRemoteEditCommands.WorldContext);
        EditorHistoryService.Current.Remove(OtherContext);
        Directory.Delete(_directory, recursive: true);
    }

    private EditorRemoteCall Start(string method, object parameters = null)
    {
        var call = new EditorRemoteCall(new RemoteRequest
        {
            Id = 1,
            Method = method,
            Params = parameters == null ? null : RemoteMessageCodec.ToElement(parameters),
        });
        _control.Dispatcher.Enqueue(call);
        _control.Pump();
        return call;
    }

    private RemoteResponse Call(string method, object parameters = null)
    {
        EditorRemoteCall call = Start(method, parameters);
        Assert.True(call.IsCompleted, $"'{method}' did not answer.");
        return call.Completion.Result;
    }

    private static JsonElement Ok(RemoteResponse response)
    {
        Assert.True(response.Error == null, response.Error?.ToString());
        return response.Result!.Value;
    }

    private static void AssertRefused(RemoteResponse response, string reason)
    {
        Assert.NotNull(response.Error);
        Assert.Equal(reason, response.Error!.Data?.Reason);
    }

    private Entity CreateEntity(string name, Entity parent = null)
    {
        Guid id = Ok(Call("entity.create", parent == null ? new { name } : new { name, parent_id = parent.Id.ToString() })).GetProperty("entity_id").GetGuid();
        return EditorRemoteWorldModel.FindEntity(_world, id)!;
    }

    [Theory]
    [InlineData("project.save")]
    [InlineData("entity.create")]
    [InlineData("entity.delete")]
    [InlineData("component.add")]
    [InlineData("property.set")]
    [InlineData("history.undo")]
    [InlineData("world.create")]
    [InlineData("world.open")]
    public void EveryEditCommand_IsRefusedDuringPlay(string method)
    {
        _host.IsPlaySessionActive = true;

        AssertRefused(Call(method, new { }), RemoteErrorReasons.PlayModeActive);
    }

    [Fact]
    public void EntityCreate_UnderAParent_SelectsIt_RefreshesTheViews_AndIsUndoable()
    {
        Entity parent = CreateEntity("Parent");
        Entity child = CreateEntity("Child", parent);

        Assert.Same(parent, child.Parent);
        Assert.Same(child, _host.SelectedEntity);
        Assert.True(_host.RefreshCount >= 2);

        Assert.True(Ok(Call("history.undo")).GetProperty("done").GetBoolean());
        Assert.Null(child.Parent);
        Assert.True(Ok(Call("history.redo")).GetProperty("done").GetBoolean());
        Assert.Same(parent, child.Parent);
    }

    /// <summary>Plan P5: history.undo acts on the world context, not on the stack of the panel that has the focus.</summary>
    [Fact]
    public void HistoryUndo_UndoesTheWorldChange_EvenWhenAnotherContextIsActive()
    {
        Entity entity = CreateEntity("Remote");
        bool otherUndone = false;
        EditorHistoryService.Current.Execute(OtherContext, new EditorDelegateCommand("Other", () => { }, () => otherUndone = true));
        EditorHistoryService.Current.SetActiveContext(OtherContext);

        JsonElement result = Ok(Call("history.undo"));

        Assert.Equal("Add Entity", result.GetProperty("description").GetString());
        Assert.DoesNotContain(entity, _world.Entities);
        Assert.False(otherUndone);
        Assert.True(EditorHistoryService.Current.GetOrCreate(OtherContext).CanUndo);
    }

    [Fact]
    public void HistoryUndo_WithNothingToUndo_SaysSo()
    {
        Assert.False(Ok(Call("history.undo")).GetProperty("done").GetBoolean());
    }

    [Fact]
    public void RenameDuplicateDelete()
    {
        Entity entity = CreateEntity("A");

        Assert.True(Ok(Call("entity.rename", new { entity_id = entity.Id.ToString(), name = "B" })).GetProperty("changed").GetBoolean());
        Assert.Equal("B", entity.Name);

        Guid copyId = Ok(Call("entity.duplicate", new { entity_id = entity.Id.ToString() })).GetProperty("entity_id").GetGuid();
        Assert.Equal("B Copy", EditorRemoteWorldModel.FindEntity(_world, copyId)!.Name);

        Ok(Call("entity.delete", new { entity_id = entity.Id.ToString() }));
        Assert.Null(EditorRemoteWorldModel.FindEntity(_world, entity.Id));
        Ok(Call("history.undo"));
        Assert.NotNull(EditorRemoteWorldModel.FindEntity(_world, entity.Id));
    }

    [Fact]
    public void EntityNames_AreChecked()
    {
        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("entity.create", new { name = " " }).Error!.Code);
        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("entity.create", new { name = new string('x', 129) }).Error!.Code);
        AssertRefused(Call("entity.create", new { name = "x", parent_id = Guid.NewGuid().ToString() }), RemoteErrorReasons.NotFound);
    }

    [Fact]
    public void ComponentAdd_FollowsThePlacementRules()
    {
        Entity entity = CreateEntity("E");
        string id = entity.Id.ToString();

        JsonElement root = Ok(Call("component.add", new { entity_id = id, type = nameof(RemoteEditSceneComponent) }));
        Assert.Equal("root", root.GetProperty("placement").GetString());

        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("component.add", new { entity_id = id, type = nameof(RemoteEditSceneComponent) }).Error!.Code);

        string rootId = root.GetProperty("component_id").GetString()!;
        JsonElement child = Ok(Call("component.add", new { entity_id = id, type = nameof(RemoteEditSceneComponent), parent_component_id = rootId }));
        Assert.Equal("child", child.GetProperty("placement").GetString());

        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("component.add", new { entity_id = id, type = nameof(RemoteEditComponent), parent_component_id = rootId }).Error!.Code);
        Assert.Equal("list", Ok(Call("component.add", new { entity_id = id, type = nameof(RemoteEditComponent) })).GetProperty("placement").GetString());
    }

    /// <summary>Security review S6: only the inspector's allowlist is instantiated.</summary>
    [Theory]
    [InlineData("Process")]
    [InlineData("System.Diagnostics.Process")]
    [InlineData(nameof(RemoteEditHiddenComponent))]
    [InlineData("EntityComponent")]
    public void ComponentAdd_RejectsTypesOutsideTheAllowlist(string type)
    {
        Entity entity = CreateEntity("E");
        RemoteEditHiddenComponent.Constructed = 0;

        RemoteResponse response = Call("component.add", new { entity_id = entity.Id.ToString(), type });

        Assert.Equal(RemoteErrorCodes.InvalidParams, response.Error!.Code);
        Assert.Equal(0, RemoteEditHiddenComponent.Constructed);
        Assert.Empty(entity.Components);
    }

    [Fact]
    public void ComponentRemove_RefusesAParentWithChildren_AndUndoRestoresAChild()
    {
        Entity entity = CreateEntity("E");
        string id = entity.Id.ToString();
        string rootId = Ok(Call("component.add", new { entity_id = id, type = nameof(RemoteEditSceneComponent) })).GetProperty("component_id").GetString()!;
        string childId = Ok(Call("component.add", new { entity_id = id, type = nameof(RemoteEditSceneComponent), parent_component_id = rootId })).GetProperty("component_id").GetString()!;

        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("component.remove", new { entity_id = id, component_id = rootId }).Error!.Code);

        Ok(Call("component.remove", new { entity_id = id, component_id = childId }));
        Assert.Empty(entity.RootComponent!.Children);
        Ok(Call("history.undo"));
        Assert.Equal(Guid.Parse(childId), Assert.Single(entity.RootComponent.Children).Id);
    }

    [Fact]
    public void PropertySet_ChangesTypedValues_AndIsUndoable()
    {
        Entity entity = CreateEntity("E");
        string id = entity.Id.ToString();
        Guid componentId = Ok(Call("component.add", new { entity_id = id, type = nameof(RemoteEditComponent) })).GetProperty("component_id").GetGuid();
        var component = (RemoteEditComponent)EditorRemoteWorldModel.FindComponent(entity, componentId)!;
        string cid = componentId.ToString();

        Assert.True(Ok(Call("property.set", new { entity_id = id, component_id = cid, property = "Speed", value = 4.5 })).GetProperty("changed").GetBoolean());
        Ok(Call("property.set", new { entity_id = id, component_id = cid, property = "Offset", value = new[] { 1, 2, 3 } }));
        Ok(Call("property.set", new { entity_id = id, component_id = cid, property = "Tint", value = new[] { 10, 20, 30 } }));
        Ok(Call("property.set", new { entity_id = id, component_id = cid, property = "Mode", value = "Fast" }));
        Ok(Call("property.set", new { entity_id = id, component_id = cid, property = "Label", value = "hello" }));

        Assert.Equal(4.5f, component.Speed);
        Assert.Equal(new Vector3(1, 2, 3), component.Offset);
        Assert.Equal(new Color(10, 20, 30, 255), component.Tint);
        Assert.Equal(RemoteEditMode.Fast, component.Mode);
        Assert.Equal("hello", component.Label);

        Assert.False(Ok(Call("property.set", new { entity_id = id, component_id = cid, property = "Speed", value = 4.5 })).GetProperty("changed").GetBoolean());
        Ok(Call("history.undo"));
        Assert.Null(component.Label);
    }

    /// <summary>Security review S7: only editable properties of the inspector's types, with checked values.</summary>
    [Theory]
    [InlineData("Owner", "\"x\"")]
    [InlineData("Name", "\"x\"")]
    [InlineData("ReadOnlyValue", "1")]
    [InlineData("Hidden", "1")]
    [InlineData("Unsupported", "1")]
    [InlineData("Offset.X", "1")]
    [InlineData("Speed", "\"NaN\"")]
    [InlineData("Speed", "1e39")]
    [InlineData("Speed", "true")]
    [InlineData("Mode", "\"Medium\"")]
    [InlineData("Mode", "\"1\"")]
    [InlineData("Mode", "1")]
    [InlineData("Offset", "[1, 2]")]
    [InlineData("Tint", "[1, 2, 300]")]
    [InlineData("Label", "12")]
    public void PropertySet_RejectsWhatTheInspectorWouldNotDo(string property, string valueJson)
    {
        Entity entity = CreateEntity("E");
        string id = entity.Id.ToString();
        string cid = Ok(Call("component.add", new { entity_id = id, type = nameof(RemoteEditComponent) })).GetProperty("component_id").GetString()!;
        long revision = EditorHistoryService.Current.GetOrCreate(EditorRemoteEditCommands.WorldContext).CurrentRevision;

        RemoteResponse response = Call("property.set", new { entity_id = id, component_id = cid, property, value = JsonDocument.Parse(valueJson).RootElement });

        Assert.Equal(RemoteErrorCodes.InvalidParams, response.Error!.Code);
        Assert.Equal(revision, EditorHistoryService.Current.GetOrCreate(EditorRemoteEditCommands.WorldContext).CurrentRevision);
    }

    [Fact]
    public void PropertySet_RejectsTooLongText()
    {
        Entity entity = CreateEntity("E");
        string id = entity.Id.ToString();
        string cid = Ok(Call("component.add", new { entity_id = id, type = nameof(RemoteEditComponent) })).GetProperty("component_id").GetString()!;

        RemoteResponse response = Call("property.set", new { entity_id = id, component_id = cid, property = "Label", value = new string('x', EditorRemoteValidation.MaxPropertyTextLength + 1) });

        Assert.Equal(RemoteErrorCodes.InvalidParams, response.Error!.Code);
    }

    [Fact]
    public void ProjectSave_ReportsAFailedSave()
    {
        Assert.True(Ok(Call("project.save")).GetProperty("saved").GetBoolean());

        _host.SaveSucceeds = false;
        AssertRefused(Call("project.save"), RemoteErrorReasons.Failed);
        Assert.Equal(2, _host.SaveCount);
    }

    [Fact]
    public void WorldCreate_CreatesOpensAndAnswersOnceLoaded()
    {
        EditorRemoteCall call = Start("world.create", new { name = "Level 2" });

        Assert.Equal(("Level 2", "Level 2.world"), Assert.Single(_host.CreatedWorlds));
        Assert.Equal(Path.Combine(_host.ProjectDirectory!, "Level 2.world"), Assert.Single(_host.OpenedWorlds));
        Assert.False(call.IsCompleted);

        _host.CurrentWorld = new World { Name = "Level 2", FileName = "Level 2.world" };
        _control.Pump();

        Assert.Equal("Level 2", Ok(call.Completion.Result).GetProperty("world_name").GetString());
    }

    /// <summary>Security review S5: a world is created by name, inside the project, never over an existing file.</summary>
    [Theory]
    [InlineData("../outside")]
    [InlineData("C:\\outside")]
    [InlineData("Gameplay.csproj")]
    [InlineData("Directory.Build")]
    [InlineData("CON")]
    [InlineData("a:b")]
    [InlineData("")]
    [InlineData("trailing ")]
    public void WorldCreate_RejectsBadNames(string name)
    {
        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("world.create", new { name }).Error!.Code);
        Assert.Empty(_host.CreatedWorlds);
    }

    [Fact]
    public void WorldCreate_RefusesAnExistingFile_AndUnsavedChanges()
    {
        File.WriteAllText(Path.Combine(_host.ProjectDirectory!, "Taken.world"), "{}");
        AssertRefused(Call("world.create", new { name = "Taken" }), RemoteErrorReasons.InvalidArgument);

        _host.HasUnsavedWorldChanges = true;
        AssertRefused(Call("world.create", new { name = "Fresh" }), RemoteErrorReasons.UnsavedChanges);
        AssertRefused(Call("world.open", new { file = "Level.world" }), RemoteErrorReasons.UnsavedChanges);
        Assert.Empty(_host.CreatedWorlds);
        Assert.Empty(_host.OpenedWorlds);
    }

    [Theory]
    [InlineData("../other.world")]
    [InlineData("..\\project-x\\a.world")]
    [InlineData("C:/x.world")]
    [InlineData("Level.txt")]
    public void WorldOpen_RejectsFilesOutsideTheProjectOrNotWorlds(string file)
    {
        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("world.open", new { file }).Error!.Code);
        Assert.Empty(_host.OpenedWorlds);
    }

    [Fact]
    public void WorldOpen_UnknownToTheCatalog_IsNotFound()
    {
        _host.OpenSucceeds = false;

        AssertRefused(Call("world.open", new { file = "Missing.world" }), RemoteErrorReasons.NotFound);
    }

    [Fact]
    public void ResolveInsideProject_DoesNotConfuseASiblingFolderWithTheSamePrefix()
    {
        string project = Path.Combine(_directory, "proj");

        Assert.False(EditorRemoteValidation.TryResolveInsideProject(project, "../proj-x/a.world", out _));
        Assert.False(EditorRemoteValidation.TryResolveInsideProject(project, ".", out _));
        Assert.True(EditorRemoteValidation.TryResolveInsideProject(project, "sub/../a.world", out string full));
        Assert.Equal(Path.Combine(Path.GetFullPath(project), "a.world"), full);
    }

    [Fact]
    public void SelectionSet_SelectsAndClears()
    {
        Entity entity = CreateEntity("E");

        Ok(Call("selection.set", new { entity_id = entity.Id.ToString() }));
        Assert.Same(entity, _host.SelectedEntity);

        Ok(Call("selection.set", new { }));
        Assert.Null(_host.SelectedEntity);
        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("selection.set", new { component_id = Guid.NewGuid().ToString() }).Error!.Code);
    }

    public enum RemoteEditMode
    {
        Slow,
        Fast,
    }

    public sealed class RemoteEditComponent : EntityComponent
    {
        public float Speed { get; set; }

        public Vector3 Offset { get; set; }

        public Color Tint { get; set; }

        public RemoteEditMode Mode { get; set; }

        public string Label { get; set; }

        public int ReadOnlyValue => 3;

        [System.ComponentModel.Browsable(false)]
        public int Hidden { get; set; }

        public Matrix Unsupported { get; set; }

        public override RemoteEditComponent Clone() => new();
    }

    [System.ComponentModel.Browsable(false)]
    public sealed class RemoteEditHiddenComponent : EntityComponent
    {
        public static int Constructed;

        public RemoteEditHiddenComponent()
        {
            Constructed++;
        }

        public override RemoteEditHiddenComponent Clone() => new();
    }

    public sealed class RemoteEditSceneComponent : SceneComponent
    {
        public RemoteEditSceneComponent()
        {
        }

        private RemoteEditSceneComponent(RemoteEditSceneComponent other) : base(other)
        {
        }

        public override RemoteEditSceneComponent Clone() => new(this);
    }
}
