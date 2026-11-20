using System.Diagnostics;
using System.Text.Json;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Remote;
using CasaEngine.Editor.Remote.Protocol;
using CasaEngine.Editor.Runtime;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using Xunit.Abstractions;
using static CasaEngine.Tests.Editor.Remote.EditorRemoteEditCommandsTests;

namespace CasaEngine.Tests.Editor.Remote;

/// <summary>
/// ADR-0145, task T1.2: the <c>entity_asset.*</c> commands edit the entity of a <c>.entity</c> asset through its open
/// panel and the panel's history (plans P1, P2, P5, P7), refused in play mode (P14), with the asset reference checked
/// before any host call and the panel checked to hold the resolved file before any read or edit (P13, security review
/// S3-1, S3-5, S3-7).
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class EditorRemoteEntityAssetCommandsTests : IDisposable
{
    private const string BoxFile = "Entities/Box.entity";

    private static readonly string[] Methods =
    {
        "entity_asset.get",
        "entity_asset.set_property",
        "entity_asset.add_component",
        "entity_asset.remove_component",
        "entity_asset.save",
    };

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "casa-remote-entity-assets-" + Guid.NewGuid().ToString("N"));
    private readonly string _project;
    private readonly RemoteTestHost _host;
    private readonly EditorRemoteControl _control;
    private readonly List<Guid> _catalogEntries = new();
    private readonly List<string> _links = new();
    private readonly ITestOutputHelper _log;
    private readonly Guid _assetId;
    private readonly Entity _entity = new() { Name = "Box" };
    private readonly RemoteTestHost.EntityAssetPanel _panel;

    public EditorRemoteEntityAssetCommandsTests(ITestOutputHelper log)
    {
        _log = log;
        EditorHistoryService.Current.IsSuspended = false;
        _project = Path.GetFullPath(Path.Combine(_directory, "project"));
        Directory.CreateDirectory(_project);
        _host = new RemoteTestHost
        {
            ProjectFilePath = Path.Combine(_project, "game.casaproject"),
            ProjectDirectory = _project,
        };
        _control = new EditorRemoteControl(_host, Path.Combine(_directory, "discovery"));

        // Catalogued with '\' to prove the answer's file uses '/'.
        _assetId = Catalog(BoxFile.Replace('/', '\\'));
        _panel = _host.AddEntityAsset(_assetId, _entity, BoxFile);
        ResetContext(_panel.Context);
    }

    public void Dispose()
    {
        _control.Dispose();
        foreach (RemoteTestHost.EntityAssetPanel panel in _host.EntityAssets.Values)
        {
            ResetContext(panel.Context);
        }

        foreach (Guid id in _catalogEntries)
        {
            AssetCatalog.RemoveInternal(id, out _);
        }

        // A link is removed on its own first, so that nothing walks through it into its target.
        for (int i = _links.Count - 1; i >= 0; i--)
        {
            if (Directory.Exists(_links[i]))
            {
                Directory.Delete(_links[i]);
            }
        }

        Directory.Delete(_directory, recursive: true);
    }

    private static void ResetContext(EditorHistoryContext context)
    {
        EditorHistoryService.Current.Remove(context);
        EditorDirtyStateService.Current.Remove(context);
    }

    private string Asset => _assetId.ToString();

    private Guid Catalog(string fileName, bool createFile = true)
    {
        Guid id = Guid.NewGuid();
        AssetCatalog.AddInternal(id, "remote-entity-assets-test-" + id.ToString("N"), fileName);
        _catalogEntries.Add(id);
        if (createFile)
        {
            WriteProjectFile(fileName);
        }

        return id;
    }

    private void WriteProjectFile(string fileName)
    {
        string path = Path.Combine(_project, fileName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}");
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

    private static void AssertInvalidParams(RemoteResponse response)
    {
        Assert.NotNull(response.Error);
        Assert.Equal(RemoteErrorCodes.InvalidParams, response.Error!.Code);
    }

    /// <summary>Arguments a command would accept on the fixture's entity, with <paramref name="asset"/> as the reference.</summary>
    private static object Args(string method, string asset, Guid componentId = default) => method switch
    {
        "entity_asset.set_property" => new { asset, component_id = componentId.ToString(), property = "Speed", value = 9.0 },
        "entity_asset.add_component" => new { asset, type = nameof(RemoteEditComponent) },
        "entity_asset.remove_component" => (object)new { asset, component_id = componentId.ToString() },
        _ => new { asset },
    };

    private void AssertHostNotCalled()
    {
        Assert.Empty(_host.OpenedAssets);
        Assert.Equal(0, _host.EntityAssetCalls);
        Assert.Empty(_host.SavedAssets);
        Assert.Empty(_host.SelectedEntityAssetComponents);
    }

    private bool HasHistory() => EditorHistoryService.Current.TryGet(_panel.Context, out _);

    private Guid AddComponent(string type, Guid? parentId = null)
    {
        object parameters = parentId is { } parent
            ? new { asset = Asset, type, parent_component_id = parent.ToString() }
            : new { asset = Asset, type };
        return Ok(Call("entity_asset.add_component", parameters)).GetProperty("component_id").GetGuid();
    }

    private bool IsModified() => Ok(Call("entity_asset.get", new { asset = Asset })).GetProperty("modified").GetBoolean();

    private JsonElement Undo() => Ok(Call("history.undo", new { context = Asset }));

    private JsonElement Redo() => Ok(Call("history.redo", new { context = Asset }));

    /// <summary>A root scene component and a list component set on the entity directly, without history.</summary>
    private (RemoteEditSceneComponent Root, RemoteEditComponent Listed) SeedEntity()
    {
        var root = new RemoteEditSceneComponent();
        _entity.RootComponent = root;
        var listed = new RemoteEditComponent { Speed = 2.5f, Label = "hi" };
        _entity.AddComponent(listed);
        return (root, listed);
    }

    [Fact]
    public void Get_ReadsTheEntityOfTheAsset_ThroughItsPanel()
    {
        (RemoteEditSceneComponent root, RemoteEditComponent listed) = SeedEntity();

        JsonElement result = Ok(Call("entity_asset.get", new { asset = Asset }));
        Ok(Call("entity_asset.get", new { asset = BoxFile }));

        Assert.Equal(_assetId, result.GetProperty("asset_id").GetGuid());
        Assert.Equal(BoxFile, result.GetProperty("file").GetString());
        Assert.False(result.GetProperty("modified").GetBoolean());
        JsonElement entity = result.GetProperty("entity");
        Assert.Equal(_entity.Id, entity.GetProperty("id").GetGuid());
        Assert.Equal("Box", entity.GetProperty("name").GetString());
        JsonElement[] components = entity.GetProperty("components").EnumerateArray().ToArray();
        Assert.Equal(2, components.Length);
        Assert.Equal(root.Id, components[0].GetProperty("id").GetGuid());
        Assert.Equal("root", components[0].GetProperty("placement").GetString());
        Assert.Equal(listed.Id, components[1].GetProperty("id").GetGuid());
        Assert.Equal("list", components[1].GetProperty("placement").GetString());
        Assert.Equal(nameof(RemoteEditComponent), components[1].GetProperty("type").GetString());

        var byName = components[1].GetProperty("properties").EnumerateArray().ToDictionary(p => p.GetProperty("name").GetString()!);
        Assert.Equal(
            EditorPropertyRules.GetEditableProperties(listed).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal),
            byName.Keys.OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(2.5f, byName["Speed"].GetProperty("value").GetSingle());
        Assert.Equal("hi", byName["Label"].GetProperty("value").GetString());
        Assert.False(byName.ContainsKey("Name"));
        Assert.False(byName.ContainsKey("Owner"));
        Assert.False(byName.ContainsKey("ReadOnlyValue"));
        Assert.False(byName.ContainsKey("Hidden"));
        Assert.False(byName.ContainsKey("Unsupported"));

        string expected = Path.Combine(_project, "Entities", "Box.entity");
        Assert.Equal(2, _host.OpenedAssets.Count);
        Assert.All(_host.OpenedAssets, opened =>
        {
            Assert.Equal(expected, opened.FullPath);
            Assert.Equal("entity", opened.Type);
        });
        Assert.False(HasHistory());
    }

    [Fact]
    public void SetProperty_ChangesTheValue_InOneEntryOfThePanelHistory()
    {
        RemoteEditComponent listed = SeedEntity().Listed;
        Assert.False(IsModified());

        JsonElement result = Ok(Call("entity_asset.set_property", new { asset = Asset, component_id = listed.Id.ToString(), property = "Speed", value = 4.5 }));

        Assert.True(result.GetProperty("changed").GetBoolean());
        Assert.Equal(4.5f, listed.Speed);
        Assert.True(IsModified());
        Assert.True(EditorHistoryService.Current.TryGet(_panel.Context, out EditorHistoryStack stack));
        Assert.Equal($"Edit {nameof(RemoteEditComponent)} Speed", stack.UndoDescription);
        stack.Undo();
        Assert.False(stack.CanUndo);
        Assert.Equal(2.5f, listed.Speed);
        Assert.Equal(0, _host.RefreshCount);
    }

    [Fact]
    public void SetProperty_TheSameValue_ChangesNothing_AndAddsNoEntry()
    {
        RemoteEditComponent listed = SeedEntity().Listed;

        JsonElement result = Ok(Call("entity_asset.set_property", new { asset = Asset, component_id = listed.Id.ToString(), property = "Speed", value = 2.5 }));

        Assert.False(result.GetProperty("changed").GetBoolean());
        Assert.False(HasHistory());
        Assert.False(IsModified());
    }

    /// <summary>Plan P5, security review S7: the filters of property.set (editable properties, checked values).</summary>
    [Theory]
    [InlineData("Name", "\"x\"")]
    [InlineData("Owner", "\"x\"")]
    [InlineData("ReadOnlyValue", "1")]
    [InlineData("Hidden", "1")]
    [InlineData("Unsupported", "1")]
    [InlineData("Speed", "\"fast\"")]
    [InlineData("Speed", "\"NaN\"")]
    [InlineData("Speed", "1e39")]
    [InlineData("Mode", "1")]
    [InlineData("Mode", "\"Medium\"")]
    [InlineData("Label", "12")]
    public void SetProperty_RejectsWhatTheInspectorWouldNotDo(string property, string valueJson)
    {
        RemoteEditComponent listed = SeedEntity().Listed;

        RemoteResponse response = Call("entity_asset.set_property", new { asset = Asset, component_id = listed.Id.ToString(), property, value = JsonDocument.Parse(valueJson).RootElement });

        AssertInvalidParams(response);
        Assert.Equal(2.5f, listed.Speed);
        Assert.Equal(EditorRemoteEditCommandsTests.RemoteEditMode.Slow, listed.Mode);
        Assert.Equal("hi", listed.Label);
        Assert.False(HasHistory());
    }

    [Fact]
    public void SetProperty_RejectsTooLongText()
    {
        RemoteEditComponent listed = SeedEntity().Listed;

        RemoteResponse response = Call("entity_asset.set_property", new { asset = Asset, component_id = listed.Id.ToString(), property = "Label", value = new string('x', EditorRemoteValidation.MaxPropertyTextLength + 1) });

        AssertInvalidParams(response);
        Assert.Equal("hi", listed.Label);
        Assert.False(HasHistory());
    }

    /// <summary>Plan P2: a component that is not one of the asset entity's components is not found.</summary>
    [Fact]
    public void SetProperty_UnknownComponent_OrOfAnotherEntity_IsNotFound()
    {
        SeedEntity();
        var other = new Entity { Name = "Other" };
        var foreign = new RemoteEditComponent { Speed = 1f };
        other.AddComponent(foreign);

        AssertRefused(Call("entity_asset.set_property", new { asset = Asset, component_id = Guid.NewGuid().ToString(), property = "Speed", value = 3 }), RemoteErrorReasons.NotFound);
        AssertRefused(Call("entity_asset.set_property", new { asset = Asset, component_id = foreign.Id.ToString(), property = "Speed", value = 3 }), RemoteErrorReasons.NotFound);

        Assert.Equal(1f, foreign.Speed);
        Assert.False(HasHistory());
    }

    [Fact]
    public void AddComponent_FollowsThePlacementRules_OfComponentAdd()
    {
        JsonElement root = Ok(Call("entity_asset.add_component", new { asset = Asset, type = nameof(RemoteEditSceneComponent) }));
        Assert.Equal("root", root.GetProperty("placement").GetString());
        Guid rootId = root.GetProperty("component_id").GetGuid();
        Assert.Equal(rootId, _entity.RootComponent!.Id);

        AssertInvalidParams(Call("entity_asset.add_component", new { asset = Asset, type = nameof(RemoteEditSceneComponent) }));

        JsonElement child = Ok(Call("entity_asset.add_component", new { asset = Asset, type = nameof(RemoteEditSceneComponent), parent_component_id = rootId.ToString() }));
        Assert.Equal("child", child.GetProperty("placement").GetString());
        Assert.Equal(child.GetProperty("component_id").GetGuid(), Assert.Single(_entity.RootComponent.Children).Id);

        JsonElement listed = Ok(Call("entity_asset.add_component", new { asset = Asset, type = nameof(RemoteEditComponent) }));
        Assert.Equal("list", listed.GetProperty("placement").GetString());
        Guid listedId = listed.GetProperty("component_id").GetGuid();
        Assert.Equal(listedId, Assert.Single(_entity.Components).Id);

        // A non-scene component cannot have a parent; a parent must be a SceneComponent of this entity.
        AssertInvalidParams(Call("entity_asset.add_component", new { asset = Asset, type = nameof(RemoteEditComponent), parent_component_id = rootId.ToString() }));
        AssertInvalidParams(Call("entity_asset.add_component", new { asset = Asset, type = nameof(RemoteEditSceneComponent), parent_component_id = listedId.ToString() }));
        AssertRefused(Call("entity_asset.add_component", new { asset = Asset, type = nameof(RemoteEditSceneComponent), parent_component_id = Guid.NewGuid().ToString() }), RemoteErrorReasons.NotFound);

        Assert.Single(_entity.Components);
        Assert.Single(_entity.RootComponent.Children);
        Assert.Equal(0, _host.RefreshCount);
    }

    /// <summary>Security review S6: only the allowlist of component.types is instantiated.</summary>
    [Theory]
    [InlineData("Process")]
    [InlineData("System.Diagnostics.Process")]
    [InlineData(nameof(RemoteEditHiddenComponent))]
    [InlineData("EntityComponent")]
    public void AddComponent_RejectsTypesOutsideTheAllowlist(string type)
    {
        RemoteEditHiddenComponent.Constructed = 0;

        AssertInvalidParams(Call("entity_asset.add_component", new { asset = Asset, type }));

        Assert.Equal(0, RemoteEditHiddenComponent.Constructed);
        Assert.Empty(_entity.Components);
        Assert.Null(_entity.RootComponent);
        Assert.False(HasHistory());
    }

    /// <summary>Plan P5: the added component gets the inspector's unique name and the panel selects it.</summary>
    [Fact]
    public void AddComponent_GivesAUniqueName_AndSelectsTheComponentInThePanel()
    {
        Guid first = AddComponent(nameof(RemoteEditComponent));
        Guid second = AddComponent(nameof(RemoteEditComponent));

        EntityComponent firstComponent = EditorRemoteWorldModel.FindComponent(_entity, first)!;
        EntityComponent secondComponent = EditorRemoteWorldModel.FindComponent(_entity, second)!;
        Assert.Equal(nameof(RemoteEditComponent), firstComponent.Name);
        Assert.Equal(nameof(RemoteEditComponent) + " (2)", secondComponent.Name);
        Assert.Equal(
            new[] { (_assetId, firstComponent), (_assetId, secondComponent) },
            _host.SelectedEntityAssetComponents);
    }

    [Fact]
    public void RemoveComponent_RemovesIt_RefusesAParentWithChildren_AndAComponentOfAnotherEntity()
    {
        Guid rootId = AddComponent(nameof(RemoteEditSceneComponent));
        Guid childId = AddComponent(nameof(RemoteEditSceneComponent), rootId);
        Guid listedId = AddComponent(nameof(RemoteEditComponent));
        var other = new Entity { Name = "Other" };
        var foreign = new RemoteEditComponent();
        other.AddComponent(foreign);

        AssertInvalidParams(Call("entity_asset.remove_component", new { asset = Asset, component_id = rootId.ToString() }));
        AssertRefused(Call("entity_asset.remove_component", new { asset = Asset, component_id = foreign.Id.ToString() }), RemoteErrorReasons.NotFound);
        Assert.Contains(foreign, other.Components);

        Assert.True(Ok(Call("entity_asset.remove_component", new { asset = Asset, component_id = listedId.ToString() })).GetProperty("removed").GetBoolean());
        Assert.Empty(_entity.Components);
        Ok(Call("entity_asset.remove_component", new { asset = Asset, component_id = childId.ToString() }));
        Assert.Empty(_entity.RootComponent!.Children);

        // The panel selection follows: the removed child's parent is selected.
        Assert.Equal((_assetId, (EntityComponent)_entity.RootComponent), _host.SelectedEntityAssetComponents[^1]);
        Assert.Equal(0, _host.RefreshCount);
    }

    /// <summary>Each kind of edit is one entry of the panel's history, reached by history.undo / history.redo with the asset id.</summary>
    [Fact]
    public void EachEdit_IsUndoneAndRedone_WithTheAssetIdAsContext()
    {
        Guid rootId = AddComponent(nameof(RemoteEditSceneComponent));
        Assert.Equal("Add Component", Undo().GetProperty("description").GetString());
        Assert.Null(_entity.RootComponent);
        Assert.False(IsModified());
        Assert.True(Redo().GetProperty("done").GetBoolean());
        Assert.Equal(rootId, _entity.RootComponent!.Id);

        Guid listedId = AddComponent(nameof(RemoteEditComponent));
        var listed = (RemoteEditComponent)EditorRemoteWorldModel.FindComponent(_entity, listedId)!;
        Ok(Call("entity_asset.set_property", new { asset = Asset, component_id = listedId.ToString(), property = "Speed", value = 3 }));
        Assert.Equal($"Edit {nameof(RemoteEditComponent)} Speed", Undo().GetProperty("description").GetString());
        Assert.Equal(0f, listed.Speed);
        Redo();
        Assert.Equal(3f, listed.Speed);

        Ok(Call("entity_asset.remove_component", new { asset = Asset, component_id = listedId.ToString() }));
        Assert.Equal("Remove Component", Undo().GetProperty("description").GetString());
        Assert.Contains(listed, _entity.Components);
        Redo();
        Assert.DoesNotContain(listed, _entity.Components);

        Assert.Equal(6, _host.RefreshedContexts.Count);
        Assert.All(_host.RefreshedContexts, context => Assert.Equal(_panel.Context, context));
    }

    /// <summary>Plan P14, security review S3-7: every command of the family, get included, is refused in play mode, before any host call.</summary>
    [Theory]
    [InlineData("entity_asset.get")]
    [InlineData("entity_asset.set_property")]
    [InlineData("entity_asset.add_component")]
    [InlineData("entity_asset.remove_component")]
    [InlineData("entity_asset.save")]
    public void EveryCommand_IsRefusedInPlayMode_BeforeAnyHostCall(string method)
    {
        RemoteEditComponent listed = SeedEntity().Listed;
        _host.IsPlaySessionActive = true;

        AssertRefused(Call(method, Args(method, Asset, listed.Id)), RemoteErrorReasons.PlayModeActive);

        AssertHostNotCalled();
        Assert.Equal(2.5f, listed.Speed);
        Assert.False(HasHistory());
    }

    public static IEnumerable<object[]> MethodsAndBadReferences()
    {
        foreach (string method in Methods)
        {
            foreach (string kind in new[] { "outside-path", "other-type", "not-catalogued", "catalog-outside", "catalog-type-mismatch" })
            {
                yield return new object[] { method, kind };
            }
        }
    }

    /// <summary>Security review S2-6 and S3-5: asset references the family refuses before any host call.</summary>
    [Theory]
    [MemberData(nameof(MethodsAndBadReferences))]
    public void EveryCommand_RefusesABadReference_BeforeAnyHostCall(string method, string kind)
    {
        RemoteEditComponent listed = SeedEntity().Listed;
        string reference;
        bool notFound = false;
        switch (kind)
        {
            case "outside-path":
                File.WriteAllText(Path.Combine(_directory, "outside.entity"), "{}");
                reference = "../outside.entity";
                break;
            case "other-type":
                reference = Catalog("Materials/Stone.material").ToString();
                break;
            case "not-catalogued":
                reference = Guid.NewGuid().ToString();
                notFound = true;
                break;
            case "catalog-outside":
                reference = Catalog("../outside.entity", createFile: false).ToString();
                break;
            default:
                Guid id = Guid.NewGuid();
                AssetCatalog.AddInternal(new AssetInfo(id) { Name = "remote-entity-assets-test-" + id.ToString("N"), FileName = "Entities/Mismatch.entity", AssetType = "material" });
                _catalogEntries.Add(id);
                WriteProjectFile("Entities/Mismatch.entity");
                reference = id.ToString();
                break;
        }

        RemoteResponse response = Call(method, Args(method, reference, listed.Id));

        if (notFound)
        {
            AssertRefused(response, RemoteErrorReasons.NotFound);
        }
        else
        {
            AssertInvalidParams(response);
        }

        AssertHostNotCalled();
        Assert.Equal(2.5f, listed.Speed);
        Assert.False(HasHistory());
    }

    /// <summary>ADR-0093 A3, security review S3-5: a reference under a junction, by path or by catalog entry, is refused before any host call.</summary>
    [Fact]
    public void EveryCommand_RefusesAReferenceUnderALink_BeforeAnyHostCall()
    {
        string outside = Path.Combine(_directory, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "Linked.entity"), "{}");
        string link = Path.Combine(_project, "linked");
        if (!TryCreateJunction(link, outside))
        {
            return;
        }

        Guid underLink = Guid.NewGuid();
        AssetCatalog.AddInternal(underLink, "remote-entity-assets-test-" + underLink.ToString("N"), "linked/Linked.entity");
        _catalogEntries.Add(underLink);
        RemoteEditComponent listed = SeedEntity().Listed;

        foreach (string method in Methods)
        {
            AssertInvalidParams(Call(method, Args(method, "linked/Linked.entity", listed.Id)));
            AssertInvalidParams(Call(method, Args(method, underLink.ToString(), listed.Id)));
        }

        AssertHostNotCalled();
        Assert.Equal(2.5f, listed.Speed);
        Assert.False(HasHistory());
    }

    /// <summary>Junctions need no privilege; when one cannot be made here, the case is reported as not run, as in EditorRemotePathLinkTests.</summary>
    private bool TryCreateJunction(string link, string target)
    {
        var start = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using (Process process = Process.Start(start)!)
        {
            string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                _log.WriteLine($"inconclusive: junction creation failed ({process.ExitCode}): {output}");
                return false;
            }
        }

        _links.Add(link);
        Assert.True(Directory.Exists(link), $"The junction '{link}' was not created.");
        Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0, $"'{link}' is not a reparse point.");
        _log.WriteLine($"junction created: '{link}' -> '{target}'");
        return true;
    }

    public static IEnumerable<object[]> MethodsAndForeignLoadedFiles()
    {
        foreach (string method in Methods)
        {
            yield return new object[] { method, "Entities/Other.entity" };
            yield return new object[] { method, "Entities/../Entities/Box.entity" };
            yield return new object[] { method, null };
        }
    }

    /// <summary>
    /// Plan P13, security review S3-1: when the open panel does not hold the resolved file (another file carrying the id,
    /// a path with '..', no path), every command is refused before any read or edit and the save is never reached.
    /// </summary>
    [Theory]
    [MemberData(nameof(MethodsAndForeignLoadedFiles))]
    public void EveryCommand_IsRefused_WhenThePanelHoldsAnotherFile(string method, string loadedFile)
    {
        WriteProjectFile("Entities/Other.entity");
        (RemoteEditSceneComponent root, RemoteEditComponent listed) = SeedEntity();
        _panel.LoadedFile = loadedFile;

        AssertRefused(Call(method, Args(method, Asset, listed.Id)), RemoteErrorReasons.Failed);

        Assert.Same(root, _entity.RootComponent);
        Assert.Empty(root.Children);
        Assert.Same(listed, Assert.Single(_entity.Components));
        Assert.Equal(2.5f, listed.Speed);
        Assert.False(HasHistory());
        Assert.Empty(_host.SavedAssets);
        Assert.Empty(_host.SelectedEntityAssetComponents);
    }

    [Theory]
    [InlineData("entity_asset.get")]
    [InlineData("entity_asset.set_property")]
    [InlineData("entity_asset.add_component")]
    [InlineData("entity_asset.remove_component")]
    [InlineData("entity_asset.save")]
    public void EveryCommand_IsRefused_WhenThePanelDoesNotOpenOrHasNoEntity(string method)
    {
        RemoteEditComponent listed = SeedEntity().Listed;
        _host.EntityAssetOpenFails = true;
        AssertRefused(Call(method, Args(method, Asset, listed.Id)), RemoteErrorReasons.Failed);

        _host.EntityAssetOpenFails = false;
        _host.EntityAssets[_assetId] = new RemoteTestHost.EntityAssetPanel { Entity = null, LoadedFile = BoxFile, PanelId = _panel.PanelId, Context = _panel.Context };
        AssertRefused(Call(method, Args(method, Asset, listed.Id)), RemoteErrorReasons.Failed);

        Assert.Equal(2.5f, listed.Speed);
        Assert.False(HasHistory());
        Assert.Empty(_host.SavedAssets);
    }

    /// <summary>Plan P7: written says whether the panel had unsaved changes, read before the save.</summary>
    [Fact]
    public void Save_WritesOnlyAModifiedAsset_AndReportsAFailedSave()
    {
        RemoteEditComponent listed = SeedEntity().Listed;

        JsonElement unmodified = Ok(Call("entity_asset.save", new { asset = Asset }));
        Assert.True(unmodified.GetProperty("saved").GetBoolean());
        Assert.False(unmodified.GetProperty("written").GetBoolean());

        Ok(Call("entity_asset.set_property", new { asset = Asset, component_id = listed.Id.ToString(), property = "Speed", value = 7 }));
        Assert.True(Ok(Call("entity_asset.save", new { asset = BoxFile })).GetProperty("written").GetBoolean());
        Assert.False(IsModified());
        Assert.False(Ok(Call("entity_asset.save", new { asset = Asset })).GetProperty("written").GetBoolean());
        Assert.Equal(new[] { _assetId, _assetId, _assetId }, _host.SavedAssets);

        Ok(Call("entity_asset.set_property", new { asset = Asset, component_id = listed.Id.ToString(), property = "Speed", value = 8 }));
        _host.SaveAssetError = "The disk is full.";
        RemoteResponse failed = Call("entity_asset.save", new { asset = Asset });
        AssertRefused(failed, RemoteErrorReasons.Failed);
        Assert.Contains("The disk is full.", failed.Error!.Message);
        Assert.True(IsModified());
    }

    /// <summary>Plan T1.3: when the host gives no history context for the open panel, every command is refused before any read or edit.</summary>
    [Theory]
    [InlineData("entity_asset.get")]
    [InlineData("entity_asset.set_property")]
    [InlineData("entity_asset.add_component")]
    [InlineData("entity_asset.remove_component")]
    [InlineData("entity_asset.save")]
    public void EveryCommand_IsRefused_WhenTheHostGivesNoHistoryContext(string method)
    {
        (RemoteEditSceneComponent root, RemoteEditComponent listed) = SeedEntity();
        _host.AssetsWithoutHistoryContext.Add(_assetId);

        AssertRefused(Call(method, Args(method, Asset, listed.Id)), RemoteErrorReasons.Failed);

        Assert.Same(root, _entity.RootComponent);
        Assert.Empty(root.Children);
        Assert.Same(listed, Assert.Single(_entity.Components));
        Assert.Equal(2.5f, listed.Speed);
        Assert.False(HasHistory());
        Assert.Empty(_host.SavedAssets);
        Assert.Empty(_host.SelectedEntityAssetComponents);
    }

    /// <summary>
    /// Plan T1.3 (same class as security review S3-1): when the context the host gives for the asset id is of another
    /// kind (a material panel found first under that id), every command is refused and no entity edit enters either
    /// history.
    /// </summary>
    [Theory]
    [InlineData("entity_asset.get")]
    [InlineData("entity_asset.set_property")]
    [InlineData("entity_asset.add_component")]
    [InlineData("entity_asset.remove_component")]
    [InlineData("entity_asset.save")]
    public void EveryCommand_IsRefused_WhenTheHistoryContextIsOfAnotherKind(string method)
    {
        (RemoteEditSceneComponent root, RemoteEditComponent listed) = SeedEntity();
        var materialContext = new EditorHistoryContext(
            EditorHistoryContextKind.Material,
            CasaEngine.Editor.Workspaces.EditorPanelIds.MaterialAssetDocumentPrefix + _assetId.ToString("N"));
        ResetContext(materialContext);
        _host.EntityAssets[_assetId] = new RemoteTestHost.EntityAssetPanel
        {
            Entity = _entity,
            LoadedFile = BoxFile,
            PanelId = _panel.PanelId,
            Context = materialContext,
        };

        try
        {
            RemoteResponse response = Call(method, Args(method, Asset, listed.Id));

            AssertRefused(response, RemoteErrorReasons.Failed);
            Assert.Contains("Another kind of panel", response.Error!.Message);
            Assert.Same(root, _entity.RootComponent);
            Assert.Empty(root.Children);
            Assert.Same(listed, Assert.Single(_entity.Components));
            Assert.Equal(2.5f, listed.Speed);
            Assert.False(HasHistory());
            Assert.False(EditorHistoryService.Current.TryGet(materialContext, out _));
            Assert.Empty(_host.SavedAssets);
            Assert.Empty(_host.SelectedEntityAssetComponents);
        }
        finally
        {
            ResetContext(materialContext);
            ResetContext(_panel.Context);
        }
    }
}
