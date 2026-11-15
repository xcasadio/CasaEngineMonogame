using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.Shaders;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;
using Xunit.Abstractions;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// The IBL convolution on a real device (plan T6.1): the level read by <c>SAMPLE_CUBEMAP_LEVEL</c> on the OpenGL chain (step 1b,
/// which decides the atlas design of ADR-0107), the orientation of the faces and of the atlas, the colour space of the source,
/// the shape of the generated textures and the restoration of the device state. Skipped where no device can be created.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class IblCubeGeneratorGpuTests
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

    private static readonly Vector3[] FaceColors =
    [
        new(1.0f, 0.1f, 0.1f),
        new(0.1f, 1.0f, 0.1f),
        new(0.1f, 0.1f, 1.0f),
        new(1.0f, 1.0f, 0.1f),
        new(0.1f, 1.0f, 1.0f),
        new(1.0f, 0.1f, 1.0f),
    ];

    private readonly ITestOutputHelper _output;

    public IblCubeGeneratorGpuTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // Step 1b. A cube with a full mip chain whose every level is a distinct solid colour; the shader requests one level and the
    // pixel it writes is measured. The levels are stored (GetData returns them) but the OpenGL chain reads level 0 whatever the
    // request: the compiled GLSL is a plain textureCube. This pins the fact that decides the 2D atlas of ADR-0107; a MonoGame
    // that honours the level would fail it, which is the signal to reconsider the atlas.
    [GpuFact]
    public void OpenGlChain_IgnoresTheRequestedCubeLevel_SoTheAtlasIsUsed()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.IblConvolutionContentName);

            const int size = 128;
            using var cube = new TextureCube(device, size, true, SurfaceFormat.Vector4);
            Assert.Equal(8, cube.LevelCount);

            for (int level = 0; level < cube.LevelCount; level++)
            {
                int levelSize = Math.Max(1, size >> level);
                var texels = new Vector4[levelSize * levelSize];
                Array.Fill(texels, LevelColor(level));
                foreach (CubeMapFace face in Faces)
                {
                    cube.SetData(face, level, null, texels, 0, texels.Length);
                }
            }

            // The levels are really stored: reading them back gives the colour written at each one.
            foreach (int level in new[] { 0, 2, 4 })
            {
                int levelSize = Math.Max(1, size >> level);
                var back = new Vector4[levelSize * levelSize];
                cube.GetData(CubeMapFace.PositiveX, level, null, back, 0, back.Length);
                AssertClose(LevelColor(level), back[0], 0.01f);
            }

            Vector4 level0 = LevelColor(0);
            foreach (int level in new[] { 0, 2, 4 })
            {
                Vector4 measured = SampleLevel(device, effect, cube, new Vector3(0.3f, 0.5f, 0.8f), level);
                _output.WriteLine($"requested level {level}: measured {measured}, colour of level {level} {LevelColor(level)}, colour of level 0 {level0}");
                AssertClose(level0, measured, 0.01f);
            }
        });
    }

    // Plan test (1): a source of six solid colours (linear); the prefiltered atlas, band 0, holds at the direction of the centre
    // of each face the colour of that face. It proves the orientation of the faces against the lat-long mapping.
    [GpuFact]
    public void PrefilteredBand0_HoldsTheColourOfEachFaceAtItsCentreDirection()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var generator = CreateGenerator(out _);
            using var source = CreateSolidFaceCube(64);
            using Texture2D atlas = generator.GeneratePrefiltered(source, sourceIsLinear: true);

            var texels = ReadAtlas(atlas);
            for (int face = 0; face < Faces.Length; face++)
            {
                Vector3 centre = IblCubeGenerator.GetTexelDirection(Faces[face], 32, 32, 64);
                Vector4 measured = ReadBandTexel(texels, 0, centre);
                AssertClose(new Vector4(FaceColors[face], 1.0f), measured, 0.02f);
            }

        });
    }

    // Orientation inside a face and of the atlas, with a source that is a smooth function of the direction: colour = 0.5 + 0.5 d.
    // Band 0 returns the source: 0.5 + 0.5 d at the direction of the texel. The irradiance of a linear function is the function
    // scaled by 2/3 around its mean (the cosine-weighted average of d around n is 2/3 n): 0.5 + 0.5 (2/3) n.
    [GpuFact]
    public void DirectionGradient_IsReproduced_ByTheAtlasBand0AndByTheIrradianceCube()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var generator = CreateGenerator(out _);
            using var source = CreateGradientCube(64);

            using Texture2D atlas = generator.GeneratePrefiltered(source, sourceIsLinear: true);
            var atlasTexels = ReadAtlas(atlas);
            int[] xs = [3, 100, 256, 300, 411, 509];
            int[] ys = [10, 64, 128, 191, 245];
            foreach (int y in ys)
            {
                foreach (int x in xs)
                {
                    Vector3 d = IblCubeGenerator.GetBandTexelDirection(x, y);
                    Vector4 expected = new(0.5f + 0.5f * d.X, 0.5f + 0.5f * d.Y, 0.5f + 0.5f * d.Z, 1.0f);
                    AssertClose(expected, atlasTexels[y * IblCubeGenerator.AtlasBandWidth + x], 0.02f);
                }
            }

            using TextureCube irradiance = generator.GenerateIrradiance(source, sourceIsLinear: true);
            const int size = IblCubeGenerator.IrradianceSize;
            var texels = new Vector4[size * size];
            foreach (CubeMapFace face in Faces)
            {
                irradiance.GetData(face, texels);
                foreach (int y in new[] { 2, 9, 16, 25, 29 })
                {
                    foreach (int x in new[] { 1, 8, 17, 24, 30 })
                    {
                        Vector3 n = IblCubeGenerator.GetTexelDirection(face, x, y, size);
                        float k = 0.5f * (2.0f / 3.0f);
                        var expected = new Vector4(0.5f + k * n.X, 0.5f + k * n.Y, 0.5f + k * n.Z, 1.0f);
                        AssertClose(expected, texels[y * size + x], 0.02f);
                    }
                }
            }
        });
    }

    // Plan test (2): the irradiance of a uniform linear cube is its colour.
    [GpuFact]
    public void Irradiance_OfAUniformLinearCube_IsItsColour()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var generator = CreateGenerator(out _);
            using var source = CreateUniformCube(16, new Vector4(0.8f, 0.4f, 0.2f, 1.0f));
            using TextureCube irradiance = generator.GenerateIrradiance(source, sourceIsLinear: true);

            AssertAllFacesUniform(irradiance, new Vector4(0.8f, 0.4f, 0.2f, 1.0f), 0.02f);
        });
    }

    // Plan test (3): a uniform cube that is not linear is decoded before it is accumulated: 0.5 gives 0.5^2.2.
    [GpuFact]
    public void Irradiance_OfAUniformNonLinearCube_IsDecoded()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var generator = CreateGenerator(out _);
            using var source = CreateUniformCube(16, new Vector4(0.5f, 0.5f, 0.5f, 1.0f));
            using TextureCube irradiance = generator.GenerateIrradiance(source, sourceIsLinear: false);

            float decoded = MathF.Pow(0.5f, 2.2f);
            Assert.InRange(decoded, 0.2175f, 0.2177f);
            AssertAllFacesUniform(irradiance, new Vector4(decoded, decoded, decoded, 1.0f), 0.02f);
        });
    }

    // Plan test (4): the shape of the outputs and the roughness carried by each band. A source whose +X face is white and the
    // rest black: at a direction in the dark region, the prefiltered value is zero for the roughness 0 and grows with the roughness
    // (the lobe widens), while the value at the centre of the lit face falls.
    [GpuFact]
    public void PrefilteredAtlas_HasTheDocumentedShape_AndTheRoughnessGrowsWithTheBand()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var generator = CreateGenerator(out _);
            using var source = CreateOneBrightFaceCube(64);

            using Texture2D atlas = generator.GeneratePrefiltered(source, sourceIsLinear: true);
            Assert.Equal(IblCubeGenerator.AtlasBandWidth, atlas.Width);
            Assert.Equal(IblCubeGenerator.AtlasBandHeight * IblCubeGenerator.RoughnessBandCount, atlas.Height);
            Assert.Equal(1, atlas.LevelCount);
            Assert.Equal(SurfaceFormat.Vector4, atlas.Format);

            var texels = ReadAtlas(atlas);
            var probe = Vector3.Normalize(new Vector3(0.5f, 0.866f, 0.0f));
            var centre = Vector3.UnitX;
            float previousProbe = -1.0f;
            float previousCentre = 2.0f;
            float firstRoughProbe = 0.0f;
            var line = new List<string>();
            for (int band = 0; band < IblCubeGenerator.RoughnessBandCount; band++)
            {
                float atProbe = ReadBandTexel(texels, band, probe).X;
                float atCentre = ReadBandTexel(texels, band, centre).X;
                line.Add($"band {band} (roughness {IblCubeGenerator.GetRoughnessForBand(band):0.0}): probe {atProbe:0.0000}, centre {atCentre:0.0000}");

                if (band == 0)
                {
                    Assert.True(atProbe < 0.01f, $"band 0 must stay sharp, got {atProbe} in the dark region");
                    Assert.True(atCentre > 0.98f, $"band 0 must keep the lit face, got {atCentre}");
                }
                else
                {
                    // The lobe of the roughest band is so wide that the value at the probe may level off or dip: the growth is
                    // asserted up to the band 4, and the last band must still be wider than the first rough one.
                    if (band <= 4)
                    {
                        Assert.True(atProbe > previousProbe + 0.001f, $"band {band}: the probe value must grow with the roughness ({previousProbe} -> {atProbe})");
                    }
                    else
                    {
                        Assert.True(atProbe > firstRoughProbe, $"band {band}: the probe value must exceed that of the band 1 ({firstRoughProbe} -> {atProbe})");
                    }
                    Assert.True(atCentre < previousCentre - 0.001f, $"band {band}: the centre value must fall with the roughness ({previousCentre} -> {atCentre})");
                }

                if (band == 1)
                {
                    firstRoughProbe = atProbe;
                }

                previousProbe = atProbe;
                previousCentre = atCentre;
            }

            _output.WriteLine(string.Join(Environment.NewLine, line));

            using TextureCube irradiance = generator.GenerateIrradiance(source, sourceIsLinear: true);
            Assert.Equal(IblCubeGenerator.IrradianceSize, irradiance.Size);
            Assert.Equal(1, irradiance.LevelCount);
            Assert.Equal(SurfaceFormat.Vector4, irradiance.Format);
        });
    }

    // Plan test (5): after a generation the device is as before it.
    [GpuFact]
    public void Generation_RestoresTheDeviceState()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            using var generator = CreateGenerator(out _);
            using var source = CreateUniformCube(16, new Vector4(0.3f, 0.3f, 0.3f, 1.0f));
            using var userTarget = new RenderTarget2D(device, 48, 40, false, SurfaceFormat.Color, DepthFormat.None);

            var blend = BlendState.Additive;
            var depth = DepthStencilState.DepthRead;
            var rasterizer = RasterizerState.CullClockwise;
            var sampler = SamplerState.PointWrap;
            var viewport = new Viewport(3, 5, 30, 20);

            device.SetRenderTarget(userTarget);
            device.BlendState = blend;
            device.DepthStencilState = depth;
            device.RasterizerState = rasterizer;
            device.SamplerStates[0] = sampler;
            device.Viewport = viewport;

            using TextureCube irradiance = generator.GenerateIrradiance(source, sourceIsLinear: true);
            AssertDeviceState();
            using Texture2D atlas = generator.GeneratePrefiltered(source, sourceIsLinear: true);
            AssertDeviceState();

            device.SetRenderTarget(null);

            void AssertDeviceState()
            {
                RenderTargetBinding[] bindings = device.GetRenderTargets();
                Assert.Single(bindings);
                Assert.Same(userTarget, bindings[0].RenderTarget);
                Assert.Same(blend, device.BlendState);
                Assert.Same(depth, device.DepthStencilState);
                Assert.Same(rasterizer, device.RasterizerState);
                Assert.Same(sampler, device.SamplerStates[0]);
                Assert.Equal(viewport.Bounds, device.Viewport.Bounds);
            }
        });
    }

    [GpuFact]
    public void GenerationTime_IsMeasured()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var generator = CreateGenerator(out _);
            using var source = CreateGradientCube(128);

            // The first call pays the one-off costs of the driver (program link, first allocation); the second is the steady state.
            for (int run = 0; run < 3; run++)
            {
                using TextureCube irradiance = generator.GenerateIrradiance(source, sourceIsLinear: true);
                double irradianceMs = generator.LastGenerationMilliseconds;
                using Texture2D atlas = generator.GeneratePrefiltered(source, sourceIsLinear: true);
                double prefilterMs = generator.LastGenerationMilliseconds;
                _output.WriteLine($"run {run}: irradiance {irradianceMs:0.0} ms, prefiltered atlas {prefilterMs:0.0} ms (source 128 px per face)");
            }
        });
    }

    private static IblCubeGenerator CreateGenerator(out Effect effect)
    {
        effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.IblConvolutionContentName);
        return new IblCubeGenerator(GpuDeviceHost.Instance.GraphicsDevice, effect);
    }

    private static TextureCube CreateSolidFaceCube(int size)
    {
        var cube = new TextureCube(GpuDeviceHost.Instance.GraphicsDevice, size, false, SurfaceFormat.Vector4);
        var texels = new Vector4[size * size];
        for (int face = 0; face < Faces.Length; face++)
        {
            Array.Fill(texels, new Vector4(FaceColors[face], 1.0f));
            cube.SetData(Faces[face], texels);
        }

        return cube;
    }

    private static TextureCube CreateUniformCube(int size, Vector4 color)
    {
        var cube = new TextureCube(GpuDeviceHost.Instance.GraphicsDevice, size, false, SurfaceFormat.Vector4);
        var texels = new Vector4[size * size];
        Array.Fill(texels, color);
        foreach (CubeMapFace face in Faces)
        {
            cube.SetData(face, texels);
        }

        return cube;
    }

    private static TextureCube CreateOneBrightFaceCube(int size)
    {
        var cube = new TextureCube(GpuDeviceHost.Instance.GraphicsDevice, size, false, SurfaceFormat.Vector4);
        var texels = new Vector4[size * size];
        foreach (CubeMapFace face in Faces)
        {
            Array.Fill(texels, face == CubeMapFace.PositiveX ? new Vector4(1.0f, 1.0f, 1.0f, 1.0f) : new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
            cube.SetData(face, texels);
        }

        return cube;
    }

    // Colour = 0.5 + 0.5 * direction of the texel, with the direction convention of the engine (GetDirectionForFace).
    private static TextureCube CreateGradientCube(int size)
    {
        var cube = new TextureCube(GpuDeviceHost.Instance.GraphicsDevice, size, false, SurfaceFormat.Vector4);
        var texels = new Vector4[size * size];
        foreach (CubeMapFace face in Faces)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector3 d = IblCubeGenerator.GetTexelDirection(face, x, y, size);
                    texels[y * size + x] = new Vector4(0.5f + 0.5f * d.X, 0.5f + 0.5f * d.Y, 0.5f + 0.5f * d.Z, 1.0f);
                }
            }

            cube.SetData(face, texels);
        }

        return cube;
    }

    private static Vector4[] ReadAtlas(Texture2D atlas)
    {
        var texels = new Vector4[atlas.Width * atlas.Height];
        atlas.GetData(texels);
        return texels;
    }

    // Nearest texel of a band at a direction (the mapping of GetPanoramaUv).
    private static Vector4 ReadBandTexel(Vector4[] atlasTexels, int band, Vector3 direction)
    {
        Vector2 uv = PanoramaUv(direction);
        int x = Math.Clamp((int)(uv.X * IblCubeGenerator.AtlasBandWidth), 0, IblCubeGenerator.AtlasBandWidth - 1);
        int y = Math.Clamp((int)(uv.Y * IblCubeGenerator.AtlasBandHeight), 0, IblCubeGenerator.AtlasBandHeight - 1);
        return atlasTexels[(band * IblCubeGenerator.AtlasBandHeight + y) * IblCubeGenerator.AtlasBandWidth + x];
    }

    private static Vector2 PanoramaUv(Vector3 direction)
    {
        direction.Normalize();
        float longitude = MathF.Atan2(direction.X, -direction.Z);
        float latitude = MathF.Asin(Math.Clamp(direction.Y, -1.0f, 1.0f));
        return new Vector2(0.5f + (longitude / (2.0f * MathF.PI)), 0.5f - (latitude / MathF.PI));
    }

    private static void AssertAllFacesUniform(TextureCube cube, Vector4 expected, float tolerance)
    {
        var texels = new Vector4[cube.Size * cube.Size];
        foreach (CubeMapFace face in Faces)
        {
            cube.GetData(face, texels);
            foreach (int index in new[] { 0, texels.Length / 2, texels.Length - 1 })
            {
                AssertClose(expected, texels[index], tolerance);
            }
        }
    }

    private static Vector4 LevelColor(int level)
        => new((level + 1) / 10.0f, (8 - level) / 10.0f, level % 2 == 0 ? 0.25f : 0.75f, 1.0f);

    private static Vector4 SampleLevel(GraphicsDevice device, Effect effect, TextureCube cube, Vector3 direction, float level)
    {
        using var target = new RenderTarget2D(device, 4, 4, false, SurfaceFormat.Vector4, DepthFormat.None);
        var quad = new VertexPositionTexture[]
        {
            new(new Vector3(-1.0f, 1.0f, 0.0f), new Vector2(0.0f, 0.0f)),
            new(new Vector3(1.0f, 1.0f, 0.0f), new Vector2(1.0f, 0.0f)),
            new(new Vector3(-1.0f, -1.0f, 0.0f), new Vector2(0.0f, 1.0f)),
            new(new Vector3(1.0f, -1.0f, 0.0f), new Vector2(1.0f, 1.0f)),
        };

        effect.Parameters["SourceCube"].SetValue(cube);
        effect.Parameters["FaceForward"].SetValue(direction);
        effect.Parameters["Level"].SetValue(level);
        effect.CurrentTechnique = effect.Techniques["SampleCubeLevel"];

        device.SetRenderTarget(target);
        device.Viewport = new Viewport(0, 0, 4, 4);
        device.BlendState = BlendState.Opaque;
        device.DepthStencilState = DepthStencilState.None;
        device.RasterizerState = RasterizerState.CullNone;
        device.SamplerStates[0] = SamplerState.LinearClamp;
        effect.CurrentTechnique.Passes[0].Apply();
        device.DrawUserPrimitives(PrimitiveType.TriangleStrip, quad, 0, 2);
        device.SetRenderTarget(null);

        var pixels = new Vector4[16];
        target.GetData(pixels);
        effect.Parameters["SourceCube"].SetValue((TextureCube)null);
        return pixels[5];
    }

    private static void AssertClose(Vector4 expected, Vector4 actual, float tolerance)
    {
        Assert.True(
            Math.Abs(expected.X - actual.X) <= tolerance
            && Math.Abs(expected.Y - actual.Y) <= tolerance
            && Math.Abs(expected.Z - actual.Z) <= tolerance,
            $"expected {expected}, got {actual} (tolerance {tolerance})");
    }
}
