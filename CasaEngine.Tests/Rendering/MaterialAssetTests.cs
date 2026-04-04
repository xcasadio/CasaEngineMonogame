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
    public void BlendStateName_RejectsUnknownStateName()
    {
        var materialAsset = new MaterialAsset("lit-diffuse");

        Assert.Throws<ArgumentException>(() => materialAsset.BlendStateName = "CustomBlend");
    }
}