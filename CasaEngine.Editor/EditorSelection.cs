using System;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;

namespace CasaEngine.Editor;

public sealed class EditorSelection
{
    public static EditorSelection Current { get; } = new();

    public event Action<Entity?>? SelectionChanged;

    public event Action<EntityComponent?>? ComponentSelectionChanged;

    public Entity? SelectedEntity { get; private set; }

    public EntityComponent? SelectedComponent { get; private set; }

    private EditorSelection()
    {
    }

    public void SetSelectedEntity(Entity? entity)
    {
        var component = SelectedComponent != null && ReferenceEquals(SelectedComponent.Owner, entity)
            ? SelectedComponent
            : null;

        UpdateSelection(entity, component);
    }

    public void SetSelectedComponent(EntityComponent? component)
    {
        if (component == null)
        {
            ClearSelectedComponent();
            return;
        }

        UpdateSelection(component.Owner, component);
    }

    public void ClearSelectedComponent()
    {
        UpdateSelection(SelectedEntity, null);
    }

    public void Clear()
    {
        UpdateSelection(null, null);
    }

    private void UpdateSelection(Entity? entity, EntityComponent? component)
    {
        bool entityChanged = !ReferenceEquals(SelectedEntity, entity);
        bool componentChanged = !ReferenceEquals(SelectedComponent, component);

        if (!entityChanged && !componentChanged)
        {
            return;
        }

        SelectedEntity = entity;
        SelectedComponent = component;

        if (entityChanged)
        {
            SelectionChanged?.Invoke(SelectedEntity);
        }

        if (componentChanged)
        {
            ComponentSelectionChanged?.Invoke(SelectedComponent);
        }
    }
}