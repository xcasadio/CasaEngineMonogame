using System.Text.Json;
using CasaEngine.Editor.Remote;
using CasaEngine.Editor.Remote.Protocol;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Materials.Definitions;
using CasaEngine.Framework.Materials.Runtime;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Editor.Remote;

/// <summary>ADR-0093, task T2.2: material creation, value bounds (security review S2-2) and the material commands.</summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class EditorRemoteMaterialCommandsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "casa-remote-material-" + Guid.NewGuid().ToString("N"));
    private readonly RemoteTestHost _host;
    private readonly EditorRemoteControl _control;
    private readonly List<Guid> _catalogEntries = new();

    public EditorRemoteMaterialCommandsTests()
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

    private Guid Catalog(string fileName, string name = null)
    {
        Guid id = Guid.NewGuid();
        AssetCatalog.AddInternal(id, name ?? "remote-material-test-" + id.ToString("N"), fileName);
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
    public void Create_WritesTheMaterial_AndCataloguesIt()
    {
        string name = "RemoteMaterial" + Guid.NewGuid().ToString("N")[..8];

        JsonElement result = Ok(Call("material.create", new { name, folder = "Materials" }));

        Guid id = result.GetProperty("asset_id").GetGuid();
        _catalogEntries.Add(id);
        string file = Path.Combine(_host.ProjectDirectory!, "Materials", name + ".material");
        Assert.True(File.Exists(file));
        Assert.Contains("\"definition_id\": \"lit-diffuse\"", File.ReadAllText(file));
        Assert.Equal(name, AssetCatalog.Get(id)!.Name);
        Assert.Equal("Materials/" + name + ".material", result.GetProperty("file").GetString());
    }

    /// <summary>Security review S2-4: no catalogued name, no folder outside the project, no existing file, no unknown definition.</summary>
    [Fact]
    public void Create_RefusesCollisionsAndOutsideFolders()
    {
        string taken = "RemoteTaken" + Guid.NewGuid().ToString("N")[..8];
        Catalog("Other/whatever.sprite", taken);
        File.WriteAllText(Path.Combine(_host.ProjectDirectory!, "Materials", "Existing.material"), "{}");

        Assert.Equal(RemoteErrorReasons.InvalidArgument, Call("material.create", new { name = taken.ToLowerInvariant(), folder = "Materials" }).Error!.Data!.Reason);
        Assert.Equal(RemoteErrorReasons.InvalidArgument, Call("material.create", new { name = "Existing", folder = "Materials" }).Error!.Data!.Reason);
        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("material.create", new { name = "X", folder = "../outside" }).Error!.Code);
        Assert.Equal(RemoteErrorReasons.NotFound, Call("material.create", new { name = "X", folder = "Missing" }).Error!.Data!.Reason);
        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("material.create", new { name = "a/b", folder = "Materials" }).Error!.Code);
        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("material.create", new { name = "Fresh", folder = "Materials", definition = "no-such-definition" }).Error!.Code);
        Assert.False(File.Exists(Path.Combine(_host.ProjectDirectory!, "Materials", "Fresh.material")));
    }

    /// <summary>
    /// Plan P7, security review S4-1: a folder with an empty segment, or a segment ending with a dot or a space, is refused
    /// before any file-system access, even when Windows would resolve it to a real folder; nothing is written or catalogued.
    /// </summary>
    [Theory]
    [InlineData("Materials/../Materials")]
    [InlineData("Materials/./Sub")]
    [InlineData("Materials//Sub")]
    [InlineData("Materials.")]
    [InlineData("Materials ")]
    [InlineData("Materials/...")]
    [InlineData("Materials/ /Sub")]
    public void Create_RefusesAFolderWithAnEmptySegmentOrASegmentEndingWithADotOrASpace(string folder)
    {
        Directory.CreateDirectory(Path.Combine(_host.ProjectDirectory!, "Materials", "Sub"));
        string name = "RemoteMaterial" + Guid.NewGuid().ToString("N")[..8];

        RemoteResponse response = Call("material.create", new { name, folder });

        Assert.Equal(RemoteErrorCodes.InvalidParams, response.Error!.Code);
        Assert.Contains("is not a valid folder", response.Error.Message);
        Assert.Equal(0, _host.CatalogSaves);
        Assert.Empty(Directory.GetFiles(_host.ProjectDirectory!, "*", SearchOption.AllDirectories));
        Assert.DoesNotContain(AssetCatalog.AssetInfos, info => string.Equals(info.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Plan P7: the root ('', '.', '/', '//'), a plain folder and a folder with a dot inside its name are accepted as before.</summary>
    [Theory]
    [InlineData("", "")]
    [InlineData(".", "")]
    [InlineData("/", "")]
    [InlineData("//", "")]
    [InlineData("Materials", "Materials/")]
    [InlineData("Materials/v1.2", "Materials/v1.2/")]
    [InlineData("Materials/.hidden", "Materials/.hidden/")]
    public void Create_AcceptsTheRootAPlainFolderAndAFolderWithADotInside(string folder, string expectedPrefix)
    {
        Directory.CreateDirectory(Path.Combine(_host.ProjectDirectory!, "Materials", "v1.2"));
        Directory.CreateDirectory(Path.Combine(_host.ProjectDirectory!, "Materials", ".hidden"));
        string name = "RemoteMaterial" + Guid.NewGuid().ToString("N")[..8];

        JsonElement result = Ok(Call("material.create", new { name, folder }));

        _catalogEntries.Add(result.GetProperty("asset_id").GetGuid());
        string expectedFile = expectedPrefix + name + ".material";
        Assert.Equal(expectedFile, result.GetProperty("file").GetString());
        Assert.True(File.Exists(Path.Combine(_host.ProjectDirectory!, expectedFile.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Equal(1, _host.CatalogSaves);
    }

    [Fact]
    public void Validate_BoundsEveryValueType()
    {
        var number = new MaterialPropertyDefinition("cutoff", "Cutoff", MaterialPropertyType.Float, MaterialPropertyGroup.Surface, minValue: 0f, maxValue: 1f);
        var integer = new MaterialPropertyDefinition("count", "Count", MaterialPropertyType.Integer, MaterialPropertyGroup.Surface, minValue: 1f, maxValue: 8f);
        var texture = new MaterialPropertyDefinition("albedo", "Albedo", MaterialPropertyType.Texture, MaterialPropertyGroup.Textures);
        var option = new MaterialPropertyDefinition("mode", "Mode", MaterialPropertyType.Enum, MaterialPropertyGroup.Surface,
            options: new[] { new MaterialPropertyOption("opaque", "Opaque"), new MaterialPropertyOption("cutout", "Cutout") });
        var vector = new MaterialPropertyDefinition("tint", "Tint", MaterialPropertyType.Vector3, MaterialPropertyGroup.Surface);
        var text = new MaterialPropertyDefinition("label", "Label", MaterialPropertyType.String, MaterialPropertyGroup.Surface);
        Guid textureId = Catalog("Textures/rock.texture");
        Guid spriteId = Catalog("Sprites/hero.sprite");

        Assert.Null(EditorRemoteMaterialCommands.Validate(number, MaterialValue.FromFloat(0.5f)));
        Assert.NotNull(EditorRemoteMaterialCommands.Validate(number, MaterialValue.FromFloat(float.NaN)));
        Assert.NotNull(EditorRemoteMaterialCommands.Validate(number, MaterialValue.FromFloat(float.PositiveInfinity)));
        Assert.NotNull(EditorRemoteMaterialCommands.Validate(number, MaterialValue.FromFloat(1.5f)));
        Assert.NotNull(EditorRemoteMaterialCommands.Validate(integer, MaterialValue.FromInteger(9)));
        Assert.Null(EditorRemoteMaterialCommands.Validate(texture, MaterialValue.FromTextureId(textureId)));
        Assert.Null(EditorRemoteMaterialCommands.Validate(texture, MaterialValue.FromTextureId(Guid.Empty)));
        Assert.NotNull(EditorRemoteMaterialCommands.Validate(texture, MaterialValue.FromTextureId(spriteId)));
        Assert.NotNull(EditorRemoteMaterialCommands.Validate(texture, MaterialValue.FromTextureId(Guid.NewGuid())));
        Assert.Null(EditorRemoteMaterialCommands.Validate(option, MaterialValue.FromEnum("cutout")));
        Assert.NotNull(EditorRemoteMaterialCommands.Validate(option, MaterialValue.FromEnum("1")));
        Assert.NotNull(EditorRemoteMaterialCommands.Validate(vector, MaterialValue.FromVector3(new Vector3(1, float.NaN, 0))));
        Assert.NotNull(EditorRemoteMaterialCommands.Validate(text, MaterialValue.FromString(new string('x', EditorRemoteValidation.MaxPropertyTextLength + 1))));
    }

    [Fact]
    public void ToRawValue_TurnsJsonIntoTheParserText()
    {
        Assert.Equal("0.25", EditorRemoteMaterialCommands.ToRawValue(JsonDocument.Parse("0.25").RootElement));
        Assert.Equal("true", EditorRemoteMaterialCommands.ToRawValue(JsonDocument.Parse("true").RootElement));
        Assert.Equal("1,2,3", EditorRemoteMaterialCommands.ToRawValue(JsonDocument.Parse("[1,2,3]").RootElement));
        Assert.Equal("cutout", EditorRemoteMaterialCommands.ToRawValue(JsonDocument.Parse("\"cutout\"").RootElement));
        Assert.Throws<EditorRemoteArgumentException>(() => EditorRemoteMaterialCommands.ToRawValue(JsonDocument.Parse("{}").RootElement));
        Assert.Throws<EditorRemoteArgumentException>(() => EditorRemoteMaterialCommands.ToRawValue(JsonDocument.Parse("[1,\"a\"]").RootElement));
        Assert.Throws<EditorRemoteArgumentException>(() => EditorRemoteMaterialCommands.ToRawValue(JsonDocument.Parse("[1,2,3,4,5]").RootElement));
    }

    [Fact]
    public void SetProperty_OpensTheMaterial_AndPassesTheBoundedValidator()
    {
        Guid id = Catalog("Materials/Stone.material");

        Ok(Call("material.set_property", new { asset = id.ToString(), property = "alpha_cutoff", value = 0.5 }));

        var set = Assert.Single(_host.MaterialSets);
        Assert.Equal(("alpha_cutoff", "0.5"), (set.Property, set.RawValue));
        Assert.Single(_host.OpenedAssets);
        Assert.Equal(nameof(EditorRemoteMaterialCommands.Validate), _host.LastMaterialValidator!.Method.Name);
        Assert.Equal(typeof(EditorRemoteMaterialCommands), _host.LastMaterialValidator.Method.DeclaringType);

        _host.MaterialSetError = "'alpha_cutoff' must be within [0, 1].";
        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("material.set_property", new { asset = id.ToString(), property = "alpha_cutoff", value = 5 }).Error!.Code);
    }

    [Fact]
    public void SaveAndPlayMode()
    {
        Guid id = Catalog("Materials/Stone.material");

        Assert.True(Ok(Call("material.save", new { asset = id.ToString() })).GetProperty("saved").GetBoolean());
        Assert.Equal(id, Assert.Single(_host.SavedAssets));

        _host.IsPlaySessionActive = true;
        Assert.Equal(RemoteErrorReasons.PlayModeActive, Call("material.set_property", new { asset = id.ToString(), property = "x", value = 1 }).Error!.Data!.Reason);
        Assert.Equal(RemoteErrorReasons.PlayModeActive, Call("material.create", new { name = "Y", folder = "Materials" }).Error!.Data!.Reason);
    }

    [Fact]
    public void MaterialOverride_RefusesWhatIsNotAStaticModelSlot()
    {
        var world = new World { Name = "Level" };
        var entity = new Entity { Name = "E" };
        CasaEngine.EditorServices.EditorWorldEditingService.AttachEntity(world, entity, null);
        var model = new StaticModelComponent();
        entity.AddComponent(model);
        _host.CurrentWorld = world;
        Guid material = Catalog("Materials/Stone.material");

        RemoteResponse noSlot = Call("component.material_override", new { entity_id = entity.Id.ToString(), component_id = model.Id.ToString(), slot = 0, material = material.ToString() });
        RemoteResponse wrongComponent = Call("component.material_override", new { entity_id = entity.Id.ToString(), component_id = Guid.NewGuid().ToString(), slot = 0 });

        Assert.Equal(RemoteErrorCodes.InvalidParams, noSlot.Error!.Code);
        Assert.Contains("0 material slot", noSlot.Error.Message);
        Assert.Equal(RemoteErrorCodes.InvalidParams, wrongComponent.Error!.Code);
    }
}
