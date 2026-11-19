using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using Xunit;
using Xunit.Abstractions;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// <c>emissive_shadow_factor</c> of <c>LitPbr.fx</c> on a real device (ADR-0126). Every case renders one pixel (a quad facing the
/// camera at 5 units on +Z) of a black dielectric into a floating-point target. The only thing that can colour the pixel is the
/// emissive: the ambient is zero, there is no IBL, and the one active directional light, which sits at
/// <c>ShadowedDirectionalLightIndex</c> (without a directional light at that index the shadow factor is always 1), has black
/// diffuse and specular colours. The shadow map is a 4x4 single-channel texture that is either 0 everywhere (the quad is fully
/// occluded, visibility 0) or 1 everywhere (nothing occludes it, visibility 1); <c>ShadowFadeWidth</c> is 0 and the filter radius is 0.
/// Skipped where no device can be created.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class LitPbrEmissiveShadowGpuTests
{
    private const float Emissive = 0.5f;
    private static readonly Vector3 Tint = new(0.25f, 0.26f, 0.27f);

    private readonly ITestOutputHelper _output;

    public LitPbrEmissiveShadowGpuTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [GpuFact]
    public void DefaultMode_FactorZero_LeavesTheEmissiveUnshadowed_BitForBitLikeAPixelOutsideTheShadow()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            Vector3 inShadow = Render(0.0f, occluded: true, multiplyLitColor: false);
            Vector3 outsideTheShadow = Render(0.0f, occluded: false, multiplyLitColor: false);
            Vector3 notReceiving = Render(1.0f, occluded: true, multiplyLitColor: false, receiveShadows: false);

            _output.WriteLine($"factor 0, visibility 0: {inShadow}; visibility 1: {outsideTheShadow}; factor 1, ReceiveShadows off: {notReceiving}");
            Assert.Equal(new Vector3(Emissive), inShadow);
            Assert.Equal(outsideTheShadow, inShadow);
            Assert.Equal(inShadow, notReceiving);
        });
    }

    [GpuFact]
    public void DefaultMode_FactorOne_InFullShadow_RendersBlack_AndHalfTheFactorGivesHalfTheEmissive()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            Vector3 full = Render(1.0f, occluded: true, multiplyLitColor: false);
            Vector3 half = Render(0.5f, occluded: true, multiplyLitColor: false);

            _output.WriteLine($"default mode, visibility 0: factor 1 -> {full}, factor 0.5 -> {half}");
            Assert.Equal(Vector3.Zero, full);
            Assert.Equal(new Vector3(Emissive * 0.5f), half);
        });
    }

    [GpuFact]
    public void DefaultMode_APixelOutsideTheShadow_KeepsTheWholeEmissive_ForAnyFactor()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            foreach (float factor in new[] { 0.0f, 0.5f, 1.0f })
            {
                Vector3 lit = Render(factor, occluded: false, multiplyLitColor: false);
                _output.WriteLine($"default mode, visibility 1, factor {factor}: {lit}");
                Assert.Equal(new Vector3(Emissive), lit);
            }
        });
    }

    [GpuFact]
    public void MultiplyLitColor_FactorZero_RendersTheEmissive_FactorOneTheEmissiveTimesTheTint_FactorHalfBetween()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            Vector3 zero = Render(0.0f, occluded: true, multiplyLitColor: true);
            Vector3 one = Render(1.0f, occluded: true, multiplyLitColor: true);
            Vector3 half = Render(0.5f, occluded: true, multiplyLitColor: true);

            _output.WriteLine($"MultiplyLitColor tint {Tint}, visibility 0: factor 0 -> {zero}, factor 1 -> {one}, factor 0.5 -> {half}");
            Assert.Equal(new Vector3(Emissive), zero);
            AssertClose(Emissive * Tint, one);
            Assert.NotEqual(zero, one);
            AssertClose(Emissive * Vector3.Lerp(Vector3.One, Tint, 0.5f), half);
            Assert.True(half.X < zero.X && half.X > one.X && half.Y < zero.Y && half.Y > one.Y && half.Z < zero.Z && half.Z > one.Z);
        });
    }

    [GpuFact]
    public void MultiplyLitColor_APixelOutsideTheShadow_KeepsTheWholeEmissive_ForAnyFactor()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            foreach (float factor in new[] { 0.0f, 0.5f, 1.0f })
            {
                Vector3 lit = Render(factor, occluded: false, multiplyLitColor: true);
                _output.WriteLine($"MultiplyLitColor, visibility 1, factor {factor}: {lit}");
                Assert.Equal(new Vector3(Emissive), lit);
            }
        });
    }

    private static void AssertClose(Vector3 expected, Vector3 actual)
    {
        // Half-float target: about 3 decimal digits.
        Assert.InRange(actual.X, expected.X - 0.0015f, expected.X + 0.0015f);
        Assert.InRange(actual.Y, expected.Y - 0.0015f, expected.Y + 0.0015f);
        Assert.InRange(actual.Z, expected.Z - 0.0015f, expected.Z + 0.0015f);
    }

    /// <summary>Renders the quad with an emissive of <see cref="Emissive"/> and returns the colour of the pixel.</summary>
    private static Vector3 Render(float emissiveShadowFactor, bool occluded, bool multiplyLitColor, bool receiveShadows = true)
    {
        var device = GpuDeviceHost.Instance.GraphicsDevice;
        var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.LitPbrContentName);
        var shader = new ShaderWrapper(effect);

        var material = new LitPbrMaterial
        {
            BaseColor = Color.Black,
            Roughness = 1.0f,
            EmissiveColor = new Vector3(Emissive),
            EmissiveShadowFactor = emissiveShadowFactor,
        };

        var eye = new Vector3(0.0f, 0.0f, 5.0f);
        var context = new RenderContext
        {
            Device = device,
            Frame = new RenderFrame(Matrix.Identity, Matrix.Identity, eye, new Rectangle(0, 0, 1, 1)),
        };

        effect.CurrentTechnique = effect.Techniques["LitPbr"]!;
        material.Bind(shader, in context, Matrix.Identity);

        // One directional light, black, at the shadowed index: the only light, so it is the one the shadow factor belongs to.
        shader.SetParameter("ActiveDirectionalLightCount", 1.0f);
        shader.SetParameter("ActivePointLightCount", 0.0f);
        shader.SetParameter("ActiveSpotLightCount", 0.0f);
        shader.SetParameter("DirLight0Direction", new Vector3(0.0f, 0.0f, -1.0f));
        shader.SetParameter("DirLight0DiffuseColor", Vector3.Zero);
        shader.SetParameter("DirLight0SpecularColor", Vector3.Zero);
        shader.SetParameter("AmbientColor", Vector3.Zero);
        shader.SetParameter("UseFlatAmbientSource", 1.0f);
        shader.SetParameter("HasEnvironmentCubeTexture", 0.0f);
        shader.SetParameter("HasIbl", 0.0f);
        shader.SetParameter("ColorPipelineIsLinear", 0.0f);
        shader.SetParameter("EnvironmentSpecularIntensity", 0.0f);

        // The shadow: the light projection puts the quad at depth 0.5 (0.75 once remapped on OpenGL), inside the map, which holds
        // 0 (the occluder is nearer than anything) or 1 (nothing is nearer).
        shader.SetParameter("ActiveShadowLightCount", 1.0f);
        shader.SetParameter("ShadowedDirectionalLightIndex", 0.0f);
        shader.SetParameter("ShadowLightViewProjection", Matrix.CreateTranslation(0.0f, 0.0f, 0.5f));
        shader.SetParameter("ShadowDepthBias", 0.0f);
        shader.SetParameter("ShadowNormalBias", 0.0f);
        shader.SetParameter("ShadowMapTexelSize", new Vector2(0.25f, 0.25f));
        shader.SetParameter("ReceiveShadows", receiveShadows ? 1.0f : 0.0f);
        shader.SetParameter("ShadowApplyMode", multiplyLitColor ? 1.0f : 0.0f);
        shader.SetParameter("ShadowTintColor", Tint);
        shader.SetParameter("ShadowFilterRadius", 0.0f);
        shader.SetParameter("ShadowFadeWidth", 0.0f);

        using var shadowMap = new Texture2D(device, 4, 4, false, SurfaceFormat.Single);
        var depths = new float[16];
        Array.Fill(depths, occluded ? 0.0f : 1.0f);
        shadowMap.SetData(depths);
        shader.SetTextureParameter("ShadowMapTexture", shadowMap, null);

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
        using var target = new RenderTarget2D(device, 1, 1, false, SurfaceFormat.HalfVector4, DepthFormat.None);
        try
        {
            device.SetRenderTarget(target);
            device.Clear(ClearOptions.Target, Vector4.UnitW, 0.0f, 0);
            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;

            effect.CurrentTechnique.Passes[0].Apply();
            device.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices, 0, 2);

            device.SetRenderTarget(null);
            var pixel = new HalfVector4[1];
            target.GetData(pixel);
            Vector4 value = pixel[0].ToVector4();
            return new Vector3(value.X, value.Y, value.Z);
        }
        finally
        {
            device.SetRenderTargets(previousTargets);
            device.Viewport = previousViewport;
            device.BlendState = previousBlend;
            device.DepthStencilState = previousDepth;
            device.RasterizerState = previousRasterizer;
        }
    }
}
