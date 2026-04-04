using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Materials;

public static class MaterialSlotOverrideJsonSerializer
{
    public static void Save(MaterialSlotOverride materialSlotOverride, JObject node)
    {
        ArgumentNullException.ThrowIfNull(materialSlotOverride);
        ArgumentNullException.ThrowIfNull(node);

        node["slot_name"] = materialSlotOverride.SlotName;
        node["slot_index"] = materialSlotOverride.SlotIndex;
        node["material_asset_id"] = materialSlotOverride.MaterialAssetId.ToString();

        if (!materialSlotOverride.MaterialInstanceData.IsEmpty)
        {
            var materialInstanceNode = new JObject();
            MaterialInstanceDataJsonSerializer.Save(materialSlotOverride.MaterialInstanceData, materialInstanceNode);
            node["material_instance"] = materialInstanceNode;
        }
    }

    public static void Load(MaterialSlotOverride materialSlotOverride, JObject node)
    {
        ArgumentNullException.ThrowIfNull(materialSlotOverride);
        ArgumentNullException.ThrowIfNull(node);

        materialSlotOverride.SlotName = node["slot_name"]?.Value<string>() ?? string.Empty;
        materialSlotOverride.SlotIndex = node["slot_index"]?.Value<int>() ?? -1;
        materialSlotOverride.MaterialAssetId = node["material_asset_id"]?.GetGuid() ?? Guid.Empty;
        materialSlotOverride.MaterialInstanceData.ClearPropertyOverrides();

        if (node["material_instance"] is JObject materialInstanceNode)
        {
            MaterialInstanceDataJsonSerializer.Load(materialSlotOverride.MaterialInstanceData, materialInstanceNode);
            return;
        }

        if (node["property_overrides"] is JObject propertyOverridesNode)
        {
            var compatibilityNode = new JObject
            {
                ["property_overrides"] = propertyOverridesNode.DeepClone(),
            };
            MaterialInstanceDataJsonSerializer.Load(materialSlotOverride.MaterialInstanceData, compatibilityNode);
        }
    }
}