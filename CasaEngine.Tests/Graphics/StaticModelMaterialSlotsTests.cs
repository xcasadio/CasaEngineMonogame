using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Materials;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class StaticModelMaterialSlotsTests
{
    [Fact]
    public void EnsureMetadata_AssignsUniqueSlotNamesAndIndices()
    {
        var model = new StaticModel();

        var bodyMesh = new StaticModelMesh
        {
            Name = "Body",
        };

        var doorMesh = new StaticModelMesh
        {
            Name = "Door",
        };
        doorMesh.SubMeshes.Add(new SubMesh());
        doorMesh.SubMeshes.Add(new SubMesh
        {
            SlotName = "Door Panel",
        });

        model.Meshes.Add(bodyMesh);
        model.Meshes.Add(doorMesh);

        StaticModelMaterialSlots.EnsureMetadata(model);

        Assert.Equal("Body", bodyMesh.SlotName);
        Assert.Equal(0, bodyMesh.MaterialSlotIndex);
        Assert.Equal("Door [1]", doorMesh.SubMeshes[0].SlotName);
        Assert.Equal(1, doorMesh.SubMeshes[0].MaterialSlotIndex);
        Assert.Equal("Door Panel", doorMesh.SubMeshes[1].SlotName);
        Assert.Equal(2, doorMesh.SubMeshes[1].MaterialSlotIndex);
    }

    [Fact]
    public void FindMatchingOverride_PrefersSlotNameOverSlotIndex()
    {
        var slot = new StaticModelMaterialSlot(
            slotIndex: 1,
            slotName: "Door",
            meshIndex: 0,
            subMeshIndex: -1,
            mesh: new StaticModelMesh(),
            subMesh: null);

        var overrides = new List<MaterialSlotOverride>
        {
            new()
            {
                SlotName = "Door",
                SlotIndex = 7,
                MaterialAssetId = Guid.NewGuid(),
            },
            new()
            {
                SlotName = "Body",
                SlotIndex = 1,
                MaterialAssetId = Guid.NewGuid(),
            },
        };

        var match = StaticModelMaterialSlots.FindMatchingOverride(overrides, slot);

        Assert.Same(overrides[0], match);
    }

    [Fact]
    public void FindMatchingOverride_UsesSlotNameForInstanceOnlyOverridesAfterReindex()
    {
        var slot = new StaticModelMaterialSlot(
            slotIndex: 8,
            slotName: "Door",
            meshIndex: 0,
            subMeshIndex: -1,
            mesh: new StaticModelMesh(),
            subMesh: null);

        var instanceOnlyOverride = new MaterialSlotOverride
        {
            SlotName = "Door",
            SlotIndex = 1,
            MaterialAssetId = Guid.Empty,
        };
        instanceOnlyOverride.MaterialInstanceData.SetPropertyOverride("diffuse_color", MaterialValue.FromColor(Microsoft.Xna.Framework.Color.Gold));

        var overrides = new List<MaterialSlotOverride>
        {
            instanceOnlyOverride,
        };

        var match = StaticModelMaterialSlots.FindMatchingOverride(overrides, slot);

        Assert.Same(instanceOnlyOverride, match);
    }

    [Fact]
    public void FindOrphanOverrides_ReturnsOnlyOverridesWithoutMatchingSlots()
    {
        var model = new StaticModel();
        model.Meshes.Add(new StaticModelMesh
        {
            Name = "Body",
        });
        StaticModelMaterialSlots.EnsureMetadata(model);

        var overrides = new List<MaterialSlotOverride>
        {
            new()
            {
                SlotName = "Body",
                SlotIndex = 12,
                MaterialAssetId = Guid.NewGuid(),
            },
            new()
            {
                SlotName = "Missing",
                SlotIndex = 99,
                MaterialAssetId = Guid.NewGuid(),
            },
        };

        var orphanOverrides = StaticModelMaterialSlots.FindOrphanOverrides(model, overrides);

        var orphan = Assert.Single(orphanOverrides);
        Assert.Equal("Missing", orphan.SlotName);
    }
}