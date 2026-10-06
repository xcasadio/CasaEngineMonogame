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
            DeleteTemporaryDirectory(tempDirectory);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Convert_ObjWithExternalTexture_EmbedsTheTexture(bool absoluteTexturePath)
    {
        const string OnePixelPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";
        string tempDirectory = Path.Combine(Path.GetTempPath(), "CasaEngineConvObj", Guid.NewGuid().ToString("N"));
        string sourceDirectory = Path.Combine(tempDirectory, "source");
        Directory.CreateDirectory(sourceDirectory);
        try
        {
            string objPath = Path.Combine(sourceDirectory, "quad.obj");
            File.WriteAllText(objPath,
                "mtllib quad.mtl\no Quad\nv 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\n"
                + "vt 0 0\nvt 1 0\nvt 1 1\nvt 0 1\nvn 0 0 1\nusemtl Mat\nf 1/1/1 2/2/1 3/3/1 4/4/1\n");
            byte[] png = Convert.FromBase64String(OnePixelPng);
            string texturePath;
            if (absoluteTexturePath)
            {
                // An absolute path outside the source directory, as DCC tools often write.
                string textureDirectory = Path.Combine(tempDirectory, "elsewhere");
                Directory.CreateDirectory(textureDirectory);
                texturePath = Path.Combine(textureDirectory, "tex.png");
                File.WriteAllBytes(texturePath, png);
            }
            else
            {
                // A sub-path that does not exist on disk: the texture sits next to the source file.
                texturePath = "textures/tex.png";
                File.WriteAllBytes(Path.Combine(sourceDirectory, "tex.png"), png);
            }

            File.WriteAllText(Path.Combine(sourceDirectory, "quad.mtl"), $"newmtl Mat\nKd 1 1 1\nmap_Kd {texturePath}\n");

            // Written away from the source so the glb cannot find the texture next to it.
            string glbPath = Path.Combine(tempDirectory, "output", "quad.glb");
            AssimpToGltfConverter.Convert(objPath, glbPath);

            var image = Assert.Single(SharpGLTF.Schema2.ModelRoot.Load(glbPath).LogicalImages);
            Assert.True(image.Content.IsPng);
            Assert.Equal(png, image.Content.Content.ToArray());
        }
        finally
        {
            DeleteTemporaryDirectory(tempDirectory);
        }
    }

    /// <summary>
    /// Best-effort cleanup: the native importer reads the source file through an inheritable
    /// handle, so a child process started meanwhile by another test (a <c>dotnet build</c>) can
    /// keep it undeletable for a while, and a cleanup failure must not fail a test whose
    /// assertions already passed. A few short retries cover the usual delay.
    /// </summary>
    private static void DeleteTemporaryDirectory(string directory)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }

                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(50);
            }
            catch (UnauthorizedAccessException) when (attempt < 4)
            {
                Thread.Sleep(50);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }
    }
}
