using CasaEngine.Framework.Assets;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Rendering.Shaders;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class LitDiffuseDetailTextureTests
{
    [Fact]
    public void Defaults_HaveNoDetailTextureAndATilingOfOne()
    {
        var material = new LitDiffuseMaterial();

        Assert.Null(material.DetailTexture);
        Assert.Equal(Guid.Empty, material.DetailTextureAssetId);
        Assert.Equal(1.0f, material.DetailTiling);
    }

    [Theory]
    [InlineData(4.0f, 4.0f)]
    [InlineData(0.25f, 0.25f)]
    [InlineData(24.0f, 24.0f)]
    [InlineData(0.0f, 1.0f)]
    [InlineData(-3.0f, 1.0f)]
    [InlineData(float.NaN, 1.0f)]
    [InlineData(float.PositiveInfinity, 1.0f)]
    [InlineData(float.NegativeInfinity, 1.0f)]
    public void DetailTiling_ReplacesAValueThatIsNotFiniteOrNotPositiveByOne(float assigned, float expected)
    {
        var material = new LitDiffuseMaterial { DetailTiling = assigned };

        Assert.Equal(expected, material.DetailTiling);
    }

    [Fact]
    public void DefinitionDeclaresTheDetailPropertiesWithTheirDefaultsAndAlias()
    {
        var definition = MaterialDefinitionRegistry.GetRequiredById("lit-diffuse");

        var detailTexture = definition.GetRequiredProperty("detail_texture");
        Assert.Equal(MaterialPropertyType.Texture, detailTexture.ValueType);
        Assert.Equal(MaterialPropertyGroup.Textures, detailTexture.Group);
        Assert.Equal(Guid.Empty, detailTexture.GetDefaultValue<Guid>());
        Assert.Contains("texture_detail_asset_id", detailTexture.LegacyAliases);
        Assert.Equal("texture", detailTexture.AssetKind);

        var detailTiling = definition.GetRequiredProperty("detail_tiling");
        Assert.Equal(MaterialPropertyType.Float, detailTiling.ValueType);
        Assert.Equal(1.0f, detailTiling.GetDefaultValue<float>());
    }

    [Fact]
    public void Load_ReadsTheDetailTextureAssetIdAndTheTiling()
    {
        var detailTextureId = Guid.NewGuid();
        var material = new LitDiffuseMaterial();

        material.Load(JObject.Parse($$"""
            {
              "texture_detail_asset_id": "{{detailTextureId}}",
              "detail_tiling": 24.0
            }
            """));

        Assert.Equal(detailTextureId, material.DetailTextureAssetId);
        Assert.Equal(24.0f, material.DetailTiling);
    }

    [Fact]
    public void Load_AssetWithoutTheDetailKeys_KeepsTheDefaults()
    {
        var normalMapId = Guid.NewGuid();
        var material = new LitDiffuseMaterial();

        material.Load(JObject.Parse($$"""
            {
              "normal_map_asset_id": "{{normalMapId}}",
              "specular_power": 8.0
            }
            """));

        Assert.Equal(normalMapId, material.NormalMapAssetId);
        Assert.Equal(Guid.Empty, material.DetailTextureAssetId);
        Assert.Null(material.DetailTexture);
        Assert.Equal(1.0f, material.DetailTiling);
    }

    [Fact]
    public void Load_AnInvalidTilingIsReadAsOne()
    {
        var material = new LitDiffuseMaterial();

        material.Load(JObject.Parse("""{ "detail_tiling": -2.0 }"""));

        Assert.Equal(1.0f, material.DetailTiling);
    }

    [Fact]
    public void EditorSerializer_WritesTheDetailKeysAndLoadReadsThemBack()
    {
        var detailTextureId = Guid.NewGuid();
        var saved = new LitDiffuseMaterial
        {
            Name = "Detailed",
            DetailTextureAssetId = detailTextureId,
            DetailTiling = 6.0f,
        };

        Assert.True(EditorAssetJsonSerializer.TrySerialize(saved, out var document));

        Assert.Equal(detailTextureId.ToString(), document["texture_detail_asset_id"]!.Value<string>());
        Assert.Equal(6.0f, document["detail_tiling"]!.Value<float>());

        var loaded = new LitDiffuseMaterial();
        loaded.Load(document);

        Assert.Equal(detailTextureId, loaded.DetailTextureAssetId);
        Assert.Equal(6.0f, loaded.DetailTiling);
    }

    [Fact]
    public void MaterialAsset_RoundTripsADetailTextureAndATilingThroughTheDefinitionAndTheCompiler()
    {
        var detailTextureId = Guid.NewGuid();
        var materialAsset = new MaterialAsset("lit-diffuse") { Name = "Detail Asset" };
        materialAsset.SetPropertyValue("texture_detail_asset_id", MaterialValue.FromTextureId(detailTextureId));
        materialAsset.SetPropertyValue("detail_tiling", MaterialValue.FromFloat(24.0f));

        var document = new JObject();
        MaterialAssetJsonSerializer.Save(materialAsset, document);
        var propertiesNode = Assert.IsType<JObject>(document["properties"]);
        Assert.NotNull(propertiesNode["detail_texture"]);
        Assert.NotNull(propertiesNode["detail_tiling"]);

        var loadedAsset = new MaterialAsset();
        loadedAsset.Load(document);

        var runtimeMaterial = Assert.IsType<LitDiffuseMaterial>(
            new MaterialCompiler().CompileRuntimeMaterial(loadedAsset, new AssetContentManager()));
        Assert.Equal(detailTextureId, runtimeMaterial.DetailTextureAssetId);
        Assert.Equal(24.0f, runtimeMaterial.DetailTiling);

        var compiledMaterial = new MaterialCompiler().Compile(loadedAsset, new AssetContentManager());
        Assert.True(compiledMaterial.TryGetTextureBinding("detail_texture", out var detailBinding));
        Assert.Equal(CompiledMaterialTextureBindingKind.Texture2D, detailBinding.Kind);
        Assert.Equal(detailTextureId, detailBinding.AssetId);
        Assert.True(compiledMaterial.TryGetPropertyValue("detail_tiling", out var tilingValue));
        Assert.True(tilingValue.TryGetFloat(out var tiling));
        Assert.Equal(24.0f, tiling);

        // The detail texture does not change the shader variant: same features as without it.
        var withoutDetail = new MaterialCompiler().Compile(new MaterialAsset("lit-diffuse"), new AssetContentManager());
        Assert.Equal(withoutDetail.Features, compiledMaterial.Features);
        Assert.Equal(withoutDetail.EffectiveShader.ShaderId, compiledMaterial.EffectiveShader.ShaderId);
    }

    [Fact]
    public void MaterialAsset_WithoutTheDetailKeys_CompilesToTheDefaults()
    {
        var materialAsset = new MaterialAsset("lit-diffuse");
        materialAsset.SetPropertyValue("specular_power", MaterialValue.FromFloat(12.0f));

        var document = new JObject();
        MaterialAssetJsonSerializer.Save(materialAsset, document);
        var propertiesNode = Assert.IsType<JObject>(document["properties"]);
        Assert.Null(propertiesNode["detail_texture"]);
        Assert.Null(propertiesNode["detail_tiling"]);

        var loadedAsset = new MaterialAsset();
        loadedAsset.Load(document);

        var runtimeMaterial = Assert.IsType<LitDiffuseMaterial>(
            new MaterialCompiler().CompileRuntimeMaterial(loadedAsset, new AssetContentManager()));
        Assert.Equal(Guid.Empty, runtimeMaterial.DetailTextureAssetId);
        Assert.Null(runtimeMaterial.DetailTexture);
        Assert.Equal(1.0f, runtimeMaterial.DetailTiling);
        Assert.Equal(12.0f, runtimeMaterial.SpecularPower);
    }

    [Fact]
    public void MaterialAsset_AZeroTilingIsCompiledAsOne()
    {
        var materialAsset = new MaterialAsset("lit-diffuse");
        materialAsset.SetPropertyValue("detail_tiling", MaterialValue.FromFloat(0.0f));

        var runtimeMaterial = Assert.IsType<LitDiffuseMaterial>(
            new MaterialCompiler().CompileRuntimeMaterial(materialAsset, new AssetContentManager()));

        Assert.Equal(1.0f, runtimeMaterial.DetailTiling);
    }

    [Fact]
    public void ShaderParameterNames_MatchTheShaderUniforms()
    {
        Assert.Equal("DetailTexture", ShaderParameterNames.DetailTexture);
        Assert.Equal("HasDetailTexture", ShaderParameterNames.HasDetailTexture);
        Assert.Equal("DetailTiling", ShaderParameterNames.DetailTiling);
    }
}
