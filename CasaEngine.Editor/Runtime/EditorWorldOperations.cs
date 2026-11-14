using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using CasaEngine.Editor.History;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;

namespace CasaEngine.Editor.Runtime;

/// <summary>Where a component sits in its entity.</summary>
internal enum ComponentPlacement
{
    /// <summary>In the entity's component list.</summary>
    List,

    /// <summary>The entity's root <see cref="SceneComponent"/>.</summary>
    Root,

    /// <summary>A child of another <see cref="SceneComponent"/>.</summary>
    Child,
}

/// <summary>
/// The world edits of the entity and details panels, shared with the remote commands (ADR-0092, plan P6). Every
/// change is one undoable entry in the given history context. The optional selection callbacks run inside the
/// command, on execute, undo and redo, as the panels did.
/// </summary>
internal static class EditorWorldOperations
{
    private static readonly Lazy<IReadOnlyDictionary<string, Type>> ComponentTypeLookup = new(CreateComponentTypeLookup);

    /// <summary>
    /// The component types a user may add, by display name (case-insensitive): concrete, non-generic subclasses of
    /// <see cref="EntityComponent"/> with a parameterless constructor, not marked <c>[Browsable(false)]</c>. This is the
    /// only way the remote commands create a component (security review S6).
    /// </summary>
    public static IReadOnlyDictionary<string, Type> ComponentTypes => ComponentTypeLookup.Value;

    public static void AddEntity(EditorHistoryContext context, World world, Entity entity, Entity parent, Entity selectionAfterUndo, Action<Entity> select = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(entity);

        Execute(
            context,
            "Add Entity",
            () =>
            {
                EditorWorldEditingService.AttachEntity(world, entity, parent);
                select?.Invoke(entity);
            },
            () =>
            {
                EditorWorldEditingService.DetachEntity(world, entity);
                select?.Invoke(selectionAfterUndo);
            });
    }

    public static void DuplicateEntity(EditorHistoryContext context, World world, Entity duplicate, Entity parent, Entity selectionAfterUndo, Action<Entity> select = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(duplicate);

        Execute(
            context,
            "Duplicate Entity",
            () =>
            {
                EditorWorldEditingService.AttachEntity(world, duplicate, parent);
                select?.Invoke(duplicate);
            },
            () =>
            {
                EditorWorldEditingService.DetachEntity(world, duplicate);
                select?.Invoke(selectionAfterUndo);
            });
    }

    public static void DeleteEntity(EditorHistoryContext context, World world, Entity entity, Action<Entity> select = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(entity);

        Entity parent = entity.Parent;
        Execute(
            context,
            "Delete Entity",
            () =>
            {
                EditorWorldEditingService.DetachEntity(world, entity);
                select?.Invoke(parent);
            },
            () =>
            {
                EditorWorldEditingService.AttachEntity(world, entity, parent);
                select?.Invoke(entity);
            });
    }

    /// <summary>Returns false, without a history entry, when the name does not change.</summary>
    public static bool RenameEntity(EditorHistoryContext context, Entity entity, string name, Action<Entity> select = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        string previousName = entity.Name;
        if (string.Equals(previousName, name, StringComparison.Ordinal))
        {
            return false;
        }

        Execute(
            context,
            "Rename Entity",
            () =>
            {
                entity.Name = name;
                select?.Invoke(entity);
            },
            () =>
            {
                entity.Name = previousName;
                select?.Invoke(entity);
            });
        return true;
    }

    /// <summary>Creates a component of an allowed type (<see cref="ComponentTypes"/>); false for any other name.</summary>
    public static bool TryCreateComponent(string typeName, out EntityComponent component)
    {
        component = null;
        if (string.IsNullOrWhiteSpace(typeName) || !ComponentTypes.TryGetValue(typeName, out Type type))
        {
            return false;
        }

        component = (EntityComponent)Activator.CreateInstance(type);
        return true;
    }

    /// <summary>
    /// Adds <paramref name="component"/> at <paramref name="placement"/>; <paramref name="parent"/> is required for
    /// <see cref="ComponentPlacement.Child"/>. Undo puts everything back as it was.
    /// </summary>
    public static void AddComponent(
        EditorHistoryContext context,
        Entity entity,
        EntityComponent component,
        ComponentPlacement placement,
        SceneComponent parent,
        EntityComponent selectionAfterUndo,
        Action<EntityComponent> select = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(component);
        ValidatePlacement(entity, component, placement, parent);

        Execute(
            context,
            "Add Component",
            () =>
            {
                Attach(entity, component, placement, parent);
                select?.Invoke(component);
            },
            () =>
            {
                Detach(entity, component, placement, parent);
                select?.Invoke(selectionAfterUndo);
            });
    }

