using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Materials;

internal static class LegacyMaterialAssetAdapter
{
    public static bool TryLoad(MaterialAsset materialAsset, JObject node)
    {
        ArgumentNullException.ThrowIfNull(materialAsset);
        ArgumentNullException.ThrowIfNull(node);

        var legacyTypeName = node["type"]?.Value<string>();
        if (string.IsNullOrWhiteSpace(legacyTypeName))
        {
            return false;
        }

        if (!MaterialDefinitionRegistry.TryGetByLegacyTypeName(legacyTypeName, out var definition))
        {
            throw new NotSupportedException($"Legacy material type '{legacyTypeName}' is not supported.");
        }

        materialAsset.ClearPropertyValues();
        materialAsset.DefinitionId = definition.Id;
        materialAsset.ParentMaterialAssetId = Guid.Empty;
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

        for (int i = 0; i < definition.Properties.Count; i++)
        {
            var propertyDefinition = definition.Properties[i];
            if (!TryGetLegacyPropertyToken(node, propertyDefinition, out var propertyToken))
            {
                continue;
            }

            materialAsset.SetPropertyValue(propertyDefinition.Key, ParseLegacyValue(propertyDefinition, propertyToken));
        }

        return true;
    }

    private static bool TryGetLegacyPropertyToken(
        JObject node,
        MaterialPropertyDefinition propertyDefinition,
        out JToken propertyToken)
    {
        if (node.TryGetValue(propertyDefinition.Key, StringComparison.OrdinalIgnoreCase, out propertyToken!))
        {
            return true;
        }

        for (int i = 0; i < propertyDefinition.LegacyAliases.Count; i++)
        {
            if (node.TryGetValue(propertyDefinition.LegacyAliases[i], StringComparison.OrdinalIgnoreCase, out propertyToken!))
            {
                return true;
            }
        }

        propertyToken = null!;
        return false;
    }

    private static MaterialValue ParseLegacyValue(MaterialPropertyDefinition propertyDefinition, JToken propertyToken)
        => propertyDefinition.ValueType switch
        {
            MaterialPropertyType.Float => MaterialValue.FromFloat(propertyToken.Value<float>()),
            MaterialPropertyType.Integer => MaterialValue.FromInteger(propertyToken.Value<int>()),
            MaterialPropertyType.Boolean => MaterialValue.FromBoolean(propertyToken.Value<bool>()),
            MaterialPropertyType.Color => MaterialValue.FromColor(propertyToken.GetColor()),
            MaterialPropertyType.Vector2 => MaterialValue.FromVector2(propertyToken.GetVector2()),
            MaterialPropertyType.Vector3 => MaterialValue.FromVector3(ReadVector3(propertyToken)),
            MaterialPropertyType.Vector4 => MaterialValue.FromVector4(propertyToken.GetVector4()),
            MaterialPropertyType.Texture => MaterialValue.FromTextureId(propertyToken.GetGuid()),
            MaterialPropertyType.Enum => MaterialValue.FromEnum(propertyToken.Value<string>() ?? string.Empty),
            MaterialPropertyType.String => MaterialValue.FromString(propertyToken.Value<string>() ?? string.Empty),
            _ => throw new InvalidOperationException(
                $"Legacy material property type '{propertyDefinition.ValueType}' is not supported for '{propertyDefinition.Key}'."),
        };

    private static Vector3 ReadVector3(JToken token)
    {
        if (token["x"] is not null || token["y"] is not null || token["z"] is not null)
        {
            return token.GetVector3();
        }

        return new Vector3(
            token["r"]?.Value<float>() ?? 0.0f,
            token["g"]?.Value<float>() ?? 0.0f,
            token["b"]?.Value<float>() ?? 0.0f);
    }
}