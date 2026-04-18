
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class EffectiveShaderResolverTests
{
    private sealed class CustomCapabilityMaterial : MaterialBase
    {
        public CustomCapabilityMaterial(MaterialShaderCapabilities capabilities)
        {
            Capabilities = capabilities;
        }

        private MaterialShaderCapabilities Capabilities { get; }

        public override void Bind(ShaderWrapper shader, in RenderContext context, Microsoft.Xna.Framework.Matrix world)
            => throw new NotSupportedException();

        public override MaterialShaderCapabilities GetShaderCapabilities()
            => Capabilities;
    }

    [Fact]
    public void Resolve_ReturnsExplicitShaderAsset_WhenMaterialDefinesOne()
    {
        var shaderAssetId = Guid.NewGuid();
        var material = new LitDiffuseMaterial
        {
            ShaderAssetId = shaderAssetId,
        };

        var resolved = EffectiveShaderResolver.Resolve(material, ShaderFeature.Skinned | ShaderFeature.BasColorTexture);

        Assert.Equal(shaderAssetId, resolved.ShaderId);
        Assert.False(resolved.IsBuiltIn);
        Assert.Null(resolved.ContentName);
    }

    [Fact]
    public void Resolve_ReturnsLitForwardFallback_ForLitDiffuseMaterialWithoutShaderAsset()
    {
        var resolved = EffectiveShaderResolver.Resolve(new LitDiffuseMaterial());

        Assert.Equal(EffectiveShaderResolver.LitForwardShaderId, resolved.ShaderId);
        Assert.True(resolved.IsBuiltIn);
        Assert.Equal(EffectiveShaderResolver.LitForwardContentName, resolved.ContentName);
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

        Assert.Equal(EffectiveShaderResolver.ReflectiveLitForwardShaderId, resolved.ShaderId);
        Assert.True(resolved.IsBuiltIn);
        Assert.Equal(EffectiveShaderResolver.ReflectiveLitForwardContentName, resolved.ContentName);
    }

    [Fact]
    public void Resolve_ReturnsSkinnedFallback_WhenSkinnedFeaturesArePresent()
    {
        var resolved = EffectiveShaderResolver.Resolve(
            new LitDiffuseMaterial(),
            ShaderFeature.Skinned | ShaderFeature.BasColorTexture);

        Assert.Equal(EffectiveShaderResolver.SkinnedEffectShaderId, resolved.ShaderId);
        Assert.True(resolved.IsBuiltIn);
        Assert.Equal(EffectiveShaderResolver.SkinnedEffectContentName, resolved.ContentName);
    }

    [Fact]
    public void Resolve_ReturnsExplicitLinearBlendSkinnedFallback_WhenModeIsProvided()
    {
        var resolved = EffectiveShaderResolver.Resolve(
            new LitDiffuseMaterial(),
            ShaderFeature.Skinned | ShaderFeature.BasColorTexture,
            SkinningMode.LinearBlend);

        Assert.Equal(EffectiveShaderResolver.LinearBlendSkinnedEffectShaderId, resolved.ShaderId);
        Assert.True(resolved.IsBuiltIn);
        Assert.Equal(EffectiveShaderResolver.LinearBlendSkinnedEffectContentName, resolved.ContentName);
    }

    [Fact]
    public void Resolve_UsesMaterialCapabilityContract_ForUnknownMaterialTypes()
    {
        var material = new CustomCapabilityMaterial(new MaterialShaderCapabilities(
            MaterialShaderFamily.Unlit,
            hasBasColorTexture: true));

        var resolved = EffectiveShaderResolver.Resolve(material);

        Assert.Equal(EffectiveShaderResolver.UnlitTextureShaderId, resolved.ShaderId);
        Assert.Equal(EffectiveShaderResolver.UnlitTextureContentName, resolved.ContentName);
    }

    [Fact]
    public void Resolve_UsesReflectionCapability_ForUnknownLitMaterialTypes()
    {
        var material = new CustomCapabilityMaterial(new MaterialShaderCapabilities(
            MaterialShaderFamily.Lit,
            hasReflection: true));

        var resolved = EffectiveShaderResolver.Resolve(material);

        Assert.Equal(EffectiveShaderResolver.ReflectiveLitForwardShaderId, resolved.ShaderId);
        Assert.Equal(EffectiveShaderResolver.ReflectiveLitForwardContentName, resolved.ContentName);
    }
}