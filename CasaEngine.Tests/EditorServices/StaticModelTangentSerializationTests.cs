using CasaEngine.Core.Logging;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Rendering.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

/// <summary>
/// A <c>.staticModel</c> keeps the tangents of its meshes in an optional <c>tangents</c> array (ADR-0123).
/// One test captures the engine's static log, so this class runs in <see cref="ProjectEnvironmentCollection"/>, like the
/// other classes that do.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class StaticModelTangentSerializationTests
{
    [Fact]
    public void MeshWithTangents_RoundTrips()
    {
        var mesh = new StaticModelMesh { Name = "Road", SlotName = "Asphalt", MaterialSlotIndex = 0, MaterialIndex = -1 };
        mesh.SetData(
            [
                new VertexPositionNormalTextureTangent(new Vector3(0.1f, -1f / 3f, 123456.789f), Vector3.UnitY, new Vector2(0.3f, 0.7f), new Vector4(1f, 0f, 0f, 1f)),
                new VertexPositionNormalTextureTangent(new Vector3(-0.0000123f, 1e-7f, 5f), Vector3.UnitY, new Vector2(1f / 7f, 0f), new Vector4(0.6f, 0f, 0.8f, -1f)),
                new VertexPositionNormalTextureTangent(new Vector3(1e10f, -2.5e-3f, 0f), Vector3.UnitZ, new Vector2(1f, 1f), new Vector4(0.57735026f, 0.57735026f, 0.57735026f, 1f)),
            ],
            [0u, 1u, 2u, 2u, 1u, 0u]);
        mesh.SubMeshes.Add(new SubMesh { SlotName = "Asphalt", MaterialSlotIndex = 0, IndexStart = 0, PrimitiveCount = 2, VertexOffset = 0 });

        JObject content = Serialize(CreateModel(mesh));
        StaticModelMesh reloaded = Reload(content).Meshes[0];

        Assert.True(reloaded.HasTangents);
        Assert.Equal(mesh.GetVertices(), reloaded.GetVertices());
        Assert.Equal(mesh.GetIndices(), reloaded.GetIndices());
        Assert.Equal(mesh.GetTangentVertices().Count, reloaded.GetTangentVertices().Count);
        for (int i = 0; i < mesh.GetTangentVertices().Count; i++)
        {
            VertexPositionNormalTextureTangent expected = mesh.GetTangentVertices()[i];
            VertexPositionNormalTextureTangent actual = reloaded.GetTangentVertices()[i];
            Assert.Equal(expected.Position, actual.Position);
            Assert.Equal(expected.Normal, actual.Normal);
            Assert.Equal(expected.TextureCoordinate, actual.TextureCoordinate);
            Assert.Equal(expected.Tangent, actual.Tangent);
        }

        SubMesh subMesh = Assert.Single(reloaded.SubMeshes);
        Assert.Equal(0, subMesh.IndexStart);
        Assert.Equal(2, subMesh.PrimitiveCount);

        //  One entry per vertex, each with the four components.
        var tangents = Assert.IsType<JArray>(content["meshes"]![0]!["tangents"]);
        Assert.Equal(3, tangents.Count);
        Assert.Equal(-1f, tangents[1]!["w"]!.Value<float>());
    }

    [Fact]
    public void MeshWithoutTangents_WritesNoTangents_AndReloadsWithoutThem()
    {
        var mesh = new StaticModelMesh { Name = "Box", MaterialIndex = -1 };
        mesh.SetData(
            [
                new VertexPositionNormalTexture(new Vector3(2f, 3f, 4f), Vector3.UnitX, new Vector2(0.25f, 0.75f)),
                new VertexPositionNormalTexture(new Vector3(5f, 6f, 7f), Vector3.UnitX, new Vector2(0.125f, 0.0625f)),
                new VertexPositionNormalTexture(new Vector3(8f, 9f, 10f), Vector3.UnitX, new Vector2(0.6f, 0.9f)),
            ],
            [0u, 1u, 2u]);

        JObject content = Serialize(CreateModel(mesh));
        StaticModelMesh reloaded = Reload(content).Meshes[0];

        Assert.Null(content["meshes"]![0]!["tangents"]);
        Assert.False(reloaded.HasTangents);
        Assert.Empty(reloaded.GetTangentVertices());
        Assert.Equal(mesh.GetVertices(), reloaded.GetVertices());
        Assert.Equal(mesh.GetIndices(), reloaded.GetIndices());
    }

    [Fact]
    public void TangentsOfAnotherCount_AreReported_AndTheMeshLoadsWithoutThem()
    {
        var mesh = new StaticModelMesh { Name = "Rail", MaterialIndex = -1 };
        mesh.SetData(
            [
                new VertexPositionNormalTextureTangent(Vector3.Zero, Vector3.UnitY, Vector2.Zero, new Vector4(1f, 0f, 0f, 1f)),
                new VertexPositionNormalTextureTangent(Vector3.UnitX, Vector3.UnitY, Vector2.UnitX, new Vector4(1f, 0f, 0f, 1f)),
                new VertexPositionNormalTextureTangent(Vector3.UnitZ, Vector3.UnitY, Vector2.UnitY, new Vector4(1f, 0f, 0f, 1f)),
            ],
            [0u, 1u, 2u]);
        JObject content = Serialize(CreateModel(mesh));
        ((JArray)content["meshes"]![0]!["tangents"]!).RemoveAt(2);

        StaticModel reloaded = null!;
        List<string> errors = CaptureErrors(() => reloaded = Reload(content));

        StaticModelMesh reloadedMesh = reloaded.Meshes[0];
        Assert.False(reloadedMesh.HasTangents);
        Assert.Equal(mesh.GetVertices(), reloadedMesh.GetVertices());
        Assert.Equal(mesh.GetIndices(), reloadedMesh.GetIndices());
        //  The log is shared by the test run: look for this mesh's report among what was captured.
        Assert.Contains(errors, error => error.Contains("'Rail'") && error.Contains("2 entries for 3 vertices"));
    }

    [Fact]
    public void ExistingAssetWithoutTangents_GainsNoTangents()
    {
        string path = Path.Combine(FindRepositoryRoot(), "Projects", "SampleProject", "Entities", "Box.staticModel");
        JObject fileContent = JObject.Parse(File.ReadAllText(path));
        StaticModel model = Reload(fileContent);

        JObject rewritten = Serialize(model);
        StaticModel reloaded = Reload(JObject.Parse(rewritten.ToString()));

        var fileMeshes = (JArray)fileContent["meshes"]!;
        var rewrittenMeshes = (JArray)rewritten["meshes"]!;
        Assert.NotEmpty(fileMeshes);
        Assert.Equal(fileMeshes.Count, rewrittenMeshes.Count);
        Assert.Equal(fileMeshes.Count, reloaded.Meshes.Count);
        for (int i = 0; i < fileMeshes.Count; i++)
        {
            Assert.Null(rewrittenMeshes[i]!["tangents"]);
            Assert.False(reloaded.Meshes[i].HasTangents);
            Assert.Equal(model.Meshes[i].GetVertices(), reloaded.Meshes[i].GetVertices());
            Assert.Equal(model.Meshes[i].GetIndices(), reloaded.Meshes[i].GetIndices());
            Assert.Equal(((JArray)fileMeshes[i]!["vertices"]!).Count, reloaded.Meshes[i].GetVertices().Count);
            Assert.Equal(
                ((JArray)fileMeshes[i]!["indices"]!).Select(token => token.Value<uint>()),
                reloaded.Meshes[i].GetIndices());
            Assert.True(JToken.DeepEquals(fileMeshes[i]!["vertices"], rewrittenMeshes[i]!["vertices"]));
        }
    }

    private static StaticModel CreateModel(StaticModelMesh mesh)
    {
        var model = new StaticModel
        {
            Name = "Model",
            RootNode = new StaticModelNode { Name = "Root", MeshIndex = 0, Rotation = Quaternion.Identity, Scale = Vector3.One },
        };
        model.Meshes.Add(mesh);
        return model;
    }

    //  Through text, as the writer service does, so that the float formatting is part of the round trip.
    private static JObject Serialize(StaticModel model)
    {
        Assert.True(EditorAssetJsonSerializer.TrySerialize(model, out JObject root));
        return JObject.Parse(root.ToString());
    }

    private static StaticModel Reload(JObject content)
    {
        var model = new StaticModel();
        model.Load(content);
        return model;
    }

    private static List<string> CaptureErrors(Action action)
    {
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            action();
        }
        finally
        {
            Logs.Close(); // detaches every logger added during this test, including `logger`.
        }

        return logger.Errors;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.MonoGame.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found from " + AppContext.BaseDirectory);
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Errors { get; } = new();

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) { }
        public void WriteError(string msg) => Errors.Add(msg);
    }
}
