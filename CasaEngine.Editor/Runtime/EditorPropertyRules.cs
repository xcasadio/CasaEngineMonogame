using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Editor.Runtime;

/// <summary>
/// Which component properties the editor lets a user change: those the generic inspector section edits
/// (<c>ComponentEditorBase</c>), shared with the remote commands so they reach nothing more (ADR-0092, security review
/// S7). Specialised component editors may hide more properties for presentation; those are not repeated here.
/// </summary>
internal static class EditorPropertyRules
{
    /// <summary>Properties never edited, whatever their type.</summary>
    public static readonly HashSet<string> UnsupportedPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(EntityComponent.Owner),
        nameof(SceneComponent.Children),
        nameof(SceneComponent.Parent),
        nameof(SceneComponent.Forward),
        nameof(SceneComponent.Up),
        nameof(SceneComponent.BoundingBox),
        nameof(SceneComponent.WorldMatrixWithScale),
        nameof(SceneComponent.WorldMatrixNoParentScale),
        nameof(SceneComponent.WorldMatrixNoScale),
        nameof(SceneComponent.WorldInvertTransposeMatrix),
        nameof(PhysicsBaseComponent.PhysicsDefinition),
        nameof(StaticModelComponent.StaticModel),
        nameof(PhysicsBaseComponent.Collisions),
        nameof(EntityComponent.Name),
    };

    /// <summary>True for the value types the inspector has an editor for.</summary>
    public static bool IsSupportedType(Type type)
    {
        Type valueType = Nullable.GetUnderlyingType(type) ?? type;
        return valueType == typeof(string)
            || valueType == typeof(bool)
            || valueType == typeof(int)
            || valueType == typeof(float)
            || valueType == typeof(double)
            || valueType == typeof(Vector3)
            || valueType == typeof(Color)
            || valueType == typeof(Guid)
            || valueType.IsEnum;
    }

    /// <summary>The inspector's filter: browsable, writable, not read-only, not in <see cref="UnsupportedPropertyNames"/>.</summary>
    public static bool CanEditProperty(PropertyDescriptor property)
    {
        if (!property.IsBrowsable || property.IsReadOnly)
        {
            return false;
        }

        if (property.Attributes.OfType<ReadOnlyAttribute>().Any(attribute => attribute.IsReadOnly))
        {
            return false;
        }

        return !UnsupportedPropertyNames.Contains(property.Name);
    }

    /// <summary>The properties of <paramref name="target"/> a remote command may read and change, in declaration order.</summary>
    public static IReadOnlyList<PropertyDescriptor> GetEditableProperties(object target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var properties = new List<PropertyDescriptor>();
        foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(target))
        {
            if (CanEditProperty(property) && IsSupportedType(property.PropertyType))
            {
                properties.Add(property);
            }
        }

        return properties;
    }

    public static PropertyDescriptor FindEditableProperty(object target, string name)
    {
        IReadOnlyList<PropertyDescriptor> properties = GetEditableProperties(target);
        for (int i = 0; i < properties.Count; i++)
        {
            if (string.Equals(properties[i].Name, name, StringComparison.Ordinal))
            {
                return properties[i];
            }
        }

        return null;
    }
}
