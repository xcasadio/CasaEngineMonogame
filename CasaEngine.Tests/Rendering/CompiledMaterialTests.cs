
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class CompiledMaterialTests
{
    [Fact]
    public void Constructor_StoresResolvedRuntimeDataAndLookups()
    {
        var sourceAssetId = Guid.NewGuid();
        var shaderReference = new EffectiveShaderReference(Guid.NewGuid(), "Shaders\\Test");
        var compiledMaterial = new CompiledMaterial(
            definitionId: "unlit-texture",
            effectiveShader: shaderReference,
            properties: new[]
            {
                new KeyValuePair<string, MaterialValue>("alpha", MaterialValue.FromFloat(0.5f)),
                new KeyValuePair<string, MaterialValue>("tint_color", MaterialValue.FromColor(Color.White)),
            },
            textures: new[]
            {
                new KeyValuePair<string, Texture2D?>("base_color_texture", null),
            },
            sourceAssetId: sourceAssetId,
            name: "Test Material",
            features: ShaderFeature.Transparent,
            blendState: BlendState.AlphaBlend,
            depthStencilState: DepthStencilState.DepthRead,
            rasterizerState: RasterizerState.CullNone,
            samplerState: SamplerState.LinearWrap,
            isTransparent: true,
            queue: RenderQueue.Transparent,
            castShadows: false,
            receiveShadows: false);

        Assert.Equal(sourceAssetId, compiledMaterial.SourceAssetId);
        Assert.Equal("Test Material", compiledMaterial.Name);
        Assert.Equal("unlit-texture", compiledMaterial.DefinitionId);
        Assert.Equal(shaderReference, compiledMaterial.EffectiveShader);
        Assert.Equal(ShaderFeature.Transparent, compiledMaterial.Features);
        Assert.Same(BlendState.AlphaBlend, compiledMaterial.BlendState);
        Assert.Same(DepthStencilState.DepthRead, compiledMaterial.DepthStencilState);
        Assert.Same(RasterizerState.CullNone, compiledMaterial.RasterizerState);
        Assert.Same(SamplerState.LinearWrap, compiledMaterial.SamplerState);
        Assert.True(compiledMaterial.IsTransparent);
        Assert.Equal(RenderQueue.Transparent, compiledMaterial.Queue);
        Assert.False(compiledMaterial.CastShadows);
        Assert.False(compiledMaterial.ReceiveShadows);
        Assert.True(compiledMaterial.TryGetPropertyValue("ALPHA", out var alphaValue));
        Assert.True(alphaValue.TryGetFloat(out var alpha));
        Assert.Equal(0.5f, alpha);
        Assert.True(compiledMaterial.TryGetTexture("base_color_texture", out var texture));
        Assert.Null(texture);
        Assert.True(compiledMaterial.TryGetTextureBinding("base_color_texture", out var textureBinding));
        Assert.Equal(CompiledMaterialTextureBindingKind.Texture2D, textureBinding.Kind);
    }

    [Fact]
    public void Constructor_UsesDefaultPreparedStates_WhenOverridesAreOmitted()
    {
        var compiledMaterial = new CompiledMaterial(
            definitionId: "lit-diffuse",
            effectiveShader: new EffectiveShaderReference(Guid.NewGuid()),
            properties: Array.Empty<KeyValuePair<string, MaterialValue>>());

        Assert.Same(BlendState.Opaque, compiledMaterial.BlendState);
        Assert.Same(DepthStencilState.Default, compiledMaterial.DepthStencilState);
        Assert.Same(RasterizerState.CullCounterClockwise, compiledMaterial.RasterizerState);
        Assert.Same(SamplerState.AnisotropicClamp, compiledMaterial.SamplerState);
    }

    [Fact]
    public void Constructor_RejectsDuplicatePropertyKeysIgnoringCase()
    {
        var properties = new[]
        {
            new KeyValuePair<string, MaterialValue>("alpha", MaterialValue.FromFloat(1.0f)),
            new KeyValuePair<string, MaterialValue>("ALPHA", MaterialValue.FromFloat(0.5f)),
        };

        Assert.Throws<ArgumentException>(() => new CompiledMaterial(
            definitionId: "unlit-texture",
            effectiveShader: new EffectiveShaderReference(Guid.NewGuid()),
            properties: properties));
    }

    [Fact]
    public void Constructor_RejectsDuplicateTextureKeysIgnoringCase()
    {
        var textures = new[]
        {
            new KeyValuePair<string, Texture2D?>("base_color_texture", null),
            new KeyValuePair<string, Texture2D?>("BASE_COLOR_TEXTURE", null),
        };

        Assert.Throws<ArgumentException>(() => new CompiledMaterial(
            definitionId: "unlit-texture",
            effectiveShader: new EffectiveShaderReference(Guid.NewGuid()),
            properties: Array.Empty<KeyValuePair<string, MaterialValue>>(),
            textures: textures));
    }

    [Fact]
    public void Constructor_RejectsDuplicateTextureBindingKeysIgnoringCase()
    {
        var textureBindings = new[]
        {
            new KeyValuePair<string, CompiledMaterialTextureBinding>(
                "reflection_texture",
                new CompiledMaterialTextureBinding(Guid.NewGuid(), CompiledMaterialTextureBindingKind.TextureCube)),
            new KeyValuePair<string, CompiledMaterialTextureBinding>(
                "REFLECTION_TEXTURE",
                new CompiledMaterialTextureBinding(Guid.NewGuid(), CompiledMaterialTextureBindingKind.TextureCube)),
        };

        Assert.Throws<ArgumentException>(() => new CompiledMaterial(
            definitionId: "lit-diffuse",
            effectiveShader: new EffectiveShaderReference(Guid.NewGuid()),
            properties: Array.Empty<KeyValuePair<string, MaterialValue>>(),
            textureBindings: textureBindings));
    }

    [Fact]
    public void Constructor_RejectsMissingDefinitionIdOrResolvedShaderId()
    {
        Assert.Throws<ArgumentException>(() => new CompiledMaterial(
            definitionId: string.Empty,
            effectiveShader: new EffectiveShaderReference(Guid.NewGuid()),
            properties: Array.Empty<KeyValuePair<string, MaterialValue>>()));

        Assert.Throws<ArgumentException>(() => new CompiledMaterial(
            definitionId: "lit-diffuse",
            effectiveShader: new EffectiveShaderReference(Guid.Empty),
            properties: Array.Empty<KeyValuePair<string, MaterialValue>>()));
    }
}