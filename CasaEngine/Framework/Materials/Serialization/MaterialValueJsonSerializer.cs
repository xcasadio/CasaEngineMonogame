using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Materials.Serialization;

public static class MaterialValueJsonSerializer
{
    public static JToken Save(MaterialPropertyType type, MaterialValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return type switch
        {
            MaterialPropertyType.Float when value.TryGetFloat(out var floatValue) => new JValue(floatValue),
            MaterialPropertyType.Integer when value.TryGetInteger(out var integerValue) => new JValue(integerValue),
            MaterialPropertyType.Boolean when value.TryGetBoolean(out var booleanValue) => new JValue(booleanValue),
            MaterialPropertyType.Color when value.TryGetColor(out var colorValue) => SaveColor(colorValue),
            MaterialPropertyType.Vector2 when value.TryGetVector2(out var vector2Value) => SaveVector2(vector2Value),
            MaterialPropertyType.Vector3 when value.TryGetVector3(out var vector3Value) => SaveVector3(vector3Value),
            MaterialPropertyType.Vector4 when value.TryGetVector4(out var vector4Value) => SaveVector4(vector4Value),
            MaterialPropertyType.Texture when value.TryGetTextureId(out var textureAssetId) => new JValue(textureAssetId.ToString()),
            MaterialPropertyType.Enum when value.TryGetEnum(out var enumValue) => new JValue(enumValue),
            MaterialPropertyType.String when value.TryGetString(out var stringValue) => new JValue(stringValue),
            _ => throw new InvalidOperationException(
                $"Material value '{value}' is incompatible with '{type}'."),
        };
    }

    public static JObject SaveTyped(MaterialValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new JObject
        {
            ["type"] = value.Type.ToString(),
            ["value"] = Save(value.Type, value),
        };
    }

    public static MaterialValue Load(MaterialPropertyType type, JToken valueToken)
    {
        ArgumentNullException.ThrowIfNull(valueToken);

        return type switch
        {
            MaterialPropertyType.Float => MaterialValue.FromFloat(valueToken.Value<float>()),
            MaterialPropertyType.Integer => MaterialValue.FromInteger(valueToken.Value<int>()),
            MaterialPropertyType.Boolean => MaterialValue.FromBoolean(valueToken.Value<bool>()),
            MaterialPropertyType.Color => MaterialValue.FromColor(valueToken.GetColor()),
            MaterialPropertyType.Vector2 => MaterialValue.FromVector2(valueToken.GetVector2()),
            MaterialPropertyType.Vector3 => MaterialValue.FromVector3(valueToken.GetVector3()),
            MaterialPropertyType.Vector4 => MaterialValue.FromVector4(valueToken.GetVector4()),
            MaterialPropertyType.Texture => MaterialValue.FromTextureId(valueToken.Type == JTokenType.Null ? Guid.Empty : valueToken.GetGuid()),
            MaterialPropertyType.Enum => MaterialValue.FromEnum(valueToken.Value<string>() ?? string.Empty),
            MaterialPropertyType.String => MaterialValue.FromString(valueToken.Value<string>() ?? string.Empty),
            _ => throw new InvalidOperationException($"Material property type '{type}' is not supported."),
        };
    }

    public static MaterialValue LoadTyped(JObject node)
    {
        ArgumentNullException.ThrowIfNull(node);

        string typeName = node["type"]?.Value<string>()
            ?? throw new InvalidOperationException("Typed material value is missing 'type'.");
        if (!Enum.TryParse<MaterialPropertyType>(typeName, true, out var type))
        {
            throw new InvalidOperationException($"Unknown material property type '{typeName}'.");
        }

        var valueToken = node["value"]
            ?? throw new InvalidOperationException("Typed material value is missing 'value'.");
        return Load(type, valueToken);
    }

    private static JObject SaveColor(Color color)
        => new()
        {
            ["r"] = color.R,
            ["g"] = color.G,
            ["b"] = color.B,
            ["a"] = color.A,
        };

    private static JObject SaveVector2(Vector2 vector)
        => new()
        {
            ["x"] = vector.X,
            ["y"] = vector.Y,
        };

    private static JObject SaveVector3(Vector3 vector)
        => new()
        {
            ["x"] = vector.X,
            ["y"] = vector.Y,
            ["z"] = vector.Z,
        };

    private static JObject SaveVector4(Vector4 vector)
        => new()
        {
            ["x"] = vector.X,
            ["y"] = vector.Y,
            ["z"] = vector.Z,
            ["w"] = vector.W,
        };
}