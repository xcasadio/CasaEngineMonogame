using System.ComponentModel;

using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Rendering.Models;

using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

/// <summary>
/// Renders a <see cref="StaticModel"/> asset in the world.
/// On <see cref="InitializeWithWorld"/>, the model hierarchy is expanded into
/// child <see cref="StaticModelSubMeshComponent"/> instances (one per
/// <see cref="StaticModelNode"/> referencing a mesh).  Each child is marked
/// <see cref="StaticModelSubMeshComponent.IsGeneratedFromModel"/> so that
/// <see cref="Save"/> skips them — they are always rebuilt from the asset.
/// </summary>
[DisplayName("Static Model")]
public class StaticModelComponent : PrimitiveComponent
{
    private Guid _staticModelAssetId = Guid.Empty;

    /// <summary>Asset ID of the <see cref="StaticModel"/> to render.</summary>
    public Guid StaticModelAssetId
    {
        get => _staticModelAssetId;
        set
        {
            if (_staticModelAssetId == value)
            {
                return;
            }

            _staticModelAssetId = value;
            StaticModel = null;
        }
    }

    /// <summary>Runtime reference to the loaded model.</summary>
    public StaticModel? StaticModel { get; set; }

    public List<MaterialSlotOverride> MaterialOverrides { get; } = new();

    public StaticModelComponent() { }

    public StaticModelComponent(StaticModelComponent other) : base(other)
    {
        StaticModelAssetId = other.StaticModelAssetId;

        foreach (var materialOverride in other.MaterialOverrides)
        {
            MaterialOverrides.Add(materialOverride.Clone());
        }
    }

    public override StaticModelComponent Clone() => new(this);

    public IReadOnlyList<StaticModelMaterialSlot> GetMaterialSlots()
    {
        if (StaticModel == null)
        {
            return Array.Empty<StaticModelMaterialSlot>();
        }

        return StaticModelMaterialSlots.Create(StaticModel);
    }

    public Guid GetMaterialOverrideAssetId(StaticModelMaterialSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);

