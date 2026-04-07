using CasaEngine.Framework.Materials;
using CasaEngine.Framework.Rendering.Shaders;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class EffectiveShaderResolverTests
{
    [Fact]
    public void Resolve_ReturnsExplicitShaderAsset_WhenMaterialDefinesOne()
    {
        var shaderAssetId = Guid.NewGuid();
        var material = new LitDiffuseMaterial
        {
            ShaderAssetId = shaderAssetId,
        };

        var resolved = EffectiveShaderResolver.Resolve(material);

        Assert.Equal(shaderAssetId, resolved.ShaderId);
        Assert.False(resolved.IsBuiltIn);
        Assert.Null(resolved.ContentName);
    }

    [Fact]
    public void Resolve_ReturnsBasicEffectFallback_ForLitDiffuseMaterialWithoutShaderAsset()
    {
        var resolved = EffectiveShaderResolver.Resolve(new LitDiffuseMaterial());

        Assert.Equal(EffectiveShaderResolver.BasicEffectShaderId, resolved.ShaderId);
        Assert.True(resolved.IsBuiltIn);
        Assert.Equal(EffectiveShaderResolver.BasicEffectContentName, resolved.ContentName);
    }

    [Fact]
    public void Resolve_ReturnsUnlitFallback_ForUnlitTextureMaterialWithoutShaderAsset()
    {
        var resolved = EffectiveShaderResolver.Resolve(new UnlitTextureMaterial());

        Assert.Equal(EffectiveShaderResolver.UnlitTextureShaderId, resolved.ShaderId);
        Assert.True(resolved.IsBuiltIn);
        Assert.Equal(EffectiveShaderResolver.UnlitTextureContentName, resolved.ContentName);
    }

    [Fact]
    public void Resolve_ReturnsReflectiveFallback_ForReflectiveLitDiffuseMaterialWithoutShaderAsset()
    {
        var material = new LitDiffuseMaterial
        {
            ReflectionCubeAssetId = Guid.NewGuid(),
        };

        var resolved = EffectiveShaderResolver.Resolve(material);

        Assert.Equal(EffectiveShaderResolver.ReflectiveBasicEffectShaderId, resolved.ShaderId);
        Assert.True(resolved.IsBuiltIn);
        Assert.Equal(EffectiveShaderResolver.ReflectiveBasicEffectContentName, resolved.ContentName);
    }
}