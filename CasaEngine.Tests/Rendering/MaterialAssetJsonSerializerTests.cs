using CasaEngine.Framework.Materials;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class MaterialAssetJsonSerializerTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsMaterialAssetAuthoringFormat()
    {
        var materialAsset = new MaterialAsset("lit-diffuse")
        {
            Name = "Lit Material",
            ParentMaterialAssetId = Guid.NewGuid(),
            ShaderAssetId = Guid.NewGuid(),
            IsTransparent = true,
            Queue = RenderQueue.Transparent,
            CastShadows = false,
            ReceiveShadows = false,
            BlendStateName = "AlphaBlend",
            DepthStencilStateName = "Read",
            RasterizerStateName = "CullNone",
            SamplerStateName = "LinearWrap",
        };
        var baseColorTextureId = Guid.NewGuid();
        materialAsset.SetPropertyValue("base_color_texture", MaterialValue.FromTextureId(baseColorTextureId));
        materialAsset.SetPropertyValue("diffuse_color", MaterialValue.FromColor(Color.Red));

        var document = new JObject();
        MaterialAssetJsonSerializer.Save(materialAsset, document);

        var propertiesNode = Assert.IsType<JObject>(document["properties"]);
        Assert.Equal(2, propertiesNode.Count);
        Assert.NotNull(propertiesNode["base_color_texture"]);
        Assert.NotNull(propertiesNode["diffuse_color"]);
        Assert.Null(propertiesNode["specular_power"]);

        var loadedMaterialAsset = new MaterialAsset();
        loadedMaterialAsset.Load(document);

        Assert.Equal(materialAsset.Id, loadedMaterialAsset.Id);
        Assert.Equal(materialAsset.Name, loadedMaterialAsset.Name);
        Assert.Equal(materialAsset.DefinitionId, loadedMaterialAsset.DefinitionId);
        Assert.Equal(materialAsset.ParentMaterialAssetId, loadedMaterialAsset.ParentMaterialAssetId);
        Assert.Equal(materialAsset.ShaderAssetId, loadedMaterialAsset.ShaderAssetId);
        Assert.Equal(materialAsset.IsTransparent, loadedMaterialAsset.IsTransparent);
        Assert.Equal(materialAsset.Queue, loadedMaterialAsset.Queue);
        Assert.Equal(materialAsset.CastShadows, loadedMaterialAsset.CastShadows);
        Assert.Equal(materialAsset.ReceiveShadows, loadedMaterialAsset.ReceiveShadows);
        Assert.Equal(materialAsset.BlendStateName, loadedMaterialAsset.BlendStateName);
        Assert.Equal(materialAsset.DepthStencilStateName, loadedMaterialAsset.DepthStencilStateName);
        Assert.Equal(materialAsset.RasterizerStateName, loadedMaterialAsset.RasterizerStateName);
        Assert.Equal(materialAsset.SamplerStateName, loadedMaterialAsset.SamplerStateName);
        Assert.True(loadedMaterialAsset.TryGetPropertyValue("base_color_texture", out var loadedTextureValue));
        Assert.True(loadedTextureValue.TryGetTextureId(out var loadedTextureId));
        Assert.Equal(baseColorTextureId, loadedTextureId);
        Assert.True(loadedMaterialAsset.TryGetPropertyValue("diffuse_color", out var loadedColorValue));
        Assert.True(loadedColorValue.TryGetColor(out var loadedColor));
        Assert.Equal(Color.Red, loadedColor);
        var defaultSpecularPower = loadedMaterialAsset.GetPropertyValueOrDefault("specular_power");
        Assert.NotNull(defaultSpecularPower);
        Assert.True(defaultSpecularPower!.TryGetFloat(out var specularPower));
        Assert.Equal(16.0f, specularPower);
    }

    [Fact]
    public void Load_RejectsUnknownPropertyKey()
    {
        var document = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "Broken Material",
            ["definition_id"] = "unlit-texture",
            ["shader_asset_id"] = Guid.Empty.ToString(),
            ["is_transparent"] = false,
            ["queue"] = RenderQueue.Opaque.ToString(),
            ["cast_shadows"] = true,
            ["receive_shadows"] = true,
            ["blend_state"] = MaterialAsset.DefaultBlendStateName,
            ["depth_stencil_state"] = MaterialAsset.DefaultDepthStencilStateName,
            ["rasterizer_state"] = MaterialAsset.DefaultRasterizerStateName,
            ["sampler_state"] = MaterialAsset.DefaultSamplerStateName,
            ["properties"] = new JObject
            {
                ["unknown_property"] = 1.0f,
            },
        };

        Assert.Throws<KeyNotFoundException>(() => new MaterialAsset().Load(document));
    }

    [Fact]
    public void Load_LegacyLitDiffuseMaterial_MapsToAuthoringDefinition()
    {
        var legacyBaseColorTextureId = Guid.NewGuid();
        var legacyNormalTextureId = Guid.NewGuid();
        var shaderAssetId = Guid.NewGuid();
        var document = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "Legacy Lit",
            ["type"] = nameof(LitDiffuseMaterial),
            ["is_transparent"] = false,
            ["queue"] = RenderQueue.Opaque.ToString(),
            ["shader_asset_id"] = shaderAssetId.ToString(),
            ["cast_shadows"] = true,
            ["receive_shadows"] = false,
            ["blend_state"] = "Opaque",
            ["depth_stencil_state"] = "Default",
            ["rasterizer_state"] = "CullCounterClockwise",
            ["sampler_state"] = "AnisotropicClamp",
            ["BasColor_asset_id"] = legacyBaseColorTextureId.ToString(),
            ["normal_map_asset_id"] = legacyNormalTextureId.ToString(),
            ["diffuse_color"] = new JObject
            {
                ["r"] = 10,
                ["g"] = 20,
                ["b"] = 30,
                ["a"] = 255,
            },
            ["emissive_color"] = new JObject
            {
                ["r"] = 1.0f,
                ["g"] = 2.0f,
                ["b"] = 3.0f,
            },
            ["specular_color"] = new JObject
            {
                ["r"] = 0.1f,
                ["g"] = 0.2f,
                ["b"] = 0.3f,
            },
            ["specular_power"] = 32.0f,
        };

        var materialAsset = new MaterialAsset();
        materialAsset.Load(document);

        Assert.Equal("lit-diffuse", materialAsset.DefinitionId);
        Assert.Equal(shaderAssetId, materialAsset.ShaderAssetId);
        Assert.False(materialAsset.ReceiveShadows);
        Assert.True(materialAsset.TryGetPropertyValue("base_color_texture", out var baseColorTextureValue));
        Assert.True(baseColorTextureValue.TryGetTextureId(out var loadedBaseColorTextureId));
        Assert.Equal(legacyBaseColorTextureId, loadedBaseColorTextureId);
        Assert.True(materialAsset.TryGetPropertyValue("normal_texture", out var normalTextureValue));
        Assert.True(normalTextureValue.TryGetTextureId(out var loadedNormalTextureId));
        Assert.Equal(legacyNormalTextureId, loadedNormalTextureId);
        Assert.True(materialAsset.TryGetPropertyValue("diffuse_color", out var diffuseColorValue));
        Assert.True(diffuseColorValue.TryGetColor(out var diffuseColor));
        Assert.Equal(new Color(10, 20, 30, 255), diffuseColor);
        Assert.True(materialAsset.TryGetPropertyValue("emissive_color", out var emissiveColorValue));
        Assert.True(emissiveColorValue.TryGetVector3(out var emissiveColor));
        Assert.Equal(new Vector3(1.0f, 2.0f, 3.0f), emissiveColor);
        Assert.True(materialAsset.TryGetPropertyValue("specular_color", out var specularColorValue));
        Assert.True(specularColorValue.TryGetVector3(out var specularColor));
        Assert.Equal(new Vector3(0.1f, 0.2f, 0.3f), specularColor);
        Assert.True(materialAsset.TryGetPropertyValue("specular_power", out var specularPowerValue));
        Assert.True(specularPowerValue.TryGetFloat(out var specularPower));
        Assert.Equal(32.0f, specularPower);
    }

    [Fact]
    public void Load_LegacyUnlitTextureMaterial_MapsToAuthoringDefinition()
    {
        var baseColorTextureId = Guid.NewGuid();
        var document = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "Legacy Unlit",
            ["type"] = nameof(UnlitTextureMaterial),
            ["is_transparent"] = true,
            ["queue"] = RenderQueue.Transparent.ToString(),
            ["shader_asset_id"] = Guid.Empty.ToString(),
            ["cast_shadows"] = false,
            ["receive_shadows"] = false,
            ["blend_state"] = "AlphaBlend",
            ["depth_stencil_state"] = "Read",
            ["rasterizer_state"] = "CullNone",
            ["sampler_state"] = "LinearWrap",
            ["BasColor_asset_id"] = baseColorTextureId.ToString(),
            ["tint_color"] = new JObject
            {
                ["r"] = 200,
                ["g"] = 150,
                ["b"] = 100,
                ["a"] = 50,
            },
            ["alpha"] = 0.25f,
        };

        var materialAsset = new MaterialAsset();
        materialAsset.Load(document);

        Assert.Equal("unlit-texture", materialAsset.DefinitionId);
        Assert.True(materialAsset.IsTransparent);
        Assert.Equal(RenderQueue.Transparent, materialAsset.Queue);
        Assert.Equal("AlphaBlend", materialAsset.BlendStateName);
        Assert.Equal("Read", materialAsset.DepthStencilStateName);
        Assert.Equal("CullNone", materialAsset.RasterizerStateName);
        Assert.Equal("LinearWrap", materialAsset.SamplerStateName);
        Assert.True(materialAsset.TryGetPropertyValue("base_color_texture", out var baseColorTextureValue));
        Assert.True(baseColorTextureValue.TryGetTextureId(out var loadedBaseColorTextureId));
        Assert.Equal(baseColorTextureId, loadedBaseColorTextureId);
        Assert.True(materialAsset.TryGetPropertyValue("tint_color", out var tintColorValue));
        Assert.True(tintColorValue.TryGetColor(out var tintColor));
        Assert.Equal(new Color(200, 150, 100, 50), tintColor);
        Assert.True(materialAsset.TryGetPropertyValue("alpha", out var alphaValue));
        Assert.True(alphaValue.TryGetFloat(out var alpha));
        Assert.Equal(0.25f, alpha);
    }

    [Fact]
    public void Load_LegacyMaterial_MapsToLegacyMultiTextureDefinition()
    {
        var baseColorTextureId = Guid.NewGuid();
        var normalTextureId = Guid.NewGuid();
        var roughnessTextureId = Guid.NewGuid();
        var document = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "Legacy Multi Texture",
            ["type"] = nameof(Material),
            ["is_transparent"] = false,
            ["queue"] = RenderQueue.Opaque.ToString(),
            ["shader_asset_id"] = Guid.Empty.ToString(),
            ["cast_shadows"] = true,
            ["receive_shadows"] = true,
            ["blend_state"] = "Opaque",
            ["depth_stencil_state"] = "Default",
            ["rasterizer_state"] = "CullCounterClockwise",
            ["sampler_state"] = "AnisotropicClamp",
            ["texture_base_color_asset_id"] = baseColorTextureId.ToString(),
            ["texture_normal_asset_id"] = normalTextureId.ToString(),
            ["texture_roughness_asset_id"] = roughnessTextureId.ToString(),
        };

        var materialAsset = new MaterialAsset();
        materialAsset.Load(document);

        Assert.Equal("legacy-multi-texture", materialAsset.DefinitionId);
        Assert.True(materialAsset.TryGetPropertyValue("base_color_texture", out var baseColorTextureValue));
        Assert.True(baseColorTextureValue.TryGetTextureId(out var loadedBaseColorTextureId));
        Assert.Equal(baseColorTextureId, loadedBaseColorTextureId);
        Assert.True(materialAsset.TryGetPropertyValue("normal_texture", out var normalTextureValue));
        Assert.True(normalTextureValue.TryGetTextureId(out var loadedNormalTextureId));
        Assert.Equal(normalTextureId, loadedNormalTextureId);
        Assert.True(materialAsset.TryGetPropertyValue("roughness_texture", out var roughnessTextureValue));
        Assert.True(roughnessTextureValue.TryGetTextureId(out var loadedRoughnessTextureId));
        Assert.Equal(roughnessTextureId, loadedRoughnessTextureId);
    }
}