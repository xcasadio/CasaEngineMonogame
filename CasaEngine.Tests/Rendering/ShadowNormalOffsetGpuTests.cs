using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Materials.Runtime;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Rendering.Shaders;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using Xunit;
using Xunit.Abstractions;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// The <c>NormalBias</c> offset of the directional-shadow lookup follows the interpolated vertex normal, never the
/// normal-mapped one, in <c>LitPbr_NormalMap</c> and in the normal-map technique of <c>LitForward.fx</c>. Every case renders a
/// 64x8 image of a quad facing the camera (identity view and projection: the quad covers the image, pixel column c is at
/// x = -1 + 2 (c + 0.5) / 64) with a normal map that is uniform over the quad.
/// <para>The shadow map is a 100x100 single-channel texture whose left half holds 0 (occluded) and whose right half holds 1
/// (lit), sampled with <see cref="SamplerState.PointClamp"/> set by the test; the filter radius is 0 and the fade width is 0.
/// The shadow light matrix maps x to (x + Shear * z) / 2, y to y / 2 and z to z + 0.5: the map covers twice the quad, so a
/// shifted lookup stays inside it, and the light is oblique to the quad, so that the quad normal (0, 0, 1) has a component
/// along the axis that crosses the shadow edge (a normal perpendicular to that axis could not move the edge). A lookup offset
/// of NormalBias * n therefore moves the lookup by s = NormalBias * (n.x + Shear * n.z) world units in x.</para>
/// <para>Observable: the edge column of each row. Even at radius 0 the filter averages four reads at +-0.5 texel, so the
/// image has two plateaus (in the shadow, outside the shadow) separated by a band whose visibility is 0.5; the render is
/// normalised between its own two plateaus and the edge column is the first column, from the shadow side (the left), whose
/// normalised level exceeds 0.375 (a level no filter value can equal). A plateau must cover at least a quarter of the
/// image.</para>
/// Skipped where no device can be created.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class ShadowNormalOffsetGpuTests
{
    private const int Width = 64;
    private const int Height = 8;
    private const int ShadowMapSize = 100;
    private const int ShadowMapSlot = 6;
    private const float Shear = 1.0f;
    private const float NormalBias = 0.15f;

    // Normal-map texels, encoded as n * 0.5 + 0.5. Flat: the vertex normal. Tilted: about 60 degrees from the vertex normal,
    // towards -x, which cancels most of the offset the vertex normal gives (n.x + Shear * n.z changes sign from +1 to -0.37).
    private static readonly Color FlatTexel = new(128, 128, 255, 255);
    private static readonly Color TiltedTexel = new(17, 128, 191, 255);

    private readonly ITestOutputHelper _output;

    public ShadowNormalOffsetGpuTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private enum Shader
    {
        LitPbrNormalMap,
        LitForwardNormalMap,
    }

    // Expected edge columns (computed from the geometry, see ExpectedEdgeColumn):
    //   the lookup of column c, at x = -1 + 2 (c + 0.5) / 64, offset by s = NormalBias * (n.x + Shear * n.z), is at texel
    //   t = 100 * (0.5 + (x + s) / 4); the filter reads at t + 0.5, which is lit from texel 50, so the edge column is the first c
    //   with c >= 32 * (1 - s - 0.02) - 0.5.
    //   Vertex normal (0, 0, 1) with the flat map:  s = 0.15 * 1.004 = 0.1506  -> 26.04, edge at column 27.
    //   NormalBias 0:                               s = 0                      -> 30.86, edge at column 31.
    //   Tilted normal (-0.867, 0.004, 0.498):       s = 0.15 * (-0.867 + 0.498) = -0.0553 -> 32.6, edge at column 33.
    // The normal-mapped offset therefore moves the edge by 33 - 27 = 6 pixels (at least 4 are required), and the geometric
    // offset moves it by 31 - 27 = 4 pixels (at least 2 are required).

    [GpuFact]
    public void LitPbrNormalMap_ANormalMapDoesNotMoveTheShadowEdge()
    {
        AssertANormalMapDoesNotMoveTheEdge(Shader.LitPbrNormalMap);
    }

    [GpuFact]
    public void LitPbrNormalMap_TheNormalBiasStillMovesTheShadowEdge()
    {
        AssertTheNormalBiasMovesTheEdge(Shader.LitPbrNormalMap);
    }

    [GpuFact]
    public void LitForwardNormalMap_ANormalMapDoesNotMoveTheShadowEdge()
    {
        AssertANormalMapDoesNotMoveTheEdge(Shader.LitForwardNormalMap);
    }

    [GpuFact]
    public void LitForwardNormalMap_TheNormalBiasStillMovesTheShadowEdge()
    {
        AssertTheNormalBiasMovesTheEdge(Shader.LitForwardNormalMap);
    }

    // Case 1: with a fixed NormalBias, a strongly tilted normal map gives the same edge as a flat one, on every row.
    private void AssertANormalMapDoesNotMoveTheEdge(Shader shader)
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            int[] flat = EdgeColumns(Render(shader, FlatTexel, NormalBias), "flat");
            int[] tilted = EdgeColumns(Render(shader, TiltedTexel, NormalBias), "tilted");

            int expectedFlat = ExpectedEdgeColumn(DecodeNormal(FlatTexel), NormalBias);
            int expectedTilted = ExpectedEdgeColumn(DecodeNormal(TiltedTexel), NormalBias);
            _output.WriteLine($"{shader}: edge columns, flat map {string.Join(",", flat)} (expected {expectedFlat}), tilted map {string.Join(",", tilted)} (what the tilted normal would give: {expectedTilted})");

            // The test is only meaningful if the perturbed normal would move the edge by at least 4 pixels.
            Assert.True(Math.Abs(expectedTilted - expectedFlat) >= 4, $"the scene moves the edge by {expectedTilted - expectedFlat} pixels only");

            foreach (int column in flat)
            {
                Assert.InRange(column, expectedFlat - 1, expectedFlat + 1);
            }

            for (int row = 0; row < Height; row++)
            {
                Assert.True(flat[row] == tilted[row], $"row {row}: edge at column {tilted[row]} with the tilted normal map, {flat[row]} with the flat one");
            }
        });
    }

    // Case 2: with the flat map, the offset still moves the edge by what the vertex normal gives.
    private void AssertTheNormalBiasMovesTheEdge(Shader shader)
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            int[] withoutBias = EdgeColumns(Render(shader, FlatTexel, 0.0f), "bias 0");
            int[] withBias = EdgeColumns(Render(shader, FlatTexel, NormalBias), "bias");

            int expectedWithout = ExpectedEdgeColumn(DecodeNormal(FlatTexel), 0.0f);
            int expectedWith = ExpectedEdgeColumn(DecodeNormal(FlatTexel), NormalBias);
            int expectedMove = expectedWithout - expectedWith;
            _output.WriteLine($"{shader}: edge columns, NormalBias 0 {string.Join(",", withoutBias)} (expected {expectedWithout}), NormalBias {NormalBias} {string.Join(",", withBias)} (expected {expectedWith})");

            Assert.True(expectedMove >= 2, $"the scene moves the edge by {expectedMove} pixels only");
            for (int row = 0; row < Height; row++)
            {
                Assert.InRange(withoutBias[row], expectedWithout - 1, expectedWithout + 1);
                Assert.InRange(withBias[row], expectedWith - 1, expectedWith + 1);
                Assert.InRange(withoutBias[row] - withBias[row], expectedMove - 1, expectedMove + 1);
                Assert.True(withoutBias[row] - withBias[row] >= 2, $"row {row}: the edge moved by {withoutBias[row] - withBias[row]} pixels only");
            }
        });
    }

    private static Vector3 DecodeNormal(Color texel)
    {
        var normal = new Vector3(texel.R / 255.0f * 2.0f - 1.0f, texel.G / 255.0f * 2.0f - 1.0f, texel.B / 255.0f * 2.0f - 1.0f);
        normal.Normalize();
        return normal;
    }

    // The edge column the geometry predicts for an offset along 'normal' (world x, y, z; the quad frame is the tangent frame
    // T = +x, B = +y, N = +z, so a tangent-space normal is also a world normal).
    private static int ExpectedEdgeColumn(Vector3 normal, float bias)
    {
        double shift = bias * (normal.X + Shear * normal.Z);
        double firstLitColumn = Width / 2.0 * (1.0 - shift - 2.0 / ShadowMapSize) - 0.5;
        return (int)Math.Ceiling(firstLitColumn);
    }

    // The edge column of every row; fails if the render does not have two plateaus that each cover a quarter of the image.
    private int[] EdgeColumns(float[] image, string name)
    {
        float low = image.Min();
        float high = image.Max();
        Assert.True(high - low > 0.05f, $"{name}: the image has no contrast between the shadow and the lit plateaus ({low}..{high})");

        int inShadow = 0;
        int outsideTheShadow = 0;
        foreach (float value in image)
        {
            float level = (value - low) / (high - low);
            if (level <= 0.05f)
            {
                inShadow++;
            }

            if (level >= 0.95f)
            {
                outsideTheShadow++;
            }
        }

        Assert.True(inShadow >= image.Length / 4, $"{name}: the shadow plateau covers {inShadow} of {image.Length} pixels");
        Assert.True(outsideTheShadow >= image.Length / 4, $"{name}: the lit plateau covers {outsideTheShadow} of {image.Length} pixels");

        var columns = new int[Height];
        for (int row = 0; row < Height; row++)
        {
            columns[row] = -1;
            for (int column = 0; column < Width; column++)
            {
                if ((image[row * Width + column] - low) / (high - low) > 0.375f)
                {
                    columns[row] = column;
                    break;
                }
            }

            Assert.True(columns[row] >= 0, $"{name}: row {row} has no lit pixel");
        }

        return columns;
    }

    /// <summary>Renders the strip and returns the red channel of each pixel, row by row.</summary>
    private static float[] Render(Shader shader, Color normalMapTexel, float normalBias)
    {
        var device = GpuDeviceHost.Instance.GraphicsDevice;
        var eye = new Vector3(0.0f, 0.0f, 5.0f);
        var context = new RenderContext
        {
            Device = device,
            Frame = new RenderFrame(Matrix.Identity, Matrix.Identity, eye, new Rectangle(0, 0, Width, Height)),
        };

        using var normalMap = new Texture2D(device, 1, 1, false, SurfaceFormat.Color);
        normalMap.SetData(new[] { normalMapTexel });
        using var white = new Texture2D(device, 1, 1, false, SurfaceFormat.Color);
        white.SetData(new[] { Color.White });

        Effect effect;
        ShaderWrapper wrapper;
        if (shader == Shader.LitPbrNormalMap)
        {
            effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.LitPbrContentName);
            wrapper = new ShaderWrapper(effect);
            var material = new LitPbrMaterial
            {
                BaseColor = Color.Black,
                Roughness = 1.0f,
                EmissiveColor = new Vector3(0.5f),
                EmissiveShadowFactor = 1.0f,
                NormalTexture = normalMap,
            };

            effect.CurrentTechnique = effect.Techniques["LitPbr_NormalMap"]!;
            material.Bind(wrapper, in context, Matrix.Identity);

            // The only colour is the emissive, which the shadow scales (EmissiveShadowFactor 1): the light is black.
            wrapper.SetParameter("DirLight0DiffuseColor", Vector3.Zero);
            wrapper.SetParameter("DirLight0SpecularColor", Vector3.Zero);
            wrapper.SetParameter("HasIbl", 0.0f);
        }
        else
        {
            effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.LitForwardContentName);
            wrapper = new ShaderWrapper(effect);
            var material = new LitDiffuseMaterial
            {
                BasColor = white,
                NormalMap = normalMap,
                DiffuseColor = Color.White,
                SpecularColor = Vector3.Zero,
            };

            effect.CurrentTechnique = effect.Techniques["LitForward_PixelLighting_Texture_NormalMap"]!;
            material.Bind(wrapper, in context, Matrix.Identity);

            // A white light along -z (the shadow scales it, the lighting follows the normal map) and no ambient: the pixels in
            // the shadow are black, the pixels outside it have the level of the diffuse term.
            wrapper.SetParameter("DirLight0DiffuseColor", Vector3.One);
            wrapper.SetParameter("DirLight0SpecularColor", Vector3.Zero);
            wrapper.SetParameter("EnvironmentAmbientColor", Vector3.Zero);
        }

        wrapper.SetParameter("ActiveDirectionalLightCount", 1.0f);
        wrapper.SetParameter("ActivePointLightCount", 0.0f);
        wrapper.SetParameter("ActiveSpotLightCount", 0.0f);
        wrapper.SetParameter("DirLight0Direction", new Vector3(0.0f, 0.0f, -1.0f));
        wrapper.SetParameter("AmbientColor", Vector3.Zero);
        wrapper.SetParameter("UseFlatAmbientSource", 1.0f);
        wrapper.SetParameter("HasEnvironmentCubeTexture", 0.0f);
        wrapper.SetParameter("ColorPipelineIsLinear", 0.0f);
        wrapper.SetParameter("EnvironmentSpecularIntensity", 0.0f);

        // The shadow: the light matrix maps x to (x + Shear * z) / 2, y to y / 2 and z to z + 0.5 (depth 0.5 at the quad, 0.5 + the
        // offset along the normal, always inside (0, 1) here).
        Matrix shadowLight = Matrix.Identity;
        shadowLight.M11 = 0.5f;
        shadowLight.M22 = 0.5f;
        shadowLight.M31 = 0.5f * Shear;
        shadowLight.M43 = 0.5f;
        wrapper.SetParameter("ActiveShadowLightCount", 1.0f);
        wrapper.SetParameter("ShadowedDirectionalLightIndex", 0.0f);
        wrapper.SetParameter("ShadowLightViewProjection", shadowLight);
        wrapper.SetParameter("ShadowDepthBias", 0.0f);
        wrapper.SetParameter("ShadowNormalBias", normalBias);
        wrapper.SetParameter("ShadowMapTexelSize", new Vector2(1.0f / ShadowMapSize, 1.0f / ShadowMapSize));
        wrapper.SetParameter("ReceiveShadows", 1.0f);
        wrapper.SetParameter("ShadowApplyMode", 0.0f);
        wrapper.SetParameter("ShadowTintColor", Vector3.One);
        wrapper.SetParameter("ShadowFilterRadius", 0.0f);
        wrapper.SetParameter("ShadowFadeWidth", 0.0f);

        // The left half of the map (x < 0) is occluded, the right half is lit.
        using var shadowMap = new Texture2D(device, ShadowMapSize, ShadowMapSize, false, SurfaceFormat.Single);
        var depths = new float[ShadowMapSize * ShadowMapSize];
        for (int y = 0; y < ShadowMapSize; y++)
        {
            for (int x = 0; x < ShadowMapSize; x++)
            {
                depths[y * ShadowMapSize + x] = x < ShadowMapSize / 2 ? 0.0f : 1.0f;
            }
        }

        shadowMap.SetData(depths);
        wrapper.SetTextureParameter("ShadowMapTexture", shadowMap, null);

        var vertices = new[]
        {
            new VertexPositionNormalTextureTangent(new Vector3(-1.0f, -1.0f, 0.0f), Vector3.UnitZ, new Vector2(0.0f, 1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f)),
            new VertexPositionNormalTextureTangent(new Vector3(-1.0f, 1.0f, 0.0f), Vector3.UnitZ, new Vector2(0.0f, 0.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f)),
            new VertexPositionNormalTextureTangent(new Vector3(1.0f, -1.0f, 0.0f), Vector3.UnitZ, new Vector2(1.0f, 1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f)),
            new VertexPositionNormalTextureTangent(new Vector3(1.0f, 1.0f, 0.0f), Vector3.UnitZ, new Vector2(1.0f, 0.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f)),
        };

        var previousTargets = device.GetRenderTargets();
        var previousViewport = device.Viewport;
        var previousBlend = device.BlendState;
        var previousDepth = device.DepthStencilState;
        var previousRasterizer = device.RasterizerState;
        var previousSampler = device.SamplerStates[ShadowMapSlot];
        using var target = new RenderTarget2D(device, Width, Height, false, SurfaceFormat.HalfVector4, DepthFormat.None);
        try
        {
            device.SetRenderTarget(target);
            device.Clear(ClearOptions.Target, Vector4.UnitW, 0.0f, 0);
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;
            // The shadow map is read at texel resolution whatever the default filtering is.
            device.SamplerStates[ShadowMapSlot] = SamplerState.PointClamp;

            effect.CurrentTechnique.Passes[0].Apply();
            device.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices, 0, 2);

            device.SetRenderTarget(null);
            var pixels = new HalfVector4[Width * Height];
            target.GetData(pixels);
            var image = new float[Width * Height];
            for (int i = 0; i < image.Length; i++)
            {
                image[i] = pixels[i].ToVector4().X;
            }

            return image;
        }
        finally
        {
            device.SetRenderTargets(previousTargets);
            device.Viewport = previousViewport;
            device.BlendState = previousBlend;
            device.DepthStencilState = previousDepth;
            device.RasterizerState = previousRasterizer;
            device.SamplerStates[ShadowMapSlot] = previousSampler;
        }
    }
}
