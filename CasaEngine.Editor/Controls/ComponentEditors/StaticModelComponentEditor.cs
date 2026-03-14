using System;
using System.Collections.Generic;
using CasaEngine.Framework.Entities.Components;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;

namespace CasaEngine.Editor.Controls.ComponentEditors;

public sealed class StaticModelComponentEditor : TransformComponentEditor
{
    private static readonly HashSet<string> StaticModelExcludedPropertyNames = new(TransformExcludedPropertyNames, StringComparer.OrdinalIgnoreCase)
    {
        nameof(StaticModelComponent.StaticModelAssetId),
    };

    private StaticModelComponent StaticModelComponent => (StaticModelComponent)Component;

    public StaticModelComponentEditor(MGWindow window, StaticModelComponent component)
        : base(window, component)
    {
    }

    protected override void AddAdditionalSections(MGStackPanel root)
    {
        root.TryAddChild(CreateModelSection());

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
        modelSelector.AssetChanged += (_, assetId) => StaticModelComponent.StaticModelAssetId = assetId;
        AddPropertyRow(grid, 0, "Model Asset", modelSelector);

        section.SetContent(grid);
        return section;
    }
}