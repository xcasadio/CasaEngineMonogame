using CasaEngine.Framework.Materials;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class MaterialDefinitionRegistryTests
{
    [Fact]
    public void TryGetById_ReturnsBuiltInDefinitions()
    {
        Assert.True(MaterialDefinitionRegistry.TryGetById("lit-diffuse", out var litDiffuseDefinition));
        Assert.Equal(typeof(LitDiffuseMaterial), litDiffuseDefinition.RuntimeMaterialType);

        Assert.True(MaterialDefinitionRegistry.TryGetById("unlit-texture", out var unlitDefinition));
        Assert.Equal(typeof(UnlitTextureMaterial), unlitDefinition.RuntimeMaterialType);
    }

    [Theory]
    [InlineData(nameof(LitDiffuseMaterial), "lit-diffuse")]
    [InlineData(nameof(UnlitTextureMaterial), "unlit-texture")]
    [InlineData(nameof(Material), "legacy-multi-texture")]
    public void TryGetByLegacyTypeName_MapsLegacyRuntimeTypes(string legacyTypeName, string expectedDefinitionId)
    {
        Assert.True(MaterialDefinitionRegistry.TryGetByLegacyTypeName(legacyTypeName, out var definition));
        Assert.Equal(expectedDefinitionId, definition.Id);
    }

    [Fact]
    public void LitDiffuseDefinition_ExposesExpectedPropertyMetadata()
    {
        var definition = MaterialDefinitionRegistry.GetRequiredById("lit-diffuse");

        var baseColorProperty = definition.GetRequiredProperty("base_color_texture");
        Assert.Equal(MaterialPropertyType.Texture, baseColorProperty.ValueType);
        Assert.Equal(MaterialPropertyGroup.Textures, baseColorProperty.Group);
        Assert.Equal(Guid.Empty, baseColorProperty.GetDefaultValue<Guid>());
        Assert.Contains("BasColor_asset_id", baseColorProperty.LegacyAliases);
        Assert.Contains("albedo_asset_id", baseColorProperty.LegacyAliases);
        Assert.Equal("texture", baseColorProperty.AssetKind);

        var specularPowerProperty = definition.GetRequiredProperty("specular_power");
        Assert.Equal(MaterialPropertyType.Float, specularPowerProperty.ValueType);
        Assert.Equal(MaterialPropertyGroup.Lighting, specularPowerProperty.Group);
        Assert.Equal(16.0f, specularPowerProperty.GetDefaultValue<float>());
        Assert.Equal(0.0f, specularPowerProperty.MinValue);
        Assert.Equal(128.0f, specularPowerProperty.MaxValue);
        Assert.Equal(1.0f, specularPowerProperty.Step);

        var alphaCutoffProperty = definition.GetRequiredProperty("alpha_cutoff");
        Assert.Equal(MaterialPropertyType.Float, alphaCutoffProperty.ValueType);
        Assert.Equal(MaterialPropertyGroup.Rendering, alphaCutoffProperty.Group);
        Assert.Equal(0.5f, alphaCutoffProperty.GetDefaultValue<float>());
        Assert.Equal(0.0f, alphaCutoffProperty.MinValue);
        Assert.Equal(1.0f, alphaCutoffProperty.MaxValue);
        Assert.Equal(0.01f, alphaCutoffProperty.Step);
    }

    [Fact]
    public void UnlitDefinition_ExposesAlphaSettingsAsRenderingProperties()
    {
        var definition = MaterialDefinitionRegistry.GetRequiredById("unlit-texture");

        var alphaProperty = definition.GetRequiredProperty("alpha");
        Assert.Equal(MaterialPropertyType.Float, alphaProperty.ValueType);
        Assert.Equal(MaterialPropertyGroup.Rendering, alphaProperty.Group);
        Assert.Equal(1.0f, alphaProperty.GetDefaultValue<float>());
        Assert.Equal(0.0f, alphaProperty.MinValue);
        Assert.Equal(1.0f, alphaProperty.MaxValue);
        Assert.Equal(0.01f, alphaProperty.Step);

        var alphaCutoffProperty = definition.GetRequiredProperty("alpha_cutoff");
        Assert.Equal(MaterialPropertyType.Float, alphaCutoffProperty.ValueType);
        Assert.Equal(MaterialPropertyGroup.Rendering, alphaCutoffProperty.Group);
        Assert.Equal(0.5f, alphaCutoffProperty.GetDefaultValue<float>());
        Assert.Equal(0.0f, alphaCutoffProperty.MinValue);
        Assert.Equal(1.0f, alphaCutoffProperty.MaxValue);
        Assert.Equal(0.01f, alphaCutoffProperty.Step);
    }

    [Fact]
    public void MaterialPropertyDefinition_RejectsIncompatibleDefaultValue()
    {
        Assert.Throws<ArgumentException>(() => new MaterialPropertyDefinition(
            key: "invalid_default",
            displayName: "Invalid Default",
            valueType: MaterialPropertyType.Float,
            group: MaterialPropertyGroup.Surface,
            defaultValue: Color.White));
    }

    [Fact]
    public void MaterialDefinition_RejectsDuplicateSerializedNames()
    {
        var firstProperty = new MaterialPropertyDefinition(
            key: "base_color_texture",
            displayName: "Base Color",
            valueType: MaterialPropertyType.Texture,
            group: MaterialPropertyGroup.Textures,
            defaultValue: Guid.Empty,
            legacyAliases: new[] { "albedo_asset_id" },
            assetKind: "texture");

        var conflictingProperty = new MaterialPropertyDefinition(
            key: "other_texture",
            displayName: "Other Texture",
            valueType: MaterialPropertyType.Texture,
            group: MaterialPropertyGroup.Textures,
            defaultValue: Guid.Empty,
            legacyAliases: new[] { "albedo_asset_id" },
            assetKind: "texture");

        Assert.Throws<ArgumentException>(() => new MaterialDefinition(
            id: "invalid-definition",
            displayName: "Invalid Definition",
            runtimeMaterialType: typeof(LitDiffuseMaterial),
            properties: new[] { firstProperty, conflictingProperty }));
    }
}