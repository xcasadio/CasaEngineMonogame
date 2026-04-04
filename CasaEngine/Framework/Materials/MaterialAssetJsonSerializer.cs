using CasaEngine.Core.Serialization;
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

            propertiesNode[propertyDefinition.Key] = MaterialValueJsonSerializer.Save(propertyDefinition.ValueType, value);
        }

        node["properties"] = propertiesNode;
    }

    public static void Load(MaterialAsset materialAsset, JObject node)
    {
        ArgumentNullException.ThrowIfNull(materialAsset);
        ArgumentNullException.ThrowIfNull(node);

        if (node["definition_id"] is null)
        {
            if (LegacyMaterialAssetAdapter.TryLoad(materialAsset, node))
            {
                return;
            }

            throw new InvalidOperationException("Material asset is missing 'definition_id'.");
        }

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

            var value = MaterialValueJsonSerializer.Load(propertyDefinition.ValueType, propertyNode.Value);
            materialAsset.SetPropertyValue(propertyDefinition.Key, value);
        }
    }
}