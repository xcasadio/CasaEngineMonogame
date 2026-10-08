using System.Reflection;
using System.Runtime.Loader;
using CasaEngine.Core.Logging;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Scene.Entities.Components;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets;

public static class ElementFactory
{
    private static Dictionary<string, Type> _typeCache;

    // Gameplay script assemblies live in a collectible AssemblyLoadContext and are
    // registered explicitly: the AppDomain scan skips collectible contexts so an
    // unloading script assembly can never be re-pinned by a cache rebuild.
    private static readonly List<Assembly> _scriptAssemblies = new();

    static ElementFactory()
    {
        AppDomain.CurrentDomain.AssemblyLoad += (_, _) => InvalidateCaches();
    }

    public static T Create<T>(string typeName) where T : class
    {
        var type = FindTypeByName(typeName)
                   ?? throw new ArgumentException($"Unknown type '{typeName}': no loaded assembly declares it.", nameof(typeName));
        return Activator.CreateInstance(type) as T;
    }

    public static T Load<T>(JObject element) where T : class, ISerializable
    {
        var typeName = element["type"].GetString();
        var component = Create<T>(typeName);
        component.Load(element);
        //  Properties marked [SerializedProperty] (ADR-0071), read after the type's own keys.
        SerializedProperties.Load(component, element);
        return component;
    }

    /// <summary>
    /// Loads a component node like <see cref="Load{T}"/>, except that a <c>type</c> no loaded assembly declares (or one that
    /// is not a <typeparamref name="T"/>) is reported with the component's name and id, and replaced by an
    /// <see cref="UnresolvedComponent"/> that keeps the node, so that the entity still loads and saves without loss
    /// (ADR-0072).
    /// </summary>
    public static T LoadComponent<T>(JObject element) where T : EntityComponent
    {
        var typeName = element["type"]?.Type == JTokenType.String ? element["type"]!.Value<string>() : null;
        var type = FindTypeByName(typeName);
        if (type != null && typeof(T).IsAssignableFrom(type) && !type.IsAbstract)
        {
            return Load<T>(element);
        }

        Logs.WriteError(type == null
            ? $"Unknown component type '{typeName}' for component '{element["name"]}' ({element["id"]}): no loaded assembly declares it. The component is kept unchanged, as an unresolved component."
            : $"Component type '{typeName}' of component '{element["name"]}' ({element["id"]}) is not a {typeof(T).Name}. The component is kept unchanged, as an unresolved component.");

        var unresolved = new UnresolvedComponent();
        unresolved.Load(element);
        return unresolved as T
               ?? throw new InvalidOperationException($"An unresolved component cannot stand for a {typeof(T).Name}.");
    }

    /// <summary>
    /// Makes the types of a reloadable script assembly resolvable by name.
    /// The assembly stays pinned until <see cref="UnregisterScriptAssembly"/>.
    /// </summary>
    public static void RegisterScriptAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        if (!_scriptAssemblies.Contains(assembly))
        {
            _scriptAssemblies.Add(assembly);
        }

        InvalidateCaches();
    }

    /// <summary>
    /// Removes a script assembly and drops every cached Type so its collectible
    /// load context can actually be collected.
    /// </summary>
    public static void UnregisterScriptAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        _scriptAssemblies.Remove(assembly);
        InvalidateCaches();
    }

    /// <summary>Drops the cached Type maps; they rebuild lazily on next use.</summary>
    public static void InvalidateCaches()
    {
        _typeCache = null;
        _derivedTypesCache = new();
    }

    private static IEnumerable<Assembly> GetCandidateAssemblies()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (AssemblyLoadContext.GetLoadContext(assembly)?.IsCollectible == true)
            {
                continue;
            }

            yield return assembly;
        }

        foreach (var assembly in _scriptAssemblies)
        {
            yield return assembly;
        }
    }

    private static Dictionary<string, Type> BuildTypeCache() =>
        GetCandidateAssemblies()
            .SelectMany(x => x.GetTypes())
            .GroupBy(x => x.Name, StringComparer.InvariantCultureIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.InvariantCultureIgnoreCase);

    private static Type FindTypeByName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return null;
        }

        // Local copy: InvalidateCaches can null the field from an AssemblyLoad event
        // while another thread resolves a type.
        var cache = _typeCache;
        if (cache == null)
        {
            cache = BuildTypeCache();
            _typeCache = cache;
        }

        cache.TryGetValue(typeName, out var type);
        return type;
    }

    private static Dictionary<Type, IEnumerable<Type>> _derivedTypesCache = new();

    private static IEnumerable<Type> BuildDerivedTypes(Type type) =>
        GetCandidateAssemblies()
            .SelectMany(x => x.GetTypes())
            .Where(x => x is { IsClass: true, IsGenericType: false, IsInterface: false, IsAbstract: false }
                        && x.IsSubclassOf(type))
            .ToList();

    public static IEnumerable<Type> GetDerivedTypesFrom<T>() where T : class
    {
        var type = typeof(T);
        var cache = _derivedTypesCache;
        if (!cache.TryGetValue(type, out var derived))
        {
            derived = BuildDerivedTypes(type);
            cache[type] = derived;
        }
        return derived;
    }
}
