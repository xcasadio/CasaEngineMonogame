using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Materials;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class MaterialCompilerTests
{
    [Fact]
    public void Compile_LitDiffuseMaterial_UsesDefaultsAndExplicitOverrides()
    {
        var materialAsset = new MaterialAsset("lit-diffuse")
        {
            Name = "Lit Asset",
            Queue = RenderQueue.AlphaTest,
            BlendStateName = "AlphaBlend",
        };
        materialAsset.SetPropertyValue("specular_power", MaterialValue.FromFloat(24.0f));
        materialAsset.SetPropertyValue("emissive_color", MaterialValue.FromVector3(new Vector3(0.25f, 0.5f, 0.75f)));

        var compiledMaterial = new MaterialCompiler().Compile(materialAsset, new AssetContentManager());

        Assert.Equal("lit-diffuse", compiledMaterial.DefinitionId);
        Assert.Equal(materialAsset.Id, compiledMaterial.SourceAssetId);
        Assert.Equal("Lit Asset", compiledMaterial.Name);
        Assert.Equal(EffectiveShaderResolver.BasicEffectShaderId, compiledMaterial.EffectiveShader.ShaderId);
        Assert.Equal(
            ShaderFeature.Emissive |
            ShaderFeature.AlphaTest |
            ShaderFeature.Transparent,
            compiledMaterial.Features);
        Assert.Same(BlendState.AlphaBlend, compiledMaterial.BlendState);
        Assert.True(compiledMaterial.TryGetPropertyValue("specular_power", out var specularPowerValue));
        Assert.True(specularPowerValue.TryGetFloat(out var specularPower));
        Assert.Equal(24.0f, specularPower);
        Assert.True(compiledMaterial.TryGetPropertyValue("diffuse_color", out var diffuseColorValue));
        Assert.True(diffuseColorValue.TryGetColor(out var diffuseColor));
        Assert.Equal(Color.White, diffuseColor);
        Assert.True(compiledMaterial.TryGetTexture("base_color_texture", out var baseColorTexture));
        Assert.Null(baseColorTexture);
    }

    [Fact]
    public void Compile_UnlitTextureMaterial_CompilesTextureAndTransparencyFeatures()
    {
        var materialAsset = new MaterialAsset("unlit-texture")
        {
            Name = "Unlit Asset",
        };
        materialAsset.SetPropertyValue("base_color_texture", MaterialValue.FromTextureId(Guid.NewGuid()));
        materialAsset.SetPropertyValue("alpha", MaterialValue.FromFloat(0.5f));
        materialAsset.SetPropertyValue("tint_color", MaterialValue.FromColor(Color.CornflowerBlue));

        var compiledMaterial = new MaterialCompiler().Compile(materialAsset, new AssetContentManager());

        Assert.Equal(EffectiveShaderResolver.UnlitTextureShaderId, compiledMaterial.EffectiveShader.ShaderId);
        Assert.Equal(
            ShaderFeature.BasColorTexture |
            ShaderFeature.Transparent,
            compiledMaterial.Features);
        Assert.True(compiledMaterial.TryGetPropertyValue("tint_color", out var tintValue));
        Assert.True(tintValue.TryGetColor(out var tintColor));
        Assert.Equal(Color.CornflowerBlue, tintColor);
        Assert.True(compiledMaterial.TryGetTexture("base_color_texture", out var baseColorTexture));
        Assert.Null(baseColorTexture);
    }

    [Fact]
    public void Compile_LegacyMultiTextureMaterial_CompilesLegacyTextureSlots()
    {
        var materialAsset = new MaterialAsset("legacy-multi-texture");
        materialAsset.SetPropertyValue("base_color_texture", MaterialValue.FromTextureId(Guid.NewGuid()));
        materialAsset.SetPropertyValue("normal_texture", MaterialValue.FromTextureId(Guid.NewGuid()));

        var compiledMaterial = new MaterialCompiler().Compile(materialAsset, new AssetContentManager());

        Assert.Equal("legacy-multi-texture", compiledMaterial.DefinitionId);
        Assert.Equal(EffectiveShaderResolver.BasicEffectShaderId, compiledMaterial.EffectiveShader.ShaderId);
        Assert.Equal(ShaderFeature.BasColorTexture | ShaderFeature.NormalMap, compiledMaterial.Features);
        Assert.True(compiledMaterial.TryGetTexture("base_color_texture", out var baseColorTexture));
        Assert.Null(baseColorTexture);
        Assert.True(compiledMaterial.TryGetTexture("normal_texture", out var normalTexture));
        Assert.Null(normalTexture);
    }

    [Fact]
    public void CompileRuntimeMaterial_LitDiffuseMaterial_ReturnsCurrentRuntimeType()
    {
        var materialAsset = new MaterialAsset("lit-diffuse");
        materialAsset.SetPropertyValue("diffuse_color", MaterialValue.FromColor(Color.Orange));
        materialAsset.SetPropertyValue("specular_power", MaterialValue.FromFloat(12.0f));

        var runtimeMaterial = new MaterialCompiler().CompileRuntimeMaterial(materialAsset, new AssetContentManager());

        var litMaterial = Assert.IsType<LitDiffuseMaterial>(runtimeMaterial);
        Assert.Equal(Color.Orange, litMaterial.DiffuseColor);
        Assert.Equal(12.0f, litMaterial.SpecularPower);
    }

    [Fact]
    public void CompileRuntimeMaterial_LegacyMultiTextureMaterial_ReturnsCurrentRuntimeType()
    {
        var baseColorTextureId = Guid.NewGuid();
        var reflectionTextureId = Guid.NewGuid();
        var materialAsset = new MaterialAsset("legacy-multi-texture");
        materialAsset.SetPropertyValue("base_color_texture", MaterialValue.FromTextureId(baseColorTextureId));
        materialAsset.SetPropertyValue("reflection_texture", MaterialValue.FromTextureId(reflectionTextureId));

        var runtimeMaterial = new MaterialCompiler().CompileRuntimeMaterial(materialAsset, new AssetContentManager());

        var legacyMaterial = Assert.IsType<Material>(runtimeMaterial);
        Assert.Equal(baseColorTextureId, legacyMaterial.TextureBaseColorAssetId);
        Assert.Equal(reflectionTextureId, legacyMaterial.TextureReflectionAssetId);
    }
}