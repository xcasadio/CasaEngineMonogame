using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;

namespace CasaEngine.EditorServices;

/// <summary>
/// Default names given by the editor to the components it adds to an entity: the name of the component type,
/// made unique inside the entity the same way <c>EntitiesPanel.GenerateUniqueEntityName</c> does for entities.
/// </summary>
public static class EditorComponentNames
{
    /// <summary>
    /// Returns the type name of <paramref name="component"/> when no other component of <paramref name="entity"/>
    /// uses it, otherwise the type name followed by <c> (n)</c> with the smallest free n of 2 or more.
    /// The comparison ignores the case. The names counted are those of the root component tree and of
    /// <see cref="Entity.Components"/>.
    /// </summary>
    public static string CreateUniqueName(Entity entity, EntityComponent component)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(component);

        var baseName = component.GetType().Name;
        var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (entity.RootComponent != null)
        {
            CollectNames(entity.RootComponent, component, existingNames);
        }

        foreach (var other in entity.Components)
        {
            AddName(other, component, existingNames);
        }

        if (!existingNames.Contains(baseName))
        {
            return baseName;
        }

        for (int index = 2; index < 10000; index++)
        {
            var candidate = $"{baseName} ({index})";
            if (!existingNames.Contains(candidate))
            {
                return candidate;
            }
        }

        return $"{baseName} ({Guid.NewGuid():N})";
    }

    private static void CollectNames(SceneComponent sceneComponent, EntityComponent excluded, HashSet<string> names)
    {
        AddName(sceneComponent, excluded, names);

        foreach (var child in sceneComponent.Children)
        {
            CollectNames(child, excluded, names);
        }
    }

    private static void AddName(EntityComponent other, EntityComponent excluded, HashSet<string> names)
    {
        if (!ReferenceEquals(other, excluded) && !string.IsNullOrEmpty(other.Name))
        {
            names.Add(other.Name);
        }
    }
}
