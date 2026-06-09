using System;
using System.Collections.Generic;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.Entities.Components;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;

namespace CasaEngine.Editor.Controls.ComponentEditors;

public sealed class StaticModelComponentEditor : TransformComponentEditor
{
    private static readonly HashSet<string> StaticModelExcludedPropertyNames = new(TransformExcludedPropertyNames, StringComparer.OrdinalIgnoreCase)
    {
        nameof(StaticModelComponent.StaticModelAssetId),
        nameof(StaticModelComponent.MaterialOverrides),
    };

    private StaticModelComponent StaticModelComponent => (StaticModelComponent)Component;

    public StaticModelComponentEditor(MGWindow window, StaticModelComponent component, Action refreshRequested = null)
        : base(window, component, refreshRequested)
    {
    }

    protected override void AddAdditionalSections(MGStackPanel root)
    {
        root.TryAddChild(CreateModelSection());
        root.TryAddChild(CreateMaterialsSection());

        var genericSection = CreateGenericSection(StaticModelComponent, "Properties", StaticModelExcludedPropertyNames);
        if (genericSection != null)
        {
            root.TryAddChild(genericSection);
        }
    }

    private MGExpander CreateModelSection()
    {
        var section = CreateSection("Model");
        var grid = CreatePropertyGrid();

        var modelSelector = new AssetSelector(Window)
        {
            AssetId = StaticModelComponent.StaticModelAssetId,
        };
        modelSelector.AssetChanged += (_, assetId) => OnModelAssetChanged(modelSelector, assetId);
        AddPropertyRow(grid, 0, "Model Asset", modelSelector);

        section.SetContent(grid);
        return section;
    }

    private void OnModelAssetChanged(AssetSelector modelSelector, Guid assetId)
    {
        ApplyValueChange(
            BuildComponentCommandDescription("Model Asset"),
            () => StaticModelComponent.StaticModelAssetId,
            nextValue =>
            {
                StaticModelComponent.StaticModelAssetId = nextValue;
                modelSelector.AssetId = nextValue;

                if (StaticModelComponent.Owner?.World is { } world)
                {
                    StaticModelComponent.InitializeWithWorld(world);
                }
            },
            assetId,
            RefreshRequested);
    }

    private MGExpander CreateMaterialsSection()
    {
        var section = CreateSection("Materials", false);
        if (StaticModelComponent.StaticModel == null)
        {
            section.SetContent(CreateMessage("Mesh and material metadata become available once the static model is loaded in the world."));
            return section;
        }

        var slots = StaticModelComponent.GetMaterialSlots();
        if (slots.Count == 0)
        {
            section.SetContent(CreateMessage("This static model does not expose any material slot."));
            return section;
        }

        var content = new MGStackPanel(Window, Orientation.Vertical)
        {
            Spacing = 8,
        };

        var grid = CreatePropertyGrid();
        int rowIndex = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            rowIndex = AddPropertyRow(
                grid,
                rowIndex,
                GetSlotLabel(slot),
                CreateMaterialOverrideEditor(slot));
        }

        content.TryAddChild(grid);

        string orphanOverrideMessage = BuildOrphanOverrideMessage();
        if (!string.IsNullOrWhiteSpace(orphanOverrideMessage))
        {
            content.TryAddChild(CreateMessage(orphanOverrideMessage));
        }

