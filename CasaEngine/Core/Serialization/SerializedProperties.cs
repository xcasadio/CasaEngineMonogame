using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Core.Logging;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Serialization;

/// <summary>One property marked with <see cref="SerializedPropertyAttribute"/>.</summary>
public sealed class SerializedPropertyMember
{
    internal SerializedPropertyMember(PropertyInfo property, string jsonName)
    {
        Property = property;
        JsonName = jsonName;
    }

    public PropertyInfo Property { get; }

    public string JsonName { get; }
}

/// <summary>
/// Reads and copies the properties marked with <see cref="SerializedPropertyAttribute"/> (ADR-0118). Writing them belongs
/// to the editor tooling (<c>CasaEngine.EditorServices</c>), like every asset save (AGENTS.md §9.9).
/// </summary>
public static class SerializedProperties
{
    // Keyed weakly by Type: a gameplay script assembly loaded in a collectible context can still be unloaded once
    // ElementFactory forgets it, because this table does not keep its types alive.
    private static readonly ConditionalWeakTable<Type, SerializedPropertyMember[]> Members = new();

    /// <summary>The marked properties of <paramref name="type"/>, base types first, then in declaration order.</summary>
    /// <exception cref="InvalidOperationException">A marked property is not public read-write, has an unsupported type, or
    /// two of them share a JSON key: a developer error reported at the first use of the type.</exception>
    public static IReadOnlyList<SerializedPropertyMember> GetMembers(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return Members.GetValue(type, BuildMembers);
    }

    /// <summary>True when <paramref name="type"/> can be a serialized property type.</summary>
    public static bool IsSupportedType(Type type)
        => type == typeof(bool) || type == typeof(int) || type == typeof(float) || type == typeof(double)
           || type == typeof(string) || type == typeof(Guid) || type.IsEnum
           || type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4)
           || type == typeof(Quaternion) || type == typeof(Color);

    /// <summary>
    /// Sets every marked property of <paramref name="target"/> found in <paramref name="element"/>. A missing or null key
    /// keeps the property's current value, so a property added later loads older files. A value that cannot be read
    /// is reported with the type, the key and the object, and the property keeps its value.
    /// </summary>
    public static void Load(object target, JObject element)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(element);

        var members = GetMembers(target.GetType());
        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (!element.TryGetValue(member.JsonName, out var token) || token.Type == JTokenType.Null)
            {
                continue;
            }

            object value;
            try
            {
                value = ReadValue(token, member.Property.PropertyType);
            }
            catch (Exception exception) when (exception is FormatException or InvalidCastException or ArgumentException
                                                  or OverflowException or NullReferenceException
                                                  or Newtonsoft.Json.JsonException)
            {
                Logs.WriteError($"Cannot read serialized property '{member.JsonName}' of {target.GetType().Name} "
                                + $"'{element["name"]}' ({element["id"]}) from '{token.ToString(Newtonsoft.Json.Formatting.None)}': {exception.Message}");
                continue;
            }

            member.Property.SetValue(target, value);
        }
    }

    /// <summary>Copies every marked property of <paramref name="source"/> to <paramref name="target"/>, which must be of the
    /// same type. A component's <c>Clone()</c> calls it so that a clone keeps its serialized values.</summary>
    public static void Copy(object source, object target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        if (source.GetType() != target.GetType())
        {
            throw new ArgumentException($"Cannot copy serialized properties from {source.GetType().Name} to {target.GetType().Name}.", nameof(target));
        }

        var members = GetMembers(source.GetType());
        for (int i = 0; i < members.Count; i++)
        {
            var property = members[i].Property;
            property.SetValue(target, property.GetValue(source));
        }
    }

    /// <summary>Reads a value of <paramref name="type"/> in the JSON form CasaEngine assets use (<see cref="JsonHelper"/>).</summary>
    public static object ReadValue(JToken token, Type type)
    {
        if (type == typeof(bool)) return token.GetBoolean();
        if (type == typeof(int)) return token.GetInt32();
        if (type == typeof(float)) return token.GetSingle();
        if (type == typeof(double)) return token.Value<double>();
        if (type == typeof(string)) return token.GetString();
        if (type == typeof(Guid)) return token.GetGuid();
        if (type.IsEnum) return Enum.Parse(type, token.GetString(), ignoreCase: true);
        if (type == typeof(Vector2)) return token.GetVector2();
        if (type == typeof(Vector3)) return token.GetVector3();
        if (type == typeof(Vector4)) return token.GetVector4();
        if (type == typeof(Quaternion)) return token.GetQuaternion();
        if (type == typeof(Color)) return token.GetColor();
        throw new ArgumentException($"Unsupported serialized property type {type.Name}.", nameof(type));
    }

    private static SerializedPropertyMember[] BuildMembers(Type type)
    {
        var chain = new List<Type>();
        for (var current = type; current != null && current != typeof(object); current = current.BaseType)
        {
            chain.Add(current);
        }

        chain.Reverse();
        var members = new List<SerializedPropertyMember>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var declaringType in chain)
        {
            var properties = declaringType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            Array.Sort(properties, (left, right) => left.MetadataToken.CompareTo(right.MetadataToken));
            foreach (var property in properties)
            {
                var attribute = property.GetCustomAttribute<SerializedPropertyAttribute>(inherit: true);
                if (attribute == null)
                {
                    continue;
                }

                if (property.GetMethod?.IsPublic != true || property.SetMethod?.IsPublic != true)
                {
                    throw new InvalidOperationException($"Serialized property {type.Name}.{property.Name} must be public and read-write.");
                }

                if (!IsSupportedType(property.PropertyType))
                {
                    throw new InvalidOperationException($"Serialized property {type.Name}.{property.Name} has the unsupported type {property.PropertyType.Name}.");
                }

                if (!keys.Add(attribute.JsonName))
                {
                    throw new InvalidOperationException($"Serialized property key '{attribute.JsonName}' is used twice in {type.Name}.");
                }

                members.Add(new SerializedPropertyMember(property, attribute.JsonName));
            }
        }

        return members.ToArray();
    }
}
