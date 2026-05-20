using System.Collections.Generic;
using CasaEngine.Core.Math.Geometry;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;

namespace CasaEngine.Editor.Runtime.Overlays;

public sealed class EditorParticleOverlayCollector
{
    private readonly List<EditorParticleOverlayItem> _items = [];

    public IReadOnlyList<EditorParticleOverlayItem> Items => _items;

    public IReadOnlyList<EditorParticleOverlayItem> Collect(
        World? world,
        Entity? selectedEntity,
        EntityComponent? selectedComponent)
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

    private void CollectEntity(Entity entity, Entity? selectedEntity, EntityComponent? selectedComponent)
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

    private void CollectStandaloneComponents(Entity entity, bool isSelectedEntity, EntityComponent? selectedComponent)
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
        EntityComponent? selectedComponent)
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

        if (component is ParticleSystemComponent particleSystemComponent)
        {
            AddParticle(entity, particleSystemComponent, isSelectedEntity, selectedComponent);
        }
    }

    private void CollectSceneComponent(
        SceneComponent? sceneComponent,
        Entity entity,
        bool isSelectedEntity,
        EntityComponent? selectedComponent)
    {
        if (sceneComponent == null)
        {
            return;
        }

        if (sceneComponent is ParticleSystemComponent particleSystemComponent)
        {
            AddParticle(entity, particleSystemComponent, isSelectedEntity, selectedComponent);
        }

        var children = sceneComponent.Children;
        for (int index = 0; index < children.Count; index++)
        {
            CollectSceneComponent(children[index], entity, isSelectedEntity, selectedComponent);
        }
    }

    private void CollectChildEntities(Entity entity, Entity? selectedEntity, EntityComponent? selectedComponent)
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

    private void AddParticle(
        Entity entity,
        ParticleSystemComponent particleSystemComponent,
        bool isSelectedEntity,
        EntityComponent? selectedComponent)
    {
        bool isSelectedParticle = ReferenceEquals(particleSystemComponent, selectedComponent);
        bool isSelected = isSelectedParticle || (selectedComponent == null && isSelectedEntity);
        if (!isSelected || particleSystemComponent.ParticleEffectAsset == null)
        {
            return;
        }

        BoundingBox bounds = particleSystemComponent.GetBoundingBox();
        bool hasBounds = bounds.Valid();
        _items.Add(new EditorParticleOverlayItem(
            entity,
            particleSystemComponent,
            particleSystemComponent.ParticleEffectAsset,
            particleSystemComponent.WorldMatrixWithScale,
            bounds,
            hasBounds,
            isSelected));
    }
}