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
    public void Compile_RejectsUnsupportedMaterialDefinition()
    {
        var materialAsset = new MaterialAsset("legacy-multi-texture");

        Assert.Throws<NotSupportedException>(() => new MaterialCompiler().Compile(materialAsset, new AssetContentManager()));
    }
}