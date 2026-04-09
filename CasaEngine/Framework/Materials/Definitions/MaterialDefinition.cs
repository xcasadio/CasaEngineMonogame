namespace CasaEngine.Framework.Materials.Definitions;

public sealed class MaterialDefinition
{
    private readonly Dictionary<string, MaterialPropertyDefinition> _propertiesByKey;
    private readonly Dictionary<string, MaterialPropertyDefinition> _propertiesBySerializedName;

    public MaterialDefinition(
        string id,
        string displayName,
        Type runtimeMaterialType,
        IEnumerable<MaterialPropertyDefinition> properties,
        string description = "",
        IEnumerable<string>? legacyTypeNames = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(runtimeMaterialType);
        ArgumentNullException.ThrowIfNull(properties);

        if (!typeof(MaterialBase).IsAssignableFrom(runtimeMaterialType))
        {
            throw new ArgumentException(
                $"Runtime material type '{runtimeMaterialType.FullName}' must derive from {nameof(MaterialBase)}.",
                nameof(runtimeMaterialType));
        }

        Id = id;
        DisplayName = displayName;
        RuntimeMaterialType = runtimeMaterialType;
        Description = description;
        LegacyTypeNames = BuildLegacyTypeNames(runtimeMaterialType, legacyTypeNames);
        Properties = BuildProperties(properties);

        _propertiesByKey = BuildPropertyLookupByKey(Properties);
        _propertiesBySerializedName = BuildPropertyLookupBySerializedName(Properties);
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public Type RuntimeMaterialType { get; }
    public IReadOnlyList<string> LegacyTypeNames { get; }
    public IReadOnlyList<MaterialPropertyDefinition> Properties { get; }

    public bool TryGetProperty(string key, out MaterialPropertyDefinition propertyDefinition)
        => _propertiesByKey.TryGetValue(key, out propertyDefinition!);

    public bool TryGetPropertyBySerializedName(string keyOrAlias, out MaterialPropertyDefinition propertyDefinition)
        => _propertiesBySerializedName.TryGetValue(keyOrAlias, out propertyDefinition!);

    public MaterialPropertyDefinition GetRequiredProperty(string key)
    {
        if (TryGetProperty(key, out var propertyDefinition))
        {
            return propertyDefinition;
        }

        throw new KeyNotFoundException(
            $"Material definition '{Id}' does not expose a property named '{key}'.");
    }

    private static IReadOnlyList<string> BuildLegacyTypeNames(Type runtimeMaterialType, IEnumerable<string>? legacyTypeNames)
    {
        var typeNames = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            runtimeMaterialType.Name,
        };

        typeNames.Add(runtimeMaterialType.Name);

        if (legacyTypeNames is null)
        {
            return typeNames.ToArray();
        }

        foreach (var typeName in legacyTypeNames)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentException(
                    $"Material definition for '{runtimeMaterialType.Name}' contains an empty legacy type name.");
            }

            if (!seen.Add(typeName))
            {
                continue;
            }

            typeNames.Add(typeName);
        }

        return typeNames.ToArray();
    }

    private static IReadOnlyList<MaterialPropertyDefinition> BuildProperties(IEnumerable<MaterialPropertyDefinition> properties)
    {
        var materialProperties = properties.ToArray();
        if (materialProperties.Length == 0)
        {
            throw new ArgumentException("A material definition must expose at least one property.", nameof(properties));
        }

        return materialProperties;
    }

    private static Dictionary<string, MaterialPropertyDefinition> BuildPropertyLookupByKey(
        IReadOnlyList<MaterialPropertyDefinition> properties)
    {
        var lookup = new Dictionary<string, MaterialPropertyDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in properties)
        {
            if (!lookup.TryAdd(property.Key, property))
            {
                throw new ArgumentException(
                    $"Material definition contains duplicate property key '{property.Key}'.");
            }
        }

        return lookup;
    }

    private static Dictionary<string, MaterialPropertyDefinition> BuildPropertyLookupBySerializedName(
        IReadOnlyList<MaterialPropertyDefinition> properties)
    {
        var lookup = new Dictionary<string, MaterialPropertyDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in properties)
        {
            AddSerializedName(lookup, property.Key, property);

            foreach (var alias in property.LegacyAliases)
            {
                AddSerializedName(lookup, alias, property);
            }
        }

        return lookup;
    }

    private static void AddSerializedName(
        Dictionary<string, MaterialPropertyDefinition> lookup,
        string name,
        MaterialPropertyDefinition property)
    {
        if (!lookup.TryAdd(name, property))
        {
            throw new ArgumentException(
                $"Material definition contains duplicate serialized property name '{name}'.");
        }
    }
}