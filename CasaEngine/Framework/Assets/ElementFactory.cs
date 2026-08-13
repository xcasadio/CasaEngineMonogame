using System.Reflection;
using System.Runtime.Loader;
using CasaEngine.Core.Serialization;
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
        var type = FindTypeByName(typeName);
        return Activator.CreateInstance(type) as T;
    }

    public static T Load<T>(JObject element) where T : class, ISerializable
    {
        var typeName = element["type"].GetString();
        var component = Create<T>(typeName);
        component.Load(element);
        return component;
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

        _typeCache ??= BuildTypeCache();
        _typeCache.TryGetValue(typeName, out var type);
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
        if (!_derivedTypesCache.TryGetValue(type, out var derived))
        {
            derived = BuildDerivedTypes(type);
            _derivedTypesCache[type] = derived;
        }
        return derived;
    }
}
