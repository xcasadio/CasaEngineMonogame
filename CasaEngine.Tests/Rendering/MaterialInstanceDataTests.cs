using CasaEngine.Framework.Materials;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class MaterialInstanceDataTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsTypedPropertyOverrides()
    {
        var materialInstanceData = new MaterialInstanceData();
        var textureId = Guid.NewGuid();

        materialInstanceData.SetPropertyOverride("diffuse_color", MaterialValue.FromColor(Color.CornflowerBlue));
        materialInstanceData.SetPropertyOverride("specular_power", MaterialValue.FromFloat(24.0f));
        materialInstanceData.SetPropertyOverride("base_color_texture", MaterialValue.FromTextureId(textureId));
        materialInstanceData.SetPropertyOverride("emissive_color", MaterialValue.FromVector3(new Vector3(1.0f, 2.0f, 3.0f)));
        materialInstanceData.SetPropertyOverride("use_detail", MaterialValue.FromBoolean(true));

        var document = new JObject();
        MaterialInstanceDataJsonSerializer.Save(materialInstanceData, document);

        var propertyOverridesNode = Assert.IsType<JObject>(document["property_overrides"]);
        Assert.Equal("Color", propertyOverridesNode["diffuse_color"]!["type"]!.Value<string>());
        Assert.Equal("Float", propertyOverridesNode["specular_power"]!["type"]!.Value<string>());
        Assert.Equal("Texture", propertyOverridesNode["base_color_texture"]!["type"]!.Value<string>());
        Assert.Equal("Vector3", propertyOverridesNode["emissive_color"]!["type"]!.Value<string>());
        Assert.Equal("Boolean", propertyOverridesNode["use_detail"]!["type"]!.Value<string>());

        var loadedMaterialInstanceData = new MaterialInstanceData();
        loadedMaterialInstanceData.Load(document);

        Assert.True(loadedMaterialInstanceData.TryGetPropertyOverride("diffuse_color", out var loadedDiffuseColor));
        Assert.Equal(MaterialValue.FromColor(Color.CornflowerBlue), loadedDiffuseColor);
        Assert.True(loadedMaterialInstanceData.TryGetPropertyOverride("specular_power", out var loadedSpecularPower));
        Assert.Equal(MaterialValue.FromFloat(24.0f), loadedSpecularPower);
        Assert.True(loadedMaterialInstanceData.TryGetPropertyOverride("base_color_texture", out var loadedBaseColorTexture));
        Assert.Equal(MaterialValue.FromTextureId(textureId), loadedBaseColorTexture);
        Assert.True(loadedMaterialInstanceData.TryGetPropertyOverride("emissive_color", out var loadedEmissiveColor));
        Assert.Equal(MaterialValue.FromVector3(new Vector3(1.0f, 2.0f, 3.0f)), loadedEmissiveColor);
        Assert.True(loadedMaterialInstanceData.TryGetPropertyOverride("use_detail", out var loadedUseDetail));
        Assert.Equal(MaterialValue.FromBoolean(true), loadedUseDetail);
    }

    [Fact]
    public void TrySetPropertyOverride_ValidatesAgainstDefinitionAndNormalizesStoredKey()
    {
        var definition = CreateDefinition();
        var materialInstanceData = new MaterialInstanceData();

        bool updated = materialInstanceData.TrySetPropertyOverride(
            definition,
            "legacy_tint",
            MaterialValue.FromColor(Color.White),
            out var validationError);

        Assert.True(updated);
        Assert.Null(validationError);
        Assert.True(materialInstanceData.TryGetPropertyOverride("tint_color", out var tintColor));
        Assert.Equal(MaterialValue.FromColor(Color.White), tintColor);
        Assert.False(materialInstanceData.HasPropertyOverride("legacy_tint"));
    }

    [Fact]
    public void ValidateAgainst_ReturnsErrorsForUnknownAndIncompatibleOverrides()
    {
        var definition = CreateDefinition();
        var materialInstanceData = new MaterialInstanceData();
        materialInstanceData.SetPropertyOverride("missing_property", MaterialValue.FromFloat(1.0f));
        materialInstanceData.SetPropertyOverride("roughness", MaterialValue.FromColor(Color.Red));

        var errors = materialInstanceData.ValidateAgainst(definition);

        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, error => error.Contains("missing_property", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("roughness", StringComparison.Ordinal));
    }

    private static MaterialDefinition CreateDefinition()
        => new(
            id: "test-material",
            displayName: "Test Material",
            runtimeMaterialType: typeof(LitDiffuseMaterial),
            properties: new[]
            {
                new MaterialPropertyDefinition(
                    key: "tint_color",
                    displayName: "Tint",
                    valueType: MaterialPropertyType.Color,
                    group: MaterialPropertyGroup.Surface,
                    legacyAliases: new[] { "legacy_tint" }),
                new MaterialPropertyDefinition(
                    key: "roughness",
                    displayName: "Roughness",
                    valueType: MaterialPropertyType.Float,
                    group: MaterialPropertyGroup.Surface,
                    minValue: 0.0f,
                    maxValue: 1.0f,
                    step: 0.05f),
            });
}