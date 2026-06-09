using System;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;

namespace CasaEngine.Editor;

public sealed class EditorSelection
{
    public static EditorSelection Current { get; } = new();

    public event Action<World> WorldSelectionChanged;

    public event Action<Entity> SelectionChanged;

    public event Action<EntityComponent> ComponentSelectionChanged;

    public World SelectedWorld { get; private set; }

    public Entity SelectedEntity { get; private set; }

    public EntityComponent SelectedComponent { get; private set; }

    private EditorSelection()
    {
    }

    public void SetSelectedWorld(World world)
    {
        UpdateSelection(world, null, null);
    }

    public void SetSelectedEntity(Entity entity)
    {
        var component = SelectedComponent != null && ReferenceEquals(SelectedComponent.Owner, entity)
            ? SelectedComponent
            : null;

        UpdateSelection(entity?.World, entity, component);
    }

    public void SetSelectedComponent(EntityComponent component)
    {
        if (component == null)
        {
            ClearSelectedComponent();
            return;
        }

        UpdateSelection(component.Owner.World, component.Owner, component);
    }

    public void ClearSelectedComponent()
    {
        UpdateSelection(SelectedEntity?.World, SelectedEntity, null);
    }

    public void Clear()
    {
        UpdateSelection(null, null, null);
    }

    private void UpdateSelection(World world, Entity entity, EntityComponent component)
    {
        bool worldChanged = !ReferenceEquals(SelectedWorld, world);
        bool entityChanged = !ReferenceEquals(SelectedEntity, entity);
        bool componentChanged = !ReferenceEquals(SelectedComponent, component);

        if (!worldChanged && !entityChanged && !componentChanged)
        {
            return;
        }

        SelectedWorld = world;
        SelectedEntity = entity;
        SelectedComponent = component;

        if (worldChanged && entity == null && component == null)
        {
            WorldSelectionChanged?.Invoke(SelectedWorld);
        }

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