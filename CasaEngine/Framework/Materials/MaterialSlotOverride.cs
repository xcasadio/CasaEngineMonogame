using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Materials;

public sealed class MaterialSlotOverride : ISerializable
{
    public string SlotName { get; set; } = string.Empty;

    public int SlotIndex { get; set; } = -1;

    public Guid MaterialAssetId { get; set; } = Guid.Empty;

    public void Load(JObject element)
    {
        SlotName = element["slot_name"]?.GetString() ?? string.Empty;
        SlotIndex = element["slot_index"]?.GetInt32() ?? -1;
        MaterialAssetId = element["material_asset_id"]?.GetGuid() ?? Guid.Empty;
    }
}