    /// <summary>Where <paramref name="component"/> sits in <paramref name="entity"/>; false when it is not one of its components.</summary>
    public static bool TryGetPlacement(Entity entity, EntityComponent component, out ComponentPlacement placement, out SceneComponent parent)
    {
        placement = ComponentPlacement.List;
        parent = null;
        if (entity == null || component == null || !ReferenceEquals(component.Owner, entity))
        {
            return false;
        }

        if (component is SceneComponent sceneComponent)
        {
            if (sceneComponent.Parent != null)
            {
                placement = ComponentPlacement.Child;
                parent = sceneComponent.Parent;
                return true;
            }

            if (ReferenceEquals(entity.RootComponent, sceneComponent))
            {
                placement = ComponentPlacement.Root;
                return true;
            }
        }

        return entity.Components.Contains(component);
    }

    /// <summary>
    /// Removes <paramref name="component"/> (plan P5): refused for a <see cref="SceneComponent"/> that has children,
    /// allowed for the root without children. Undo puts it back at the same place.
    /// </summary>
    public static bool TryRemoveComponent(
        EditorHistoryContext context,
        Entity entity,
        EntityComponent component,
        out string error,
        Action<EntityComponent> select = null)
    {
        if (!TryGetPlacement(entity, component, out ComponentPlacement placement, out SceneComponent parent))
        {
            error = "The component does not belong to the entity.";
            return false;
        }

        if (component is SceneComponent { Children.Count: > 0 })
        {
            error = "The component has child components: remove them first.";
            return false;
        }

        error = null;
        Execute(
            context,
            "Remove Component",
            () =>
            {
                Detach(entity, component, placement, parent);
                select?.Invoke(parent ?? entity.RootComponent);
            },
            () =>
            {
                Attach(entity, component, placement, parent);
                select?.Invoke(component);
            });
        return true;
    }

    /// <summary>Sets a property as the inspector does; false, without a history entry, when the value does not change.</summary>
    public static bool SetProperty(EditorHistoryContext context, string description, object target, PropertyDescriptor property, object newValue, Action afterApply = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(property);

        object currentValue = property.GetValue(target);
        if (Equals(currentValue, newValue))
        {
            return false;
        }

        Execute(
            context,
            description,
            () =>
            {
                property.SetValue(target, newValue);
                afterApply?.Invoke();
            },
            () =>
            {
                property.SetValue(target, currentValue);
                afterApply?.Invoke();
            });
        return true;
    }

    private static void Execute(EditorHistoryContext context, string description, Action execute, Action undo)
    {
        EditorHistoryService.Current.Execute(context, new EditorDelegateCommand(description, execute, undo));
    }

    private static void ValidatePlacement(Entity entity, EntityComponent component, ComponentPlacement placement, SceneComponent parent)
    {
        switch (placement)
        {
            case ComponentPlacement.Child when component is not SceneComponent || parent == null:
                throw new ArgumentException("A child component must be a SceneComponent with a parent SceneComponent.", nameof(placement));
            case ComponentPlacement.Root when component is not SceneComponent || entity.RootComponent != null:
                throw new ArgumentException("A root component must be a SceneComponent, on an entity without a root.", nameof(placement));
        }
    }

    private static void Attach(Entity entity, EntityComponent component, ComponentPlacement placement, SceneComponent parent)
    {
        switch (placement)
        {
            case ComponentPlacement.Child:
                parent.AddChildComponent((SceneComponent)component);
                break;
            case ComponentPlacement.Root:
                entity.RootComponent = (SceneComponent)component;
                break;
            default:
                entity.AddComponent(component);
                break;
        }

        component.Initialize();
        if (entity.World != null)
        {
            component.InitializeWithWorld(entity.World);
        }
    }

    private static void Detach(Entity entity, EntityComponent component, ComponentPlacement placement, SceneComponent parent)
    {
        switch (placement)
        {
            case ComponentPlacement.Child:
                parent.RemoveChildComponent((SceneComponent)component);
                break;
            case ComponentPlacement.Root:
                if (ReferenceEquals(entity.RootComponent, component))
                {
                    entity.RootComponent = null;
                }
                else
                {
                    // No longer the root (replaced since): fall back to the component list, as the details panel did.
                    entity.RemoveComponent(component);
                }

                break;
            default:
                entity.RemoveComponent(component);
                break;
        }
    }

    private static IReadOnlyDictionary<string, Type> CreateComponentTypeLookup()
    {
        var componentType = typeof(EntityComponent);

        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(GetLoadableTypes)
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericType: false }
                && type.IsSubclassOf(componentType)
                && type.GetConstructor(Type.EmptyTypes) != null
                && IsAddableComponentType(type))
            .Select(type => new
            {
                Type = type,
                Name = type.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? type.Name,
            })
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Type, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(type => type != null);
        }
    }

    private static bool IsAddableComponentType(Type type)
    {
        return type.GetCustomAttribute<BrowsableAttribute>()?.Browsable != false;
    }
}
