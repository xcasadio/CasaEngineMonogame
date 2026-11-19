using System.Text.Json;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Remote;
using CasaEngine.Editor.Remote.Protocol;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Assets;
using Xunit;
using Xunit.Abstractions;

namespace CasaEngine.Tests.Editor.Remote;

/// <summary>ADR-0093, task T2.1: asset references, asset.list, asset.open, panels and the undo context.</summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class EditorRemoteAssetCommandsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "casa-remote-assets-" + Guid.NewGuid().ToString("N"));
    private readonly RemoteTestHost _host;
    private readonly EditorRemoteControl _control;
    private readonly List<Guid> _catalogEntries = new();
    private readonly List<string> _links = new();
    private readonly ITestOutputHelper _log;

    public EditorRemoteAssetCommandsTests(ITestOutputHelper log)
    {
        _log = log;
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

        PathSegmentJunctions.RemoveLinks(_links);
        PathSegmentJunctions.DeleteTreeWithoutLinks(_directory);
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

    /// <summary>
    /// Plan remote-path-segments P5, set A4: a catalog entry written 'Real /x.material' (or with a backslash), where 'Real '
    /// is a junction to a folder outside the project that holds the only file of that name, is refused by its path and by
    /// its id, and asset.open never reaches the host. Without the real 'Real' folder (realFolder false) too.
    /// </summary>
    [Theory]
    [InlineData('/', true)]
    [InlineData('\\', true)]
    [InlineData('/', false)]
    public void SetA4_Resolve_ACatalogEntryUnderAJunctionEndingWithASpace_IsRefused_ByPathAndById(char separator, bool realFolder)
    {
        string project = Path.GetFullPath(_host.ProjectDirectory!);
        string outside = Path.Combine(_directory, "outside");
        Directory.CreateDirectory(outside);
        string onlyOutsideFile = "only-outside-" + Guid.NewGuid().ToString("N")[..8] + ".material";
        File.WriteAllText(Path.Combine(outside, onlyOutsideFile), "{\"outside\":true}");
        if (realFolder)
        {
            Directory.CreateDirectory(Path.Combine(project, "Real"));
        }

        if (!PathSegmentJunctions.TryCreate(project, "Real ", outside, _log, _links))
        {
            return;
        }

        Assert.Equal(realFolder, PathSegmentJunctions.IsRealFolderInEnumeration(project, "Real"));
        Assert.False(File.Exists(Path.Combine(project, "Real", onlyOutsideFile)));
        Assert.True(File.Exists(Path.Combine(project, "Real ", onlyOutsideFile)));
        string[] target = PathSegmentJunctions.Snapshot(outside);
        string fileName = "Real " + separator + onlyOutsideFile;
        Guid id = Catalog(fileName, createFile: false);
        string component = "'" + Path.Combine(project, "Real ") + "'";

        Exception byPath = Record.Exception(() => EditorRemoteAssets.Resolve(project, fileName, null));
        Exception byId = Record.Exception(() => EditorRemoteAssets.Resolve(project, id.ToString(), null));
        RemoteResponse openByPath = Call("asset.open", new { asset = fileName });
        RemoteResponse openById = Call("asset.open", new { asset = id.ToString() });
        _log.WriteLine($"Resolve by path: {byPath?.GetType().Name ?? "resolved"}: {byPath?.Message}");
        _log.WriteLine($"Resolve by id: {byId?.GetType().Name ?? "resolved"}: {byId?.Message}");
        _log.WriteLine($"asset.open by path: {openByPath.Error?.Message ?? "opened"}; by id: {openById.Error?.Message ?? "opened"}; host opens: {_host.OpenedAssets.Count}");

        Assert.IsType<EditorRemoteArgumentException>(byPath);
        Assert.Contains(component, byPath!.Message);
        Assert.Contains("ends with a space", byPath.Message);
        Assert.IsType<EditorRemoteArgumentException>(byId);
        Assert.Contains(component, byId!.Message);
        Assert.Contains("ends with a space", byId.Message);
        Assert.Equal(RemoteErrorCodes.InvalidParams, openByPath.Error?.Code);
        Assert.Equal(RemoteErrorCodes.InvalidParams, openById.Error?.Code);
        Assert.Empty(_host.OpenedAssets);
        Assert.Equal(target, PathSegmentJunctions.Snapshot(outside));
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

    /// <summary>ADR-0145, plan P3: an entity asset opens through asset.open, by id or by file, at the file Resolve checked.</summary>
    [Fact]
    public void AssetOpen_EntityByIdOrFile_OpensTheResolvedFile()
    {
        Guid id = Catalog("Entities/Box.entity");

        JsonElement byId = Ok(Call("asset.open", new { asset = id.ToString() }));
        JsonElement byFile = Ok(Call("asset.open", new { asset = "Entities/Box.entity" }));

        Assert.Equal("entity", byId.GetProperty("type").GetString());
        Assert.Equal("entity", byFile.GetProperty("type").GetString());
        Assert.Equal(id, byFile.GetProperty("asset_id").GetGuid());
        Assert.Equal(2, _host.OpenedAssets.Count);
        string expected = Path.Combine(Path.GetFullPath(_host.ProjectDirectory!), "Entities", "Box.entity");
        Assert.All(_host.OpenedAssets, opened =>
        {
            Assert.Equal(expected, opened.FullPath);
            Assert.Equal("entity", opened.Type);
        });
    }

    /// <summary>Security review S2-5, S2-6 and S3-5: the refusals of asset.open hold for an entity asset, before any host call.</summary>
    [Fact]
    public void AssetOpen_Entity_RefusesWhatItMustNotOpen()
    {
        Guid escaping = Catalog("../outside.entity", createFile: false);
        Guid missing = Catalog("Entities/Missing.entity", createFile: false);

        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("asset.open", new { asset = "../outside.entity" }).Error!.Code);
        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("asset.open", new { asset = escaping.ToString() }).Error!.Code);
        Assert.Equal(RemoteErrorReasons.NotFound, Call("asset.open", new { asset = missing.ToString() }).Error!.Data!.Reason);
        Assert.Equal(RemoteErrorReasons.NotFound, Call("asset.open", new { asset = Guid.NewGuid().ToString() }).Error!.Data!.Reason);
        Assert.Empty(_host.OpenedAssets);
    }

    [Fact]
    public void Resolve_EntityAndMaterial_RefuseEachOther()
    {
        Guid material = Catalog("Materials/Stone.material");
        Guid entity = Catalog("Entities/Box.entity");

        Assert.Equal("entity", EditorRemoteAssets.Resolve(_host.ProjectDirectory, entity.ToString(), "entity").Type);
        Assert.Throws<EditorRemoteArgumentException>(() => EditorRemoteAssets.Resolve(_host.ProjectDirectory, material.ToString(), "entity"));
        Assert.Throws<EditorRemoteArgumentException>(() => EditorRemoteAssets.Resolve(_host.ProjectDirectory, entity.ToString(), "material"));
    }

    /// <summary>ADR-0145, plan P3, security review S2-3: an entity panel closes like the four other asset panels, never when modified.</summary>
    [Fact]
    public void PanelClose_EntityPanel_ClosesOnlyWhenUnmodified()
    {
        _host.Panels.Add(new RemotePanelInfo { Id = "panel_entity_asset_saved", Kind = "entity" });
        _host.Panels.Add(new RemotePanelInfo { Id = "panel_entity_asset_modified", Kind = "entity", Modified = true });

        Assert.True(Ok(Call("panel.close", new { panel_id = "panel_entity_asset_saved" })).GetProperty("closed").GetBoolean());
        Assert.Equal(RemoteErrorReasons.UnsavedChanges, Call("panel.close", new { panel_id = "panel_entity_asset_modified" }).Error!.Data!.Reason);
        Assert.Equal(new[] { "panel_entity_asset_saved" }, _host.ClosedPanels);
    }

    /// <summary>
    /// ADR-0145, plans P3 and P13, security review S2-10 and S3-2: history.undo and history.redo reach the context of an open entity
    /// panel the host answers for; an id without a panel is refused and creates no history.
    /// </summary>
    [Fact]
    public void HistoryUndoAndRedo_WithAnEntityAssetContext_UseItsOpenPanel()
    {
        Guid assetId = Guid.NewGuid();
        var context = new EditorHistoryContext(EditorHistoryContextKind.Entity, "remote-assets-test-" + assetId.ToString("N"));
        int executed = 0;
        int undone = 0;
        EditorHistoryService.Current.IsSuspended = false;
        EditorHistoryService.Current.Execute(context, new EditorDelegateCommand("Edit Entity", () => executed++, () => undone++));
        _host.AssetContexts[assetId] = context;
        try
        {
            Assert.True(Ok(Call("history.undo", new { context = assetId.ToString() })).GetProperty("done").GetBoolean());
            Assert.Equal(1, undone);
            Assert.True(Ok(Call("history.redo", new { context = assetId.ToString() })).GetProperty("done").GetBoolean());
            Assert.Equal(2, executed);
            Assert.Equal(new[] { context, context }, _host.RefreshedContexts);

            Guid unknown = Guid.NewGuid();
            Assert.Equal(RemoteErrorReasons.NotFound, Call("history.undo", new { context = unknown.ToString() }).Error!.Data!.Reason);
            Assert.Equal(RemoteErrorReasons.NotFound, Call("history.redo", new { context = unknown.ToString() }).Error!.Data!.Reason);
            Assert.False(EditorHistoryService.Current.TryGet(
                new EditorHistoryContext(EditorHistoryContextKind.Entity, CasaEngine.Editor.Workspaces.EditorPanelIds.EntityAssetDocumentPrefix + unknown.ToString("N")),
                out _));
            Assert.False(EditorHistoryService.Current.TryGet(new EditorHistoryContext(EditorHistoryContextKind.Entity, "remote-assets-test-" + unknown.ToString("N")), out _));
        }
        finally
        {
            EditorHistoryService.Current.Remove(context);
        }
    }

    /// <summary>Plan P14, security review S3-7: an entity asset does not open during play; the four other types keep today's behaviour.</summary>
    [Fact]
    public void AssetOpen_DuringPlay_RefusesAnEntityOnly()
    {
        Guid entity = Catalog("Entities/Box.entity");
        Guid material = Catalog("Materials/Stone.material");
        _host.IsPlaySessionActive = true;

        Assert.Equal(RemoteErrorReasons.PlayModeActive, Call("asset.open", new { asset = entity.ToString() }).Error!.Data!.Reason);
        Assert.Equal(RemoteErrorReasons.PlayModeActive, Call("asset.open", new { asset = "Entities/Box.entity" }).Error!.Data!.Reason);
        Assert.Empty(_host.OpenedAssets);

        Assert.Equal("material", Ok(Call("asset.open", new { asset = material.ToString() })).GetProperty("type").GetString());
        Assert.Equal("material", Assert.Single(_host.OpenedAssets).Type);
    }
}
