
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
    public void Load_MaterialWithoutDefinitionId_ThrowsInvalidOperationException()
    {
        var document = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "Legacy Lit",
            ["is_transparent"] = false,
            ["queue"] = RenderQueue.Opaque.ToString(),
            ["shader_asset_id"] = Guid.Empty.ToString(),
            ["cast_shadows"] = true,
            ["receive_shadows"] = true,
            ["blend_state"] = "Opaque",
            ["depth_stencil_state"] = "Default",
            ["rasterizer_state"] = "CullCounterClockwise",
            ["sampler_state"] = "AnisotropicClamp",
        };

        Assert.Throws<InvalidOperationException>(() => new MaterialAsset().Load(document));
    }
}