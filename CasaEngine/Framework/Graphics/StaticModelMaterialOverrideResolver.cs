using CasaEngine.Core.Log;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Materials;

namespace CasaEngine.Framework.Graphics;

internal sealed class ResolvedStaticModelMaterialOverrides
{
    public ResolvedStaticModelMaterialOverrides(
        IReadOnlyDictionary<int, MaterialBase>? materialOverridesBySlotIndex,
        IReadOnlyDictionary<int, MaterialPropertyBlock>? propertyOverridesBySlotIndex)
    {
        MaterialOverridesBySlotIndex = materialOverridesBySlotIndex;
        PropertyOverridesBySlotIndex = propertyOverridesBySlotIndex;
    }

    public IReadOnlyDictionary<int, MaterialBase>? MaterialOverridesBySlotIndex { get; }

    public IReadOnlyDictionary<int, MaterialPropertyBlock>? PropertyOverridesBySlotIndex { get; }
}

internal static class StaticModelMaterialOverrideResolver
{
    private static readonly MaterialCompiler MaterialCompiler = new();

    public static ResolvedStaticModelMaterialOverrides? ResolveForMesh(
        StaticModelMesh modelMesh,
        IReadOnlyList<MaterialSlotOverride> materialOverrides,
        AssetContentManager assetContentManager)
    {
        ArgumentNullException.ThrowIfNull(modelMesh);
        ArgumentNullException.ThrowIfNull(materialOverrides);
        ArgumentNullException.ThrowIfNull(assetContentManager);

        if (materialOverrides.Count == 0)
        {
            return null;
        }

        Dictionary<int, MaterialBase>? resolvedMaterials = null;
        Dictionary<int, MaterialPropertyBlock>? resolvedPropertyBlocks = null;
        var resolvedMaterialAssets = new Dictionary<Guid, MaterialAsset?>();

        MaterialAsset? ResolveMaterialAsset(Guid materialAssetId)
        {
            if (materialAssetId == Guid.Empty)
            {
                return null;
            }

            if (resolvedMaterialAssets.TryGetValue(materialAssetId, out var cachedMaterialAsset))
            {
                return cachedMaterialAsset;
            }

            try
            {
                cachedMaterialAsset = assetContentManager.Load<MaterialAsset>(materialAssetId, cache: false);
            }
            catch (Exception ex)
            {
                Logs.WriteException(ex);
                cachedMaterialAsset = null;
            }

            resolvedMaterialAssets[materialAssetId] = cachedMaterialAsset;
            return cachedMaterialAsset;
        }

        if (modelMesh.SubMeshes.Count == 0)
        {
            ResolveSlot(new StaticModelMaterialSlot(modelMesh.MaterialSlotIndex, modelMesh.SlotName, -1, -1, modelMesh, null));
        }
        else
        {
            for (int subMeshIndex = 0; subMeshIndex < modelMesh.SubMeshes.Count; subMeshIndex++)
            {
                var subMesh = modelMesh.SubMeshes[subMeshIndex];
                ResolveSlot(new StaticModelMaterialSlot(subMesh.MaterialSlotIndex, subMesh.SlotName, -1, subMeshIndex, modelMesh, subMesh));
            }
        }

        return resolvedMaterials == null && resolvedPropertyBlocks == null
            ? null
            : new ResolvedStaticModelMaterialOverrides(resolvedMaterials, resolvedPropertyBlocks);

        void ResolveSlot(StaticModelMaterialSlot slot)
        {
            var slotOverride = StaticModelMaterialSlots.FindMatchingOverride(materialOverrides, slot);
            if (slotOverride == null || !slotOverride.HasAnyOverride)
            {
                return;
            }

            MaterialAsset? overrideMaterialAsset = null;
            bool hasResolvedMaterialOverride = false;
            if (slotOverride.MaterialAssetId != Guid.Empty)
            {
                overrideMaterialAsset = ResolveMaterialAsset(slotOverride.MaterialAssetId);
                if (overrideMaterialAsset != null
                    && TryCreateRuntimeMaterial(overrideMaterialAsset, assetContentManager, out var materialOverride))
                {
                    resolvedMaterials ??= new Dictionary<int, MaterialBase>();
                    resolvedMaterials[slot.SlotIndex] = materialOverride;
                    hasResolvedMaterialOverride = true;
                }
            }

            if (slotOverride.MaterialInstanceData.IsEmpty)
            {
                return;
            }

            MaterialAsset? materialAssetForInstance = null;
            if (slotOverride.MaterialAssetId != Guid.Empty)
            {
                if (!hasResolvedMaterialOverride)
                {
                    return;
                }

                materialAssetForInstance = overrideMaterialAsset;
            }
            else if (slot.DefaultMaterialAssetId != Guid.Empty)
            {
                materialAssetForInstance = ResolveMaterialAsset(slot.DefaultMaterialAssetId);
            }

            if (materialAssetForInstance == null)
            {
                return;
            }

            var propertyBlock = MaterialInstancePropertyBlockMapper.Create(
                materialAssetForInstance,
                slotOverride.MaterialInstanceData,
                ResolveMaterialAsset);
            if (propertyBlock.IsEmpty)
            {
                return;
            }

            resolvedPropertyBlocks ??= new Dictionary<int, MaterialPropertyBlock>();
            resolvedPropertyBlocks[slot.SlotIndex] = propertyBlock;
        }
    }

    private static bool TryCreateRuntimeMaterial(
        MaterialAsset materialAsset,
        AssetContentManager assetContentManager,
        out MaterialBase material)
    {
        ArgumentNullException.ThrowIfNull(materialAsset);
        ArgumentNullException.ThrowIfNull(assetContentManager);

        material = null!;
        try
        {
            var materialCache = assetContentManager.RuntimeContext?.MaterialCache;
            material = materialCache != null
                ? materialCache.GetOrCompileRuntimeMaterial(materialAsset, assetContentManager)
                : MaterialCompiler.CompileRuntimeMaterial(materialAsset, assetContentManager);
            return true;
        }
        catch (Exception ex)
        {
            Logs.WriteException(ex);
            return false;
        }
    }
}