using CasaEngine.EditorServices;
using CasaEngine.EditorServices.StaticModels;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Materials.Runtime;
using CasaEngine.Framework.Rendering.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

/// <summary>The material edits of the <c>.staticModel</c> document (plan P8): load, change a material, serialize, reload.</summary>
public sealed class StaticModelAssetDocumentTests : IDisposable
{
    private static readonly Guid FirstMaterial = Guid.Parse("afcdcca9-8833-4f9c-9b13-3972177ba7b1");
    private static readonly Guid SecondMaterial = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid TextureId = Guid.Parse("a4f6260f-45cc-58b4-9d69-de59ac8e43ef");
    private static readonly Guid NotAMaterial = Guid.Parse("99999999-0000-0000-0000-000000000001");

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "casa-static-model-document-" + Guid.NewGuid().ToString("N"));

    public StaticModelAssetDocumentTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A temp folder left behind is harmless.
        }
    }

    [Fact]
    public void RoundTrip_ChangesTheMaterialOfAMeshAndKeepsTheGeometryAndEveryOtherField()
    {
        StaticModelAssetDocument document = Load(out JObject before);

        Assert.Equal(FirstMaterial, document.GetMaterialAssetId(0));
        Assert.True(document.TrySetMaterial(0, StaticModelAssetDocument.WholeMesh, SecondMaterial, out string error), error);

        StaticModel reloaded = SerializeAndReload(document);
        StaticModel original = LoadModel(before);

        Assert.Equal(SecondMaterial, reloaded.Meshes[0].MaterialAssetId);
        Assert.Equal(FirstMaterial, original.Meshes[0].MaterialAssetId);

        // The geometry is identical, to the bit.
        Assert.Equal(original.Meshes.Count, reloaded.Meshes.Count);
        for (int meshIndex = 0; meshIndex < original.Meshes.Count; meshIndex++)
        {
            AssertSameGeometry(original.Meshes[meshIndex], reloaded.Meshes[meshIndex]);
        }

        // The other mesh and the sub-meshes are untouched.
        Assert.Equal(Guid.Empty, reloaded.Meshes[1].MaterialAssetId);
        Assert.Equal(original.Meshes[0].SubMeshes.Count, reloaded.Meshes[0].SubMeshes.Count);
        for (int i = 0; i < original.Meshes[0].SubMeshes.Count; i++)
        {
            SubMesh expected = original.Meshes[0].SubMeshes[i];
            SubMesh actual = reloaded.Meshes[0].SubMeshes[i];
            Assert.Equal(expected.SlotName, actual.SlotName);
            Assert.Equal(expected.MaterialSlotIndex, actual.MaterialSlotIndex);
            Assert.Equal(expected.IndexStart, actual.IndexStart);
            Assert.Equal(expected.PrimitiveCount, actual.PrimitiveCount);
            Assert.Equal(expected.VertexOffset, actual.VertexOffset);
            Assert.Equal(expected.MaterialAssetId, actual.MaterialAssetId);
        }

        // The model and the node tree.
        Assert.Equal(original.Id, reloaded.Id);
        Assert.Equal(original.Name, reloaded.Name);
        Assert.True(JToken.DeepEquals(Serialize(original)["root_node"], Serialize(reloaded)["root_node"]));
    }

    [Fact]
    public void RoundTrip_ChangesOnlyTheNamedField_InTheSerializedJson()
    {
        StaticModelAssetDocument document = Load(out JObject before);
        Assert.True(document.TrySetMaterial(0, StaticModelAssetDocument.WholeMesh, SecondMaterial, out string error), error);

        JObject after = Serialize(document.Model);

        Assert.Equal(SecondMaterial.ToString(), after["meshes"][0]["material_asset_id"].Value<string>());
        after["meshes"][0]["material_asset_id"] = before["meshes"][0]["material_asset_id"];
        Assert.True(JToken.DeepEquals(before, after));
    }

    [Fact]
    public void TrySetMaterial_OnASubMesh_ChangesThatSubMeshOnly()
    {
        StaticModelAssetDocument document = Load(out _);

        Assert.True(document.TrySetMaterial(0, 1, SecondMaterial, out string error), error);

        StaticModel reloaded = SerializeAndReload(document);
        Assert.Equal(FirstMaterial, reloaded.Meshes[0].MaterialAssetId);
        Assert.Equal(Guid.Empty, reloaded.Meshes[0].SubMeshes[0].MaterialAssetId);
        Assert.Equal(SecondMaterial, reloaded.Meshes[0].SubMeshes[1].MaterialAssetId);
    }

    [Fact]
    public void TrySetMaterial_WithAnEmptyId_RemovesTheMaterialFromTheFile()
    {
        StaticModelAssetDocument document = Load(out _);

        Assert.True(document.TrySetMaterial(0, StaticModelAssetDocument.WholeMesh, Guid.Empty, out string error), error);

        JObject after = Serialize(document.Model);
        Assert.Null(after["meshes"][0]["material_asset_id"]);
        Assert.Equal(Guid.Empty, document.GetMaterialAssetId(0));
    }

    [Fact]
    public void TrySetMaterial_DropsTheResolvedRuntimeMaterial_OfWhatChanged()
    {
        StaticModelAssetDocument document = Load(out _);
        StaticModelMesh mesh = document.Model.Meshes[0];
        mesh.Material = new LitDiffuseMaterial();
        mesh.SubMeshes[0].Material = new LitDiffuseMaterial();
        mesh.SubMeshes[1].Material = new LitDiffuseMaterial();

        Assert.True(document.TrySetMaterial(0, StaticModelAssetDocument.WholeMesh, SecondMaterial, out _));

        Assert.Null(mesh.Material);
        Assert.Null(mesh.SubMeshes[0].Material);  // inherits the mesh material
        Assert.NotNull(mesh.SubMeshes[1].Material); // names its own
    }

    [Theory]
    [InlineData(-1, -1)]
    [InlineData(2, -1)]
    [InlineData(5, -1)]
    [InlineData(0, 2)]
    [InlineData(1, 0)]   // mesh 1 has no sub-mesh
    [InlineData(0, -2)]
    public void TrySetMaterial_OutOfRange_IsRefused_AndChangesNothing(int meshIndex, int subMeshIndex)
    {
        StaticModelAssetDocument document = Load(out JObject before);

        Assert.False(document.TrySetMaterial(meshIndex, subMeshIndex, SecondMaterial, out string error));

        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.True(JToken.DeepEquals(before, Serialize(document.Model)));
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData(" 1 ", 1)]
    public void TryParseMeshIndex_AcceptsAWholeNumberInRange(string text, int expected)
    {
        StaticModelAssetDocument document = Load(out _);

        Assert.True(document.TryParseMeshIndex(text, out int meshIndex, out string error), error);
        Assert.Equal(expected, meshIndex);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("5")]
    [InlineData("-1")]
    [InlineData("first")]
    [InlineData("0.5")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParseMeshIndex_RefusesWhatIsNotAMeshIndex(string text)
    {
        StaticModelAssetDocument document = Load(out _);

        Assert.False(document.TryParseMeshIndex(text, out _, out string error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void TryParseMaterialId_AcceptsACataloguedMaterial()
    {
        Assert.True(StaticModelAssetDocument.TryParseMaterialId(" " + FirstMaterial + " ", Lookup, out Guid id, out string error), error);
        Assert.Equal(FirstMaterial, id);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("0f0f0f0f-0000-0000-0000-000000000000")]  // well formed, not in the catalog
    [InlineData("99999999-0000-0000-0000-000000000001")]  // in the catalog, but a texture
    public void TryParseMaterialId_RefusesAMalformedUnknownOrNonMaterialId(string text)
    {
        Assert.False(StaticModelAssetDocument.TryParseMaterialId(text, Lookup, out Guid id, out string error));

        Assert.Equal(Guid.Empty, id);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void TryReadMeshMaterialId_ReadsTheSavedFile()
    {
        StaticModelAssetDocument document = Load(out _);
        Assert.True(document.TrySetMaterial(1, StaticModelAssetDocument.WholeMesh, SecondMaterial, out _));
        string path = Path.Combine(_folder, "saved.staticModel");
        File.WriteAllText(path, Serialize(document.Model).ToString());

        Assert.True(StaticModelAssetDocument.TryReadMeshMaterialId(path, 1, out Guid readBack));
        Assert.Equal(SecondMaterial, readBack);
        Assert.True(StaticModelAssetDocument.TryReadMeshMaterialId(path, 0, out readBack));
        Assert.Equal(FirstMaterial, readBack);
        Assert.False(StaticModelAssetDocument.TryReadMeshMaterialId(path, 2, out _));
    }

    [Fact]
    public void TryLoad_OfAFileThatIsNotJson_FailsWithAMessage()
    {
        string path = Path.Combine(_folder, "broken.staticModel");
        File.WriteAllText(path, "not json");

        Assert.False(StaticModelAssetDocument.TryLoad(path, out StaticModelAssetDocument document, out string error));

        Assert.Null(document);
        Assert.Contains("broken.staticModel", error);
    }

    private static AssetInfo Lookup(Guid id)
    {
        if (id == FirstMaterial || id == SecondMaterial)
        {
            return new AssetInfo(id) { Name = "Material " + id.ToString("D")[..4], AssetType = "material" };
        }

        if (id == NotAMaterial)
        {
            return new AssetInfo(id) { Name = "Some texture", AssetType = "texture" };
        }

        return null;
    }

    /// <summary>
    /// Two meshes with awkward floats: the first has a material, two sub-meshes (the second names its own material) and a
    /// node tree with a child; the second has no material.
    /// </summary>
    private StaticModelAssetDocument Load(out JObject fileContent)
    {
        var first = new StaticModelMesh
        {
            Name = "Body",
            SlotName = "Paint",
            MaterialSlotIndex = 0,
            MaterialIndex = 3,
            TextureAssetId = TextureId,
            MaterialAssetId = FirstMaterial,
        };
        first.SetData(
            [
                new VertexPositionNormalTexture(new Vector3(0.1f, -1f / 3f, 123456.789f), new Vector3(0.57735026f, 0.57735026f, 0.57735026f), new Vector2(0.3f, 0.7f)),
                new VertexPositionNormalTexture(new Vector3(-0.0000123f, 1e-7f, float.MaxValue / 3f), Vector3.UnitY, new Vector2(1f / 7f, 0f)),
                new VertexPositionNormalTexture(new Vector3(1e10f, -2.5e-3f, 0f), Vector3.UnitZ, new Vector2(1f, 1f)),
            ],
            [0u, 1u, 2u, 2u, 1u, 0u]);
        first.SubMeshes.Add(new SubMesh { SlotName = "Paint", MaterialSlotIndex = 0, IndexStart = 0, PrimitiveCount = 1, VertexOffset = 0 });
        first.SubMeshes.Add(new SubMesh { SlotName = "Glass", MaterialSlotIndex = 1, IndexStart = 3, PrimitiveCount = 1, VertexOffset = 0, MaterialAssetId = FirstMaterial });

        var second = new StaticModelMesh { Name = "Wheel", SlotName = "Rubber", MaterialSlotIndex = 2, MaterialIndex = -1 };
        second.SetData(
            [
                new VertexPositionNormalTexture(new Vector3(2f, 3f, 4f), Vector3.UnitX, new Vector2(0.25f, 0.75f)),
                new VertexPositionNormalTexture(new Vector3(5f, 6f, 7f), Vector3.UnitX, new Vector2(0.125f, 0.0625f)),
                new VertexPositionNormalTexture(new Vector3(8f, 9f, 10f), Vector3.UnitX, new Vector2(0.6f, 0.9f)),
            ],
            [0u, 1u, 2u]);

        var model = new StaticModel
        {
            Name = "Car",
            RootNode = new StaticModelNode
            {
                Name = "Root",
                MeshIndex = 0,
                Position = new Vector3(1f, 2f, 3f),
                Rotation = Quaternion.Normalize(new Quaternion(0.1f, 0.2f, 0.3f, 0.9f)),
                Scale = new Vector3(1.5f),
            },
        };
        model.RootNode.Children.Add(new StaticModelNode { Name = "WheelNode", MeshIndex = 1, Position = new Vector3(0.5f, 0f, -0.5f), Rotation = Quaternion.Identity, Scale = Vector3.One });
        model.Meshes.Add(first);
        model.Meshes.Add(second);

        fileContent = Serialize(model);
        string path = Path.Combine(_folder, "car.staticModel");
        File.WriteAllText(path, fileContent.ToString());

        Assert.True(StaticModelAssetDocument.TryLoad(path, out StaticModelAssetDocument document, out string error), error);

        // What the file holds, as the serializer writes it for the loaded model.
        fileContent = Serialize(document.Model);
        return document;
    }

    private static JObject Serialize(StaticModel model)
    {
        Assert.True(EditorAssetJsonSerializer.TrySerialize(model, out JObject root));
        return root;
    }

    private static StaticModel LoadModel(JObject content)
    {
        var model = new StaticModel();
        model.Load(content);
        return model;
    }

    private static StaticModel SerializeAndReload(StaticModelAssetDocument document)
    {
        // Through text, as the writer service does, so that the float formatting is part of the round trip.
        return LoadModel(JObject.Parse(Serialize(document.Model).ToString()));
    }

    private static void AssertSameGeometry(StaticModelMesh expected, StaticModelMesh actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.SlotName, actual.SlotName);
        Assert.Equal(expected.MaterialSlotIndex, actual.MaterialSlotIndex);
        Assert.Equal(expected.PrimitiveType, actual.PrimitiveType);
        Assert.Equal(expected.MaterialIndex, actual.MaterialIndex);
        Assert.Equal(expected.TextureAssetId, actual.TextureAssetId);

        IReadOnlyList<VertexPositionNormalTexture> expectedVertices = expected.GetVertices();
        IReadOnlyList<VertexPositionNormalTexture> actualVertices = actual.GetVertices();
        Assert.Equal(expectedVertices.Count, actualVertices.Count);
        for (int i = 0; i < expectedVertices.Count; i++)
        {
            // Bit for bit: a float that came back one ulp away would fail here.
            Assert.Equal(BitConverter.SingleToInt32Bits(expectedVertices[i].Position.X), BitConverter.SingleToInt32Bits(actualVertices[i].Position.X));
            Assert.Equal(BitConverter.SingleToInt32Bits(expectedVertices[i].Position.Y), BitConverter.SingleToInt32Bits(actualVertices[i].Position.Y));
            Assert.Equal(BitConverter.SingleToInt32Bits(expectedVertices[i].Position.Z), BitConverter.SingleToInt32Bits(actualVertices[i].Position.Z));
            Assert.Equal(BitConverter.SingleToInt32Bits(expectedVertices[i].Normal.X), BitConverter.SingleToInt32Bits(actualVertices[i].Normal.X));
            Assert.Equal(BitConverter.SingleToInt32Bits(expectedVertices[i].Normal.Y), BitConverter.SingleToInt32Bits(actualVertices[i].Normal.Y));
            Assert.Equal(BitConverter.SingleToInt32Bits(expectedVertices[i].Normal.Z), BitConverter.SingleToInt32Bits(actualVertices[i].Normal.Z));
            Assert.Equal(BitConverter.SingleToInt32Bits(expectedVertices[i].TextureCoordinate.X), BitConverter.SingleToInt32Bits(actualVertices[i].TextureCoordinate.X));
            Assert.Equal(BitConverter.SingleToInt32Bits(expectedVertices[i].TextureCoordinate.Y), BitConverter.SingleToInt32Bits(actualVertices[i].TextureCoordinate.Y));
        }

        Assert.Equal(expected.GetIndices(), actual.GetIndices());
        Assert.Equal(expected.Min, actual.Min);
        Assert.Equal(expected.Max, actual.Max);
    }
}
