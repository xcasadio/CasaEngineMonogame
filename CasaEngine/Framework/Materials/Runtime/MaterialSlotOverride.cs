using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Materials.Runtime;

public sealed class MaterialSlotOverride : ISerializable
{
    public string SlotName { get; set; } = string.Empty;

    public int SlotIndex { get; set; } = -1;

    public Guid MaterialAssetId { get; set; } = Guid.Empty;

    public MaterialInstanceData MaterialInstanceData { get; private set; } = new();

    public bool HasAnyOverride => MaterialAssetId != Guid.Empty || !MaterialInstanceData.IsEmpty;

    public MaterialSlotOverride()
    {
    }

    public MaterialSlotOverride(MaterialSlotOverride other)
    {
        ArgumentNullException.ThrowIfNull(other);

        SlotName = other.SlotName;
        SlotIndex = other.SlotIndex;
        MaterialAssetId = other.MaterialAssetId;
        MaterialInstanceData = other.MaterialInstanceData.Clone();
    }

    public MaterialSlotOverride Clone() => new(this);

    public void Load(JObject element)
    {
        MaterialSlotOverrideJsonSerializer.Load(this, element);
    }
}