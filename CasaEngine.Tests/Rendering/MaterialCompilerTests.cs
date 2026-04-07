using CasaEngine.Framework.Assets;

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
        materialAsset.SetPropertyValue("alpha_cutoff", MaterialValue.FromFloat(0.42f));

        var compiledMaterial = new MaterialCompiler().Compile(materialAsset, new AssetContentManager());

        Assert.Equal("lit-diffuse", compiledMaterial.DefinitionId);
        Assert.Equal(materialAsset.Id, compiledMaterial.SourceAssetId);
        Assert.Equal("Lit Asset", compiledMaterial.Name);
        Assert.Equal(EffectiveShaderResolver.LitForwardShaderId, compiledMaterial.EffectiveShader.ShaderId);
        Assert.Equal(
            ShaderFeature.Emissive |
            ShaderFeature.AlphaTest,
            compiledMaterial.Features);
        Assert.Same(BlendState.AlphaBlend, compiledMaterial.BlendState);
        Assert.True(compiledMaterial.TryGetPropertyValue("specular_power", out var specularPowerValue));
        Assert.True(specularPowerValue.TryGetFloat(out var specularPower));
        Assert.Equal(24.0f, specularPower);
        Assert.True(compiledMaterial.TryGetPropertyValue("alpha_cutoff", out var alphaCutoffValue));
        Assert.True(alphaCutoffValue.TryGetFloat(out var alphaCutoff));
        Assert.Equal(0.42f, alphaCutoff);
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
        Assert.True(compiledMaterial.IsTransparent);
        Assert.Equal(RenderQueue.Transparent, compiledMaterial.Queue);
        Assert.Same(BlendState.AlphaBlend, compiledMaterial.BlendState);
        Assert.Same(DepthStencilState.DepthRead, compiledMaterial.DepthStencilState);
        Assert.True(compiledMaterial.TryGetPropertyValue("tint_color", out var tintValue));
        Assert.True(tintValue.TryGetColor(out var tintColor));
        Assert.Equal(Color.CornflowerBlue, tintColor);
        Assert.True(compiledMaterial.TryGetTexture("base_color_texture", out var baseColorTexture));
        Assert.Null(baseColorTexture);
    }

    [Fact]
    public void Compile_UnlitTextureMaterial_WithTintAlpha_InfersTransparentPipelineState()
    {
        var materialAsset = new MaterialAsset("unlit-texture")
        {
            Name = "Tint Transparent",
        };
        materialAsset.SetPropertyValue("tint_color", MaterialValue.FromColor(new Color(255, 255, 255, 96)));

        var compiledMaterial = new MaterialCompiler().Compile(materialAsset, new AssetContentManager());

        Assert.Equal(ShaderFeature.Transparent, compiledMaterial.Features);
        Assert.True(compiledMaterial.IsTransparent);
        Assert.Equal(RenderQueue.Transparent, compiledMaterial.Queue);
        Assert.Same(BlendState.AlphaBlend, compiledMaterial.BlendState);
        Assert.Same(DepthStencilState.DepthRead, compiledMaterial.DepthStencilState);
    }

    [Fact]
    public void Compile_LitDiffuseMaterial_WithDiffuseAlpha_InfersTransparentPipelineState()
    {
        var materialAsset = new MaterialAsset("lit-diffuse")
        {
            Name = "Lit Transparent",
        };
        materialAsset.SetPropertyValue("diffuse_color", MaterialValue.FromColor(new Color(255, 200, 180, 96)));

        var compiledMaterial = new MaterialCompiler().Compile(materialAsset, new AssetContentManager());

        Assert.Equal(ShaderFeature.Transparent, compiledMaterial.Features);
        Assert.True(compiledMaterial.IsTransparent);
        Assert.Equal(RenderQueue.Transparent, compiledMaterial.Queue);
        Assert.Same(BlendState.AlphaBlend, compiledMaterial.BlendState);
        Assert.Same(DepthStencilState.DepthRead, compiledMaterial.DepthStencilState);
    }

    [Fact]
    public void Compile_RemovedLegacyMultiTextureDefinition_Throws()
    {
        var exception = Assert.Throws<KeyNotFoundException>(() => new MaterialAsset("legacy-multi-texture"));
        Assert.Contains("legacy-multi-texture", exception.Message);
    }

    [Fact]
    public void CompileRuntimeMaterial_LitDiffuseMaterial_ReturnsCurrentRuntimeType()
    {
        var materialAsset = new MaterialAsset("lit-diffuse");
        materialAsset.SetPropertyValue("diffuse_color", MaterialValue.FromColor(Color.Orange));
        materialAsset.SetPropertyValue("specular_power", MaterialValue.FromFloat(12.0f));
        materialAsset.SetPropertyValue("alpha_cutoff", MaterialValue.FromFloat(0.38f));

        var runtimeMaterial = new MaterialCompiler().CompileRuntimeMaterial(materialAsset, new AssetContentManager());

        var litMaterial = Assert.IsType<LitDiffuseMaterial>(runtimeMaterial);
        Assert.Equal(Color.Orange, litMaterial.DiffuseColor);
        Assert.Equal(12.0f, litMaterial.SpecularPower);
        Assert.Equal(0.38f, litMaterial.AlphaCutoff);
    }

    [Fact]
    public void Compile_LitDiffuseMaterial_WithReflectionAndAmbient_EmitsReflectiveFeatures()
    {
        var reflectionTextureId = Guid.NewGuid();
        var materialAsset = new MaterialAsset("lit-diffuse")
        {
            Name = "Reflective Lit Asset",
        };
        materialAsset.SetPropertyValue("reflection_texture", MaterialValue.FromTextureId(reflectionTextureId));
        materialAsset.SetPropertyValue("ambient_color", MaterialValue.FromVector3(new Vector3(0.2f, 0.3f, 0.4f)));

        var compiledMaterial = new MaterialCompiler().Compile(materialAsset, new AssetContentManager());
        var runtimeMaterial = Assert.IsType<LitDiffuseMaterial>(new MaterialCompiler().CompileRuntimeMaterial(materialAsset, new AssetContentManager()));

        Assert.Equal(EffectiveShaderResolver.ReflectiveLitForwardShaderId, compiledMaterial.EffectiveShader.ShaderId);
        Assert.Equal(ShaderFeature.Reflection, compiledMaterial.Features);
        Assert.True(compiledMaterial.TryGetTexture("reflection_texture", out var reflectionTexture));
        Assert.Null(reflectionTexture);
        Assert.True(compiledMaterial.TryGetTextureBinding("reflection_texture", out var reflectionBinding));
        Assert.Equal(CompiledMaterialTextureBindingKind.TextureCube, reflectionBinding.Kind);
        Assert.Equal(reflectionTextureId, reflectionBinding.AssetId);
        Assert.Equal(new Vector3(0.2f, 0.3f, 0.4f), runtimeMaterial.AmbientColor);
        Assert.Equal(reflectionTextureId, runtimeMaterial.ReflectionCubeAssetId);
    }

    [Fact]
    public void Compile_UsesRegisteredRuntimeMaterialFactory_ForBuiltInDefinition()
    {
        var customShaderId = Guid.NewGuid();
        var materialAsset = new MaterialAsset("lit-diffuse");

        using var registration = MaterialCompiler.RegisterRuntimeMaterialFactory(
            "lit-diffuse",
            (asset, definition, effectiveValues, resolvedTextures, assetContentManager) => new LitDiffuseMaterial
            {
                Id = asset.Id,
                Name = asset.Name,
                ShaderAssetId = customShaderId,
            });

        var compiledMaterial = new MaterialCompiler().Compile(materialAsset, new AssetContentManager());

        Assert.Equal(customShaderId, compiledMaterial.EffectiveShader.ShaderId);
    }
}