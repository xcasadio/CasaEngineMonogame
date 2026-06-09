using System.Collections.Generic;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;

namespace CasaEngine.Editor.Runtime.Overlays;

public sealed class EditorLightOverlayCollector
{
    private readonly List<EditorLightOverlayItem> _items = [];

    public IReadOnlyList<EditorLightOverlayItem> Items => _items;

    public IReadOnlyList<EditorLightOverlayItem> Collect(
        World world,
        Entity selectedEntity,
        EntityComponent selectedComponent)
    {
        _items.Clear();

        if (world == null)
        {
            return _items;
        }

        var entities = world.Entities;
        for (int index = 0; index < entities.Count; index++)
        {
            CollectEntity(entities[index], selectedEntity, selectedComponent);
        }

        return _items;
    }

    private void CollectEntity(Entity entity, Entity selectedEntity, EntityComponent selectedComponent)
    {
        if (entity.ToBeRemoved || !entity.IsEnabled || !entity.IsVisible)
        {
            return;
        }

        bool isSelectedEntity = selectedComponent == null && ReferenceEquals(entity, selectedEntity);

        CollectSceneComponent(entity.RootComponent, entity, isSelectedEntity, selectedComponent);
        CollectStandaloneComponents(entity, isSelectedEntity, selectedComponent);
        CollectChildEntities(entity, selectedEntity, selectedComponent);
    }

    private void CollectStandaloneComponents(Entity entity, bool isSelectedEntity, EntityComponent selectedComponent)
    {
        if (entity.Components is IReadOnlyList<EntityComponent> components)
        {
            for (int index = 0; index < components.Count; index++)
            {
                CollectEntityComponent(components[index], entity, isSelectedEntity, selectedComponent);
            }

            return;
        }

        foreach (var component in entity.Components)
        {
            CollectEntityComponent(component, entity, isSelectedEntity, selectedComponent);
        }
    }

    private void CollectEntityComponent(
        EntityComponent component,
        Entity entity,
        bool isSelectedEntity,
        EntityComponent selectedComponent)
    {
        if (ReferenceEquals(component, entity.RootComponent))
        {
            return;
        }

        if (component is SceneComponent sceneComponent)
        {
            CollectSceneComponent(sceneComponent, entity, isSelectedEntity, selectedComponent);
            return;
        }

        if (component is LightComponent lightComponent)
        {
            AddLight(entity, lightComponent, isSelectedEntity, selectedComponent);
        }
    }

    private void CollectSceneComponent(
        SceneComponent sceneComponent,
        Entity entity,
        bool isSelectedEntity,
        EntityComponent selectedComponent)
    {
        if (sceneComponent == null)
        {
            return;
        }

        if (sceneComponent is LightComponent lightComponent)
        {
            AddLight(entity, lightComponent, isSelectedEntity, selectedComponent);
        }

        var children = sceneComponent.Children;
        for (int index = 0; index < children.Count; index++)
        {
            CollectSceneComponent(children[index], entity, isSelectedEntity, selectedComponent);
        }
    }

    private void CollectChildEntities(Entity entity, Entity selectedEntity, EntityComponent selectedComponent)
    {
        if (entity.Children is IReadOnlyList<Entity> children)
        {
            for (int index = 0; index < children.Count; index++)
            {
                CollectEntity(children[index], selectedEntity, selectedComponent);
            }

            return;
        }

        foreach (var child in entity.Children)
        {
            CollectEntity(child, selectedEntity, selectedComponent);
        }
    }

    private void AddLight(
        Entity entity,
        LightComponent lightComponent,
        bool isSelectedEntity,
        EntityComponent selectedComponent)
    {
        bool isSelectedLight = ReferenceEquals(lightComponent, selectedComponent);
        bool isSelected = isSelectedLight || (selectedComponent == null && isSelectedEntity);

        _items.Add(new EditorLightOverlayItem(
            entity,
            lightComponent,
            lightComponent.Type,
            lightComponent.Position,
            NormalizeDirection(lightComponent.Direction),
            lightComponent.Range,
            lightComponent.InnerConeAngleRadians,
            lightComponent.OuterConeAngleRadians,
            lightComponent.Color,
            isSelected));
    }

    private static Vector3 NormalizeDirection(Vector3 direction)
    {
        if (!float.IsFinite(direction.X)
            || !float.IsFinite(direction.Y)
            || !float.IsFinite(direction.Z)
            || direction.LengthSquared() < 0.000001f)
        {
            return Vector3.Forward;
        }

        direction.Normalize();
        return direction;
    }
}