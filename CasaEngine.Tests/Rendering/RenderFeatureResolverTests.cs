using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Materials;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class RenderFeatureResolverTests
{
    [Fact]
    public void ResolveMaterialFeatures_ReturnsExpectedFlags_ForLitDiffuseMaterial()
    {
        var material = new LitDiffuseMaterial
        {
            BasColorAssetId = Guid.NewGuid(),
            NormalMapAssetId = Guid.NewGuid(),
            EmissiveColor = new Vector3(0.25f, 0.5f, 0.75f),
            Queue = RenderQueue.AlphaTest,
            BlendState = BlendState.AlphaBlend,
        };

        var features = RenderFeatureResolver.ResolveMaterialFeatures(material);

        Assert.Equal(
            ShaderFeature.BasColorTexture |
            ShaderFeature.NormalMap |
            ShaderFeature.Emissive |
            ShaderFeature.AlphaTest |
            ShaderFeature.Transparent,
            features);
    }

    [Fact]
    public void Resolve_ReturnsStructuralFlags_FromExplicitRenderInputs()
    {
        var features = RenderFeatureResolver.Resolve(new RenderFeatureInput
        {
            Material = new LitDiffuseMaterial(),
            IsSkinned = true,
            IsInstanced = true,
            HasVertexColor = true,
        });

        Assert.Equal(
            ShaderFeature.Skinned |
            ShaderFeature.Instanced |
            ShaderFeature.VertexColor,
            features);
    }

    [Fact]
    public void ResolveSkinned_ReturnsSkinnedAndVertexColor_WhenRiggedMeshHasVertexColors()
    {
        var skinnedMesh = new RiggedModel.RiggedModelMesh
        {
            HasVertexColors = true,
        };

        var features = RenderFeatureResolver.ResolveSkinned(new UnlitTextureMaterial(), skinnedMesh);

        Assert.Equal(ShaderFeature.Skinned | ShaderFeature.VertexColor, features);
    }

    [Fact]
    public void ResolveMaterialFeatures_ReturnsTransparent_ForUnlitMaterialWithAlphaBelowOne()
    {
        var material = new UnlitTextureMaterial
        {
            Alpha = 0.5f,
        };

        var features = RenderFeatureResolver.ResolveMaterialFeatures(material);

        Assert.Equal(ShaderFeature.Transparent, features);
    }

    [Theory]
    [InlineData(ShaderFeature.None, "Opaque")]
    [InlineData(ShaderFeature.BasColorTexture, "Opaque_Textured")]
    [InlineData(ShaderFeature.AlphaTest | ShaderFeature.BasColorTexture, "AlphaTest_Textured")]
    [InlineData(ShaderFeature.Transparent | ShaderFeature.BasColorTexture, "Transparent")]
    [InlineData(ShaderFeature.Skinned, "Skinned")]
    [InlineData(ShaderFeature.Skinned | ShaderFeature.BasColorTexture, "Skinned_Textured")]
    public void BuildTechniqueName_ReturnsExpectedCanonicalTechnique(ShaderFeature features, string expectedTechnique)
    {
        var techniqueName = ShaderVariantLibrary.BuildTechniqueName(features);

        Assert.Equal(expectedTechnique, techniqueName);
    }

    [Fact]
    public void BuildTechniqueAliases_IncludeTransparentAndSkinnedMappings()
    {
        var basicEffectAliases = ShaderVariantLibrary.BuildBasicEffectAliases();
        var unlitAliases = ShaderVariantLibrary.BuildUnlitTextureAliases();

        Assert.Equal("BasicEffect_PixelLighting_Texture", basicEffectAliases["Transparent"]);
        Assert.Equal("BasicEffect_PixelLighting_Texture", basicEffectAliases["Skinned_Textured"]);
        Assert.Equal("Unlit_Textured", unlitAliases["Transparent"]);
        Assert.Equal("Unlit_Textured", unlitAliases["Skinned_Textured"]);
    }
}