using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Rendering.Shaders;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class LitPbrMaterialTests
{
    private static StaticModelMesh CreateMesh(bool withTangents)
    {
        var mesh = new StaticModelMesh();
        if (withTangents)
        {
            mesh.SetData(
                new[]
                {
                    new VertexPositionNormalTextureTangent(new Vector3(-1f, 0f, 0f), Vector3.Up, Vector2.Zero, Vector4.UnitX),
                    new VertexPositionNormalTextureTangent(new Vector3(1f, 0f, 0f), Vector3.Up, Vector2.UnitX, Vector4.UnitX),
                    new VertexPositionNormalTextureTangent(new Vector3(0f, 0f, 1f), Vector3.Up, Vector2.UnitY, Vector4.UnitX),
                },
                new uint[] { 0, 1, 2 });
        }
        else
        {
            mesh.SetData(
                new[]
                {
                    new VertexPositionNormalTexture(new Vector3(-1f, 0f, 0f), Vector3.Up, Vector2.Zero),
                    new VertexPositionNormalTexture(new Vector3(1f, 0f, 0f), Vector3.Up, Vector2.UnitX),
                    new VertexPositionNormalTexture(new Vector3(0f, 0f, 1f), Vector3.Up, Vector2.UnitY),
                },
                new uint[] { 0, 1, 2 });
        }

        return mesh;
    }

    [Fact]
    public void Defaults_AreAnOpaqueWhiteDielectric()
    {
        var material = new LitPbrMaterial();

        Assert.Equal(Color.White, material.BaseColor);
        Assert.Equal(0.0f, material.Metallic);
        Assert.Equal(0.5f, material.Roughness);
        Assert.Equal(Vector3.Zero, material.EmissiveColor);
        Assert.Equal(1.0f, material.OcclusionStrength);
        Assert.Equal(1.0f, material.NormalScale);
        Assert.Equal(0.0f, material.ClearCoat);
        Assert.Equal(0.1f, material.ClearCoatRoughness);
        Assert.Equal(RenderQueue.Opaque, material.Queue);
    }

    [Fact]
    public void Capabilities_OfADefaultMaterial_AreTheLitPbrFamilyAndNothingElse()
    {
        var capabilities = new LitPbrMaterial().GetShaderCapabilities();

        Assert.Equal(MaterialShaderFamily.LitPbr, capabilities.ShaderFamily);
        Assert.False(capabilities.HasBasColorTexture);
        Assert.False(capabilities.HasNormalMap);
        Assert.False(capabilities.HasEmissive);
        Assert.False(capabilities.HasReflection);
        Assert.False(capabilities.IsAlphaTest);
        Assert.False(capabilities.IsTransparent);
    }

    [Fact]
    public void Capabilities_ReadTexturePresenceFromTheAssetIds()
    {
        var capabilities = new LitPbrMaterial
        {
            BaseColorTextureAssetId = Guid.NewGuid(),
            NormalTextureAssetId = Guid.NewGuid(),
            EmissiveTextureAssetId = Guid.NewGuid(),
        }.GetShaderCapabilities();

        Assert.True(capabilities.HasBasColorTexture);
        Assert.True(capabilities.HasNormalMap);
        Assert.True(capabilities.HasEmissive);
    }

    [Fact]
    public void Capabilities_AnEmissiveColorAloneIsEmissive()
    {
        var capabilities = new LitPbrMaterial { EmissiveColor = new Vector3(0.1f, 0.0f, 0.0f) }.GetShaderCapabilities();

        Assert.True(capabilities.HasEmissive);
        Assert.False(capabilities.HasBasColorTexture);
    }

    [Theory]
    [InlineData(RenderQueue.Opaque, 255, false, false)]
    [InlineData(RenderQueue.Opaque, 254, false, true)]
    [InlineData(RenderQueue.AlphaTest, 255, true, false)]
    [InlineData(RenderQueue.AlphaTest, 100, true, true)]
    [InlineData(RenderQueue.Transparent, 255, false, true)]
    [InlineData(RenderQueue.Transparent, 100, false, true)]
    public void Capabilities_AlphaTestAndTransparencyFollowTheQueueAndTheBaseColorAlpha(
        RenderQueue queue, int alpha, bool expectedAlphaTest, bool expectedTransparent)
    {
        var capabilities = new LitPbrMaterial
        {
            Queue = queue,
            BaseColor = new Color(255, 255, 255, alpha),
        }.GetShaderCapabilities();

        Assert.Equal(expectedAlphaTest, capabilities.IsAlphaTest);
        Assert.Equal(expectedTransparent, capabilities.IsTransparent);
    }

    [Fact]
    public void Features_AnAlphaTestedMaterialWithAnAlphaBelowOne_IsAlphaTestNotTransparent()
    {
        var features = RenderFeatureResolver.ResolveMaterialFeatures(new LitPbrMaterial
        {
            Queue = RenderQueue.AlphaTest,
            BaseColor = new Color(255, 255, 255, 100),
        });

        Assert.Equal(ShaderFeature.AlphaTest, features);
    }

    [Fact]
    public void Capabilities_ABlendStateThatBlendsMakesTheMaterialTransparent()
    {
        var capabilities = new LitPbrMaterial { BlendState = BlendState.AlphaBlend }.GetShaderCapabilities();

        Assert.True(capabilities.IsTransparent);
    }

    [Theory]
    [InlineData(ShaderFeature.None, "LitPbr")]
    [InlineData(ShaderFeature.BasColorTexture, "LitPbr")]
    [InlineData(ShaderFeature.AlphaTest, "LitPbr_AlphaTest")]
    [InlineData(ShaderFeature.AlphaTest | ShaderFeature.BasColorTexture, "LitPbr_AlphaTest")]
    [InlineData(ShaderFeature.NormalMap, "LitPbr_NormalMap")]
    [InlineData(ShaderFeature.NormalMap | ShaderFeature.BasColorTexture, "LitPbr_NormalMap")]
    [InlineData(ShaderFeature.NormalMap | ShaderFeature.AlphaTest, "LitPbr_NormalMap_AlphaTest")]
    [InlineData(ShaderFeature.NormalMap | ShaderFeature.AlphaTest | ShaderFeature.Emissive | ShaderFeature.VertexColor, "LitPbr_NormalMap_AlphaTest")]
    [InlineData(ShaderFeature.Transparent | ShaderFeature.Emissive, "LitPbr_Transparent")]
    [InlineData(ShaderFeature.Transparent | ShaderFeature.BasColorTexture, "LitPbr_Transparent")]
    [InlineData(ShaderFeature.Transparent | ShaderFeature.NormalMap, "LitPbr_NormalMap_Transparent")]
    [InlineData(ShaderFeature.Transparent | ShaderFeature.NormalMap | ShaderFeature.BasColorTexture | ShaderFeature.Emissive, "LitPbr_NormalMap_Transparent")]
    [InlineData(ShaderFeature.Transparent | ShaderFeature.AlphaTest, "LitPbr_AlphaTest")]
    [InlineData(ShaderFeature.Transparent | ShaderFeature.NormalMap | ShaderFeature.AlphaTest, "LitPbr_NormalMap_AlphaTest")]
    public void GetTechniqueName_IsLitPbrWithItsNormalMapThenAlphaTestSuffixes(ShaderFeature features, string expected)
    {
        Assert.Equal(expected, LitPbrMaterial.GetTechniqueName(features));
    }

    [Fact]
    public void GetTechniqueName_ProducesExactlyTheSixTechniquesOfTheShaderFile()
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        for (uint bits = 0; bits < 8; bits++)
        {
            var features = ShaderFeature.None;
            features |= (bits & 1) != 0 ? ShaderFeature.NormalMap : ShaderFeature.None;
            features |= (bits & 4) != 0 ? ShaderFeature.Transparent : ShaderFeature.None;
            features |= (bits & 2) != 0 ? ShaderFeature.AlphaTest : ShaderFeature.None;
            names.Add(LitPbrMaterial.GetTechniqueName(features));
        }

        Assert.Equal(
            new[]
            {
                "LitPbr", "LitPbr_AlphaTest", "LitPbr_NormalMap", "LitPbr_NormalMap_AlphaTest",
                "LitPbr_NormalMap_Transparent", "LitPbr_Transparent",
            },
            names);
    }

    [Fact]
    public void RequiresMaterialTechniqueSelection_IsTrueForANormalMapABlendedDrawOrWhenTheSelectorChoseNothing()
    {
        var material = new LitPbrMaterial();
        var context = default(RenderContext);

        Assert.False(material.RequiresMaterialTechniqueSelection(true, in context, ShaderFeature.BasColorTexture));
        // A blended draw: the alias table has no transparent technique, the material names it.
        Assert.True(material.RequiresMaterialTechniqueSelection(true, in context, ShaderFeature.Transparent));
        Assert.True(material.RequiresMaterialTechniqueSelection(true, in context, ShaderFeature.Transparent | ShaderFeature.BasColorTexture));
        Assert.False(material.RequiresMaterialTechniqueSelection(true, in context, ShaderFeature.AlphaTest | ShaderFeature.BasColorTexture));
        Assert.True(material.RequiresMaterialTechniqueSelection(true, in context, ShaderFeature.NormalMap));
        Assert.True(material.RequiresMaterialTechniqueSelection(true, in context, ShaderFeature.NormalMap | ShaderFeature.BasColorTexture));
        Assert.True(material.RequiresMaterialTechniqueSelection(false, in context, ShaderFeature.BasColorTexture));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Resolve_OnAMeshWithTangents_GivesANormalMappedTechnique_WithOrWithoutABaseTexture(bool withBaseTexture)
    {
        var material = new LitPbrMaterial
        {
            NormalTextureAssetId = Guid.NewGuid(),
            BaseColorTextureAssetId = withBaseTexture ? Guid.NewGuid() : Guid.Empty,
        };

        var features = RenderFeatureResolver.Resolve(material, CreateMesh(withTangents: true));

        Assert.NotEqual(ShaderFeature.None, features & ShaderFeature.NormalMap);
        Assert.Equal(withBaseTexture, (features & ShaderFeature.BasColorTexture) != 0);
        Assert.Equal("LitPbr_NormalMap", LitPbrMaterial.GetTechniqueName(features));
    }

    [Fact]
    public void Resolve_OnAMeshWithoutTangents_DropsTheNormalMap()
    {
        var material = new LitPbrMaterial { NormalTextureAssetId = Guid.NewGuid() };

        var features = RenderFeatureResolver.Resolve(material, CreateMesh(withTangents: false));

        Assert.Equal(ShaderFeature.None, features);
        Assert.Equal("LitPbr", LitPbrMaterial.GetTechniqueName(features));
    }

    [Fact]
    public void Resolve_ABlendedNormalMappedMaterial_GivesTheTransparentNormalMapTechnique_AndABlendedOneTheTransparentTechnique()
    {
        var blendedNormalMapped = new LitPbrMaterial
        {
            BaseColor = new Color(255, 255, 255, 64),
            NormalTextureAssetId = Guid.NewGuid(),
        };
        var blended = new LitPbrMaterial { BaseColor = new Color(255, 255, 255, 64) };
        var mesh = CreateMesh(withTangents: true);

        Assert.Equal(
            "LitPbr_NormalMap_Transparent",
            LitPbrMaterial.GetTechniqueName(RenderFeatureResolver.Resolve(blendedNormalMapped, mesh)));
        Assert.Equal("LitPbr_Transparent", LitPbrMaterial.GetTechniqueName(RenderFeatureResolver.Resolve(blended, mesh)));
    }

    [Fact]
    public void Resolve_AnAlphaTestedNormalMappedMaterial_GivesTheAlphaTestedNormalMapTechnique()
    {
        var material = new LitPbrMaterial
        {
            Queue = RenderQueue.AlphaTest,
            NormalTextureAssetId = Guid.NewGuid(),
            BaseColorTextureAssetId = Guid.NewGuid(),
        };

        var features = RenderFeatureResolver.Resolve(material, CreateMesh(withTangents: true));

        Assert.Equal("LitPbr_NormalMap_AlphaTest", LitPbrMaterial.GetTechniqueName(features));
    }

    [Fact]
    public void LitDiffuseFeatures_AreUnchanged_ForANormalMapWithAndWithoutABaseTexture()
    {
        var withoutBase = new LitDiffuseMaterial { NormalMapAssetId = Guid.NewGuid() };
        var withBase = new LitDiffuseMaterial { NormalMapAssetId = Guid.NewGuid(), BasColorAssetId = Guid.NewGuid() };
        var mesh = CreateMesh(withTangents: true);

        Assert.Equal(ShaderFeature.None, RenderFeatureResolver.Resolve(withoutBase, mesh));
        Assert.Equal(ShaderFeature.BasColorTexture | ShaderFeature.NormalMap, RenderFeatureResolver.Resolve(withBase, mesh));
    }

    [GpuFact]
    public void Bind_WritesEveryMaterialConstantAndFlag_OnEveryBind()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var device = GpuDeviceHost.Instance.GraphicsDevice;
            var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.LitPbrContentName);
            var shader = new ShaderWrapper(effect);
            var context = new RenderContext { Device = device };

            using var baseTexture = new Texture2D(device, 1, 1);
            using var normalTexture = new Texture2D(device, 1, 1);
            using var metallicRoughnessTexture = new Texture2D(device, 1, 1);
            using var occlusionTexture = new Texture2D(device, 1, 1);
            using var emissiveTexture = new Texture2D(device, 1, 1);

            var textured = new LitPbrMaterial
            {
                BaseColor = new Color(255, 128, 0, 255),
                Metallic = 0.75f,
                Roughness = 0.25f,
                EmissiveColor = new Vector3(0.5f, 0.25f, 0.125f),
                OcclusionStrength = 0.5f,
                NormalScale = 2.0f,
                ClearCoat = 0.6f,
                ClearCoatRoughness = 0.35f,
                Queue = RenderQueue.AlphaTest,
                AlphaCutoff = 0.4f,
                BaseColorTexture = baseTexture,
                NormalTexture = normalTexture,
                MetallicRoughnessTexture = metallicRoughnessTexture,
                OcclusionTexture = occlusionTexture,
                EmissiveTexture = emissiveTexture,
            };

            textured.Bind(shader, in context, Matrix.Identity);

            Assert.Equal(new Vector4(1.0f, 128.0f / 255.0f, 0.0f, 1.0f), effect.Parameters[ShaderParameterNames.BaseColor]!.GetValueVector4());
            Assert.Equal(0.75f, effect.Parameters[ShaderParameterNames.Metallic]!.GetValueSingle());
            Assert.Equal(0.25f, effect.Parameters[ShaderParameterNames.Roughness]!.GetValueSingle());
            Assert.Equal(new Vector3(0.5f, 0.25f, 0.125f), effect.Parameters[ShaderParameterNames.EmissiveColor]!.GetValueVector3());
            Assert.Equal(0.5f, effect.Parameters[ShaderParameterNames.OcclusionStrength]!.GetValueSingle());
            Assert.Equal(2.0f, effect.Parameters[ShaderParameterNames.NormalScale]!.GetValueSingle());
            Assert.Equal(0.6f, effect.Parameters[ShaderParameterNames.ClearCoat]!.GetValueSingle());
            Assert.Equal(0.35f, effect.Parameters[ShaderParameterNames.ClearCoatRoughness]!.GetValueSingle());
            Assert.Equal(0.4f, effect.Parameters[ShaderParameterNames.AlphaCutoff]!.GetValueSingle());
            Assert.Equal(1.0f, effect.Parameters[ShaderParameterNames.HasBaseColorTexture]!.GetValueSingle());
            Assert.Equal(1.0f, effect.Parameters[ShaderParameterNames.HasMetallicRoughnessTexture]!.GetValueSingle());
            Assert.Equal(1.0f, effect.Parameters[ShaderParameterNames.HasOcclusionTexture]!.GetValueSingle());
            Assert.Equal(1.0f, effect.Parameters[ShaderParameterNames.HasEmissiveTexture]!.GetValueSingle());
            Assert.Same(baseTexture, effect.Parameters[ShaderParameterNames.BasColorTexture]!.GetValueTexture2D());
            Assert.Same(normalTexture, effect.Parameters[ShaderParameterNames.NormalTexture]!.GetValueTexture2D());

            // A second bind of a bare material in the opaque queue resets the flags and the cutoff left by the first one.
            new LitPbrMaterial().Bind(shader, in context, Matrix.Identity);

            Assert.Equal(0.0f, effect.Parameters[ShaderParameterNames.AlphaCutoff]!.GetValueSingle());
            Assert.Equal(0.0f, effect.Parameters[ShaderParameterNames.ClearCoat]!.GetValueSingle());
            Assert.Equal(0.1f, effect.Parameters[ShaderParameterNames.ClearCoatRoughness]!.GetValueSingle());
            Assert.Equal(0.0f, effect.Parameters[ShaderParameterNames.HasBaseColorTexture]!.GetValueSingle());
            Assert.Equal(0.0f, effect.Parameters[ShaderParameterNames.HasMetallicRoughnessTexture]!.GetValueSingle());
            Assert.Equal(0.0f, effect.Parameters[ShaderParameterNames.HasOcclusionTexture]!.GetValueSingle());
            Assert.Equal(0.0f, effect.Parameters[ShaderParameterNames.HasEmissiveTexture]!.GetValueSingle());
            Assert.Equal(0.0f, effect.Parameters[ShaderParameterNames.Metallic]!.GetValueSingle());
            Assert.Equal(0.5f, effect.Parameters[ShaderParameterNames.Roughness]!.GetValueSingle());
            Assert.Equal(Vector3.Zero, effect.Parameters[ShaderParameterNames.EmissiveColor]!.GetValueVector3());
        });
    }

    [GpuFact]
    public void SelectTechnique_SelectsTheNamedTechnique_OnTheCompiledEffect()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.LitPbrContentName);
            var shader = new ShaderWrapper(effect);
            var material = new LitPbrMaterial();
            var context = default(RenderContext);

            material.SelectTechnique(shader, in context, ShaderFeature.NormalMap | ShaderFeature.AlphaTest);
            Assert.Equal("LitPbr_NormalMap_AlphaTest", effect.CurrentTechnique.Name);

            material.SelectTechnique(shader, in context, ShaderFeature.NormalMap);
            Assert.Equal("LitPbr_NormalMap", effect.CurrentTechnique.Name);

            material.SelectTechnique(shader, in context, ShaderFeature.None);
            Assert.Equal("LitPbr", effect.CurrentTechnique.Name);
        });
    }
}
