using CasaEngine.EditorServices.Import;
using CasaEngine.Framework.Assets.Loaders;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

/// <summary>
/// Validates the AssimpNetter -> glb -> SharpGLTF conversion pipeline used by the editor import
/// flow (non-glTF sources are normalised to a self-contained binary glTF before the SharpGLTF
/// readers build the engine assets).
/// </summary>
public class AssimpToGltfConverterTests
{
    [Fact]
    public void GetSupportedExportFormats_IncludesBinaryGltf2()
    {
        using var context = new Assimp.AssimpContext();
        Assert.Contains(
            context.GetSupportedExportFormats(),
            format => string.Equals(format.FormatId, "glb2", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RequiresConversion_IsTrueForNonGltf_AndFalseForGltf()
    {
        Assert.True(AssimpToGltfConverter.RequiresConversion("model.obj"));
        Assert.True(AssimpToGltfConverter.RequiresConversion("model.fbx"));
        Assert.False(AssimpToGltfConverter.RequiresConversion("model.gltf"));
        Assert.False(AssimpToGltfConverter.RequiresConversion("model.glb"));
    }

    [Fact]
    public void Convert_ObjStaticMesh_ProducesReadableSelfContainedGlb()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "CasaEngineConvObj", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            string objPath = Path.Combine(tempDirectory, "tri.obj");
            File.WriteAllText(objPath, "o Tri\nv 0 0 0\nv 1 0 0\nv 0 1 0\nvn 0 0 1\nf 1//1 2//1 3//1\n");

            string glbPath = Path.Combine(tempDirectory, "tri.glb");
            AssimpToGltfConverter.Convert(objPath, glbPath);
            Assert.True(File.Exists(glbPath), "Converter did not produce a glb.");

            var result = new GltfStaticModelReader().ReadWithMetadata(glbPath);
            var mesh = Assert.Single(result.Model.Meshes);
            Assert.Equal(3, mesh.GetVertices().Count);
            Assert.Equal(3, mesh.GetIndices().Count);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
