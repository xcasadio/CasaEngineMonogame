using System.Text.Json;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Remote;
using CasaEngine.Editor.Remote.Protocol;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Assets;
using Xunit;

namespace CasaEngine.Tests.Editor.Remote;

/// <summary>ADR-0093, task T2.1: asset references, asset.list, asset.open, panels and the undo context.</summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class EditorRemoteAssetCommandsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "casa-remote-assets-" + Guid.NewGuid().ToString("N"));
    private readonly RemoteTestHost _host;
    private readonly EditorRemoteControl _control;
    private readonly List<Guid> _catalogEntries = new();

    public EditorRemoteAssetCommandsTests()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "project", "Materials"));
        _host = new RemoteTestHost { ProjectDirectory = Path.Combine(_directory, "project") };
        _control = new EditorRemoteControl(_host, Path.Combine(_directory, "discovery"));
    }

    public void Dispose()
    {
        _control.Dispose();
        foreach (Guid id in _catalogEntries)
        {
            AssetCatalog.RemoveInternal(id, out _);
        }

        Directory.Delete(_directory, recursive: true);
    }

    private Guid Catalog(string fileName, bool createFile = true, string name = null)
    {
        Guid id = Guid.NewGuid();
        AssetCatalog.AddInternal(id, name ?? "remote-assets-test-" + id.ToString("N"), fileName);
        _catalogEntries.Add(id);
        if (createFile)
        {
            string path = Path.Combine(_host.ProjectDirectory!, fileName.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{}");
        }

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
    public void AssetOpen_ByIdOrFile_OpensTheResolvedFile()
    {
        Guid id = Catalog("Materials/Stone.material");

        Assert.Equal("material", Ok(Call("asset.open", new { asset = id.ToString() })).GetProperty("type").GetString());
        Ok(Call("asset.open", new { asset = "Materials/Stone.material" }));

        Assert.Equal(2, _host.OpenedAssets.Count);
        Assert.All(_host.OpenedAssets, opened => Assert.Equal(Path.Combine(Path.GetFullPath(_host.ProjectDirectory!), "Materials", "Stone.material"), opened.FullPath));
    }

    /// <summary>Security review S2-5 and S2-6: worlds, other types, outside paths, catalog entries that leave the project, missing files.</summary>
    [Fact]
    public void AssetOpen_RefusesWhatItMustNotOpen()
    {
        Guid world = Catalog("Level.world");
        Guid sprite = Catalog("Hero.sprite");
        Guid escaping = Catalog("../outside.material", createFile: false);
        Guid missing = Catalog("Materials/Missing.material", createFile: false);

        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("asset.open", new { asset = world.ToString() }).Error!.Code);
        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("asset.open", new { asset = sprite.ToString() }).Error!.Code);
        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("asset.open", new { asset = escaping.ToString() }).Error!.Code);
        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("asset.open", new { asset = "../outside.material" }).Error!.Code);
        Assert.Equal(RemoteErrorReasons.NotFound, Call("asset.open", new { asset = missing.ToString() }).Error!.Data!.Reason);
        Assert.Equal(RemoteErrorReasons.NotFound, Call("asset.open", new { asset = Guid.NewGuid().ToString() }).Error!.Data!.Reason);
        Assert.Empty(_host.OpenedAssets);
    }

    [Fact]
    public void Resolve_ChecksTheExpectedType()
    {
        Guid id = Catalog("Materials/Stone.material");

        Assert.Equal("material", EditorRemoteAssets.Resolve(_host.ProjectDirectory, id.ToString(), "material").Type);
        Assert.Throws<EditorRemoteArgumentException>(() => EditorRemoteAssets.Resolve(_host.ProjectDirectory, id.ToString(), "sound"));
    }

    [Fact]
    public void AssetList_FiltersByType_AndIsCappedAt500()
    {
        for (int i = 0; i <= EditorRemoteAssetCommands.MaxListedAssets; i++)
        {
            Catalog($"Cap/a{i:D4}.remotelisttest", createFile: false);
        }

        JsonElement result = Ok(Call("asset.list", new { type = "remotelisttest" }));

        Assert.Equal(EditorRemoteAssetCommands.MaxListedAssets, result.GetProperty("assets").GetArrayLength());
        Assert.True(result.GetProperty("truncated").GetBoolean());
        Assert.Equal(EditorRemoteAssetCommands.MaxListedAssets + 1, result.GetProperty("total").GetInt32());
        Assert.Equal("Cap/a0000.remotelisttest", result.GetProperty("assets")[0].GetProperty("file").GetString());
    }

    [Fact]
    public void Panels_ListActivateAndClose_FollowTheClosedList()
    {
        _host.Panels.Add(new RemotePanelInfo { Id = "panel_material_asset_x", Kind = "material" });
        _host.Panels.Add(new RemotePanelInfo { Id = "panel_sound_asset_y", Kind = "sound", Modified = true });
        _host.Panels.Add(new RemotePanelInfo { Id = "panel_hierarchy", Kind = "other" });

        Assert.Equal(3, Ok(Call("panel.list")).GetProperty("panels").GetArrayLength());
        Ok(Call("panel.activate", new { panel_id = "panel_hierarchy" }));
        Assert.Equal(RemoteErrorReasons.NotFound, Call("panel.activate", new { panel_id = "nope" }).Error!.Data!.Reason);

        Assert.True(Ok(Call("panel.close", new { panel_id = "panel_material_asset_x" })).GetProperty("closed").GetBoolean());
        Assert.Equal(RemoteErrorReasons.UnsavedChanges, Call("panel.close", new { panel_id = "panel_sound_asset_y" }).Error!.Data!.Reason);
        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("panel.close", new { panel_id = "panel_hierarchy" }).Error!.Code);
        Assert.Equal(RemoteErrorReasons.NotFound, Call("panel.close", new { panel_id = "nope" }).Error!.Data!.Reason);
        Assert.Equal(new[] { "panel_material_asset_x" }, _host.ClosedPanels);
    }

    /// <summary>Security review S2-10: the undo context must be the panel of an open asset, never a new one.</summary>
    [Fact]
    public void HistoryUndo_WithAnAssetContext_UsesItsOpenPanel()
    {
        Guid assetId = Guid.NewGuid();
        var context = new EditorHistoryContext(EditorHistoryContextKind.Material, "remote-assets-test-" + assetId.ToString("N"));
        bool undone = false;
        EditorHistoryService.Current.IsSuspended = false;
        EditorHistoryService.Current.Execute(context, new EditorDelegateCommand("Edit Material", () => { }, () => undone = true));
        _host.AssetContexts[assetId] = context;
        try
        {
            JsonElement result = Ok(Call("history.undo", new { context = assetId.ToString() }));

            Assert.True(result.GetProperty("done").GetBoolean());
            Assert.True(undone);
            Assert.Equal(context, Assert.Single(_host.RefreshedContexts));

            Guid unknown = Guid.NewGuid();
            Assert.Equal(RemoteErrorReasons.NotFound, Call("history.undo", new { context = unknown.ToString() }).Error!.Data!.Reason);
            Assert.Equal(RemoteErrorCodes.InvalidParams, Call("history.redo", new { context = "elsewhere" }).Error!.Code);
            Assert.False(EditorHistoryService.Current.TryGet(new EditorHistoryContext(EditorHistoryContextKind.Material, "remote-assets-test-" + unknown.ToString("N")), out _));
        }
        finally
        {
            EditorHistoryService.Current.Remove(context);
        }
    }
}
