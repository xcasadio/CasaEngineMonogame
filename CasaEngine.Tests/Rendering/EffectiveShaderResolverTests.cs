
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
    public void Resolve_ReturnsExplicitDualQuaternionSkinnedFallback_WhenModeIsProvided()
    {
        var resolved = EffectiveShaderResolver.Resolve(
            new LitDiffuseMaterial(),
            ShaderFeature.Skinned | ShaderFeature.BasColorTexture,
            SkinningMode.DualQuaternion);

        Assert.Equal(EffectiveShaderResolver.DualQuaternionSkinnedEffectShaderId, resolved.ShaderId);
        Assert.True(resolved.IsBuiltIn);
        Assert.Equal(EffectiveShaderResolver.DualQuaternionSkinnedEffectContentName, resolved.ContentName);
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

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Resolve_MapsTheLitPbrFamilyToTheLitPbrShader(bool hasBasColorTexture, bool isAlphaTest)
    {
        var material = new CustomCapabilityMaterial(new MaterialShaderCapabilities(
            MaterialShaderFamily.LitPbr,
            hasBasColorTexture: hasBasColorTexture,
            isAlphaTest: isAlphaTest));

        var resolved = EffectiveShaderResolver.Resolve(material);

        Assert.Equal(EffectiveShaderResolver.LitPbrShaderId, resolved.ShaderId);
        Assert.True(resolved.IsBuiltIn);
        Assert.Equal(EffectiveShaderResolver.LitPbrContentName, resolved.ContentName);
        Assert.Equal("Shaders\\LitPbr", resolved.ContentName);
    }

    [Fact]
    public void Resolve_LitPbrFamilyWinsOverTheReflectionCapabilityOfTheLitFamily()
    {
        var material = new CustomCapabilityMaterial(new MaterialShaderCapabilities(
            MaterialShaderFamily.LitPbr,
            hasReflection: true));

        var resolved = EffectiveShaderResolver.Resolve(material);

        Assert.Equal(EffectiveShaderResolver.LitPbrShaderId, resolved.ShaderId);
    }

    [Fact]
    public void Resolve_KeepsTheLitFamilyOnLitForward_AndTheLitPbrIdsApart()
    {
        var lit = new CustomCapabilityMaterial(new MaterialShaderCapabilities(MaterialShaderFamily.Lit, hasBasColorTexture: true));

        var resolved = EffectiveShaderResolver.Resolve(lit);

        Assert.Equal(EffectiveShaderResolver.LitForwardShaderId, resolved.ShaderId);
        Assert.Equal(EffectiveShaderResolver.LitForwardContentName, resolved.ContentName);
        Assert.NotEqual(EffectiveShaderResolver.LitForwardShaderId, EffectiveShaderResolver.LitPbrShaderId);
        Assert.NotEqual(EffectiveShaderResolver.ReflectiveLitForwardShaderId, EffectiveShaderResolver.LitPbrShaderId);
        Assert.NotEqual(EffectiveShaderResolver.UnlitTextureShaderId, EffectiveShaderResolver.LitPbrShaderId);
        Assert.NotEqual(EffectiveShaderResolver.SkinnedEffectShaderId, EffectiveShaderResolver.LitPbrShaderId);
        Assert.NotEqual(EffectiveShaderResolver.DualQuaternionSkinnedEffectShaderId, EffectiveShaderResolver.LitPbrShaderId);

        // A built-in material that has no capability family of its own still resolves to LitForward.
        Assert.Equal(EffectiveShaderResolver.LitForwardShaderId, EffectiveShaderResolver.Resolve(new LitDiffuseMaterial()).ShaderId);
    }

    [Fact]
    public void Resolve_KeepsTheSkinningShaderForALitPbrFamilyMaterialOnASkinnedMesh()
    {
        // Until phase 8 of the PBR plan a PBR material drawn on a skinned mesh keeps the skinning shader.
        var material = new CustomCapabilityMaterial(new MaterialShaderCapabilities(MaterialShaderFamily.LitPbr));

        var resolved = EffectiveShaderResolver.Resolve(material, ShaderFeature.Skinned);

        Assert.Equal(EffectiveShaderResolver.SkinnedEffectShaderId, resolved.ShaderId);
        Assert.Equal(EffectiveShaderResolver.SkinnedEffectContentName, resolved.ContentName);
    }

    [Fact]
    public void BuiltInShaderCatalog_ListsLitPbrByContentNameAndSourcePath()
    {
        Assert.Equal(EffectiveShaderResolver.LitPbrContentName, BuiltInShaderCatalog.LitPbrContentName);

        Assert.True(BuiltInShaderCatalog.TryGetByContentName("Shaders\\LitPbr", out var byContentName));
        Assert.Equal("Shaders/LitPbr.fx", byContentName.SourceRelativePath);
        Assert.True(BuiltInShaderCatalog.TryGetByContentName("Shaders/LitPbr", out var byAltContentName));
        Assert.Equal(byContentName, byAltContentName);
        Assert.True(BuiltInShaderCatalog.TryGetBySourceRelativePath("Shaders/LitPbr.fx", out var bySource));
        Assert.Equal(byContentName, bySource);
        Assert.Contains(byContentName, BuiltInShaderCatalog.RuntimeShaders);

        // The other built-in shaders are still listed once each.
        Assert.True(BuiltInShaderCatalog.TryGetByContentName(BuiltInShaderCatalog.LitForwardContentName, out _));
        Assert.Equal(
            BuiltInShaderCatalog.RuntimeShaders.Count,
            BuiltInShaderCatalog.RuntimeShaders.Select(shader => shader.ContentName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}