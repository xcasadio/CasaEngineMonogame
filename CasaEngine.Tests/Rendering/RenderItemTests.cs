
using CasaEngine.Framework.Rendering.Draw;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class RenderItemTests
{
    [Fact]
    public void Accessors_PreferCompiledMaterial_WhenPresent()
    {
        var runtimeMaterial = new LitDiffuseMaterial
        {
            Queue = RenderQueue.Opaque,
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
            queue: RenderQueue.Transparent);

        var item = new RenderItem
        {
            Material = runtimeMaterial,
            CompiledMaterial = compiledMaterial,
            EffectiveShaderId = Guid.NewGuid(),
            Features = ShaderFeature.None,
        };

        Assert.Equal(compiledMaterial.EffectiveShader.ShaderId, item.EffectiveShaderId);
        Assert.Equal(compiledMaterial.Features, item.Features);
        Assert.Equal(RenderQueue.Transparent, item.Queue);
    }

    [Fact]
    public void Accessors_FallBackToRuntimeMaterial_WhenNoCompiledMaterialExists()
    {
        var runtimeMaterial = new LitDiffuseMaterial
        {
            Queue = RenderQueue.AlphaTest,
        };
        var effectiveShaderId = Guid.NewGuid();

        var item = new RenderItem
        {
            Material = runtimeMaterial,
            EffectiveShaderId = effectiveShaderId,
            Features = ShaderFeature.Emissive,
        };

        Assert.Equal(effectiveShaderId, item.EffectiveShaderId);
        Assert.Equal(ShaderFeature.Emissive, item.Features);
        Assert.Equal(RenderQueue.AlphaTest, item.Queue);
    }
}