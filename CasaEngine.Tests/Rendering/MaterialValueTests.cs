
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class MaterialValueTests
{
    [Fact]
    public void FromObject_CreatesExpectedTypedValues()
    {
        Assert.True(MaterialValue.FromObject(MaterialPropertyType.Float, 1.5f).TryGetFloat(out var floatValue));
        Assert.Equal(1.5f, floatValue);

        Assert.True(MaterialValue.FromObject(MaterialPropertyType.Integer, 3).TryGetInteger(out var integerValue));
        Assert.Equal(3, integerValue);

        Assert.True(MaterialValue.FromObject(MaterialPropertyType.Boolean, true).TryGetBoolean(out var booleanValue));
        Assert.True(booleanValue);

        Assert.True(MaterialValue.FromObject(MaterialPropertyType.Color, Color.CornflowerBlue).TryGetColor(out var colorValue));
        Assert.Equal(Color.CornflowerBlue, colorValue);

        Assert.True(MaterialValue.FromObject(MaterialPropertyType.Vector3, new Vector3(1.0f, 2.0f, 3.0f)).TryGetVector3(out var vector3Value));
        Assert.Equal(new Vector3(1.0f, 2.0f, 3.0f), vector3Value);

        var textureId = Guid.NewGuid();
        Assert.True(MaterialValue.FromObject(MaterialPropertyType.Texture, textureId).TryGetTextureId(out var resolvedTextureId));
        Assert.Equal(textureId, resolvedTextureId);

        Assert.True(MaterialValue.FromObject(MaterialPropertyType.Enum, "opaque").TryGetEnum(out var enumValue));
        Assert.Equal("opaque", enumValue);

        Assert.True(MaterialValue.FromObject(MaterialPropertyType.String, "material-name").TryGetString(out var stringValue));
        Assert.Equal("material-name", stringValue);
    }

    [Fact]
    public void FromObject_RejectsIncompatibleValueType()
    {
        Assert.Throws<ArgumentException>(() => MaterialValue.FromObject(MaterialPropertyType.Texture, Color.White));
    }

    [Fact]
    public void IsCompatibleWith_RejectsMismatchedValueType()
    {
        var definition = new MaterialPropertyDefinition(
            key: "alpha",
            displayName: "Alpha",
            valueType: MaterialPropertyType.Float,
            group: MaterialPropertyGroup.Rendering,
            minValue: 0.0f,
            maxValue: 1.0f,
            step: 0.01f);

        var isCompatible = MaterialValue.FromInteger(1).IsCompatibleWith(definition, out var validationError);

        Assert.False(isCompatible);
        Assert.NotNull(validationError);
        Assert.Contains("alpha", validationError);
    }

    [Fact]
    public void IsCompatibleWith_ValidatesNumericRanges()
    {
        var definition = new MaterialPropertyDefinition(
            key: "alpha",
            displayName: "Alpha",
            valueType: MaterialPropertyType.Float,
            group: MaterialPropertyGroup.Rendering,
            minValue: 0.0f,
            maxValue: 1.0f,
            step: 0.01f);

        Assert.True(MaterialValue.FromFloat(0.5f).IsCompatibleWith(definition, out var inRangeError));
        Assert.Null(inRangeError);

        var isCompatible = MaterialValue.FromFloat(2.0f).IsCompatibleWith(definition, out var validationError);
        Assert.False(isCompatible);
        Assert.NotNull(validationError);
        Assert.Contains("alpha", validationError);
    }

    [Fact]
    public void IsCompatibleWith_RejectsUnknownEnumValue()
    {
        var definition = new MaterialPropertyDefinition(
            key: "surface_mode",
            displayName: "Surface Mode",
            valueType: MaterialPropertyType.Enum,
            group: MaterialPropertyGroup.Surface,
            defaultValue: "opaque",
            options: new[]
            {
                new MaterialPropertyOption("opaque", "Opaque"),
                new MaterialPropertyOption("transparent", "Transparent"),
            });

        var isCompatible = MaterialValue.FromEnum("masked").IsCompatibleWith(definition, out var validationError);

        Assert.False(isCompatible);
        Assert.NotNull(validationError);
        Assert.Contains("surface_mode", validationError);
    }

    [Fact]
    public void MaterialPropertyDefinition_GetDefaultMaterialValue_ReturnsTypedDefault()
    {
        var definition = new MaterialPropertyDefinition(
            key: "specular_power",
            displayName: "Specular Power",
            valueType: MaterialPropertyType.Float,
            group: MaterialPropertyGroup.Lighting,
            defaultValue: 16.0f,
            minValue: 0.0f,
            maxValue: 128.0f,
            step: 1.0f);

        var defaultValue = definition.GetDefaultMaterialValue();

        Assert.NotNull(defaultValue);
        Assert.True(defaultValue!.TryGetFloat(out var resolvedDefaultValue));
        Assert.Equal(16.0f, resolvedDefaultValue);
    }

    [Fact]
    public void MaterialPropertyDefinition_RejectsOutOfRangeDefaultValue()
    {
        Assert.Throws<ArgumentException>(() => new MaterialPropertyDefinition(
            key: "alpha",
            displayName: "Alpha",
            valueType: MaterialPropertyType.Float,
            group: MaterialPropertyGroup.Rendering,
            defaultValue: 2.0f,
            minValue: 0.0f,
            maxValue: 1.0f,
            step: 0.01f));
    }
}