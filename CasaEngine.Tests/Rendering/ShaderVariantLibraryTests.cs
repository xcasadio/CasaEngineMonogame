
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class ShaderVariantLibraryTests
{
    private sealed class FakeShader
    {
        public string? CurrentTechnique { get; set; }
    }

    [Fact]
    public void GetOrResolve_ReappliesSelection_WhenCacheHitReusesSharedShader()
    {
        var sharedShader = new FakeShader();
        var resolved = new Dictionary<ShaderVariantKey, FakeShader?>();
        var baseShaderId = Guid.NewGuid();
        var opaqueKey = new ShaderVariantKey(baseShaderId, ShaderFeature.None);
        var texturedKey = new ShaderVariantKey(baseShaderId, ShaderFeature.BasColorTexture);

        static void ApplySelection(FakeShader shader, ShaderVariantKey key)
            => shader.CurrentTechnique = ShaderVariantLibrary.BuildTechniqueName(key.Features);

        FakeShader ResolveSharedShader(ShaderVariantKey _) => sharedShader;

        var first = ShaderVariantLibrary.GetOrResolve(opaqueKey, resolved, ResolveSharedShader, ApplySelection);
        Assert.Same(sharedShader, first);
        Assert.Equal("Opaque", sharedShader.CurrentTechnique);

        var second = ShaderVariantLibrary.GetOrResolve(texturedKey, resolved, ResolveSharedShader, ApplySelection);
        Assert.Same(sharedShader, second);
        Assert.Equal("Opaque_Textured", sharedShader.CurrentTechnique);

        var third = ShaderVariantLibrary.GetOrResolve(opaqueKey, resolved, ResolveSharedShader, ApplySelection);
        Assert.Same(sharedShader, third);
        Assert.Equal("Opaque", sharedShader.CurrentTechnique);
    }

    [Fact]
    public void RequiresMaterialTechniqueSelection_OnlyOverridesCanonicalLitVariantsWhenNeeded()
    {
        var material = new LitDiffuseMaterial();
        var defaultContext = default(RenderContext);

        Assert.False(material.RequiresMaterialTechniqueSelection(
            techniqueSelectedBySelector: true,
            in defaultContext,
            ShaderFeature.BasColorTexture | ShaderFeature.VertexColor));
        Assert.False(material.RequiresMaterialTechniqueSelection(
            techniqueSelectedBySelector: true,
            in defaultContext,
            ShaderFeature.BasColorTexture | ShaderFeature.Instanced));
        Assert.True(material.RequiresMaterialTechniqueSelection(
            techniqueSelectedBySelector: true,
            in defaultContext,
            ShaderFeature.BasColorTexture | ShaderFeature.NormalMap));
        Assert.True(material.RequiresMaterialTechniqueSelection(
            techniqueSelectedBySelector: true,
            in defaultContext,
            ShaderFeature.Reflection));

        var oneLightContext = new RenderContext
        {
            Lighting = new LightingContext
            {
                ActiveDirectionalLightCount = 1,
            },
        };

        Assert.True(material.RequiresMaterialTechniqueSelection(
            techniqueSelectedBySelector: true,
            in oneLightContext,
            ShaderFeature.BasColorTexture));
        Assert.True(material.RequiresMaterialTechniqueSelection(
            techniqueSelectedBySelector: false,
            in defaultContext,
            ShaderFeature.BasColorTexture));
    }

    [Theory]
    [InlineData(ShaderFeature.VertexColor, false, "LitForward_PixelLighting_VertexColor")]
    [InlineData(ShaderFeature.BasColorTexture | ShaderFeature.VertexColor, false, "LitForward_PixelLighting_Texture_VertexColor")]
    [InlineData(ShaderFeature.VertexColor, true, "LitForward_PixelLighting_OneLight_VertexColor")]
    [InlineData(ShaderFeature.BasColorTexture | ShaderFeature.VertexColor, true, "LitForward_PixelLighting_OneLight_Texture_VertexColor")]
    public void GetTechniqueName_PreservesVertexColorForCanonicalLitVariants(ShaderFeature features, bool oneLight, string expectedTechnique)
    {
        string techniqueName = LitDiffuseMaterial.GetTechniqueName(features, oneLight);

        Assert.Equal(expectedTechnique, techniqueName);
    }

    [Theory]
    [InlineData(ShaderFeature.BasColorTexture | ShaderFeature.NormalMap | ShaderFeature.VertexColor, false, "LitForward_PixelLighting_Texture_NormalMap")]
    [InlineData(ShaderFeature.BasColorTexture | ShaderFeature.Reflection | ShaderFeature.VertexColor, false, "LitForward_PixelLighting_Texture_Reflection")]
    public void GetTechniqueName_KeepsMaterialSpecificVariantsAheadOfCanonicalVertexColor(ShaderFeature features, bool oneLight, string expectedTechnique)
    {
        string techniqueName = LitDiffuseMaterial.GetTechniqueName(features, oneLight);

        Assert.Equal(expectedTechnique, techniqueName);
    }
}