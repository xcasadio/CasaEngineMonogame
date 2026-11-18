using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Materials.Runtime;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using CasaEngine.Framework.Rendering.Shadows;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using Xunit;
using Xunit.Abstractions;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// <c>ShadowFilterTaps</c> (12, 32 or 64 shadow-map reads of the soft-edge filter, ADR-0127) on a real device. Every case
/// renders a 512x4 image of a quad facing the camera (identity view and projection: the quad covers the image, pixel column c
/// is at x = -1 + 2 (c + 0.5 - 63/128) / 512 on DesktopGL, see <see cref="GlVertexShift"/>) and compares it with a model computed on the processor.
/// <para>Geometry. The shadow map is a 512x512 single-channel texture whose left half holds 0 (occluded) and whose right half
/// holds 1 (lit), read in point mode (<see cref="SamplerState.PointClamp"/> set by the test), so a read is lit exactly when its
/// u is 0.5 or more. The shadow light matrix maps x to x / 2, y to y / 2 and z to z + 0.5, hence u = 0.5 + x / 4 at the quad.
/// The filter radius is 120 texels, the fade width and the normal bias are 0. A tap at offset (dx, dy) in the unit disc reads
/// u + dx * 120 / 512, so it is lit when dx >= -1.0667 x: the visibility of column c is the share of taps whose dx is at least
/// t = -1.0667 x. One column moves t by 0.00417 of the unit disc (at most 0.005, so the penumbra, which spans t from -1 to 1,
/// covers about 480 columns, at least 400) and the edge is vertical, so only dx matters.</para>
/// <para>Model. The visibility of each column for the 12 points of the fixed Poisson disc, and for the Vogel disc of 32 and 64
/// points (angle i * 2.39996323 rad, radius sqrt((i + 0.5) / N)). Measured on the model: 12 taps give 13 levels and a largest
/// step between neighbouring columns of 1/12; 32 give 32 levels and 2/32; 64 give 63 levels and 2/64. The test asserts the
/// thresholds the plan announces (12: at most 13 levels, step of at least 1/12 - 0.005; 32: at least 20 levels, step of at
/// most 3/32; 64: at least 40 levels, step of at most 3/64).</para>
/// <para>Observable: the red channel of the render, normalised between its own two plateaus (the quad is lit by an emissive,
/// <c>LitPbr</c>, or by a white light, <c>LitForward</c>, and nothing else, so the level is proportional to the visibility).
/// Every column is within one read (1/N) of the model and at least 95 % of the columns are at the model's level within half a
/// read; the tolerance 0.002 is the precision of the half-float target.</para>
/// Skipped where no device can be created.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class ShadowFilterTapsGpuTests
{
    private const int Width = 512;
    private const int Height = 4;
    private const int ShadowMapSize = 512;
    private const int ShadowMapSlot = 6;
    private const float FilterRadius = 120.0f;
    private const float ProjectionScale = 0.5f;
    private const double GoldenAngle = 2.39996323;
    private const double HalfFloatTolerance = 0.002;

    // MonoGame DesktopGL (the backend of the test device) moves every vertex by 63/64 of Direct3D 9's half pixel (posFixup in
    // the generated vertex shaders), i.e. by 63/128 pixel to the right: the pixel of column c sees the quad at
    // x = -1 + 2 (c + 0.5 - 63/128) / 512.
    private const double GlVertexShift = 63.0 / 128.0;

    private static readonly (double X, double Y)[] PoissonDisc =
    {
        (-0.326212, -0.405805),
        (-0.840144, -0.073580),
        (-0.695914, 0.457137),
        (-0.203345, 0.620716),
        (0.962340, -0.194983),
        (0.473434, -0.480026),
        (0.519456, 0.767022),
        (0.185461, -0.893124),
        (0.507431, 0.064425),
        (0.896420, 0.412458),
        (-0.321940, -0.932615),
        (-0.791559, -0.597705),
    };

    private readonly ITestOutputHelper _output;

    public ShadowFilterTapsGpuTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private enum Shader
    {
        LitPbr,
        LitForward,
    }

    [GpuFact]
    public void LitPbr_12Taps_FollowTheModelOfTheFixedPoissonDisc()
    {
        AssertTheImageFollowsTheModel(Shader.LitPbr, 12);
    }

    [GpuFact]
    public void LitPbr_32Taps_FollowTheModelOfTheVogelDisc()
    {
        AssertTheImageFollowsTheModel(Shader.LitPbr, 32);
    }

    [GpuFact]
    public void LitPbr_64Taps_FollowTheModelOfTheVogelDisc()
    {
        AssertTheImageFollowsTheModel(Shader.LitPbr, 64);
    }

    [GpuFact]
    public void LitForward_64Taps_FollowTheModelOfTheVogelDisc()
    {
        AssertTheImageFollowsTheModel(Shader.LitForward, 64);
    }

    [GpuFact]
    public void LitPbr_AnUniformOf12IsPixelForPixelTheImageOfAnUniformOf0()
    {
        AssertTwelveEqualsZero(Shader.LitPbr);
    }

    [GpuFact]
    public void LitForward_AnUniformOf12IsPixelForPixelTheImageOfAnUniformOf0()
    {
        AssertTwelveEqualsZero(Shader.LitForward);
    }

    // The binder test: ForwardLightBinder.Bind itself, with a real atlas and a visible directional shadow light, because the
    // snapshot of CreateSnapshot never runs the shader.SetParameter calls of Bind and ShaderWrapper ignores a missing parameter.
    [GpuFact]
    public void ForwardLightBinderBind_SendsTheFilterTapsOfTheSettingsToTheShader()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var device = GpuDeviceHost.Instance.GraphicsDevice;
            var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.LitPbrContentName);
            var wrapper = new ShaderWrapper(effect);

            var lighting = new LightingContext();
            lighting.BeginCollection(Vector3.Zero, Vector3.Zero);
            lighting.AddDirectionalLight(new CasaEngine.Framework.Rendering.DirectionalLight(new Vector3(0.0f, 0.0f, -1.0f), Vector3.One, Vector3.One, intensity: 1.0f));

            using var atlas = new RenderTarget2D(device, 16, 16, false, SurfaceFormat.Single, DepthFormat.None);
            var shadows = new ForwardShadowResources { ShadowMapAtlas = atlas };
            shadows.AddVisibleLight(new ShadowLight(ShadowLightType.Directional, 0, Matrix.Identity, new Rectangle(0, 0, 16, 16), 0.001f, 0.0f));

            var binder = new ForwardLightBinder();
            EffectParameter? tapsParameter = effect.Parameters["ShadowFilterTaps"];
            Assert.True(tapsParameter is not null, "LitPbr has no ShadowFilterTaps parameter");

            shadows.Settings.FilterTaps = 64;
            binder.Bind(wrapper, lighting, shadows);
            float shadowLights = effect.Parameters["ActiveShadowLightCount"]!.GetValueSingle();
            float taps64 = tapsParameter!.GetValueSingle();

            shadows.Settings.ResetToDefaults();
            binder.Bind(wrapper, lighting, shadows);
            float tapsDefault = tapsParameter.GetValueSingle();

            _output.WriteLine($"ActiveShadowLightCount {shadowLights}; ShadowFilterTaps after Bind: 64 -> {taps64}, default -> {tapsDefault}");
            Assert.Equal(1.0f, shadowLights);
            Assert.Equal(64.0f, taps64);
            Assert.Equal(12.0f, tapsDefault);
        });
    }

    private void AssertTheImageFollowsTheModel(Shader shader, int taps)
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            double[] model = ExpectedVisibility(taps);
            AssertTheGeometryHoldsWhatThePlanAnnounces(model, taps);

            float[] image = Render(shader, taps);
            float low = image.Min();
            float high = image.Max();
            Assert.True(high - low > 0.05f, $"{shader} {taps} taps: the image has no contrast between the shadow and the lit plateaus ({low}..{high})");

            double read = 1.0 / taps;
            double worst = 0.0;
            int exact = 0;
            int worstColumn = 0;
            for (int column = 0; column < Width; column++)
            {
                double level = (image[column] - low) / (high - low);
                double difference = Math.Abs(level - model[column]);
                if (difference > worst)
                {
                    worst = difference;
                    worstColumn = column;
                }

                if (difference <= 0.5 * read + HalfFloatTolerance)
                {
                    exact++;
                }
            }

            int distinct = DistinctLevels(image, low, high);
            _output.WriteLine($"{shader} {taps} taps: {distinct} distinct levels in the image, worst column {worstColumn} off by {worst * taps:0.00} read(s), {exact} of {Width} columns at the model level");
            Assert.True(worst <= read + HalfFloatTolerance, $"{shader} {taps} taps: column {worstColumn} is off the model by {worst:0.0000} ({worst * taps:0.00} read(s)); the render follows the {(taps == 12 ? "12" : "32 or 64")}-tap model only within one read");
            Assert.True(exact >= Width * 0.95, $"{shader} {taps} taps: only {exact} of {Width} columns are at the model level (worst column {worstColumn}, {worst * taps:0.00} read(s) off)");
        });
    }

    private void AssertTwelveEqualsZero(Shader shader)
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            float[] zero = Render(shader, 0);
            float[] twelve = Render(shader, 12);

            Assert.True(zero.Max() - zero.Min() > 0.05f, $"{shader}: the image has no contrast ({zero.Min()}..{zero.Max()})");
            int different = 0;
            for (int i = 0; i < zero.Length; i++)
            {
                if (BitConverter.SingleToInt32Bits(zero[i]) != BitConverter.SingleToInt32Bits(twelve[i]))
                {
                    different++;
                }
            }

            _output.WriteLine($"{shader}: {different} of {zero.Length} pixels differ between an uniform of 12 and an uniform of 0");
            Assert.Equal(0, different);
        });
    }

    // The model of the penumbra: for each column, the share of the taps whose read is in the lit half of the shadow map.
    private static double[] ExpectedVisibility(int taps)
    {
        (double X, double Y)[] disc = taps == 12 ? PoissonDisc : VogelDisc(taps);
        var visibility = new double[Width];
        for (int column = 0; column < Width; column++)
        {
            double x = -1.0 + 2.0 * (column + 0.5 - GlVertexShift) / Width;
            double u = 0.5 + ProjectionScale * x / 2.0;
            int lit = 0;
            foreach ((double dx, _) in disc)
            {
                // Texel index of the read is floor(read * size); the edge is at texel 256, i.e. read >= 0.5.
                if (u + dx * FilterRadius / ShadowMapSize >= 0.5)
                {
                    lit++;
                }
            }

            visibility[column] = (double)lit / taps;
        }

        return visibility;
    }

    private static (double X, double Y)[] VogelDisc(int taps)
    {
        var disc = new (double X, double Y)[taps];
        for (int i = 0; i < taps; i++)
        {
            double radius = Math.Sqrt((i + 0.5) / taps);
            double angle = i * GoldenAngle;
            disc[i] = (radius * Math.Cos(angle), radius * Math.Sin(angle));
        }

        return disc;
    }

    // The geometry holds what the plan announces about the model.
    private void AssertTheGeometryHoldsWhatThePlanAnnounces(double[] model, int taps)
    {
        var levels = new HashSet<int>();
        int penumbraColumns = 0;
        double largestStep = 0.0;
        for (int column = 0; column < Width; column++)
        {
            int level = (int)Math.Round(model[column] * taps);
            levels.Add(level);
            if (level > 0 && level < taps)
            {
                penumbraColumns++;
            }

            if (column > 0)
            {
                largestStep = Math.Max(largestStep, Math.Abs(model[column] - model[column - 1]));
            }
        }

        _output.WriteLine($"model, {taps} taps: {levels.Count} levels, largest step between neighbouring columns {largestStep * taps:0.00}/{taps}, penumbra over {penumbraColumns} columns");
        Assert.True(penumbraColumns >= 400, $"{taps} taps: the penumbra covers {penumbraColumns} columns only");
        switch (taps)
        {
            case 12:
                Assert.True(levels.Count <= 13, $"12 taps: {levels.Count} levels");
                Assert.True(largestStep >= 1.0 / 12.0 - 0.005, $"12 taps: the largest step is {largestStep}");
                break;
            case 32:
                Assert.True(levels.Count >= 20, $"32 taps: {levels.Count} levels");
                Assert.True(largestStep <= 3.0 / 32.0 + 1e-9, $"32 taps: the largest step is {largestStep}");
                break;
            default:
                Assert.True(levels.Count >= 40, $"64 taps: {levels.Count} levels");
                Assert.True(largestStep <= 3.0 / 64.0 + 1e-9, $"64 taps: the largest step is {largestStep}");
                break;
        }
    }

    private static int DistinctLevels(float[] image, float low, float high)
    {
        var levels = new HashSet<int>();
        for (int column = 0; column < Width; column++)
        {
            levels.Add((int)Math.Round((image[column] - low) / (high - low) * 64.0 * 4.0));
        }

        return levels.Count;
    }

    /// <summary>
    /// Renders the strip with the <c>ShadowFilterTaps</c> uniform set to <paramref name="taps"/> and returns the red channel of
    /// the first row (the other rows are checked to be the same: the edge is vertical).
    /// </summary>
    private static float[] Render(Shader shader, int taps)
    {
        var device = GpuDeviceHost.Instance.GraphicsDevice;
        var eye = new Vector3(0.0f, 0.0f, 5.0f);
        var context = new RenderContext
        {
            Device = device,
            Frame = new RenderFrame(Matrix.Identity, Matrix.Identity, eye, new Rectangle(0, 0, Width, Height)),
        };

        using var white = new Texture2D(device, 1, 1, false, SurfaceFormat.Color);
        white.SetData(new[] { Color.White });

        Effect effect;
        ShaderWrapper wrapper;
        if (shader == Shader.LitPbr)
        {
            effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.LitPbrContentName);
            wrapper = new ShaderWrapper(effect);
            var material = new LitPbrMaterial
            {
                BaseColor = Color.Black,
                Roughness = 1.0f,
                EmissiveColor = new Vector3(0.5f),
                EmissiveShadowFactor = 1.0f,
            };

            effect.CurrentTechnique = effect.Techniques["LitPbr"]!;
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
                DiffuseColor = Color.White,
                SpecularColor = Vector3.Zero,
            };

            effect.CurrentTechnique = effect.Techniques["LitForward_PixelLighting_Texture"]!;
            material.Bind(wrapper, in context, Matrix.Identity);

            // A white light along -z (the shadow scales it) and no ambient: the pixels in the shadow are black.
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

        // The shadow: the light matrix maps x to ProjectionScale * x, y to ProjectionScale * y and z to z + 0.5 (depth 0.5 at the
        // quad, inside (0, 1)); u = 0.5 + ProjectionScale * x / 2.
        Matrix shadowLight = Matrix.Identity;
        shadowLight.M11 = ProjectionScale;
        shadowLight.M22 = ProjectionScale;
        shadowLight.M43 = 0.5f;
        wrapper.SetParameter("ActiveShadowLightCount", 1.0f);
        wrapper.SetParameter("ShadowedDirectionalLightIndex", 0.0f);
        wrapper.SetParameter("ShadowLightViewProjection", shadowLight);
        wrapper.SetParameter("ShadowDepthBias", 0.0f);
        wrapper.SetParameter("ShadowNormalBias", 0.0f);
        wrapper.SetParameter("ShadowMapTexelSize", new Vector2(1.0f / ShadowMapSize, 1.0f / ShadowMapSize));
        wrapper.SetParameter("ReceiveShadows", 1.0f);
        wrapper.SetParameter("ShadowApplyMode", 0.0f);
        wrapper.SetParameter("ShadowTintColor", Vector3.One);
        wrapper.SetParameter("ShadowFilterRadius", FilterRadius);
        wrapper.SetParameter("ShadowFilterTaps", (float)taps);
        wrapper.SetParameter("ShadowFadeWidth", 0.0f);

        // The left half of the map is occluded, the right half is lit.
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
            new VertexPositionNormalTexture(new Vector3(-1.0f, -1.0f, 0.0f), Vector3.UnitZ, new Vector2(0.0f, 1.0f)),
            new VertexPositionNormalTexture(new Vector3(-1.0f, 1.0f, 0.0f), Vector3.UnitZ, new Vector2(0.0f, 0.0f)),
            new VertexPositionNormalTexture(new Vector3(1.0f, -1.0f, 0.0f), Vector3.UnitZ, new Vector2(1.0f, 1.0f)),
            new VertexPositionNormalTexture(new Vector3(1.0f, 1.0f, 0.0f), Vector3.UnitZ, new Vector2(1.0f, 0.0f)),
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
            var firstRow = new float[Width];
            for (int column = 0; column < Width; column++)
            {
                firstRow[column] = pixels[column].ToVector4().X;
                for (int row = 1; row < Height; row++)
                {
                    Assert.Equal(firstRow[column], pixels[row * Width + column].ToVector4().X);
                }
            }

            return firstRow;
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
