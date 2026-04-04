using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Materials;

public static class MaterialAssetJsonSerializer
{
    public static void Save(MaterialAsset materialAsset, JObject node)
    {
        ArgumentNullException.ThrowIfNull(materialAsset);
        ArgumentNullException.ThrowIfNull(node);

        var definition = materialAsset.GetRequiredDefinition();

        node["id"] = materialAsset.Id.ToString();
        node["name"] = materialAsset.Name;
        node["definition_id"] = definition.Id;
        if (materialAsset.ParentMaterialAssetId != Guid.Empty)
        {
            node["parent_material_asset_id"] = materialAsset.ParentMaterialAssetId.ToString();
        }

        node["shader_asset_id"] = materialAsset.ShaderAssetId.ToString();
        node["is_transparent"] = materialAsset.IsTransparent;
        node["queue"] = materialAsset.Queue.ToString();
        node["cast_shadows"] = materialAsset.CastShadows;
        node["receive_shadows"] = materialAsset.ReceiveShadows;
        node["blend_state"] = materialAsset.BlendStateName;
        node["depth_stencil_state"] = materialAsset.DepthStencilStateName;
        node["rasterizer_state"] = materialAsset.RasterizerStateName;
        node["sampler_state"] = materialAsset.SamplerStateName;

        var propertiesNode = new JObject();
        for (int i = 0; i < definition.Properties.Count; i++)
        {
            var propertyDefinition = definition.Properties[i];
            if (!materialAsset.PropertyValues.TryGetValue(propertyDefinition.Key, out var value))
            {
                continue;
            }

            propertiesNode[propertyDefinition.Key] = SaveValue(propertyDefinition, value);
        }

        node["properties"] = propertiesNode;
    }

    public static void Load(MaterialAsset materialAsset, JObject node)
    {
        ArgumentNullException.ThrowIfNull(materialAsset);
        ArgumentNullException.ThrowIfNull(node);

        materialAsset.ClearPropertyValues();
        materialAsset.DefinitionId = node["definition_id"]?.Value<string>()
            ?? throw new InvalidOperationException("Material asset is missing 'definition_id'.");
        materialAsset.ParentMaterialAssetId = node["parent_material_asset_id"]?.GetGuid() ?? Guid.Empty;
        materialAsset.ShaderAssetId = node["shader_asset_id"]?.GetGuid() ?? Guid.Empty;
        materialAsset.IsTransparent = node["is_transparent"]?.Value<bool>() ?? false;
        materialAsset.Queue = node["queue"] is { } queueToken
            && Enum.TryParse<RenderQueue>(queueToken.Value<string>(), true, out var queue)
                ? queue
                : RenderQueue.Opaque;
        materialAsset.CastShadows = node["cast_shadows"]?.Value<bool>() ?? true;
        materialAsset.ReceiveShadows = node["receive_shadows"]?.Value<bool>() ?? true;
        materialAsset.BlendStateName = node["blend_state"]?.Value<string>() ?? MaterialAsset.DefaultBlendStateName;
        materialAsset.DepthStencilStateName = node["depth_stencil_state"]?.Value<string>() ?? MaterialAsset.DefaultDepthStencilStateName;
        materialAsset.RasterizerStateName = node["rasterizer_state"]?.Value<string>() ?? MaterialAsset.DefaultRasterizerStateName;
        materialAsset.SamplerStateName = node["sampler_state"]?.Value<string>() ?? MaterialAsset.DefaultSamplerStateName;

        if (node["properties"] is not JObject propertiesNode)
        {
            return;
        }

        var definition = materialAsset.GetRequiredDefinition();
        foreach (var propertyNode in propertiesNode.Properties())
        {
            if (!definition.TryGetPropertyBySerializedName(propertyNode.Name, out var propertyDefinition))
            {
                throw new KeyNotFoundException(
                    $"Material definition '{definition.Id}' does not expose a property named '{propertyNode.Name}'.");
            }

            var value = LoadValue(propertyDefinition, propertyNode.Value);
            materialAsset.SetPropertyValue(propertyDefinition.Key, value);
        }
    }

    private static JToken SaveValue(MaterialPropertyDefinition propertyDefinition, MaterialValue value)
    {
        if (!propertyDefinition.IsValueCompatible(value, out var validationError))
        {
            throw new InvalidOperationException(validationError);
        }

        return propertyDefinition.ValueType switch
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
                $"Material value for property '{propertyDefinition.Key}' is incompatible with '{propertyDefinition.ValueType}'."),
        };
    }

    private static MaterialValue LoadValue(MaterialPropertyDefinition propertyDefinition, JToken valueToken)
        => propertyDefinition.ValueType switch
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
            _ => throw new InvalidOperationException(
                $"Material property type '{propertyDefinition.ValueType}' is not supported for '{propertyDefinition.Key}'."),
        };

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