        return StaticModelMaterialSlots.FindMatchingOverride(MaterialOverrides, slot)?.MaterialAssetId ?? Guid.Empty;
    }

    public IReadOnlyList<MaterialSlotOverride> GetOrphanMaterialOverrides()
    {
        if (StaticModel == null)
        {
            return Array.Empty<MaterialSlotOverride>();
        }

        return StaticModelMaterialSlots.FindOrphanOverrides(StaticModel, MaterialOverrides);
    }

    public bool ReferencesAnyMaterialAsset(ISet<Guid> materialAssetIds)
    {
        ArgumentNullException.ThrowIfNull(materialAssetIds);

        if (materialAssetIds.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < MaterialOverrides.Count; i++)
        {
            Guid materialAssetId = MaterialOverrides[i].MaterialAssetId;
            if (materialAssetId != Guid.Empty && materialAssetIds.Contains(materialAssetId))
            {
                return true;
            }
        }

        return StaticModel != null && StaticModel.ReferencesAnyMaterialAsset(materialAssetIds);
    }

    public bool RefreshResolvedMaterials(Assets.AssetContentManager assetContentManager, ISet<Guid>? affectedMaterialAssetIds = null)
        => RefreshResolvedMaterialsDetailed(assetContentManager, affectedMaterialAssetIds).RefreshedAny;

    internal StaticModelComponentRefreshMetrics RefreshResolvedMaterialsDetailed(Assets.AssetContentManager assetContentManager, ISet<Guid>? affectedMaterialAssetIds = null)
    {
        ArgumentNullException.ThrowIfNull(assetContentManager);

        if (StaticModel == null)
        {
            return default;
        }

        if (affectedMaterialAssetIds != null
            && affectedMaterialAssetIds.Count > 0
            && !ReferencesAnyMaterialAsset(affectedMaterialAssetIds))
        {
            return default;
        }

        bool refreshedModelMaterials = StaticModel.RefreshResolvedMaterials(assetContentManager, affectedMaterialAssetIds);
        var refreshedMaterialOverrides = RefreshGeneratedMaterialOverrides(affectedMaterialAssetIds);
        return new StaticModelComponentRefreshMetrics(
            refreshedModelMaterials || refreshedMaterialOverrides.RefreshedAny,
            refreshedMaterialOverrides.RecalculatedOverrideSlotCount,
            refreshedMaterialOverrides.AuthoringMaterialCacheHitCount,
            refreshedMaterialOverrides.AuthoringMaterialCacheMissCount);
    }

    public void SetMaterialOverride(StaticModelMaterialSlot slot, Guid materialAssetId)
    {
        ArgumentNullException.ThrowIfNull(slot);

        if (materialAssetId == Guid.Empty)
        {
            ClearMaterialOverride(slot);
            return;
        }

        MaterialSlotOverride? existingOverride = null;
        for (int i = 0; i < MaterialOverrides.Count; i++)
        {
            if (IsMatchingSlot(MaterialOverrides[i], slot))
            {
                existingOverride = MaterialOverrides[i];
                break;
            }
        }

        if (existingOverride == null)
        {
            existingOverride = new MaterialSlotOverride();
            MaterialOverrides.Add(existingOverride);
        }

        existingOverride.SlotName = slot.SlotName;
        existingOverride.SlotIndex = slot.SlotIndex;
        existingOverride.MaterialAssetId = materialAssetId;
        RemoveDuplicateOverrides(slot, existingOverride);
        RefreshGeneratedMaterialOverrides();
    }

    public void ClearMaterialOverride(StaticModelMaterialSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);

        for (int i = MaterialOverrides.Count - 1; i >= 0; i--)
        {
            if (IsMatchingSlot(MaterialOverrides[i], slot))
            {
                MaterialOverrides.RemoveAt(i);
            }
        }

        RefreshGeneratedMaterialOverrides();
    }

    public override void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world)
    {
        base.InitializeWithWorld(world);

        if (StaticModelAssetId != Guid.Empty && StaticModel == null)
        {
            StaticModel = world.Game.AssetContentManager.Load<StaticModel>(StaticModelAssetId);
        }

        StaticModel?.Initialize(world.Game.AssetContentManager);
        NormalizeMaterialOverrides();

        if (StaticModel?.RootNode != null)
        {
            // Remove any previously generated children (e.g. re-initialize).
            var old = Children
                .OfType<StaticModelSubMeshComponent>()
                .Where(c => c.IsGeneratedFromModel)
                .ToList();
            foreach (var c in old)
            {
                RemoveChildComponent(c);
            }

            // Build the component hierarchy from the model node tree.
            BuildHierarchy(StaticModel.RootNode, this, world);
        }
    }

    private void BuildHierarchy(StaticModelNode node, SceneComponent parent, CasaEngine.Framework.Scene.World.World world)
    {
        StaticModelMesh? modelMesh = null;
        if (node.MeshIndex >= 0 && node.MeshIndex < StaticModel!.Meshes.Count)
        {
            modelMesh = StaticModel.Meshes[node.MeshIndex];
        }

        var sub = new StaticModelSubMeshComponent
        {
            Name = GetGeneratedComponentName(node, modelMesh),
            IsGeneratedFromModel = true,
        };

        // Apply the node's local transform.
        sub.Coordinates.Position    = node.Position;
        sub.Coordinates.Orientation = node.Rotation;
        sub.Coordinates.Scale       = node.Scale;

        // Wire up the mesh if this node has one.
        if (modelMesh != null)
        {
            sub.ModelMesh = modelMesh;
            var resolvedOverrides = CreateResolvedMaterialOverrides(modelMesh, world.Game.AssetContentManager, out _);
            sub.MaterialOverridesBySlotIndex = resolvedOverrides?.MaterialOverridesBySlotIndex;
            sub.PropertyOverridesBySlotIndex = resolvedOverrides?.PropertyOverridesBySlotIndex;
        }

        parent.AddChildComponent(sub);
        sub.InitializeWithWorld(world);

        foreach (var child in node.Children)
        {
            BuildHierarchy(child, sub, world);
        }
    }

    private static string GetGeneratedComponentName(StaticModelNode node, StaticModelMesh? modelMesh)
    {
        if (modelMesh == null)
        {
            return node.Name;
        }

        if (string.IsNullOrWhiteSpace(node.Name))
        {
            return modelMesh.Name;
        }

        if (IsSyntheticGeneratedNodeName(node.Name) && !string.IsNullOrWhiteSpace(modelMesh.Name))
        {
            return modelMesh.Name;
        }

        return node.Name;
    }

    private static bool IsSyntheticGeneratedNodeName(string nodeName)
    {
        int index = nodeName.LastIndexOf("_mesh", StringComparison.OrdinalIgnoreCase);
        if (index < 0 || index == nodeName.Length - 1)
        {
            return false;
        }

        for (int i = index + 5; i < nodeName.Length; i++)
        {
            if (!char.IsDigit(nodeName[i]))
            {
                return false;
            }
        }

        return true;
    }

    // Draw and BoundingBox are fully delegated to the child StaticModelSubMeshComponents
    // via the SceneComponent.Draw() / GetBoundingBox() propagation chain.

    public override BoundingBox GetBoundingBox()
    {
        bool hasBounds = false;
        BoundingBox bounds = default;

        foreach (SceneComponent child in Children)
        {
            ExpandBounds(child, ref hasBounds, ref bounds);
        }

        return hasBounds ? bounds : base.GetBoundingBox();
    }

    private static void ExpandBounds(SceneComponent component, ref bool hasBounds, ref BoundingBox bounds)
    {
        BoundingBox componentBounds = component.BoundingBox;
        if (!hasBounds)
        {
            bounds = componentBounds;
            hasBounds = true;
        }
        else
        {
            bounds.ExpandBy(componentBounds);
        }

        foreach (SceneComponent child in component.Children)
        {
            ExpandBounds(child, ref hasBounds, ref bounds);
        }
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        MaterialOverrides.Clear();

        if (element.ContainsKey("static_model_asset_id"))
        {
            StaticModelAssetId = element["static_model_asset_id"]!.GetGuid();
        }

        if (element["material_slot_overrides"] is JArray overridesArray)
        {
            foreach (var overrideToken in overridesArray)
            {
                if (overrideToken is not JObject overrideObject)
                {
                    continue;
                }

                var materialOverride = new MaterialSlotOverride();
                materialOverride.Load(overrideObject);
                if (materialOverride.HasAnyOverride)
                {
                    MaterialOverrides.Add(materialOverride);
                }
            }
        }
    }

    private void RefreshGeneratedMaterialOverrides()
        => _ = RefreshGeneratedMaterialOverrides(affectedMaterialAssetIds: null);

    private StaticModelOverrideRefreshMetrics RefreshGeneratedMaterialOverrides(ISet<Guid>? affectedMaterialAssetIds)
    {
        if (StaticModel == null || Owner?.World == null)
        {
            return default;
        }

        NormalizeMaterialOverrides();
        var resolvedOverridesByMesh = new Dictionary<StaticModelMesh, (ResolvedStaticModelMaterialOverrides? Overrides, MaterialOverrideResolutionMetrics Metrics)>();
        return RefreshGeneratedMaterialOverrides(this, Owner.World.Game.AssetContentManager, affectedMaterialAssetIds, resolvedOverridesByMesh);
    }

    private StaticModelOverrideRefreshMetrics RefreshGeneratedMaterialOverrides(
        SceneComponent parent,
        Assets.AssetContentManager assetContentManager,
        ISet<Guid>? affectedMaterialAssetIds,
        Dictionary<StaticModelMesh, (ResolvedStaticModelMaterialOverrides? Overrides, MaterialOverrideResolutionMetrics Metrics)> resolvedOverridesByMesh)
    {
        bool refreshedAnyOverrides = false;
        int recalculatedOverrideSlotCount = 0;
        int authoringMaterialCacheHitCount = 0;
        int authoringMaterialCacheMissCount = 0;

        foreach (var child in parent.Children)
        {
            if (child is StaticModelSubMeshComponent { IsGeneratedFromModel: true } generatedSubMeshComponent)
            {
                if (generatedSubMeshComponent.ModelMesh == null)
                {
                    if (affectedMaterialAssetIds == null || affectedMaterialAssetIds.Count == 0)
                    {
                        generatedSubMeshComponent.MaterialOverridesBySlotIndex = null;
                        generatedSubMeshComponent.PropertyOverridesBySlotIndex = null;
                        refreshedAnyOverrides = true;
                    }
                }
                else if (ShouldRefreshGeneratedMaterialOverrides(generatedSubMeshComponent.ModelMesh, affectedMaterialAssetIds))
                {
                    if (!resolvedOverridesByMesh.TryGetValue(generatedSubMeshComponent.ModelMesh, out var resolvedOverrideResult))
                    {
                        var resolvedOverrides = CreateResolvedMaterialOverrides(generatedSubMeshComponent.ModelMesh, assetContentManager, out var metrics);
                        resolvedOverrideResult = (resolvedOverrides, metrics);
                        resolvedOverridesByMesh.Add(generatedSubMeshComponent.ModelMesh, resolvedOverrideResult);
                        recalculatedOverrideSlotCount += metrics.RecalculatedSlotCount;
                        authoringMaterialCacheHitCount += metrics.AuthoringMaterialCacheHitCount;
                        authoringMaterialCacheMissCount += metrics.AuthoringMaterialCacheMissCount;
                    }

                    generatedSubMeshComponent.MaterialOverridesBySlotIndex = resolvedOverrideResult.Overrides?.MaterialOverridesBySlotIndex;
                    generatedSubMeshComponent.PropertyOverridesBySlotIndex = resolvedOverrideResult.Overrides?.PropertyOverridesBySlotIndex;
                    refreshedAnyOverrides = true;
                }
            }

            var childMetrics = RefreshGeneratedMaterialOverrides(child, assetContentManager, affectedMaterialAssetIds, resolvedOverridesByMesh);
            refreshedAnyOverrides |= childMetrics.RefreshedAny;
            recalculatedOverrideSlotCount += childMetrics.RecalculatedOverrideSlotCount;
            authoringMaterialCacheHitCount += childMetrics.AuthoringMaterialCacheHitCount;
            authoringMaterialCacheMissCount += childMetrics.AuthoringMaterialCacheMissCount;
        }

        return new StaticModelOverrideRefreshMetrics(
            refreshedAnyOverrides,
            recalculatedOverrideSlotCount,
            authoringMaterialCacheHitCount,
            authoringMaterialCacheMissCount);
    }

    private ResolvedStaticModelMaterialOverrides? CreateResolvedMaterialOverrides(
        StaticModelMesh modelMesh,
        Assets.AssetContentManager assetContentManager,
        out MaterialOverrideResolutionMetrics metrics)
    {
        if (MaterialOverrides.Count == 0)
        {
            metrics = default;
            return null;
        }

        return StaticModelMaterialOverrideResolver.ResolveForMesh(modelMesh, MaterialOverrides, assetContentManager, out metrics);
    }

    private bool ShouldRefreshGeneratedMaterialOverrides(StaticModelMesh modelMesh, ISet<Guid>? affectedMaterialAssetIds)
    {
        ArgumentNullException.ThrowIfNull(modelMesh);

        if (affectedMaterialAssetIds == null || affectedMaterialAssetIds.Count == 0)
        {
            return true;
        }

        if (MaterialOverrides.Count == 0)
        {
            return false;
        }

        if (modelMesh.SubMeshes.Count == 0)
        {
            return IsAffectedMaterialOverrideSlot(
                new StaticModelMaterialSlot(modelMesh.MaterialSlotIndex, modelMesh.SlotName, -1, -1, modelMesh, null),
                affectedMaterialAssetIds);
        }

        for (int subMeshIndex = 0; subMeshIndex < modelMesh.SubMeshes.Count; subMeshIndex++)
        {
            var subMesh = modelMesh.SubMeshes[subMeshIndex];
            if (IsAffectedMaterialOverrideSlot(
                new StaticModelMaterialSlot(subMesh.MaterialSlotIndex, subMesh.SlotName, -1, subMeshIndex, modelMesh, subMesh),
                affectedMaterialAssetIds))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsAffectedMaterialOverrideSlot(StaticModelMaterialSlot slot, ISet<Guid> affectedMaterialAssetIds)
    {
        var slotOverride = StaticModelMaterialSlots.FindMatchingOverride(MaterialOverrides, slot);
        if (slotOverride == null || !slotOverride.HasAnyOverride)
        {
            return false;
        }

        if (slotOverride.MaterialAssetId != Guid.Empty && affectedMaterialAssetIds.Contains(slotOverride.MaterialAssetId))
        {
            return true;
        }

        if (slotOverride.MaterialInstanceData.IsEmpty)
        {
            return false;
        }

        Guid instanceMaterialAssetId = slotOverride.MaterialAssetId != Guid.Empty
            ? slotOverride.MaterialAssetId
            : slot.DefaultMaterialAssetId;
        return instanceMaterialAssetId != Guid.Empty && affectedMaterialAssetIds.Contains(instanceMaterialAssetId);
    }

    private void NormalizeMaterialOverrides()
    {
        for (int i = MaterialOverrides.Count - 1; i >= 0; i--)
        {
            if (!MaterialOverrides[i].HasAnyOverride)
            {
                MaterialOverrides.RemoveAt(i);
            }
        }

        if (StaticModel == null)
        {
            return;
        }

        var slots = StaticModelMaterialSlots.Create(StaticModel);
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            var slotOverride = StaticModelMaterialSlots.FindMatchingOverride(MaterialOverrides, slot);
            if (slotOverride == null)
            {
                continue;
            }

            slotOverride.SlotName = slot.SlotName;
            slotOverride.SlotIndex = slot.SlotIndex;
        }
    }

    private void RemoveDuplicateOverrides(StaticModelMaterialSlot slot, MaterialSlotOverride materialOverrideToKeep)
    {
        for (int i = MaterialOverrides.Count - 1; i >= 0; i--)
        {
            var existingOverride = MaterialOverrides[i];
            if (ReferenceEquals(existingOverride, materialOverrideToKeep))
            {
                continue;
            }

            if (IsMatchingSlot(existingOverride, slot))
            {
                MaterialOverrides.RemoveAt(i);
            }
        }
    }

    private static bool IsMatchingSlot(MaterialSlotOverride materialOverride, StaticModelMaterialSlot slot)
    {
        if (!string.IsNullOrWhiteSpace(materialOverride.SlotName)
            && string.Equals(materialOverride.SlotName, slot.SlotName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return materialOverride.SlotIndex == slot.SlotIndex;
    }

}

internal readonly record struct StaticModelComponentRefreshMetrics(
    bool RefreshedAny,
    int RecalculatedOverrideSlotCount,
    int AuthoringMaterialCacheHitCount,
    int AuthoringMaterialCacheMissCount);

internal readonly record struct StaticModelOverrideRefreshMetrics(
    bool RefreshedAny,
    int RecalculatedOverrideSlotCount,
    int AuthoringMaterialCacheHitCount,
    int AuthoringMaterialCacheMissCount);
