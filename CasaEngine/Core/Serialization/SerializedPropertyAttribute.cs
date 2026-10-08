namespace CasaEngine.Core.Serialization;

/// <summary>
/// Marks a public read-write property of a component (or of any type loaded through
/// <see cref="CasaEngine.Framework.Assets.ElementFactory"/>) as part of its serialized form, under the JSON key
/// <see cref="JsonName"/>. The runtime reads it after the type's own <c>Load</c> (<see cref="SerializedProperties.Load"/>);
/// the editor writes it when it saves a world, an entity or a Play snapshot. Supported property types are listed by
/// <see cref="SerializedProperties.IsSupportedType"/> (ADR-0071).
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class SerializedPropertyAttribute : Attribute
{
    public SerializedPropertyAttribute(string jsonName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonName);
        JsonName = jsonName;
    }

    /// <summary>The JSON key of the property, in the snake_case convention of CasaEngine assets.</summary>
    public string JsonName { get; }
}
