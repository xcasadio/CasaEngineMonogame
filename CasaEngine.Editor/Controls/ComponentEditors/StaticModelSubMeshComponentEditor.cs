using System;
using System.Collections.Generic;
using CasaEngine.Framework.Scene.Entities.Components;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;

namespace CasaEngine.Editor.Controls.ComponentEditors;

public sealed class StaticModelSubMeshComponentEditor : TransformComponentEditor
{
    private static readonly HashSet<string> StaticModelSubMeshExcludedPropertyNames = new(TransformExcludedPropertyNames, StringComparer.OrdinalIgnoreCase)
    {
        nameof(StaticModelSubMeshComponent.ModelMesh),
        nameof(StaticModelSubMeshComponent.PropertyOverrides),
    };

    private StaticModelSubMeshComponent StaticModelSubMeshComponent => (StaticModelSubMeshComponent)Component;

    public StaticModelSubMeshComponentEditor(MGWindow window, StaticModelSubMeshComponent component)
        : base(window, component)
    {
    }

    protected override void AddAdditionalSections(MGStackPanel root)
    {
        root.TryAddChild(CreateMeshSection());

        var genericSection = CreateGenericSection(StaticModelSubMeshComponent, "Properties", StaticModelSubMeshExcludedPropertyNames);
        if (genericSection != null)
        {
            root.TryAddChild(genericSection);
        }
    }

    private MGExpander CreateMeshSection()
    {
        var section = CreateSection("Mesh");
        var modelMesh = StaticModelSubMeshComponent.ModelMesh;
        if (modelMesh == null)
        {
            section.SetContent(CreateMessage("Mesh and material metadata become available once the static model is loaded in the world."));
            return section;
        }

        var grid = CreatePropertyGrid();
        int rowIndex = 0;
        rowIndex = AddPropertyRow(grid, rowIndex, "Mesh", CreateReadOnlyValue(string.IsNullOrWhiteSpace(modelMesh.Name) ? "<unnamed>" : modelMesh.Name));
        rowIndex = AddPropertyRow(grid, rowIndex, "Material", CreateReadOnlyValue(FormatAssetReference(modelMesh.MaterialAssetId)));
        rowIndex = AddPropertyRow(grid, rowIndex, "Texture", CreateReadOnlyValue(FormatAssetReference(modelMesh.TextureAssetId)));
        rowIndex = AddPropertyRow(grid, rowIndex, "Material Index", CreateReadOnlyValue(modelMesh.MaterialIndex.ToString()));

        if (modelMesh.SubMeshes.Count > 0)
        {
            AddPropertyRow(grid, rowIndex, "Sub Material Count", CreateReadOnlyValue(modelMesh.SubMeshes.Count.ToString()));
        }

        section.SetContent(grid);
        return section;
    }
}