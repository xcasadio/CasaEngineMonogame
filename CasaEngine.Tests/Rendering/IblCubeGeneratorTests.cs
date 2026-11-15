using System.Text.RegularExpressions;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// T6.1 of the PBR plan, parts that need no device: the pure functions of the IBL generator (directions of the texels of a face and
/// of an atlas band, roughness of a band, size of the atlas) and the registration of <c>IblConvolution.fx</c> (ADR-0109). The
/// real-GPU checks are in <see cref="IblCubeGeneratorGpuTests"/>.
/// </summary>
public class IblCubeGeneratorTests
{
    private static readonly CubeMapFace[] Faces =
    [
        CubeMapFace.PositiveX,
        CubeMapFace.NegativeX,
        CubeMapFace.PositiveY,
        CubeMapFace.NegativeY,
        CubeMapFace.PositiveZ,
        CubeMapFace.NegativeZ,
    ];

    private static readonly Regex TechniqueRegex = new(
        @"technique\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{",
        RegexOptions.Compiled);

    [Fact]
    public void TexelDirection_OfTheCentreOfAFace_IsTheFaceAxis()
    {
        var axes = new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ };

        for (int face = 0; face < Faces.Length; face++)
        {
            // The centre of an even-sized face lies between four texels: the mean of their directions is the axis.
            Vector3 sum = IblCubeGenerator.GetTexelDirection(Faces[face], 31, 31, 64)
                + IblCubeGenerator.GetTexelDirection(Faces[face], 32, 31, 64)
                + IblCubeGenerator.GetTexelDirection(Faces[face], 31, 32, 64)
                + IblCubeGenerator.GetTexelDirection(Faces[face], 32, 32, 64);
            sum.Normalize();
            AssertClose(axes[face], sum, 1e-5f);
        }
    }

    [Fact]
    public void TexelDirection_RowZeroIsTheTopOfTheFace_AndColumnZeroItsLeft()
    {
        // +Z face: x grows to the right (+X in the world), y grows downwards (-Y in the world).
        Vector3 topLeft = IblCubeGenerator.GetTexelDirection(CubeMapFace.PositiveZ, 0, 0, 8);
        Vector3 topRight = IblCubeGenerator.GetTexelDirection(CubeMapFace.PositiveZ, 7, 0, 8);
        Vector3 bottomLeft = IblCubeGenerator.GetTexelDirection(CubeMapFace.PositiveZ, 0, 7, 8);

        Assert.True(topRight.X > topLeft.X);
        Assert.True(bottomLeft.Y < topLeft.Y);
        Assert.True(topLeft.X < 0.0f && topLeft.Y > 0.0f && topLeft.Z > 0.0f);

        // +X face: x grows towards -Z, as the engine convention of the panorama generator says.
        Vector3 xLeft = IblCubeGenerator.GetTexelDirection(CubeMapFace.PositiveX, 0, 4, 8);
        Vector3 xRight = IblCubeGenerator.GetTexelDirection(CubeMapFace.PositiveX, 7, 4, 8);
        Assert.True(xLeft.Z > xRight.Z);
    }

    [Fact]
    public void FaceBasis_RebuildsTheTexelDirectionOfTheEngineConvention()
    {
        const int size = 16;
        foreach (CubeMapFace face in Faces)
        {
            IblCubeGenerator.GetFaceBasis(face, out Vector3 forward, out Vector3 right, out Vector3 up);

            foreach (int y in new[] { 0, 5, 15 })
            {
                foreach (int x in new[] { 0, 7, 15 })
                {
                    float u = (2.0f * (x + 0.5f) / size) - 1.0f;
                    float v = 1.0f - (2.0f * (y + 0.5f) / size);
                    Vector3 rebuilt = Vector3.Normalize(forward + (u * right) + (v * up));
                    AssertClose(IblCubeGenerator.GetTexelDirection(face, x, y, size), rebuilt, 1e-5f);
                }
            }
        }
    }

    [Fact]
    public void BandTexelDirection_IsTheInverseOfThePanoramaMapping()
    {
        foreach (int y in new[] { 0, 40, 128, 200, 255 })
        {
            foreach (int x in new[] { 0, 100, 256, 380, 511 })
            {
                Vector3 direction = IblCubeGenerator.GetBandTexelDirection(x, y);
                Assert.Equal(1.0f, direction.Length(), 1e-4f);

                Vector2 uv = PanoramaEnvironmentGenerator.GetPanoramaUv(direction);
                Assert.Equal((x + 0.5f) / IblCubeGenerator.AtlasBandWidth, uv.X, 1e-3f);
                Assert.Equal((y + 0.5f) / IblCubeGenerator.AtlasBandHeight, uv.Y, 1e-3f);
            }
        }
    }

    [Fact]
    public void AtlasUv_StaysInsideItsBand_WhateverTheDirection()
    {
        float halfTexel = 0.5f / IblCubeGenerator.AtlasBandHeight;

        for (int band = 0; band < IblCubeGenerator.RoughnessBandCount; band++)
        {
            float bottom = (float)band / IblCubeGenerator.RoughnessBandCount;
            float top = (float)(band + 1) / IblCubeGenerator.RoughnessBandCount;
            float margin = halfTexel / IblCubeGenerator.RoughnessBandCount;

            foreach (Vector3 direction in new[] { Vector3.UnitY, -Vector3.UnitY, Vector3.UnitX, new Vector3(0.3f, 0.9f, -0.2f) })
            {
                Vector2 uv = IblCubeGenerator.GetAtlasUv(direction, band);
                Assert.InRange(uv.Y, bottom + margin - 1e-6f, top - margin + 1e-6f);
                Assert.InRange(uv.X, 0.0f, 1.0f);
            }
        }
    }

    [Fact]
    public void RoughnessOfABand_IsTheBandOverFive()
    {
        Assert.Equal(6, IblCubeGenerator.RoughnessBandCount);
        for (int band = 0; band < IblCubeGenerator.RoughnessBandCount; band++)
        {
            Assert.Equal(band / 5.0f, IblCubeGenerator.GetRoughnessForBand(band), 1e-6f);
        }

        Assert.Equal(0.0f, IblCubeGenerator.GetRoughnessForBand(0));
        Assert.Equal(1.0f, IblCubeGenerator.GetRoughnessForBand(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => IblCubeGenerator.GetRoughnessForBand(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => IblCubeGenerator.GetRoughnessForBand(6));
    }

    [Fact]
    public void Atlas_HasTheDocumentedSizes()
    {
        Assert.Equal(32, IblCubeGenerator.IrradianceSize);
        Assert.Equal(512, IblCubeGenerator.AtlasBandWidth);
        Assert.Equal(256, IblCubeGenerator.AtlasBandHeight);
        Assert.Equal(1536, IblCubeGenerator.AtlasHeight);
    }

    [Fact]
    public void IblConvolutionShader_DeclaresItsTechniques_AndDecodesANonLinearSourceOnce()
    {
        string shader = ReadRepositoryFile("CasaEngine", "Content", "Shaders", "IblConvolution.fx");

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in TechniqueRegex.Matches(shader))
        {
            names.Add(match.Groups["name"].Value);
        }

        Assert.Contains("Irradiance", names);
        Assert.Contains("PrefilterGgx", names);
        Assert.Contains("SampleCubeLevel", names);

        Assert.Single(Regex.Matches(shader, Regex.Escape("pow(abs(c), 2.2f)")));
        Assert.DoesNotContain("ColorPipelineIsLinear", shader, StringComparison.Ordinal);
        Assert.DoesNotContain("DecodeColor", shader, StringComparison.Ordinal);
        Assert.Contains("PrefilterSampleCount = 64", shader, StringComparison.Ordinal);
    }

    [Fact]
    public void Macros_DefineTheCubeLevelSamplerForBothTargets()
    {
        string macros = ReadRepositoryFile("CasaEngine", "Content", "Shaders", "Macros.fxh");

        Assert.Contains("texCUBElod(Name##Sampler, float4(texCoord, level))", macros, StringComparison.Ordinal);
        Assert.Contains("#define SAMPLE_CUBEMAP_LEVEL(Name, texCoord, level)  Name.SampleLevel(Name##Sampler, texCoord, level)", macros, StringComparison.Ordinal);
    }

    [Fact]
    public void IblConvolutionShader_IsCompiledRegisteredAndHeldByThePipeline()
    {
        string mgcb = ReadRepositoryFile("CasaEngine", "Content", "Content.mgcb");
        Assert.Contains("#begin Shaders/IblConvolution.fx", mgcb, StringComparison.Ordinal);
        Assert.Contains("/build:Shaders/IblConvolution.fx", mgcb, StringComparison.Ordinal);

        Assert.True(BuiltInShaderCatalog.TryGetByContentName(BuiltInShaderCatalog.IblConvolutionContentName, out var byName));
        Assert.Equal("Shaders/IblConvolution.fx", byName.SourceRelativePath);

        string game = ReadRepositoryFile("CasaEngine", "Framework", "Application", "CasaEngineGame.cs");
        Assert.Contains("SetIblConvolutionEffect(Content.Load<Effect>(BuiltInShaderCatalog.IblConvolutionContentName))", game, StringComparison.Ordinal);
        Assert.Contains("case BuiltInShaderCatalog.IblConvolutionContentName:", game, StringComparison.Ordinal);

        string pipeline = ReadRepositoryFile("CasaEngine", "Framework", "Rendering", "RenderPipeline.cs");
        Assert.Contains("public void SetIblConvolutionEffect(Effect effect)", pipeline, StringComparison.Ordinal);
        Assert.Contains("BuiltInShaderCatalog.IblConvolutionContentName", pipeline, StringComparison.Ordinal);
    }

    private static void AssertClose(Vector3 expected, Vector3 actual, float tolerance)
    {
        Assert.True((expected - actual).Length() <= tolerance, $"expected {expected}, got {actual}");
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        DirectoryInfo directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.Editor.MonoGame.sln")))
            {
                return File.ReadAllText(Path.Combine([directory.FullName, .. segments]));
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test output directory.");
    }
}
