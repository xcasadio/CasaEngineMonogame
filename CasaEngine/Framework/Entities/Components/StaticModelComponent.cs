using System.ComponentModel;
using CasaEngine.Core.Log;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Materials;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

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
            MaterialOverrides.Add(new MaterialSlotOverride
            {
                SlotName = materialOverride.SlotName,
                SlotIndex = materialOverride.SlotIndex,
                MaterialAssetId = materialOverride.MaterialAssetId,
            });
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

    public override void InitializeWithWorld(World.World world)
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

    private void BuildHierarchy(StaticModelNode node, SceneComponent parent, World.World world)
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
            sub.MaterialOverridesBySlotIndex = CreateResolvedMaterialOverrides(modelMesh, world.Game.AssetContentManager);
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
                if (materialOverride.MaterialAssetId != Guid.Empty)
                {
                    MaterialOverrides.Add(materialOverride);
                }
            }
        }
    }

    private void RefreshGeneratedMaterialOverrides()
    {
        if (StaticModel == null || Owner?.World == null)
        {
            return;
        }

        NormalizeMaterialOverrides();
        RefreshGeneratedMaterialOverrides(this, Owner.World.Game.AssetContentManager);
    }

    private void RefreshGeneratedMaterialOverrides(SceneComponent parent, Assets.AssetContentManager assetContentManager)
    {
        foreach (var child in parent.Children)
        {
            if (child is StaticModelSubMeshComponent { IsGeneratedFromModel: true } generatedSubMeshComponent)
            {
                generatedSubMeshComponent.MaterialOverridesBySlotIndex = generatedSubMeshComponent.ModelMesh == null
                    ? null
                    : CreateResolvedMaterialOverrides(generatedSubMeshComponent.ModelMesh, assetContentManager);
            }

            RefreshGeneratedMaterialOverrides(child, assetContentManager);
        }
    }

    private Dictionary<int, MaterialBase>? CreateResolvedMaterialOverrides(StaticModelMesh modelMesh, Assets.AssetContentManager assetContentManager)
    {
        if (MaterialOverrides.Count == 0)
        {
            return null;
        }

        Dictionary<int, MaterialBase>? resolvedOverrides = null;

        if (modelMesh.SubMeshes.Count == 0)
        {
            var slot = new StaticModelMaterialSlot(modelMesh.MaterialSlotIndex, modelMesh.SlotName, -1, -1, modelMesh, null);
            if (TryResolveMaterialOverride(slot, assetContentManager, out var materialOverride))
            {
                resolvedOverrides = new Dictionary<int, MaterialBase>
                {
                    [slot.SlotIndex] = materialOverride,
                };
            }

            return resolvedOverrides;
        }

        for (int subMeshIndex = 0; subMeshIndex < modelMesh.SubMeshes.Count; subMeshIndex++)
        {
            var subMesh = modelMesh.SubMeshes[subMeshIndex];
            var slot = new StaticModelMaterialSlot(subMesh.MaterialSlotIndex, subMesh.SlotName, -1, subMeshIndex, modelMesh, subMesh);
            if (!TryResolveMaterialOverride(slot, assetContentManager, out var materialOverride))
            {
                continue;
            }

            resolvedOverrides ??= new Dictionary<int, MaterialBase>();
            resolvedOverrides[slot.SlotIndex] = materialOverride;
        }

        return resolvedOverrides;
    }

    private bool TryResolveMaterialOverride(StaticModelMaterialSlot slot, Assets.AssetContentManager assetContentManager, out MaterialBase materialOverride)
    {
        materialOverride = null!;

        var slotOverride = StaticModelMaterialSlots.FindMatchingOverride(MaterialOverrides, slot);
        if (slotOverride == null || slotOverride.MaterialAssetId == Guid.Empty)
        {
            return false;
        }

        slotOverride.SlotName = slot.SlotName;
        slotOverride.SlotIndex = slot.SlotIndex;

        try
        {
            if (!MaterialRuntimeResolver.TryLoadRuntimeMaterial(slotOverride.MaterialAssetId, assetContentManager, out var loadedMaterial))
            {
                return false;
            }

            materialOverride = loadedMaterial;
            return true;
        }
        catch (Exception ex)
        {
            Logs.WriteException(ex);
            return false;
        }
    }

    private void NormalizeMaterialOverrides()
    {
        for (int i = MaterialOverrides.Count - 1; i >= 0; i--)
        {
            if (MaterialOverrides[i].MaterialAssetId == Guid.Empty)
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
