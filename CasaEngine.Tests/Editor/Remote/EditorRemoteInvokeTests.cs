using CasaEngine.Editor.Remote;
using CasaEngine.Editor.Remote.Protocol;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.World;
using Xunit;

namespace CasaEngine.Tests.Editor.Remote;

/// <summary>ADR-0093 P9: the in-process executor of the automation options runs the registry, without the pipe.</summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class EditorRemoteInvokeTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "casa-remote-invoke-" + Guid.NewGuid().ToString("N"));
    private readonly RemoteTestHost _host = new();
    private readonly EditorRemoteControl _control;

    public EditorRemoteInvokeTests()
    {
        _control = new EditorRemoteControl(_host, Path.Combine(_directory, "discovery"));
    }

    public void Dispose()
    {
        _control.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Invoke_RunsACommandAtOnce_WithoutTheServer()
    {
        var world = new World { Name = "Level" };
        var entity = new Entity { Name = "Hero" };
        CasaEngine.EditorServices.EditorWorldEditingService.AttachEntity(world, entity, null);
        _host.CurrentWorld = world;

        RemoteResponse response = _control.Invoke("selection.set", new { entity_id = entity.Id });

        Assert.False(_control.IsRunning);
        Assert.Null(response.Error);
        Assert.Same(entity, _host.SelectedEntity);
        Assert.False(Directory.Exists(Path.Combine(_directory, "discovery")) && Directory.EnumerateFiles(Path.Combine(_directory, "discovery")).Any());
    }

    [Fact]
    public void Invoke_ChecksArgumentsLikeAClient()
    {
        _host.CurrentWorld = new World { Name = "Level" };

        Assert.Equal(RemoteErrorReasons.NotFound, _control.Invoke("selection.set", new { entity_id = Guid.NewGuid() }).Error!.Data!.Reason);
        Assert.Equal(RemoteErrorCodes.MethodNotFound, _control.Invoke("no.such.command").Error!.Code);
    }
}
