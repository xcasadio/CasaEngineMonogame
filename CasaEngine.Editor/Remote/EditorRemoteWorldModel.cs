using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using CasaEngine.Editor.Runtime;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;

namespace CasaEngine.Editor.Remote;

/// <summary>Lookups and JSON shapes of the world for the remote commands (ADR-0092). Game thread only.</summary>
internal static class EditorRemoteWorldModel
{
    /// <summary>Longest text value returned to a client (security review S9: returned text is truncated).</summary>
    public const int MaxReturnedTextLength = 2000;

    public static IEnumerable<Entity> EnumerateEntities(World world)
    {
        var visited = new HashSet<Entity>();
        foreach (Entity root in world.Entities)
        {
            if (root.Parent == null)
            {
                foreach (Entity entity in EnumerateTree(root, visited))
                {
                    yield return entity;
                }
            }
        }
    }

    public static Entity FindEntity(World world, Guid id)
    {
        foreach (Entity entity in EnumerateEntities(world))
        {
            if (entity.Id == id)
            {
                return entity;
            }
        }

        return null;
    }

    /// <summary>Every component of the entity: the root tree, then the list and the trees under list scene components.</summary>
    public static IEnumerable<EntityComponent> EnumerateComponents(Entity entity)
    {
        if (entity.RootComponent != null)
        {
            foreach (SceneComponent component in EnumerateSceneTree(entity.RootComponent))
            {
                yield return component;
            }
        }

        foreach (EntityComponent component in entity.Components)
        {
            if (component is SceneComponent sceneComponent)
            {
                foreach (SceneComponent nested in EnumerateSceneTree(sceneComponent))
                {
                    yield return nested;
                }
            }
            else
            {
                yield return component;
            }
        }
    }

    public static EntityComponent FindComponent(Entity entity, Guid id)
    {
        foreach (EntityComponent component in EnumerateComponents(entity))
        {
            if (component.Id == id)
            {
                return component;
            }
        }

        return null;
    }

    public static string GetPath(Entity entity)
    {
        var names = new List<string>();
        for (Entity current = entity; current != null; current = current.Parent)
        {
            names.Add(current.Name ?? string.Empty);
        }

        names.Reverse();
        return string.Join("/", names);
    }

    public static string GetTypeName(EntityComponent component)
        => component.GetType().GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? component.GetType().Name;

    public static RemoteEntitySummary DescribeEntity(Entity entity)
    {
        var components = new List<RemoteComponentSummary>();
        foreach (EntityComponent component in EnumerateComponents(entity))
        {
            components.Add(DescribeComponent(entity, component, includeProperties: false));
        }

        return new RemoteEntitySummary
        {
            Id = entity.Id,
            Name = Truncate(entity.Name),
            Path = Truncate(GetPath(entity)),
            ParentId = entity.Parent?.Id,
            Components = components,
        };
    }

    public static RemoteComponentSummary DescribeComponent(Entity entity, EntityComponent component, bool includeProperties)
    {
        EditorWorldOperations.TryGetPlacement(entity, component, out ComponentPlacement placement, out SceneComponent parent);
        List<RemotePropertyValue> properties = null;
        if (includeProperties)
        {
            properties = new List<RemotePropertyValue>();
            foreach (PropertyDescriptor property in EditorPropertyRules.GetEditableProperties(component))
            {
                properties.Add(new RemotePropertyValue
                {
                    Name = property.Name,
                    Type = DescribeType(property.PropertyType),
                    Value = ToJsonValue(property.GetValue(component)),
                });
            }
        }

        return new RemoteComponentSummary
        {
            Id = component.Id,
            Type = GetTypeName(component),
            Placement = placement.ToString().ToLowerInvariant(),
            ParentId = parent?.Id,
            Properties = properties,
        };
    }

    /// <summary>Type name a client sees: the primitive names, <c>vector3</c>, <c>color</c>, <c>guid</c>, or <c>enum:A|B|C</c>.</summary>
    public static string DescribeType(Type type)
    {
        Type valueType = Nullable.GetUnderlyingType(type) ?? type;
        if (valueType.IsEnum)
        {
            return "enum:" + string.Join("|", Enum.GetNames(valueType));
        }

        if (valueType == typeof(string)) return "string";
        if (valueType == typeof(bool)) return "bool";
        if (valueType == typeof(int)) return "int";
        if (valueType == typeof(float)) return "float";
        if (valueType == typeof(double)) return "double";
        if (valueType == typeof(Vector3)) return "vector3";
        if (valueType == typeof(Color)) return "color";
        if (valueType == typeof(Guid)) return "guid";
        return valueType.Name;
    }

    /// <summary>A JSON-friendly copy of a supported value: vectors as [x, y, z], colours as [r, g, b, a].</summary>
    public static object ToJsonValue(object value) => value switch
    {
        null => null,
        string text => Truncate(text),
        Vector3 vector => new[] { vector.X, vector.Y, vector.Z },
        Color color => new[] { (int)color.R, color.G, color.B, color.A },
        Guid guid => guid.ToString(),
        Enum enumValue => enumValue.ToString(),
        _ => value,
    };

    public static string Truncate(string text)
        => text == null || text.Length <= MaxReturnedTextLength ? text : text[..MaxReturnedTextLength] + "…";

    private static IEnumerable<Entity> EnumerateTree(Entity entity, HashSet<Entity> visited)
    {
        if (!visited.Add(entity))
        {
            yield break;
        }

        yield return entity;
        foreach (Entity child in entity.Children)
        {
            foreach (Entity nested in EnumerateTree(child, visited))
            {
                yield return nested;
            }
        }
    }

    private static IEnumerable<SceneComponent> EnumerateSceneTree(SceneComponent component)
    {
        yield return component;
        foreach (SceneComponent child in component.Children)
        {
            foreach (SceneComponent nested in EnumerateSceneTree(child))
            {
                yield return nested;
            }
        }
    }
}

internal sealed class RemoteEntitySummary
{
    public Guid Id { get; init; }

    public string Name { get; init; }

    public string Path { get; init; }

    public Guid? ParentId { get; init; }

    public IReadOnlyList<RemoteComponentSummary> Components { get; init; }
}

internal sealed class RemoteComponentSummary
{
    public Guid Id { get; init; }

    public string Type { get; init; }

    /// <summary><c>list</c>, <c>root</c> or <c>child</c>.</summary>
    public string Placement { get; init; }

    public Guid? ParentId { get; init; }

    public IReadOnlyList<RemotePropertyValue> Properties { get; init; }
}

internal sealed class RemotePropertyValue
{
    public string Name { get; init; }

    public string Type { get; init; }

    public object Value { get; init; }
}
