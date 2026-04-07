using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Materials;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class RenderFeatureResolverTests
{
    private sealed class CustomCapabilityMaterial : MaterialBase
    {
        public override void Bind(ShaderWrapper shader, in RenderContext context, Matrix world)
            => throw new NotSupportedException();

        public override MaterialShaderCapabilities GetShaderCapabilities()
            => CreateShaderCapabilities(
                MaterialShaderFamily.Unlit,
                hasBasColorTexture: true,
                hasNormalMap: true,
                hasEmissive: true,
                hasReflection: true,
                isTransparent: true);
    }

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
            ShaderFeature.AlphaTest,
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
    public void Resolve_ClearsNormalMap_WhenStaticMeshHasNoTangents()
    {
        var material = new LitDiffuseMaterial
        {
            BasColorAssetId = Guid.NewGuid(),
            NormalMapAssetId = Guid.NewGuid(),
        };
        var mesh = new StaticModelMesh();
        mesh.SetData(
            new[]
            {
                new VertexPositionNormalTexture(new Vector3(-1f, 0f, 0f), Vector3.Up, Vector2.Zero),
                new VertexPositionNormalTexture(new Vector3(1f, 0f, 0f), Vector3.Up, Vector2.UnitX),
                new VertexPositionNormalTexture(new Vector3(0f, 0f, 1f), Vector3.Up, Vector2.UnitY),
            },
            new uint[] { 0, 1, 2 });

        var features = RenderFeatureResolver.Resolve(material, mesh);

        Assert.Equal(ShaderFeature.BasColorTexture, features);
    }

    [Fact]
    public void Resolve_PreservesNormalMap_WhenStaticMeshHasTangents()
    {
        var material = new LitDiffuseMaterial
        {
            BasColorAssetId = Guid.NewGuid(),
            NormalMapAssetId = Guid.NewGuid(),
        };
        var mesh = new StaticModelMesh();
        mesh.SetData(
            new[]
            {
                new VertexPositionNormalTextureTangent(new Vector3(-1f, 0f, 0f), Vector3.Up, Vector2.Zero, Vector4.UnitX),
                new VertexPositionNormalTextureTangent(new Vector3(1f, 0f, 0f), Vector3.Up, Vector2.UnitX, Vector4.UnitX),
                new VertexPositionNormalTextureTangent(new Vector3(0f, 0f, 1f), Vector3.Up, Vector2.UnitY, Vector4.UnitX),
            },
            new uint[] { 0, 1, 2 });

        var features = RenderFeatureResolver.Resolve(material, mesh);

        Assert.Equal(ShaderFeature.BasColorTexture | ShaderFeature.NormalMap, features);
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

    [Fact]
    public void ResolveMaterialFeatures_ReturnsTransparent_ForUnlitMaterialWithTintAlphaBelowOne()
    {
        var material = new UnlitTextureMaterial
        {
            Tint = new Color(255, 255, 255, 64),
        };

        var features = RenderFeatureResolver.ResolveMaterialFeatures(material);

        Assert.Equal(ShaderFeature.Transparent, features);
    }

    [Fact]
    public void ResolveMaterialFeatures_ReturnsReflection_ForReflectiveLitMaterial()
    {
        var material = new LitDiffuseMaterial
        {
            ReflectionCubeAssetId = Guid.NewGuid(),
        };

        var features = RenderFeatureResolver.ResolveMaterialFeatures(material);

        Assert.Equal(ShaderFeature.Reflection, features);
    }

    [Fact]
    public void ResolveMaterialFeatures_DoesNotMarkAlphaTestMaterialAsTransparent()
    {
        var material = new UnlitTextureMaterial
        {
            Alpha = 0.25f,
            Queue = RenderQueue.AlphaTest,
        };

        var features = RenderFeatureResolver.ResolveMaterialFeatures(material);

        Assert.Equal(ShaderFeature.AlphaTest, features);
    }

    [Fact]
    public void ResolveMaterialFeatures_UsesMaterialCapabilityContract_BeforeLegacyTypeChecks()
    {
        var material = new CustomCapabilityMaterial
        {
            Queue = RenderQueue.Transparent,
        };

        var features = RenderFeatureResolver.ResolveMaterialFeatures(material);

        Assert.Equal(
            ShaderFeature.BasColorTexture |
            ShaderFeature.NormalMap |
            ShaderFeature.Emissive |
            ShaderFeature.Reflection |
            ShaderFeature.Transparent,
            features);
    }

    [Fact]
    public void GetFeatures_DelegatesToResolver_ForMaterialOnlyQueries()
    {
        var material = new LitDiffuseMaterial
        {
            BasColorAssetId = Guid.NewGuid(),
            NormalMapAssetId = Guid.NewGuid(),
            ReflectionCubeAssetId = Guid.NewGuid(),
            EmissiveColor = new Vector3(0.25f, 0.5f, 0.75f),
            Queue = RenderQueue.AlphaTest,
        };

        var features = material.GetFeatures();

        Assert.Equal(RenderFeatureResolver.ResolveMaterialFeatures(material), features);
    }

    [Fact]
    public void GetFeatures_DelegatesToResolver_ForMeshAwareQueries()
    {
        var material = new LitDiffuseMaterial
        {
            BasColorAssetId = Guid.NewGuid(),
            NormalMapAssetId = Guid.NewGuid(),
        };
        var mesh = new StaticModelMesh();
        mesh.SetData(
            new[]
            {
                new VertexPositionNormalTexture(new Vector3(-1f, 0f, 0f), Vector3.Up, Vector2.Zero),
                new VertexPositionNormalTexture(new Vector3(1f, 0f, 0f), Vector3.Up, Vector2.UnitX),
                new VertexPositionNormalTexture(new Vector3(0f, 0f, 1f), Vector3.Up, Vector2.UnitY),
            },
            new uint[] { 0, 1, 2 });

        var features = material.GetFeatures(mesh);

        Assert.Equal(RenderFeatureResolver.Resolve(material, mesh), features);
    }

    [Theory]
    [InlineData(ShaderFeature.None, "Opaque")]
    [InlineData(ShaderFeature.BasColorTexture, "Opaque_Textured")]
    [InlineData(ShaderFeature.VertexColor, "Opaque_VertexColor")]
    [InlineData(ShaderFeature.BasColorTexture | ShaderFeature.VertexColor, "Opaque_Textured_VertexColor")]
    [InlineData(ShaderFeature.Instanced, "Opaque_Instanced")]
    [InlineData(ShaderFeature.Transparent | ShaderFeature.VertexColor | ShaderFeature.Instanced, "Transparent_VertexColor_Instanced")]
    [InlineData(ShaderFeature.Transparent | ShaderFeature.BasColorTexture | ShaderFeature.VertexColor, "Transparent_Textured_VertexColor")]
    [InlineData(ShaderFeature.AlphaTest | ShaderFeature.BasColorTexture, "AlphaTest_Textured")]
    [InlineData(ShaderFeature.Transparent, "Transparent")]
    [InlineData(ShaderFeature.Transparent | ShaderFeature.BasColorTexture, "Transparent_Textured")]
    [InlineData(ShaderFeature.Skinned, "Skinned")]
    [InlineData(ShaderFeature.Skinned | ShaderFeature.BasColorTexture, "Skinned_Textured")]
    [InlineData(ShaderFeature.Skinned | ShaderFeature.BasColorTexture | ShaderFeature.VertexColor | ShaderFeature.Instanced, "Skinned_Textured_VertexColor_Instanced")]
    public void BuildTechniqueName_ReturnsExpectedCanonicalTechnique(ShaderFeature features, string expectedTechnique)
    {
        var techniqueName = ShaderVariantLibrary.BuildTechniqueName(features);

        Assert.Equal(expectedTechnique, techniqueName);
    }

    [Theory]
    [InlineData(ShaderFeature.BasColorTexture | ShaderFeature.NormalMap, "Opaque_Textured")]
    [InlineData(ShaderFeature.Reflection | ShaderFeature.VertexColor, "Opaque_VertexColor")]
    [InlineData(ShaderFeature.BasColorTexture | ShaderFeature.Reflection | ShaderFeature.Instanced, "Opaque_Textured_Instanced")]
    public void BuildTechniqueName_IgnoresMaterialSpecificDimensionsOutsideCanonicalPolicy(ShaderFeature features, string expectedTechnique)
    {
        var techniqueName = ShaderVariantLibrary.BuildTechniqueName(features);

        Assert.Equal(expectedTechnique, techniqueName);
    }

    [Fact]
    public void BuildTechniqueAliases_IncludeTransparentAndSkinnedMappings()
    {
        var litForwardAliases = ShaderVariantLibrary.BuildLitForwardAliases();
        var unlitAliases = ShaderVariantLibrary.BuildUnlitTextureAliases();
        var skinnedAliases = ShaderVariantLibrary.BuildSkinnedEffectAliases();

        Assert.Equal("LitForward_PixelLighting", litForwardAliases["Transparent"]);
        Assert.Equal("LitForward_PixelLighting_Texture", litForwardAliases["Transparent_Textured"]);
        Assert.Equal("LitForward_PixelLighting_VertexColor", litForwardAliases["Opaque_VertexColor"]);
        Assert.Equal("LitForward_PixelLighting_Texture_VertexColor", litForwardAliases["Transparent_Textured_VertexColor_Instanced"]);
        Assert.Equal("LitForward_PixelLighting_Texture", litForwardAliases["Skinned_Textured"]);
        Assert.Equal("Unlit_Colored", unlitAliases["Transparent"]);
        Assert.Equal("Unlit_Textured", unlitAliases["Transparent_Textured"]);
        Assert.Equal("Unlit_Textured", unlitAliases["Opaque_Textured_Instanced"]);
        Assert.Equal("Unlit_Textured", unlitAliases["Skinned_Textured"]);
        Assert.Equal("RiggedModelDraw", skinnedAliases["Opaque"]);
        Assert.Equal("RiggedModelDraw", skinnedAliases["Skinned_VertexColor_Instanced"]);
        Assert.Equal("RiggedModelDraw", skinnedAliases["Skinned_Textured"]);
    }
}