        section.SetContent(content);
        return section;
    }

    private MGElement CreateMaterialOverrideEditor(StaticModelMaterialSlot slot)
    {
        var content = new MGStackPanel(Window, Orientation.Vertical)
        {
            Spacing = 4,
        };

        var selectorRow = new MGStackPanel(Window, Orientation.Horizontal)
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var selector = new AssetSelector(Window)
        {
            AssetId = StaticModelComponent.GetMaterialOverrideAssetId(slot),
            Filter = static assetInfo => string.Equals(assetInfo.AssetType, "material", StringComparison.OrdinalIgnoreCase),
        };

        var summaryText = CreateReadOnlyValue(GetSlotSummary(slot));

        var resetButton = new MGButton(Window, _ =>
        {
            ApplyValueChange(
                BuildComponentCommandDescription(GetSlotLabel(slot)),
                () => StaticModelComponent.GetMaterialOverrideAssetId(slot),
                nextValue =>
                {
                    if (nextValue == Guid.Empty)
                    {
                        StaticModelComponent.ClearMaterialOverride(slot);
                    }
                    else
                    {
                        StaticModelComponent.SetMaterialOverride(slot, nextValue);
                    }

                    selector.AssetId = nextValue;
                    summaryText.Text = GetSlotSummary(slot);
                },
                Guid.Empty,
                RefreshRequested);
        });
        resetButton.SetContent(new MGTextBlock(Window, "Reset")
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        resetButton.PreferredWidth = 60;

        selector.AssetChanged += (_, assetId) =>
        {
            ApplyValueChange(
                BuildComponentCommandDescription(GetSlotLabel(slot)),
                () => StaticModelComponent.GetMaterialOverrideAssetId(slot),
                nextValue =>
                {
                    if (nextValue == Guid.Empty)
                    {
                        StaticModelComponent.ClearMaterialOverride(slot);
                    }
                    else
                    {
                        StaticModelComponent.SetMaterialOverride(slot, nextValue);
                    }

                    selector.AssetId = nextValue;
                    summaryText.Text = GetSlotSummary(slot);
                },
                assetId,
                RefreshRequested);
        };

        selectorRow.TryAddChild(selector);
        selectorRow.TryAddChild(resetButton);

        content.TryAddChild(selectorRow);
        content.TryAddChild(summaryText);
        return content;
    }

    private string BuildOrphanOverrideMessage()
    {
        var orphanOverrides = StaticModelComponent.GetOrphanMaterialOverrides();
        if (orphanOverrides.Count == 0)
        {
            return string.Empty;
        }

        var slotNames = new List<string>();
        for (int i = 0; i < orphanOverrides.Count; i++)
        {
            var slotName = orphanOverrides[i].SlotName;
            if (string.IsNullOrWhiteSpace(slotName))
            {
                slotName = $"slot #{orphanOverrides[i].SlotIndex + 1}";
            }

            slotNames.Add(slotName);
        }

        return $"Stored overrides without a matching slot are kept for reimport compatibility: {string.Join(", ", slotNames)}.";
    }

    private static string GetSlotLabel(StaticModelMaterialSlot slot)
        => string.IsNullOrWhiteSpace(slot.SlotName)
            ? $"Slot {slot.SlotIndex + 1}"
            : slot.SlotName;

    private string GetSlotSummary(StaticModelMaterialSlot slot)
    {
        string defaultLabel = GetDefaultMaterialLabel(slot);
        Guid overrideAssetId = StaticModelComponent.GetMaterialOverrideAssetId(slot);

        return overrideAssetId == Guid.Empty
            ? $"Default: {defaultLabel}"
            : $"Override: {FormatAssetReference(overrideAssetId)} | Default: {defaultLabel}";
    }

    private static string GetDefaultMaterialLabel(StaticModelMaterialSlot slot)
    {
        if (slot.SubMesh != null && slot.SubMesh.MaterialAssetId != Guid.Empty)
        {
            return $"{FormatAssetReference(slot.SubMesh.MaterialAssetId)} (slot default)";
        }

        if (slot.Mesh.MaterialAssetId != Guid.Empty)
        {
            return $"{FormatAssetReference(slot.Mesh.MaterialAssetId)} (mesh default)";
        }

        if (slot.Mesh.TextureAssetId != Guid.Empty)
        {
            return $"{FormatAssetReference(slot.Mesh.TextureAssetId)} (generated LitDiffuseMaterial)";
        }

        return "<missing material>";
    }
}