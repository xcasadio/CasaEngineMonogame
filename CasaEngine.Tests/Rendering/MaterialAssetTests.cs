using CasaEngine.Framework.Materials;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class MaterialAssetTests
{
    [Fact]
    public void Constructor_RejectsUnknownDefinitionId()
    {
        Assert.Throws<KeyNotFoundException>(() => new MaterialAsset("missing-definition"));
    }

    [Fact]
    public void SetPropertyValue_StoresCanonicalPropertyKeyWhenUsingLegacyAlias()
    {
        var materialAsset = new MaterialAsset("lit-diffuse");
        var textureId = Guid.NewGuid();

        materialAsset.SetPropertyValue("albedo_asset_id", MaterialValue.FromTextureId(textureId));

        Assert.True(materialAsset.TryGetPropertyValue("base_color_texture", out var storedValue));
        Assert.Equal(MaterialValue.FromTextureId(textureId), storedValue);
        Assert.Single(materialAsset.PropertyValues);
        Assert.True(materialAsset.PropertyValues.ContainsKey("base_color_texture"));
    }

    [Fact]
    public void GetPropertyValueOrDefault_ReturnsDefinitionDefaultValue()
    {
        var materialAsset = new MaterialAsset("unlit-texture");

        var defaultValue = materialAsset.GetPropertyValueOrDefault("alpha");

        Assert.NotNull(defaultValue);
        Assert.True(defaultValue!.TryGetFloat(out var alpha));
        Assert.Equal(1.0f, alpha);
    }

    [Fact]
    public void GetPropertyValueOrDefault_UsesInheritedParentValueWhenLocalOverrideIsMissing()
    {
        var parentMaterial = new MaterialAsset("lit-diffuse");
        parentMaterial.SetPropertyValue("specular_power", MaterialValue.FromFloat(24.0f));

        var childMaterial = new MaterialAsset("lit-diffuse")
        {
            ParentMaterialAssetId = parentMaterial.Id,
        };

        MaterialValue? resolvedValue = childMaterial.GetPropertyValueOrDefault(
            "specular_power",
            assetId => assetId == parentMaterial.Id ? parentMaterial : null);

        Assert.NotNull(resolvedValue);
        Assert.True(resolvedValue!.TryGetFloat(out var specularPower));
        Assert.Equal(24.0f, specularPower);
        Assert.False(childMaterial.HasLocalPropertyValue("specular_power"));
        Assert.True(childMaterial.TryGetInheritedPropertyValue(
            "specular_power",
            assetId => assetId == parentMaterial.Id ? parentMaterial : null,
            out var inheritedValue));
        Assert.Equal(MaterialValue.FromFloat(24.0f), inheritedValue);
    }

    [Fact]
    public void GetPropertyValueOrDefault_WhenParentChainCycles_FallsBackToDefinitionDefaultValue()
    {
        var parentMaterial = new MaterialAsset("unlit-texture");
        var childMaterial = new MaterialAsset("unlit-texture")
        {
            ParentMaterialAssetId = parentMaterial.Id,
        };
        parentMaterial.ParentMaterialAssetId = childMaterial.Id;

        MaterialValue? resolvedValue = childMaterial.GetPropertyValueOrDefault(
            "alpha",
            assetId => assetId == parentMaterial.Id ? parentMaterial : childMaterial);

        Assert.NotNull(resolvedValue);
        Assert.True(resolvedValue!.TryGetFloat(out var alpha));
        Assert.Equal(1.0f, alpha);
    }

    [Fact]
    public void SetPropertyValue_RejectsIncompatibleOrOutOfRangeValues()
    {
        var materialAsset = new MaterialAsset("unlit-texture");

        Assert.Throws<ArgumentException>(() => materialAsset.SetPropertyValue("alpha", MaterialValue.FromInteger(1)));
        Assert.Throws<ArgumentException>(() => materialAsset.SetPropertyValue("alpha", MaterialValue.FromFloat(2.0f)));
    }

    [Fact]
    public void ChangingDefinition_PreservesCompatibleValuesAndDropsInvalidOnes()
    {
        var materialAsset = new MaterialAsset("lit-diffuse");
        var textureId = Guid.NewGuid();

        materialAsset.SetPropertyValue("base_color_texture", MaterialValue.FromTextureId(textureId));
        materialAsset.SetPropertyValue("specular_power", MaterialValue.FromFloat(24.0f));

        materialAsset.DefinitionId = "unlit-texture";

        Assert.True(materialAsset.TryGetPropertyValue("base_color_texture", out var preservedValue));
        Assert.Equal(MaterialValue.FromTextureId(textureId), preservedValue);
        Assert.False(materialAsset.TryGetPropertyValue("specular_power", out _));
        Assert.Single(materialAsset.PropertyValues);
    }

    [Fact]
    public void Validate_ReturnsErrorWhenAssetParentsItself()
    {
        var materialAsset = new MaterialAsset("lit-diffuse")
        {
            ParentMaterialAssetId = Guid.Empty,
        };

        materialAsset.ParentMaterialAssetId = materialAsset.Id;

        var errors = materialAsset.Validate();

        Assert.Single(errors);
        Assert.Equal("Material asset cannot parent itself.", errors[0]);
    }

    [Fact]
    public void Validate_WithParentResolver_ReturnsCycleErrorWhenParentChainLoops()
    {
        var parentMaterial = new MaterialAsset("lit-diffuse");
        var childMaterial = new MaterialAsset("lit-diffuse")
        {
            ParentMaterialAssetId = parentMaterial.Id,
        };
        parentMaterial.ParentMaterialAssetId = childMaterial.Id;

        var errors = childMaterial.Validate(assetId => assetId == parentMaterial.Id ? parentMaterial : childMaterial);

        Assert.Contains($"Material asset '{childMaterial.Name}' participates in a parent cycle.", errors);
    }

    [Fact]
    public void BlendStateName_RejectsUnknownStateName()
    {
        var materialAsset = new MaterialAsset("lit-diffuse");

        Assert.Throws<ArgumentException>(() => materialAsset.BlendStateName = "CustomBlend");
    }
}