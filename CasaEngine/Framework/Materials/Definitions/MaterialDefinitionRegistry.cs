namespace CasaEngine.Framework.Materials.Definitions;

public static class MaterialDefinitionRegistry
{
    private static readonly object Sync = new();
    private static readonly List<MaterialDefinition> Definitions = new();
    private static readonly Dictionary<string, MaterialDefinition> DefinitionsById = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<Type, MaterialDefinition> DefinitionsByRuntimeType = new();
    private static IReadOnlyList<MaterialDefinition> DefinitionsSnapshot = Array.Empty<MaterialDefinition>();

    static MaterialDefinitionRegistry()
    {
        var builtInDefinitions = BuiltInMaterialDefinitions.CreateAll();
        for (int i = 0; i < builtInDefinitions.Count; i++)
        {
            AddDefinition(builtInDefinitions[i]);
        }
    }

    public static IReadOnlyList<MaterialDefinition> All => DefinitionsSnapshot;

    public static IDisposable Register(
        MaterialDefinition definition,
        MaterialCompiler.RuntimeMaterialFactory runtimeMaterialFactory = null,
        MaterialInstancePropertyBlockMapper.OverrideMapper overrideMapper = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        lock (Sync)
        {
            AddDefinition(definition);
        }

        IDisposable runtimeFactoryRegistration = null;
        IDisposable overrideMapperRegistration = null;

        try
        {
            runtimeFactoryRegistration = runtimeMaterialFactory is null
                ? null
                : MaterialCompiler.RegisterRuntimeMaterialFactory(definition.Id, runtimeMaterialFactory);
            overrideMapperRegistration = overrideMapper is null
                ? null
                : MaterialInstancePropertyBlockMapper.RegisterOverrideMapper(definition.Id, overrideMapper);

            return new ScopedRegistration(() =>
            {
                overrideMapperRegistration?.Dispose();
                runtimeFactoryRegistration?.Dispose();

                lock (Sync)
                {
                    RemoveDefinition(definition);
                }
            });
        }
        catch
        {
            overrideMapperRegistration?.Dispose();
            runtimeFactoryRegistration?.Dispose();

            lock (Sync)
            {
                RemoveDefinition(definition);
            }

            throw;
        }
    }

    public static bool TryGetById(string id, out MaterialDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        lock (Sync)
        {
            return DefinitionsById.TryGetValue(id, out definition!);
        }
    }

    public static bool TryGetByRuntimeType(Type runtimeMaterialType, out MaterialDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(runtimeMaterialType);

        lock (Sync)
        {
            return DefinitionsByRuntimeType.TryGetValue(runtimeMaterialType, out definition!);
        }
    }

    public static MaterialDefinition GetRequiredById(string id)
    {
        if (TryGetById(id, out var definition))
        {
            return definition;
        }

        throw new KeyNotFoundException($"Unknown material definition '{id}'.");
    }

    private static void AddDefinition(MaterialDefinition definition)
    {
        if (!DefinitionsById.TryAdd(definition.Id, definition))
        {
            throw new InvalidOperationException($"Duplicate material definition id '{definition.Id}'.");
        }

        if (!DefinitionsByRuntimeType.TryAdd(definition.RuntimeMaterialType, definition))
        {
            DefinitionsById.Remove(definition.Id);
            throw new InvalidOperationException(
                $"Duplicate runtime material type '{definition.RuntimeMaterialType.FullName}' in material definition registry.");
        }

        Definitions.Add(definition);
        DefinitionsSnapshot = Definitions.ToArray();
    }

    private static void RemoveDefinition(MaterialDefinition definition)
    {
        RemoveDefinitionLookups(definition);
        Definitions.Remove(definition);
        DefinitionsSnapshot = Definitions.ToArray();
    }

    private static void RemoveDefinitionLookups(MaterialDefinition definition)
    {
        DefinitionsById.Remove(definition.Id);
        DefinitionsByRuntimeType.Remove(definition.RuntimeMaterialType);
    }
}