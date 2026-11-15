using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Draw;
using CasaEngine.Framework.Rendering.Shaders;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// Transparent PBR on a real device (plan T5.1, decision P10): the premultiplied output of <c>LitPbr_Transparent</c> attenuates
/// the diffuse parts by the alpha and keeps the specular and the emissive parts whole. Every case renders one pixel (a quad facing
/// the camera, lit head-on by one directional light) into a floating-point target cleared to a known colour, with the blend state
/// of the compiled transparent queue (<see cref="BlendState.AlphaBlend"/>, premultiplied in MonoGame). Skipped without a device.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class LitPbrTransparentGpuTests
{
    private const float Alpha = 64.0f / 255.0f;
    private static readonly Vector4 Background = new(0.2f, 0.4f, 0.6f, 1.0f);
    private static readonly Vector4 Black = new(0.0f, 0.0f, 0.0f, 1.0f);

    [GpuFact]
    public void DiffuseSurface_IsAttenuatedByTheAlpha_OverTheBackground()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var material = new LitPbrMaterial
            {
                BaseColor = new Color(255, 255, 255, 64),
                Metallic = 0.0f,
                Roughness = 1.0f,
            };

            // Diffuse alone: a rough dielectric under a light that faces the surface head-on (the specular part is a few percent).
            Vector3 opaque = Render(material, "LitPbr", Black, lightIntensity: 2.0f);
            Vector3 transparent = Render(material, "LitPbr_Transparent", Background, lightIntensity: 2.0f);

            Vector3 expected = Alpha * opaque + (1.0f - Alpha) * ToVector3(Background);
            AssertWithinTwoPercent(expected, transparent);

            // The proof is discriminating: without the diffuse attenuation the pixel would be far from the expectation.
            Vector3 unattenuated = opaque + (1.0f - Alpha) * ToVector3(Background);
            Assert.True(Math.Abs(unattenuated.X - expected.X) > 0.1f * expected.X);
        });
    }

    [GpuFact]
    public void EmissiveSurface_KeepsItsEmissive_AndBlendsTheRestOfTheBackground()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var emissive = new Vector3(0.3f, 0.2f, 0.1f);
            var material = new LitPbrMaterial
            {
                BaseColor = new Color(0, 0, 0, 64),
                EmissiveColor = emissive,
            };

            // No light and no ambient: only the emissive part is left.
            Vector3 transparent = Render(material, "LitPbr_Transparent", Background, lightIntensity: 0.0f);

            Vector3 expected = emissive + (1.0f - Alpha) * ToVector3(Background);
            AssertWithinTwoPercent(expected, transparent);
        });
    }

    [GpuFact]
    public void SpecularSurface_KeepsItsSpecular_ItIsNotAttenuatedByTheAlpha()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            // A metal has no diffuse albedo and no diffuse ambient: only the highlight is left, and the light and the eye
            // are on the surface normal, so the highlight covers the measured pixel.
            var material = new LitPbrMaterial
            {
                BaseColor = new Color(255, 255, 255, 64),
                Metallic = 1.0f,
                Roughness = 0.5f,
            };

            Vector3 opaque = Render(material, "LitPbr", Black, lightIntensity: 1.0f);
            Vector3 transparent = Render(material, "LitPbr_Transparent", Background, lightIntensity: 1.0f);

            Assert.True(opaque.X > 0.5f, $"The highlight must be bright for the proof to mean something (got {opaque.X}).");

            Vector3 expected = opaque + (1.0f - Alpha) * ToVector3(Background);
            AssertWithinTwoPercent(expected, transparent);

            // A specular multiplied by the alpha would fall short by (1 - alpha) of the highlight.
            Vector3 attenuated = Alpha * opaque + (1.0f - Alpha) * ToVector3(Background);
            Assert.True(Math.Abs(attenuated.X - expected.X) > 0.1f * expected.X);
        });
    }

    [GpuFact]
    public void ACompiledTransparentMaterial_GetsTheBlendedStateAndTheTransparentTechnique_AfterTheRenderPassSelection()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var device = GpuDeviceHost.Instance.GraphicsDevice;
            var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.LitPbrContentName);
            var wrapper = new ShaderWrapper(effect);

            using var shaderManager = new ShaderManager(new AssetContentManager());
            shaderManager.RegisterShader(EffectiveShaderResolver.LitPbrShaderId, wrapper);
            var library = new ShaderVariantLibrary(shaderManager);
            library.RegisterTechniqueAliases(EffectiveShaderResolver.LitPbrShaderId, ShaderVariantLibrary.BuildLitPbrAliases());
            var selector = new RenderShaderSelector(wrapper, shaderManager, library);
            selector.RegisterShader(EffectiveShaderResolver.LitPbrShaderId, wrapper);

            var previousBlend = device.BlendState;
            var previousDepth = device.DepthStencilState;
            var previousRasterizer = device.RasterizerState;
            try
            {
                // Alpha below 255 : transparent queue, AlphaBlend, depth read.
                AssertCompiledDraw(
                    device, selector,
                    CreateAsset(RenderQueue.Opaque, new Color(255, 200, 180, 96), withNormal: false, withBase: false),
                    expectedQueue: RenderQueue.Transparent,
                    expectedTechnique: "LitPbr_Transparent");

                // With a normal texture : the normal map transparent technique.
                AssertCompiledDraw(
                    device, selector,
                    CreateAsset(RenderQueue.Opaque, new Color(255, 200, 180, 96), withNormal: true, withBase: false),
                    expectedQueue: RenderQueue.Transparent,
                    expectedTechnique: "LitPbr_NormalMap_Transparent");

                // Alpha-test queue keeps the alpha-test technique, whatever the alpha of the base colour.
                AssertCompiledDraw(
                    device, selector,
                    CreateAsset(RenderQueue.AlphaTest, new Color(255, 200, 180, 96), withNormal: false, withBase: true),
                    expectedQueue: RenderQueue.AlphaTest,
                    expectedTechnique: "LitPbr_AlphaTest");

                // An opaque material keeps the opaque technique.
                AssertCompiledDraw(
                    device, selector,
                    CreateAsset(RenderQueue.Opaque, Color.White, withNormal: false, withBase: false),
                    expectedQueue: RenderQueue.Opaque,
                    expectedTechnique: "LitPbr");
            }
            finally
            {
                device.BlendState = previousBlend;
                device.DepthStencilState = previousDepth;
                device.RasterizerState = previousRasterizer;
            }
        });
    }

    // The path of RenderPass.DrawItem up to the draw: the render states from the compiled snapshot (RenderStateCache), the shader
    // and the technique the selector leaves, then the material renaming the technique when it must.
    private static void AssertCompiledDraw(
        GraphicsDevice device,
        RenderShaderSelector selector,
        MaterialAsset asset,
        RenderQueue expectedQueue,
        string expectedTechnique)
    {
        var compiler = new MaterialCompiler();
        var contentManager = new AssetContentManager();
        var item = new RenderItem
        {
            Material = compiler.CompileRuntimeMaterial(asset, contentManager),
            CompiledMaterial = compiler.Compile(asset, contentManager),
        };

        Assert.Equal(expectedQueue, item.Queue);

        var stateCache = new RenderStateCache();
        stateCache.Apply(device, in item);

        var resolved = selector.Resolve(in item);
        var context = new RenderContext { Device = device };
        if (item.Material.RequiresMaterialTechniqueSelection(resolved.TechniqueSelectedBySelector, in context, item.Features))
        {
            item.Material.SelectTechnique(resolved.Shader, in context, item.Features);
        }

        Assert.Equal(expectedTechnique, resolved.Shader.Effect.CurrentTechnique.Name);

        if (expectedQueue == RenderQueue.Transparent)
        {
            Assert.Same(BlendState.AlphaBlend, device.BlendState);
            Assert.Same(DepthStencilState.DepthRead, device.DepthStencilState);
            Assert.False(device.DepthStencilState.DepthBufferWriteEnable);
            Assert.True(device.DepthStencilState.DepthBufferEnable);
        }
    }

    private static MaterialAsset CreateAsset(RenderQueue queue, Color baseColor, bool withNormal, bool withBase)
    {
        var asset = new MaterialAsset("lit-pbr") { Queue = queue };
        asset.SetPropertyValue("base_color", MaterialValue.FromColor(baseColor));
        if (withNormal)
        {
            asset.SetPropertyValue("normal_texture", MaterialValue.FromTextureId(Guid.NewGuid()));
        }

        if (withBase)
        {
            asset.SetPropertyValue("base_color_texture", MaterialValue.FromTextureId(Guid.NewGuid()));
        }

        return asset;
    }

    private static void AssertWithinTwoPercent(Vector3 expected, Vector3 actual)
    {
        Assert.True(Math.Abs(actual.X - expected.X) <= 0.02f * expected.X, $"R: expected {expected.X} got {actual.X}");
        Assert.True(Math.Abs(actual.Y - expected.Y) <= 0.02f * expected.Y, $"G: expected {expected.Y} got {actual.Y}");
        Assert.True(Math.Abs(actual.Z - expected.Z) <= 0.02f * expected.Z, $"B: expected {expected.Z} got {actual.Z}");
    }

    private static Vector3 ToVector3(Vector4 value) => new(value.X, value.Y, value.Z);

    /// <summary>
    /// Draws one quad facing the camera into a 1x1 floating-point target cleared to <paramref name="background"/>, with
    /// <see cref="BlendState.AlphaBlend"/>, and returns the colour of the pixel. One directional light along the view axis
    /// (zero intensity for none), no ambient, no environment, linear pipeline off.
    /// </summary>
    private static Vector3 Render(LitPbrMaterial material, string technique, Vector4 background, float lightIntensity)
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
        shader.SetParameter("DirLight0Direction", new Vector3(0.0f, 0.0f, -1.0f));
        shader.SetParameter("DirLight0DiffuseColor", new Vector3(lightIntensity));
        shader.SetParameter("DirLight0SpecularColor", Vector3.Zero);
        shader.SetParameter("AmbientColor", Vector3.Zero);
        shader.SetParameter("UseFlatAmbientSource", 1.0f);
        shader.SetParameter("HasEnvironmentCubeTexture", 0.0f);
        shader.SetParameter("ActiveShadowLightCount", 0.0f);
        shader.SetParameter("ReceiveShadows", 0.0f);
        shader.SetParameter("ShadowApplyMode", 0.0f);
        shader.SetParameter("ColorPipelineIsLinear", 0.0f);

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
            device.Clear(ClearOptions.Target, background, 0.0f, 0);
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
