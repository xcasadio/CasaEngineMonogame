using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.Shaders;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using Xunit;
using Xunit.Abstractions;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// The clear coat of <c>LitPbr.fx</c> on a real device (ADR-0115, plan T2.1). Every case renders one pixel (a quad facing the camera
/// at 5 units on +Z) into a floating-point target with one directional light, no flat ambient unless the case asks for it, no
/// shadow, and the linear colour pipeline off. The expected values are computed from the closed form of the header of Pbr.fxh.
/// Skipped where no device can be created.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class LitPbrClearCoatGpuTests
{
    private readonly ITestOutputHelper _output;

    public LitPbrClearCoatGpuTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly Vector3 AlongTheAxis = new(0.0f, 0.0f, -1.0f);
    private static readonly Vector3 FromTheSide = Vector3.Normalize(new Vector3(-1.0f, 0.0f, -1.0f));

    // Rough black dielectric head-on, light on the axis: the base specular is 0.04 * (1 / pi) * 0.25 = 0.003183 and the coat lobe,
    // at roughness 0.1, is (1 / (pi * 1e-4)) * 0.25 * 0.04 = 31.83.
    private const float BaseSpecular = 0.04f * 0.25f / MathF.PI;
    private const float FullCoatLobe = 0.04f * 0.25f / (MathF.PI * 1.0e-4f);

    [GpuFact]
    public void ClearCoatZero_RendersLikeAMaterialWithoutACoat_WhateverTheCoatRoughness()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var plain = new LitPbrMaterial { BaseColor = Color.Black, Roughness = 1.0f };
            var zeroWithRough = new LitPbrMaterial { BaseColor = Color.Black, Roughness = 1.0f, ClearCoat = 0.0f, ClearCoatRoughness = 0.9f };

            Vector3 reference = Render(plain, AlongTheAxis, 1.0f);
            Vector3 zero = Render(zeroWithRough, AlongTheAxis, 1.0f);

            // (a) identical pixels, and equal to the base lobe alone, computed by hand: the coat adds nothing.
            _output.WriteLine($"(a) reference {reference.X:G6}, ClearCoat 0 with coat roughness 0.9 {zero.X:G6}, hand-computed base {BaseSpecular:G6}");
            Assert.Equal(reference, zero);
            Assert.InRange(reference.X, BaseSpecular * 0.98f, BaseSpecular * 1.02f);
        });
    }

    [GpuFact]
    public void FullClearCoat_OnARoughBlackDielectric_AddsAMirrorHighlightFarAboveTheBase()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            Vector3 none = Render(Coated(0.0f), AlongTheAxis, 1.0f);
            Vector3 full = Render(Coated(1.0f), AlongTheAxis, 1.0f);

            // (b) at least a factor 5; the closed form gives 31.83 + 0.96 * 0.003183 over 0.003183.
            _output.WriteLine($"(b) none {none.X:G6}, full {full.X:G6}, ratio {full.X / none.X:G6}, closed form {FullCoatLobe + 0.96f * BaseSpecular:G6}");
            Assert.True(full.X >= 5.0f * none.X, $"Full coat {full.X} must be at least 5 times {none.X}.");
            Assert.InRange(full.X, (FullCoatLobe + 0.96f * BaseSpecular) * 0.97f, (FullCoatLobe + 0.96f * BaseSpecular) * 1.03f);
        });
    }

    [GpuFact]
    public void HalfClearCoat_IsBetweenNoneAndFull_AndTheLobeGrowsWithTheCoat()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            float none = Render(Coated(0.0f), AlongTheAxis, 1.0f).X;
            float quarter = Render(Coated(0.25f), AlongTheAxis, 1.0f).X;
            float half = Render(Coated(0.5f), AlongTheAxis, 1.0f).X;
            float threeQuarters = Render(Coated(0.75f), AlongTheAxis, 1.0f).X;
            float full = Render(Coated(1.0f), AlongTheAxis, 1.0f).X;

            // (c) strictly between (a) and (b), and strictly increasing with the coat.
            _output.WriteLine($"(c) 0: {none:G6}, 0.25: {quarter:G6}, 0.5: {half:G6}, 0.75: {threeQuarters:G6}, 1: {full:G6}");
            Assert.True(none < quarter && quarter < half && half < threeQuarters && threeQuarters < full);
            Assert.InRange(half, none, full);
            Assert.InRange(half, (0.5f * FullCoatLobe) * 0.97f, (0.5f * FullCoatLobe + BaseSpecular) * 1.03f);
        });
    }

    [GpuFact]
    public void FullClearCoat_OnAWhiteDiffuseSeenHeadOn_LowersTheDiffuseByAboutFourPercent()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            // (d) The light comes from 45 degrees, the eye is on the normal: the coat highlight is far from the measured pixel,
            // so the ratio is the base attenuation 1 - Fc with Fc = 0.04 (VoH = cos 22.5 degrees).
            var bare = new LitPbrMaterial { BaseColor = Color.White, Roughness = 1.0f };
            var coated = new LitPbrMaterial { BaseColor = Color.White, Roughness = 1.0f, ClearCoat = 1.0f, ClearCoatRoughness = 0.1f };

            float without = Render(bare, FromTheSide, 2.0f).X;
            float with = Render(coated, FromTheSide, 2.0f).X;

            float ratio = with / without;
            _output.WriteLine($"(d) diffuse without {without:G6}, with {with:G6}, ratio {ratio:G6}");
            Assert.InRange(ratio, 0.94f, 0.98f);
        });
    }

    [GpuFact]
    public void FlatAmbient_IsLoweredByAboutFourPercentUnderAFullCoat()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            // (g) HasIbl = 0 (the flat ambient), no light, white diffuse seen head-on: 1 - ClearCoat * Schlick(0.04, 1) = 0.96.
            var bare = new LitPbrMaterial { BaseColor = Color.White, Roughness = 1.0f };
            var coated = new LitPbrMaterial { BaseColor = Color.White, Roughness = 1.0f, ClearCoat = 1.0f, ClearCoatRoughness = 0.1f };

            float without = Render(bare, AlongTheAxis, 0.0f, ambient: new Vector3(0.5f)).X;
            float with = Render(coated, AlongTheAxis, 0.0f, ambient: new Vector3(0.5f)).X;

            _output.WriteLine($"(g) flat ambient without {without:G6}, with {with:G6}, ratio {with / without:G6}");
            Assert.InRange(without, 0.49f, 0.51f);
            Assert.InRange(with / without, 0.94f, 0.98f);
        });
    }

    [GpuFact]
    public void ImageBasedLighting_AddsTheCoatReflectionOfAUniformEnvironment_ByRadianceTimesTheBrdfEnergy()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var device = GpuDeviceHost.Instance.GraphicsDevice;
            var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.LitPbrContentName);
            var shader = new ShaderWrapper(effect);
            var generatorEffect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.IblConvolutionContentName);
            using var generator = new IblCubeGenerator(device, generatorEffect);
            using var cache = new IblCache(device);

            const float radiance = 0.5f;
            using var source = CreateUniformCube(8, new Vector4(radiance, radiance, radiance, 1.0f));
            var environment = new ResolvedEnvironmentSettings { SpecularEnvironmentCubemap = source, SpecularEnvironmentCubemapIsLinear = true };

            // The first binding registers the request, the flush generates the textures, the next binding binds them.
            EnvironmentShaderBinder.BindImageBasedLighting(shader, in environment, null, cache);
            cache.Flush(generator);

            void BindIbl(ShaderWrapper wrapper)
            {
                EnvironmentShaderBinder.BindImageBasedLighting(wrapper, in environment, null, cache);
                wrapper.SetParameter("ColorPipelineIsLinear", 1.0f);
                wrapper.SetParameter("EnvironmentSpecularIntensity", 1.0f);
                wrapper.SetParameter("EnvironmentAmbientColor", Vector3.One);
            }

            // Rough black dielectric, no direct light, head-on: only the ambient reflection of the base is left, and the coat adds
            // radiance * E_c and removes ClearCoat * E_c of the base reflection.
            var bare = new LitPbrMaterial { BaseColor = Color.Black, Roughness = 1.0f };
            var coated = new LitPbrMaterial { BaseColor = Color.Black, Roughness = 1.0f, ClearCoat = 1.0f, ClearCoatRoughness = 0.1f };

            float without = Render(bare, AlongTheAxis, 0.0f, bindExtra: BindIbl).X;
            float with = Render(coated, AlongTheAxis, 0.0f, bindExtra: BindIbl).X;
            Assert.True(without > 0.0f, "The base must reflect the environment for the proof to mean something.");

            var coatBrdf = BrdfLookupTable.Integrate(1.0f, 0.1f);
            var baseBrdf = BrdfLookupTable.Integrate(1.0f, 1.0f);
            float coatEnergy = 0.04f * coatBrdf.X + coatBrdf.Y;
            float baseEnergy = 0.04f * baseBrdf.X + baseBrdf.Y;

            // (e) with - without = radiance * E_c * (1 - E_b): the coat reflection, minus the share of the base it covers.
            float expected = radiance * coatEnergy * (1.0f - baseEnergy);
            _output.WriteLine($"(e) radiance {radiance}, E_c {coatEnergy:G6}, E_b {baseEnergy:G6}, base {without:G6}, coat {with:G6}, delta {with - without:G6}, expected {expected:G6}, radiance*E_c {radiance * coatEnergy:G6}");
            Assert.InRange(with - without, expected * 0.9f, expected * 1.1f);
            Assert.InRange(without, radiance * baseEnergy * 0.9f, radiance * baseEnergy * 1.1f);

        });
    }

    [GpuFact]
    public void TransparentSurface_KeepsTheCoatLobeWhole_ItIsNotScaledByTheAlpha()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            const float alpha = 64.0f / 255.0f;
            var opaque = new LitPbrMaterial { BaseColor = Color.Black, Roughness = 1.0f, ClearCoat = 1.0f, ClearCoatRoughness = 0.1f };
            var transparent = new LitPbrMaterial { BaseColor = new Color(0, 0, 0, 64), Roughness = 1.0f, ClearCoat = 1.0f, ClearCoatRoughness = 0.1f };

            float solid = Render(opaque, AlongTheAxis, 1.0f).X;
            float blended = Render(transparent, AlongTheAxis, 1.0f, technique: "LitPbr_Transparent").X;

            _output.WriteLine($"(transparent) opaque {solid:G6}, blended alpha 64/255 {blended:G6}");
            // Black background, premultiplied output: the specular (the coat lobe included) arrives whole.
            Assert.InRange(blended, solid * 0.98f, solid * 1.02f);
            Assert.True(blended > alpha * solid * 2.0f);
        });
    }

    private static LitPbrMaterial Coated(float clearCoat)
        => new() { BaseColor = Color.Black, Roughness = 1.0f, ClearCoat = clearCoat, ClearCoatRoughness = 0.1f };

    private static TextureCube CreateUniformCube(int size, Vector4 color)
    {
        var cube = new TextureCube(GpuDeviceHost.Instance.GraphicsDevice, size, false, SurfaceFormat.Vector4);
        var texels = new Vector4[size * size];
        Array.Fill(texels, color);
        foreach (CubeMapFace face in new[]
                 {
                     CubeMapFace.PositiveX, CubeMapFace.NegativeX, CubeMapFace.PositiveY,
                     CubeMapFace.NegativeY, CubeMapFace.PositiveZ, CubeMapFace.NegativeZ,
                 })
        {
            cube.SetData(face, texels);
        }

        return cube;
    }

    /// <summary>
    /// One quad facing the camera (eye at (0, 0, 5)) into a 1x1 half-float target cleared to black, lit by one directional light of
    /// <paramref name="lightIntensity"/> travelling along <paramref name="lightDirection"/> (none at zero), with the flat ambient
    /// <paramref name="ambient"/>, and returns the colour of the pixel.
    /// </summary>
    private static Vector3 Render(
        LitPbrMaterial material,
        Vector3 lightDirection,
        float lightIntensity,
        Vector3? ambient = null,
        string technique = "LitPbr",
        Action<ShaderWrapper>? bindExtra = null)
    {
        var device = GpuDeviceHost.Instance.GraphicsDevice;
        var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.LitPbrContentName);
        var shader = new ShaderWrapper(effect);

        var eye = new Vector3(0.0f, 0.0f, 5.0f);
        var context = new RenderContext
        {
            Device = device,
            Frame = new RenderFrame(Matrix.Identity, Matrix.Identity, eye, new Rectangle(0, 0, 1, 1)),
        };

        effect.CurrentTechnique = effect.Techniques[technique]!;
        material.Bind(shader, in context, Matrix.Identity);

        shader.SetParameter("ActiveDirectionalLightCount", lightIntensity > 0.0f ? 1.0f : 0.0f);
        shader.SetParameter("ActivePointLightCount", 0.0f);
        shader.SetParameter("ActiveSpotLightCount", 0.0f);
        shader.SetParameter("DirLight0Direction", lightDirection);
        shader.SetParameter("DirLight0DiffuseColor", new Vector3(lightIntensity));
        shader.SetParameter("DirLight0SpecularColor", Vector3.Zero);
        shader.SetParameter("AmbientColor", ambient ?? Vector3.Zero);
        shader.SetParameter("UseFlatAmbientSource", 1.0f);
        shader.SetParameter("HasEnvironmentCubeTexture", 0.0f);
        shader.SetParameter("HasIbl", 0.0f);
        shader.SetParameter("ActiveShadowLightCount", 0.0f);
        shader.SetParameter("ReceiveShadows", 0.0f);
        shader.SetParameter("ShadowApplyMode", 0.0f);
        shader.SetParameter("ColorPipelineIsLinear", 0.0f);
        shader.SetParameter("EnvironmentSpecularIntensity", 0.0f);
        bindExtra?.Invoke(shader);

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
