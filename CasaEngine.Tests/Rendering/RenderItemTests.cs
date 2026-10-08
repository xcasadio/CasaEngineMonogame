
using CasaEngine.Framework.Rendering.Draw;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class RenderItemTests
{
    [Fact]
    public void Accessors_PreferCompiledMaterial_WhenPresent_ExceptFeatures()
    {
        var runtimeMaterial = new LitDiffuseMaterial
        {
            Queue = RenderQueue.Opaque,
            CastShadows = false,
            ReceiveShadows = true,
        };
        var compiledMaterial = new CompiledMaterial(
            definitionId: "lit-diffuse",
            effectiveShader: new EffectiveShaderReference(Guid.NewGuid()),
            properties: Array.Empty<KeyValuePair<string, MaterialValue>>(),
            sourceAssetId: Guid.NewGuid(),
            features: ShaderFeature.Reflection,
            blendState: BlendState.AlphaBlend,
            depthStencilState: DepthStencilState.DepthRead,
            rasterizerState: RasterizerState.CullNone,
            samplerState: SamplerState.PointClamp,
            queue: RenderQueue.Transparent,
            castShadows: true,
            receiveShadows: false);

        var item = new RenderItem
        {
            Material = runtimeMaterial,
            CompiledMaterial = compiledMaterial,
            ComponentCastShadows = true,
            ComponentReceiveShadows = true,
            EffectiveShaderId = Guid.NewGuid(),
            Features = ShaderFeature.None,
        };

        Assert.Equal(compiledMaterial.EffectiveShader.ShaderId, item.EffectiveShaderId);
        // Features is the value the producer set (resolved against the drawn mesh), not the compiled snapshot.
        Assert.Equal(ShaderFeature.None, item.Features);
        Assert.Equal(RenderQueue.Transparent, item.Queue);
        Assert.True(item.EffectiveCastShadows);
        Assert.False(item.EffectiveReceiveShadows);
    }

    [Fact]
    public void Accessors_FallBackToRuntimeMaterial_WhenNoCompiledMaterialExists()
    {
        var runtimeMaterial = new LitDiffuseMaterial
        {
            Queue = RenderQueue.AlphaTest,
            CastShadows = false,
            ReceiveShadows = true,
        };
        var effectiveShaderId = Guid.NewGuid();

        var item = new RenderItem
        {
            Material = runtimeMaterial,
            ComponentCastShadows = true,
            ComponentReceiveShadows = true,
            EffectiveShaderId = effectiveShaderId,
            Features = ShaderFeature.Emissive,
        };

        Assert.Equal(effectiveShaderId, item.EffectiveShaderId);
        Assert.Equal(ShaderFeature.Emissive, item.Features);
        Assert.Equal(RenderQueue.AlphaTest, item.Queue);
        Assert.False(item.EffectiveCastShadows);
        Assert.True(item.EffectiveReceiveShadows);
    }

    [Fact]
    public void EffectiveShadowAccessors_RequireComponentFlags()
    {
        var item = new RenderItem
        {
            Material = new LitDiffuseMaterial
            {
                CastShadows = true,
                ReceiveShadows = true,
            },
            ComponentCastShadows = false,
            ComponentReceiveShadows = false,
        };

        Assert.False(item.EffectiveCastShadows);
        Assert.False(item.EffectiveReceiveShadows);
    }
}