using CasaEngine.Framework.Materials;

namespace CasaEngine.Framework.Graphics;

public sealed class StaticModelMaterialSlot
{
    public StaticModelMaterialSlot(int slotIndex, string slotName, int meshIndex, int subMeshIndex, StaticModelMesh mesh, SubMesh? subMesh)
    {
        SlotIndex = slotIndex;
        SlotName = slotName;
        MeshIndex = meshIndex;
        SubMeshIndex = subMeshIndex;
        Mesh = mesh;
        SubMesh = subMesh;
    }

    public int SlotIndex { get; }

    public string SlotName { get; }

    public int MeshIndex { get; }

    public int SubMeshIndex { get; }

    public StaticModelMesh Mesh { get; }

    public SubMesh? SubMesh { get; }

    public bool IsSubMeshSlot => SubMesh != null;

    public Guid DefaultMaterialAssetId => SubMesh?.MaterialAssetId ?? Mesh.MaterialAssetId;

    public Guid DefaultTextureAssetId => Mesh.TextureAssetId;
}

public static class StaticModelMaterialSlots
{
    public static List<StaticModelMaterialSlot> Create(StaticModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        EnsureMetadata(model);

        var slots = new List<StaticModelMaterialSlot>();
        for (int meshIndex = 0; meshIndex < model.Meshes.Count; meshIndex++)
        {
            var mesh = model.Meshes[meshIndex];
            if (mesh.SubMeshes.Count == 0)
            {
                slots.Add(new StaticModelMaterialSlot(mesh.MaterialSlotIndex, mesh.SlotName, meshIndex, -1, mesh, null));
                continue;
            }

            for (int subMeshIndex = 0; subMeshIndex < mesh.SubMeshes.Count; subMeshIndex++)
            {
                var subMesh = mesh.SubMeshes[subMeshIndex];
                slots.Add(new StaticModelMaterialSlot(subMesh.MaterialSlotIndex, subMesh.SlotName, meshIndex, subMeshIndex, mesh, subMesh));
            }
        }

        return slots;
    }

    public static void EnsureMetadata(StaticModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        int nextSlotIndex = GetNextAvailableSlotIndex(model);
        var usedSlotNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mesh in model.Meshes)
        {
            if (mesh.SubMeshes.Count == 0)
            {
                mesh.SlotName = EnsureSlotName(mesh.SlotName, mesh.Name, usedSlotNames, mesh.MaterialSlotIndex >= 0 ? mesh.MaterialSlotIndex : nextSlotIndex);
                if (mesh.MaterialSlotIndex < 0)
                {
                    mesh.MaterialSlotIndex = nextSlotIndex++;
                }

                continue;
            }

            string meshBaseName = string.IsNullOrWhiteSpace(mesh.SlotName)
                ? mesh.Name
                : mesh.SlotName;
            int meshDisplayIndex = mesh.MaterialSlotIndex >= 0
                ? mesh.MaterialSlotIndex + 1
                : nextSlotIndex + 1;
            mesh.SlotName = string.IsNullOrWhiteSpace(meshBaseName)
                ? $"Mesh {meshDisplayIndex}"
                : meshBaseName.Trim();

            for (int subMeshIndex = 0; subMeshIndex < mesh.SubMeshes.Count; subMeshIndex++)
            {
                var subMesh = mesh.SubMeshes[subMeshIndex];
                string fallbackName = $"{mesh.SlotName} [{subMeshIndex + 1}]";
                subMesh.SlotName = EnsureSlotName(subMesh.SlotName, fallbackName, usedSlotNames, subMesh.MaterialSlotIndex >= 0 ? subMesh.MaterialSlotIndex : nextSlotIndex);

                if (subMesh.MaterialSlotIndex < 0)
                {
                    subMesh.MaterialSlotIndex = nextSlotIndex++;
                }
            }
        }
    }

    public static MaterialSlotOverride? FindMatchingOverride(IReadOnlyList<MaterialSlotOverride>? overrides, StaticModelMaterialSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);

        if (overrides == null)
        {
            return null;
        }

        for (int i = 0; i < overrides.Count; i++)
        {
            var materialOverride = overrides[i];
            if (!string.IsNullOrWhiteSpace(materialOverride.SlotName)
                && string.Equals(materialOverride.SlotName, slot.SlotName, StringComparison.OrdinalIgnoreCase))
            {
                return materialOverride;
            }
        }

        for (int i = 0; i < overrides.Count; i++)
        {
            var materialOverride = overrides[i];
            if (materialOverride.SlotIndex == slot.SlotIndex)
            {
                return materialOverride;
            }
        }

        return null;
    }

    public static List<MaterialSlotOverride> FindOrphanOverrides(StaticModel model, IReadOnlyList<MaterialSlotOverride>? overrides)
    {
        var result = new List<MaterialSlotOverride>();
        if (overrides == null || overrides.Count == 0)
        {
            return result;
        }

        var slots = Create(model);
        for (int i = 0; i < overrides.Count; i++)
        {
            var materialOverride = overrides[i];
            bool matched = false;
            for (int j = 0; j < slots.Count; j++)
            {
                var slot = slots[j];
                if ((!string.IsNullOrWhiteSpace(materialOverride.SlotName)
                     && string.Equals(materialOverride.SlotName, slot.SlotName, StringComparison.OrdinalIgnoreCase))
                    || materialOverride.SlotIndex == slot.SlotIndex)
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                result.Add(materialOverride);
            }
        }

        return result;
    }

    private static int GetNextAvailableSlotIndex(StaticModel model)
    {
        int nextSlotIndex = 0;

        foreach (var mesh in model.Meshes)
        {
            if (mesh.MaterialSlotIndex >= 0)
            {
                nextSlotIndex = Math.Max(nextSlotIndex, mesh.MaterialSlotIndex + 1);
            }

            for (int subMeshIndex = 0; subMeshIndex < mesh.SubMeshes.Count; subMeshIndex++)
            {
                var subMesh = mesh.SubMeshes[subMeshIndex];
                if (subMesh.MaterialSlotIndex >= 0)
                {
                    nextSlotIndex = Math.Max(nextSlotIndex, subMesh.MaterialSlotIndex + 1);
                }
            }
        }

        return nextSlotIndex;
    }

    private static string EnsureSlotName(string currentName, string fallbackName, HashSet<string> usedSlotNames, int fallbackIndex)
    {
        string baseName = string.IsNullOrWhiteSpace(currentName)
            ? fallbackName
            : currentName;
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = $"Slot {fallbackIndex + 1}";
        }

        string candidate = baseName.Trim();
        int suffix = 2;
        while (!usedSlotNames.Add(candidate))
        {
            candidate = $"{baseName} {suffix++}";
        }

        return candidate;
    }
}