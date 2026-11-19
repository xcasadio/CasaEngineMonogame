using System.Diagnostics;
using System.Text.Json;
using CasaEngine.Editor.Controls;
using CasaEngine.Editor.Remote;
using CasaEngine.Editor.Remote.Protocol;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Xunit.Abstractions;

namespace CasaEngine.Tests.Editor.Remote;

/// <summary>
/// ADR-0129, task T1.3: <c>entity_asset.create</c> goes through the checks of <c>EditorRemoteAssetCreation.Prepare</c>
/// before the host is called (security review S3-5), and <see cref="EditorRemoteEntityAssets.Create"/> writes the file
/// before the catalog entry and undoes both on any failure after the reservation (plan P6, security review S3-4).
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class EditorRemoteEntityAssetCreateTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "casa-remote-entity-create-" + Guid.NewGuid().ToString("N"));
    private readonly string _project;
    private readonly RemoteTestHost _host;
    private readonly EditorRemoteControl _control;
    private readonly List<Guid> _catalogEntries = new();
    private readonly List<string> _links = new();
    private readonly ITestOutputHelper _log;

    public EditorRemoteEntityAssetCreateTests(ITestOutputHelper log)
    {
        _log = log;
        _project = Path.GetFullPath(Path.Combine(_directory, "project"));
        Directory.CreateDirectory(Path.Combine(_project, "Entities", "Cars"));
        _host = new RemoteTestHost
        {
            ProjectFilePath = Path.Combine(_project, "game.casaproject"),
            ProjectDirectory = _project,
        };
        _control = new EditorRemoteControl(_host, Path.Combine(_directory, "discovery"));
    }

    public void Dispose()
    {
        _control.Dispose();
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

    /// <summary>A name no catalog entry has, valid for the world name rule.</summary>
    private static string NewName(string prefix = "NewEntity") => prefix + Guid.NewGuid().ToString("N")[..8];

    private Guid Catalog(string name, string fileName)
    {
        Guid id = Guid.NewGuid();
        AssetCatalog.AddInternal(id, name, fileName);
        _catalogEntries.Add(id);
        return id;
    }

    private RemoteResponse Call(string method, object parameters)
    {
        var call = new EditorRemoteCall(new RemoteRequest
        {
            Id = 1,
            Method = method,
            Params = RemoteMessageCodec.ToElement(parameters),
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

    /// <summary>Every file of the project folder (no link is ever made inside it when this is used).</summary>
    private string[] ProjectFiles()
        => Directory.GetFiles(_project, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal).ToArray();

    /// <summary>No catalog entry carries this name (any case) or this file (either separator).</summary>
    private static void AssertNoCatalogEntry(string name, string relativeFile)
    {
        Assert.DoesNotContain(AssetCatalog.AssetInfos, info => string.Equals(info.Name, name, StringComparison.OrdinalIgnoreCase));
        Assert.Null(AssetCatalog.GetByFileName(relativeFile));
        Assert.Null(AssetCatalog.GetByFileName(relativeFile.Replace('\\', '/')));
        Assert.Null(AssetCatalog.GetByFileName(relativeFile.Replace('/', '\\')));
    }

    private RemoteNewAssetFile Prepare(string name)
        => EditorRemoteAssetCreation.Prepare(_project, "Entities", name, EditorRemoteAssetTypes.Entity);

    // --- entity_asset.create, through the dispatcher -----------------------------------------------------------------

    [Theory]
    [InlineData("Entities", "Entities/")]
    [InlineData("Entities/Cars", "Entities/Cars/")]
    [InlineData("Entities\\Cars", "Entities/Cars/")]
    [InlineData("", "")]
    public void Create_SendsTheNewFileToTheHost_AndAnswersItsIdAndFile(string folder, string expectedPrefix)
    {
        string name = NewName();

        JsonElement result = Ok(Call("entity_asset.create", new { name, folder }));

        (string relativeFile, Guid hostId) = Assert.Single(_host.CreatedEntityAssets);
        string expectedFile = expectedPrefix + name + ".entity";
        Assert.Equal(expectedFile.Replace('/', Path.DirectorySeparatorChar), relativeFile);
        Assert.Equal(hostId, result.GetProperty("asset_id").GetGuid());
        Assert.Equal(expectedFile, result.GetProperty("file").GetString());

        // The command itself writes nothing: the host does.
        Assert.Empty(ProjectFiles());
    }

    /// <summary>The world name rule (no path, no '.', no device name, 64 characters at most): refused before the host.</summary>
    [Theory]
    [InlineData("../x")]
    [InlineData("..")]
    [InlineData("bad/name")]
    [InlineData("bad\\name")]
    [InlineData("Box.v2")]
    [InlineData("Box.entity")]
    [InlineData("CON")]
    [InlineData("nul")]
    [InlineData("COM1")]
    [InlineData("")]
    [InlineData("C:x")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Create_RefusesAnInvalidName_BeforeTheHost(string name)
    {
        AssertInvalidParams(Call("entity_asset.create", new { name, folder = "Entities" }));

        Assert.Empty(_host.CreatedEntityAssets);
        Assert.Empty(ProjectFiles());
    }

    [Fact]
    public void Create_TheLongestValidName_Is64Characters()
    {
        string name = NewName().PadRight(64, 'x');

        Ok(Call("entity_asset.create", new { name, folder = "Entities" }));

        Assert.Single(_host.CreatedEntityAssets);
    }

    [Fact]
    public void Create_RefusesAFolderThatDoesNotExist_BeforeTheHost()
    {
        AssertRefused(Call("entity_asset.create", new { name = NewName(), folder = "Missing" }), RemoteErrorReasons.NotFound);

        Assert.Empty(_host.CreatedEntityAssets);
        Assert.False(Directory.Exists(Path.Combine(_project, "Missing")));
    }

    /// <summary>Security review S3-5: a folder outside the project, or given as an absolute path (even one inside it), is refused.</summary>
    [Theory]
    [InlineData("..")]
    [InlineData("../outside")]
    [InlineData("Entities/../..")]
    [InlineData("absolute-outside")]
    [InlineData("absolute-inside")]
    public void Create_RefusesAFolderOutsideTheProject_BeforeTheHost(string kind)
    {
        string outside = Path.Combine(_directory, "outside");
        Directory.CreateDirectory(outside);
        string folder = kind switch
        {
            "absolute-outside" => outside,
            "absolute-inside" => Path.Combine(_project, "Entities"),
            _ => kind,
        };
        string name = NewName();

        AssertInvalidParams(Call("entity_asset.create", new { name, folder }));

        Assert.Empty(_host.CreatedEntityAssets);
        Assert.Empty(Directory.GetFiles(outside));
        Assert.Empty(Directory.GetFiles(_directory));
        Assert.Empty(ProjectFiles());
    }

    /// <summary>ADR-0093 A3, security review S3-5: a folder that is a junction is refused, naming the junction; nothing is written in its target.</summary>
    [Fact]
    public void Create_RefusesAFolderThatIsALink_BeforeTheHost()
    {
        string outside = Path.Combine(_directory, "outside");
        Directory.CreateDirectory(outside);
        string link = Path.Combine(_project, "linked");
        if (!TryCreateJunction(link, outside))
        {
            return;
        }

        string name = NewName();
        RemoteResponse direct = Call("entity_asset.create", new { name, folder = "linked" });
        RemoteResponse below = Call("entity_asset.create", new { name, folder = "linked/." });

        AssertInvalidParams(direct);
        Assert.Contains(link, direct.Error!.Message);
        AssertInvalidParams(below);
        Assert.Empty(_host.CreatedEntityAssets);
        Assert.Empty(Directory.GetFileSystemEntries(outside));
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

    /// <summary>A name already in the catalog, of any type and in any case, is refused.</summary>
    [Fact]
    public void Create_RefusesANameAlreadyCatalogued_AnyTypeAnyCase()
    {
        string taken = NewName("Taken");
        Catalog(taken, "Materials/" + taken + ".material");

        AssertRefused(Call("entity_asset.create", new { name = taken.ToLowerInvariant(), folder = "Entities" }), RemoteErrorReasons.InvalidArgument);
        AssertRefused(Call("entity_asset.create", new { name = taken.ToUpperInvariant(), folder = "" }), RemoteErrorReasons.InvalidArgument);

        Assert.Empty(_host.CreatedEntityAssets);
        Assert.Empty(ProjectFiles());
    }

    /// <summary>A catalog entry for the file (under another name, either separator) is refused, even without the file on disk.</summary>
    [Theory]
    [InlineData('/')]
    [InlineData('\\')]
    public void Create_RefusesAFileAlreadyCatalogued(char separator)
    {
        string name = NewName();
        Catalog(NewName("Other"), "Entities" + separator + name + ".entity");

        AssertRefused(Call("entity_asset.create", new { name, folder = "Entities" }), RemoteErrorReasons.InvalidArgument);

        Assert.Empty(_host.CreatedEntityAssets);
        Assert.Empty(ProjectFiles());
    }

    [Fact]
    public void Create_RefusesAFileThatAlreadyExists_AndLeavesItUntouched()
    {
        string name = NewName();
        string existing = Path.Combine(_project, "Entities", name + ".entity");
        byte[] bytes = { 1, 2, 3, 4 };
        File.WriteAllBytes(existing, bytes);

        AssertRefused(Call("entity_asset.create", new { name, folder = "Entities" }), RemoteErrorReasons.InvalidArgument);

        Assert.Empty(_host.CreatedEntityAssets);
        Assert.Equal(bytes, File.ReadAllBytes(existing));
    }

    [Fact]
    public void Create_IsRefusedInPlayMode_BeforeAnyCheckOrHostCall()
    {
        _host.IsPlaySessionActive = true;

        AssertRefused(Call("entity_asset.create", new { name = NewName(), folder = "Entities" }), RemoteErrorReasons.PlayModeActive);
        AssertRefused(Call("entity_asset.create", new { name = "../x", folder = "Missing" }), RemoteErrorReasons.PlayModeActive);

        Assert.Empty(_host.CreatedEntityAssets);
        Assert.Empty(ProjectFiles());
    }

    [Fact]
    public void Create_ReportsAHostFailure_AsFailed_WithTheHostMessage()
    {
        _host.CreateEntityAssetError = "'Entities/X.entity' could not be created: The disk is full.";

        RemoteResponse response = Call("entity_asset.create", new { name = NewName(), folder = "Entities" });

        AssertRefused(response, RemoteErrorReasons.Failed);
        Assert.Contains("The disk is full.", response.Error!.Message);
        Assert.Single(_host.CreatedEntityAssets);
    }

    [Fact]
    public void Create_IsRegisteredFirstInTheFamily_WithItsFlagsAndSchema()
    {
        var names = _control.Registry.Commands.Select(c => c.Name).Where(n => n.StartsWith("entity_asset.", StringComparison.Ordinal)).ToList();
        Assert.Equal("entity_asset.create", names[0]);
        Assert.Equal(names.IndexOf("entity_asset.get") - 1, names.IndexOf("entity_asset.create"));

        EditorRemoteCommand command = _control.Registry.Commands.Single(c => c.Name == "entity_asset.create");
        Assert.False(command.ReadOnly);
        Assert.False(command.Destructive);
        Assert.False(command.InputSchema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(new[] { "name", "folder" }, command.InputSchema.GetProperty("required").EnumerateArray().Select(e => e.GetString()));
        AssertInvalidParams(Call("entity_asset.create", new { name = NewName() }));
        AssertInvalidParams(Call("entity_asset.create", new { folder = "Entities" }));
        Assert.Empty(_host.CreatedEntityAssets);
    }

    // --- EditorRemoteEntityAssets.Create, without a host ---------------------------------------------------------------

    public static IEnumerable<object[]> WriteFailures()
    {
        yield return new object[] { "invalid-operation" };
        yield return new object[] { "io" };
    }

    /// <summary>Plan P6, security review S3-4: a write that throws (after writing part of the file) leaves no file and no entry, and the catalog is not saved.</summary>
    [Theory]
    [MemberData(nameof(WriteFailures))]
    public void CreateFunction_AWriteThatThrows_LeavesNoFileAndNoEntry(string failure)
    {
        string name = NewName();
        RemoteNewAssetFile file = Prepare(name);
        int catalogSaves = 0;
        string writtenRelative = null;
        Entity writtenEntity = null;

        bool created = EditorRemoteEntityAssets.Create(
            file,
            (relative, entity) =>
            {
                writtenRelative = relative;
                writtenEntity = entity;
                File.WriteAllText(file.FullPath, "{\"partial\":");
                throw failure == "io" ? new IOException("The disk is full.") : new InvalidOperationException("Not supported by the writer.");
            },
            () => catalogSaves++,
            out Guid assetId,
            out string error);

        Assert.False(created);
        Assert.Equal(Guid.Empty, assetId);
        Assert.Contains(file.RelativeFile, error);
        Assert.Contains(failure == "io" ? "The disk is full." : "Not supported by the writer.", error);
        Assert.Equal(file.RelativeFile, writtenRelative);
        Assert.Equal(name, writtenEntity!.Name);
        Assert.Equal(file.RelativeFile, writtenEntity.FileName);
        Assert.False(File.Exists(file.FullPath));
        AssertNoCatalogEntry(name, file.RelativeFile);
        Assert.Null(AssetCatalog.Get(writtenEntity.Id));
        Assert.Equal(0, catalogSaves);
    }

    /// <summary>Plan P6, security review S3-4: a catalog save that throws after the entry was added leaves no file and no entry.</summary>
    [Fact]
    public void CreateFunction_ACatalogSaveThatThrows_LeavesNoFileAndNoEntry()
    {
        string name = NewName();
        RemoteNewAssetFile file = Prepare(name);
        bool entryWasAdded = false;
        bool fileWasWritten = false;

        bool created = EditorRemoteEntityAssets.Create(
            file,
            (relative, entity) => File.WriteAllText(file.FullPath, "{}"),
            () =>
            {
                entryWasAdded = AssetCatalog.GetByFileName(file.RelativeFile)?.Name == name;
                fileWasWritten = File.ReadAllText(file.FullPath) == "{}";
                throw new UnauthorizedAccessException("AssetInfos.json is read-only.");
            },
            out Guid assetId,
            out string error);

        Assert.False(created);
        Assert.Equal(Guid.Empty, assetId);
        Assert.Contains(file.RelativeFile, error);
        Assert.Contains("AssetInfos.json is read-only.", error);
        Assert.True(entryWasAdded, "The entry is added after the write, before the catalog save.");
        Assert.True(fileWasWritten, "The file is written before the catalog entry is added.");
        Assert.False(File.Exists(file.FullPath));
        AssertNoCatalogEntry(name, file.RelativeFile);
    }

    /// <summary>
    /// Plan T1.3: Add can throw from a subscriber after inserting the entry, so the removal is armed before Add is
    /// called. The subscriber throws for this file only, so no other test sees it.
    /// </summary>
    [Fact]
    public void CreateFunction_AnAddThatThrowsFromASubscriber_RemovesTheEntry()
    {
        string name = NewName();
        RemoteNewAssetFile file = Prepare(name);
        int catalogSaves = 0;
        EventHandler<AssetInfo> throwing = (_, info) =>
        {
            if (string.Equals(info.FileName, file.RelativeFile, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("A subscriber failed.");
            }
        };

        EditorAssetCatalogService.AssetAdded += throwing;
        bool created;
        string error;
        try
        {
            created = EditorRemoteEntityAssets.Create(file, (relative, entity) => File.WriteAllText(file.FullPath, "{}"), () => catalogSaves++, out _, out error);
        }
        finally
        {
            EditorAssetCatalogService.AssetAdded -= throwing;
        }

        Assert.False(created);
        Assert.Contains("A subscriber failed.", error);
        Assert.False(File.Exists(file.FullPath));
        AssertNoCatalogEntry(name, file.RelativeFile);
        Assert.Equal(0, catalogSaves);
    }

    /// <summary>A file that appeared between Prepare and the reservation: the existing refusal, the file untouched, nothing called.</summary>
    [Fact]
    public void CreateFunction_AFileThatAppearedAfterPrepare_IsRefused_Untouched()
    {
        string name = NewName();
        RemoteNewAssetFile file = Prepare(name);
        byte[] bytes = { 0x7B, 0x7D, 0x0A, 0xFF };
        File.WriteAllBytes(file.FullPath, bytes);
        int writes = 0;
        int catalogSaves = 0;

        var refusal = Assert.Throws<EditorRemoteRefusedException>(() => EditorRemoteEntityAssets.Create(
            file,
            (relative, entity) => writes++,
            () => catalogSaves++,
            out _,
            out _));

        Assert.Equal(RemoteErrorReasons.InvalidArgument, refusal.Reason);
        Assert.Equal(bytes, File.ReadAllBytes(file.FullPath));
        Assert.Equal(0, writes);
        Assert.Equal(0, catalogSaves);
        AssertNoCatalogEntry(name, file.RelativeFile);
    }

    /// <summary>
    /// Plan T1.3, the success path with the editor's writer: the catalog entry, the file, and the entity the entity asset
    /// panel loads from it (same id, same name).
    /// </summary>
    [Fact]
    public void CreateFunction_WithTheEditorWriter_CreatesAnEmptyEntityAsset_ThePanelCanLoad()
    {
        string name = NewName();
        RemoteNewAssetFile file = Prepare(name);
        string previousProjectPath = EngineEnvironment.ProjectPath;
        int catalogSaves = 0;
        Guid assetId = Guid.Empty;
        try
        {
            EngineEnvironment.ProjectPath = _project;

            bool created = EditorRemoteEntityAssets.Create(
                file,
                (relative, entity) => EditorAssetWriterService.SaveAsset(relative, entity, EditorAssetSaveSource.EntityAssetEditorPanel),
                () => catalogSaves++,
                out assetId,
                out string error);

            Assert.True(created, error);
            Assert.Null(error);
            Assert.NotEqual(Guid.Empty, assetId);
            Assert.Equal(1, catalogSaves);

            AssetInfo info = AssetCatalog.Get(assetId);
            Assert.NotNull(info);
            Assert.Equal(name, info.Name);
            Assert.Equal(file.RelativeFile, info.FileName);
            Assert.Equal(EditorRemoteAssetTypes.Entity, AssetInfo.InferAssetType(info.FileName));
            Assert.Equal(EditorRemoteAssetTypes.Entity, info.AssetType);

            Assert.True(File.Exists(file.FullPath));
            Assert.True(EntityAssetEditorPanel.TryLoadAsset(file.FullPath, out Entity loaded));
            Assert.Equal(assetId, loaded.AssetId);
            Assert.Equal(assetId, loaded.Id);
            Assert.Equal(name, loaded.Name);
            Assert.Null(loaded.RootComponent);
            Assert.Empty(loaded.Components);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
            if (assetId != Guid.Empty)
            {
                EditorAssetCatalogService.TryRemoveEntry(assetId, out _);
            }

            AssertNoCatalogEntry(name, file.RelativeFile);
        }
    }
}
